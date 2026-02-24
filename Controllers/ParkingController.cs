using Microsoft.AspNetCore.Mvc;
using SmartSlot.Data;
using SmartSlot.Models;
using SmartSlot.Services;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System.IO;

namespace SmartSlot.Controllers
{
    public class ParkingController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly DistanceService _distanceService;
        private readonly ParkingAIService _parkingAIService;
        private readonly SmsService _smsService;

        public ParkingController(
            ApplicationDbContext context,
            DistanceService distanceService,
            ParkingAIService parkingAIService,
            SmsService smsService)
        {
            _context = context;
            _distanceService = distanceService;
            _parkingAIService = parkingAIService;
            _smsService = smsService;
        }

        public IActionResult Index()
        {
            return View();
        }

        // ================= ADD SLOT =================
        [HttpPost]
        public IActionResult AddSlot(ParkingSlot slot)
        {
            _context.ParkingSlots.Add(slot);
            _context.SaveChanges();
            return RedirectToAction("SlotAdded");
        }

        public IActionResult SlotAdded()
        {
            return View();
        }

        // ================= AI ANALYSIS =================
        [HttpPost]
        public async Task<JsonResult> AnalyzeParking(IFormFile image)
        {
            if (image == null)
                return Json(new { score = 0, badge = "❌ No image", details = "No image uploaded" });

            using var ms = new MemoryStream();
            await image.CopyToAsync(ms);
            var imageBytes = ms.ToArray();

            var (score, badge, details) =
                await _parkingAIService.AnalyzeParkingImage(imageBytes);

            return Json(new { score, badge, details });
        }

        // ================= SEARCH =================
        public IActionResult Search()
        {
            return View();
        }

        [HttpGet]
        [HttpGet]
        public JsonResult NearbySlots(double lat, double lon, double radius = 6)
        {
            var allSlots = _context.ParkingSlots.ToList();

            var nearbySlots = allSlots
                .Where(slot =>
                    _distanceService.GetDistance(lat, lon, slot.Latitude, slot.Longitude) <= radius
                )
                .Select(slot => new
                {
                    id = slot.Id,
                    ownerName = slot.OwnerName,
                    latitude = slot.Latitude,
                    longitude = slot.Longitude,
                    pricePerHour = slot.PricePerHour,
                    vehicleType = slot.VehicleType,
                    availableFrom = slot.AvailableFrom,
                    availableTo = slot.AvailableTo,
                    isBooked = slot.IsBooked,
                    parkingScore = slot.ParkingScore,
                    parkingBadge = slot.ParkingBadge,

                    averageRating = _context.Reviews
                        .Where(r => r.ParkingSlotId == slot.Id)
                        .Select(r => (double?)r.Rating)
                        .Average() ?? 0
                })
                .ToList();

            return Json(nearbySlots);
        }


        // ================= BOOKING =================
        [Route("Parking/Book/{id}")]
        public IActionResult Book(int id)
        {
            var slot = _context.ParkingSlots.Find(id);
            if (slot == null) return NotFound();

            ViewBag.Slot = slot;
            return View();
        }

        [HttpPost]
        public IActionResult ConfirmBooking(Booking booking, double totalAmount)
        {
            _context.Bookings.Add(booking);

            var slot = _context.ParkingSlots.Find(booking.ParkingSlotId);
            if (slot != null)
            {
                slot.IsBooked = true;
                _context.ParkingSlots.Update(slot);
            }

            _context.SaveChanges();

            return RedirectToAction("BookingSuccess");
        }

        public IActionResult BookingSuccess()
        {
            return View();
        }

        // ================= TEST SMS =================
        [HttpGet]
        public async Task<IActionResult> TestSms(string phone)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(phone))
                    return Json(new { success = false, message = "Phone is empty" });

                await _smsService.SendReviewSms(phone, 999);

                return Json(new { success = true, message = "SMS triggered successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ================= REVIEW PAGE =================
        [HttpGet]
        [Route("Parking/Review/{id}")]
        public IActionResult Review(int id)
        {
            var booking = _context.Bookings.FirstOrDefault(b => b.Id == id);

            if (booking == null)
                return NotFound($"Booking with ID {id} not found.");

            ViewBag.BookingId = booking.Id;
            ViewBag.ParkingSlotId = booking.ParkingSlotId;

            return View();
        }

        // ================= SUBMIT REVIEW =================
        [HttpPost]
        public IActionResult SubmitReview(
            int BookingId,
            int ParkingSlotId,
            int Rating,
            string Comment)
        {
            var review = new Review
            {
                BookingId = BookingId,
                ParkingSlotId = ParkingSlotId,
                Rating = Rating,
                Comment = Comment,
                CreatedAt = DateTime.UtcNow
            };

            _context.Reviews.Add(review);

            var booking = _context.Bookings.Find(BookingId);
            if (booking != null)
            {
                booking.ReviewSubmitted = true;
                _context.Bookings.Update(booking);
            }

            _context.SaveChanges();

            return Content("Thank you! Your review has been submitted.");
        }

        // ================= DEBUG TEST =================
        [HttpGet]
        public IActionResult DebugTest()
        {
            return Content("ParkingController is working on Render.");
        }
    }
}