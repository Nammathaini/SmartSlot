using Microsoft.AspNetCore.Mvc;
using SmartSlot.Data;
using SmartSlot.Models;
using SmartSlot.Services;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Text.Json;

namespace SmartSlot.Controllers
{
    public class ParkingController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly DistanceService _distanceService;
        private readonly ParkingAIService _parkingAIService;
        private readonly SmsService _smsService;
        private readonly EmailService _emailService;

        public ParkingController(
            ApplicationDbContext context,
            DistanceService distanceService,
            ParkingAIService parkingAIService,
            SmsService smsService,
            EmailService emailService)
        {
            _context = context;
            _distanceService = distanceService;
            _parkingAIService = parkingAIService;
            _smsService = smsService;
            _emailService = emailService;
        }

        // ================= HOME =================
        public IActionResult Index()
        {
            ViewBag.Username = HttpContext.Session.GetString("Username");
            return View();
        }

        // ================= DASHBOARD =================
        public IActionResult Dashboard()
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Signin", "Auth");

            var userId = HttpContext.Session.GetString("UserId");
            ViewBag.Username = HttpContext.Session.GetString("Username");

            var user = _context.Users.FirstOrDefault(u => u.Id.ToString() == userId);
            if (user == null)
                return RedirectToAction("Signin", "Auth");

            var mySlots = _context.ParkingSlots
                .Where(s => s.OwnerPhone == user.PhoneNumber)
                .OrderByDescending(s => s.Id)
                .ToList();

            var myBookings = _context.Bookings
                .Where(b => b.CustomerPhone == user.PhoneNumber)
                .OrderByDescending(b => b.BookingFrom)
                .ToList();

            ViewBag.MySlots = mySlots;
            ViewBag.MyBookings = myBookings;

            return View();
        }

        // ================= PROFILE =================
        public IActionResult Profile()
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Signin", "Auth");

            var userId = HttpContext.Session.GetString("UserId");
            var user = _context.Users.FirstOrDefault(u => u.Id.ToString() == userId);

            ViewBag.Username = HttpContext.Session.GetString("Username");
            ViewBag.User = user;

            return View();
        }

        // ================= ADD SLOT =================
        [HttpGet]
        public IActionResult AddSlot()
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Signin", "Auth");

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> AddSlot(ParkingSlot slot)
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Signin", "Auth", new { returnUrl = HttpContext.Request.Path });

            slot.IsBooked = false;
            _context.ParkingSlots.Add(slot);
            _context.SaveChanges();

            // ✅ Send confirmation email via Brevo
            try
            {
                var userId = HttpContext.Session.GetString("UserId");
                var user = _context.Users.FirstOrDefault(u => u.Id.ToString() == userId);

                if (user != null && !string.IsNullOrEmpty(user.Email))
                {
                    await _emailService.SendSlotAddedEmail(
                        toEmail: user.Email,
                        ownerName: slot.OwnerName,
                        vehicleType: slot.VehicleType,
                        pricePerHour: slot.PricePerHour,
                        availableFrom: slot.AvailableFrom,
                        availableTo: slot.AvailableTo,
                        paymentMode: slot.PaymentMode
                    );
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"📧 Email failed: {ex.Message}");
            }

            return RedirectToAction("SlotAdded");
        }

        public IActionResult SlotAdded()
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Signin", "Auth", new { returnUrl = HttpContext.Request.Path });

            return View();
        }

        // ================= NOTIFY ME =================
        [HttpPost]
        public IActionResult NotifyMe([FromBody] NotifyMeRequest request)
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
                return Json(new { success = false, message = "Please sign in first." });

            var user = _context.Users.FirstOrDefault(u => u.Id.ToString() == userId);
            if (user == null)
                return Json(new { success = false, message = "User not found." });

            if (string.IsNullOrEmpty(user.Email))
                return Json(new { success = false, message = "No email found on your account." });

            // Avoid duplicate requests
            var existing = _context.SlotNotifyRequests
                .FirstOrDefault(n =>
                    n.ParkingSlotId == request.SlotId &&
                    n.CustomerEmail == user.Email &&
                    !n.NotificationSent);

            if (existing != null)
                return Json(new { success = true, message = "Already registered." });

            var notifyRequest = new SlotNotifyRequest
            {
                ParkingSlotId = request.SlotId,
                BookingId = request.BookingId,
                CustomerEmail = user.Email,
                CustomerName = user.Username,
                NotificationSent = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.SlotNotifyRequests.Add(notifyRequest);
            _context.SaveChanges();

            return Json(new { success = true });
        }

        // ================= AI ANALYSIS =================
        [HttpPost]
        public async Task<JsonResult> AnalyzeParking(IFormFile image)
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return Json(new { error = "Unauthorized" });

            if (image == null)
                return Json(new { score = 0, badge = "❌ No image", details = "No image uploaded" });

            using var ms = new MemoryStream();
            await image.CopyToAsync(ms);
            var imageBytes = ms.ToArray();

            var (score, badge, details) = await _parkingAIService.AnalyzeParkingImage(imageBytes);
            return Json(new { score, badge, details });
        }

        // ================= SEARCH =================
        public IActionResult Search()
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Signin", "Auth", new { returnUrl = HttpContext.Request.Path });

            return View();
        }

        [HttpGet]
        public JsonResult NearbySlots(double lat, double lon, double radius = 6)
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return Json(new { error = "Unauthorized" });

            var istNow = DateTime.UtcNow.AddHours(5.5);
            var allSlots = _context.ParkingSlots.ToList();

            var nearbySlots = allSlots
                .Where(slot => _distanceService.GetDistance(lat, lon, slot.Latitude, slot.Longitude) <= radius)
                .Select(slot =>
                {
                    var activeBooking = _context.Bookings
                        .Where(b => b.ParkingSlotId == slot.Id && b.BookingFrom <= istNow && b.BookingTo > istNow)
                        .OrderByDescending(b => b.BookingFrom)
                        .FirstOrDefault();

                    var latestBooking = _context.Bookings
                        .Where(b => b.ParkingSlotId == slot.Id)
                        .OrderByDescending(b => b.BookingTo)
                        .FirstOrDefault();

                    DateTime displayAvailableFrom = slot.AvailableFrom;
                    if (latestBooking != null && latestBooking.BookingTo > slot.AvailableFrom)
                        if (latestBooking.BookingTo < slot.AvailableTo)
                            displayAvailableFrom = latestBooking.BookingTo;

                    return new
                    {
                        id = slot.Id,
                        ownerName = slot.OwnerName,
                        ownerPhone = slot.OwnerPhone,
                        latitude = slot.Latitude,
                        longitude = slot.Longitude,
                        pricePerHour = slot.PricePerHour,
                        vehicleType = slot.VehicleType,
                        availableFrom = displayAvailableFrom,
                        availableTo = slot.AvailableTo,
                        isBooked = activeBooking != null,
                        parkingScore = slot.ParkingScore,
                        parkingBadge = slot.ParkingBadge,
                        averageRating = _context.Reviews
                            .Where(r => r.ParkingSlotId == slot.Id)
                            .Select(r => (double?)r.Rating)
                            .Average() ?? 0,
                        bookingFrom = activeBooking?.BookingFrom,
                        bookingTo = activeBooking?.BookingTo,
                        bookingId = activeBooking?.Id   // ✅ needed for NotifyMe
                    };
                })
                .ToList();

            return Json(nearbySlots);
        }

        // ================= BOOKING =================
        [Route("Parking/Book/{id}")]
        public IActionResult Book(int id)
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Signin", "Auth", new { returnUrl = HttpContext.Request.Path });

            var slot = _context.ParkingSlots.Find(id);
            if (slot == null) return NotFound();

            var bookedRanges = _context.Bookings
                .Where(b => b.ParkingSlotId == id)
                .Select(b => new { BookingFrom = b.BookingFrom, BookingTo = b.BookingTo })
                .ToList();

            ViewBag.Slot = slot;
            ViewBag.BookedRanges = bookedRanges;
            return View();
        }

        // ================= EXTEND =================
        [Route("Parking/Extend/{bookingId}")]
        public IActionResult Extend(int bookingId)
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Signin", "Auth", new { returnUrl = HttpContext.Request.Path });

            var booking = _context.Bookings.FirstOrDefault(b => b.Id == bookingId);
            if (booking == null) return NotFound();

            var slot = _context.ParkingSlots.FirstOrDefault(s => s.Id == booking.ParkingSlotId);
            if (slot == null) return NotFound();

            var conflictingBooking = _context.Bookings
                .Where(b =>
                    b.ParkingSlotId == slot.Id &&
                    b.Id != booking.Id &&
                    b.BookingFrom < slot.AvailableTo &&
                    b.BookingTo > booking.BookingTo)
                .OrderBy(b => b.BookingFrom)
                .FirstOrDefault();

            if (conflictingBooking != null)
            {
                TempData["ExtensionError"] = "Sorry, this slot is already reserved after your booking.";
                return RedirectToAction("Search");
            }

            var bookedRanges = _context.Bookings
                .Where(b => b.ParkingSlotId == slot.Id)
                .Select(b => new { BookingFrom = b.BookingFrom, BookingTo = b.BookingTo })
                .ToList();

            ViewBag.PreviousEndTime = booking.BookingTo;
            ViewBag.Slot = slot;
            ViewBag.BookedRanges = bookedRanges;
            return View("Book");
        }

        // ================= SLOT LIST PAGES =================
        [HttpGet]
        public IActionResult AvailableSlots(double lat, double lon, double radius = 3)
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Signin", "Auth", new { returnUrl = HttpContext.Request.Path });

            ViewBag.Lat = lat; ViewBag.Lon = lon; ViewBag.Radius = radius;
            return View();
        }

        [HttpGet]
        public IActionResult BookedSlots(double lat, double lon, double radius = 3)
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Signin", "Auth", new { returnUrl = HttpContext.Request.Path });

            ViewBag.Lat = lat; ViewBag.Lon = lon; ViewBag.Radius = radius;
            return View();
        }

        // ================= DEBUG =================
        [HttpGet]
        public IActionResult DebugTest()
        {
            return Content("ParkingController is working on Render.");
        }
    }

    // DTO for NotifyMe request body
    public class NotifyMeRequest
    {
        public int SlotId { get; set; }
        public int BookingId { get; set; }
    }
}
