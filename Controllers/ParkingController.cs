using Microsoft.AspNetCore.Mvc;
using SmartSlot.Data;
using SmartSlot.Models;
using SmartSlot.Services;

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

        [HttpPost]
        public IActionResult AddSlot(ParkingSlot slot)
        {
            _context.ParkingSlots.Add(slot);
            _context.SaveChanges();
            return RedirectToAction("SlotAdded");
        }

        [HttpPost]
        public async Task<JsonResult> AnalyzeParking(IFormFile image)
        {
            if (image == null)
                return Json(new { score = 0, badge = "❌ No image", details = "No image uploaded" });

            using var ms = new MemoryStream();
            await image.CopyToAsync(ms);
            var imageBytes = ms.ToArray();

            var (score, badge, details) = await _parkingAIService.AnalyzeParkingImage(imageBytes);

            return Json(new { score, badge, details });
        }

        public IActionResult SlotAdded()
        {
            return View();
        }

        public IActionResult Search()
        {
            return View();
        }

        // UPDATED: Includes Average Rating
        [HttpGet]
        [HttpGet]
        public JsonResult NearbySlots(double lat, double lon, double radius = 6)
        {
            // Step 1: Bring data to memory first
            var allSlots = _context.ParkingSlots.ToList();

            // Step 2: Then apply C# distance filtering
            var nearbySlots = allSlots
                .Where(slot =>
                    _distanceService.GetDistance(
                        lat,
                        lon,
                        slot.Latitude,
                        slot.Longitude
                    ) <= radius
                )
                .ToList();

            return Json(nearbySlots);
        }

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

        public IActionResult PaymentSuccess(int bookingId)
        {
            return View("BookingSuccess");
        }

        public IActionResult BookingSuccess()
        {
            return View();
        }

        [HttpGet]
        public JsonResult GetBookingInfo(int id)
        {
            var booking = _context.Bookings.FirstOrDefault(b => b.ParkingSlotId == id);
            if (booking == null) return Json(null);

            return Json(new { customerName = booking.CustomerName });
        }

        // ⭐ REVIEW PAGE
        public IActionResult Review(int id)
        {
            var booking = _context.Bookings.Find(id);
            if (booking == null) return NotFound();

            ViewBag.BookingId = booking.Id;
            ViewBag.ParkingSlotId = booking.ParkingSlotId;

            return View();
        }

        // ⭐ SUBMIT REVIEW
        [HttpPost]
        [HttpPost]
        public IActionResult SubmitReview(int bookingId, int parkingSlotId, int rating, string? comment)
        {
            var booking = _context.Bookings.Find(bookingId);
            if (booking == null) return NotFound();

            var review = new Review
            {
                BookingId = bookingId,
                ParkingSlotId = parkingSlotId,
                Rating = rating,
                Comment = comment
            };

            _context.Reviews.Add(review);

            booking.ReviewSubmitted = true;
            _context.Bookings.Update(booking);

            _context.SaveChanges();

            return Content("Thank you for your review!");
        }
    }
}