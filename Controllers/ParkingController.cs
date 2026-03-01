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

        public IActionResult Index()
        {
            ViewBag.Username = HttpContext.Session.GetString("Username");
            return View();
        }

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
                Console.WriteLine($"📧 Slot added email failed: {ex.Message}");
            }

            return RedirectToAction("SlotAdded");
        }

        public IActionResult SlotAdded()
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Signin", "Auth", new { returnUrl = HttpContext.Request.Path });

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ConfirmBooking(
            int ParkingSlotId,
            string CustomerName,
            string CustomerPhone,
            string VehicleNumber,
            DateTime BookingFrom,
            DateTime BookingTo,
            decimal totalAmount)
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Signin", "Auth");

            var slot = _context.ParkingSlots.Find(ParkingSlotId);
            if (slot == null) return NotFound();

            var userId = HttpContext.Session.GetString("UserId");
            var user = _context.Users.FirstOrDefault(u => u.Id.ToString() == userId);

            var booking = new Booking
            {
                ParkingSlotId = ParkingSlotId,
                CustomerName = CustomerName,
                CustomerPhone = CustomerPhone,
                CustomerEmail = user?.Email ?? "",
                VehicleNumber = VehicleNumber,
                BookingFrom = BookingFrom,
                BookingTo = BookingTo,
                ReviewSmsSent = false,
                ReviewSubmitted = false,
                OneHourAlertSent = false
            };

            _context.Bookings.Add(booking);
            _context.SaveChanges();

            try
            {
                if (user != null && !string.IsNullOrEmpty(user.Email))
                {
                    var istFrom = BookingFrom.AddHours(5.5);
                    var istTo = BookingTo.AddHours(5.5);

                    await _emailService.SendBookingConfirmationEmail(
                        toEmail: user.Email,
                        customerName: CustomerName,
                        ownerName: slot.OwnerName,
                        ownerPhone: slot.OwnerPhone,
                        vehicleType: slot.VehicleType,
                        vehicleNumber: VehicleNumber,
                        pricePerHour: slot.PricePerHour,
                        totalAmount: (double)totalAmount,
                        bookingFrom: istFrom,
                        bookingTo: istTo,
                        paymentMode: slot.PaymentMode
                    );
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"📧 Booking email failed: {ex.Message}");
            }

            return RedirectToAction("BookingSuccess");
        }

        [HttpPost]
        public async Task<IActionResult> ConfirmExtension(
            int ExistingBookingId,
            int ParkingSlotId,
            DateTime BookingFrom,
            DateTime BookingTo,
            decimal totalAmount)
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Signin", "Auth");

            var booking = _context.Bookings.FirstOrDefault(b => b.Id == ExistingBookingId);
            if (booking == null) return NotFound();

            var slot = _context.ParkingSlots.Find(ParkingSlotId);
            if (slot == null) return NotFound();

            // Check no conflict with other bookings
            var conflict = _context.Bookings
                .Where(b =>
                    b.ParkingSlotId == ParkingSlotId &&
                    b.Id != ExistingBookingId &&
                    b.BookingFrom < BookingTo &&
                    b.BookingTo > booking.BookingTo)
                .FirstOrDefault();

            if (conflict != null)
            {
                TempData["ExtensionError"] = "Sorry, this slot is already reserved after your booking.";
                return RedirectToAction("Search");
            }

            // ✅ Update existing booking
            booking.BookingTo = BookingTo;
            booking.OneHourAlertSent = false;
            booking.ReviewSmsSent = false;
            _context.Bookings.Update(booking);
            _context.SaveChanges();

            try
            {
                var userId = HttpContext.Session.GetString("UserId");
                var user = _context.Users.FirstOrDefault(u => u.Id.ToString() == userId);

                if (user != null && !string.IsNullOrEmpty(user.Email))
                {
                    var istTo = BookingTo.AddHours(5.5);

                    await _emailService.SendBookingConfirmationEmail(
                        toEmail: user.Email,
                        customerName: booking.CustomerName,
                        ownerName: slot.OwnerName,
                        ownerPhone: slot.OwnerPhone,
                        vehicleType: slot.VehicleType,
                        vehicleNumber: booking.VehicleNumber,
                        pricePerHour: slot.PricePerHour,
                        totalAmount: (double)totalAmount,
                        bookingFrom: booking.BookingFrom.AddHours(5.5),
                        bookingTo: istTo,
                        paymentMode: slot.PaymentMode
                    );
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"📧 Extension email failed: {ex.Message}");
            }

            return RedirectToAction("BookingSuccess");
        }

        public IActionResult BookingSuccess()
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Signin", "Auth");

            return View();
        }

        // ================= REVIEW =================
        [HttpGet]
        [Route("Parking/Review/{bookingId}")]
        public IActionResult Review(int bookingId)
        {
            var booking = _context.Bookings.FirstOrDefault(b => b.Id == bookingId);
            if (booking == null) return NotFound();

            if (booking.ReviewSubmitted)
                return Content("✅ You have already submitted a review. Thank you!");

            ViewBag.BookingId = booking.Id;
            ViewBag.ParkingSlotId = booking.ParkingSlotId;
            return View();
        }

        [HttpPost]
        public IActionResult SubmitReview(int BookingId, int ParkingSlotId, int Rating, string Comment)
        {
            var booking = _context.Bookings.FirstOrDefault(b => b.Id == BookingId);
            if (booking == null) return NotFound();

            if (booking.ReviewSubmitted)
                return Content("✅ You have already submitted a review. Thank you!");

            var review = new Review
            {
                ParkingSlotId = ParkingSlotId,
                BookingId = BookingId,
                Rating = Rating,
                Comment = Comment,
                CreatedAt = DateTime.UtcNow
            };

            _context.Reviews.Add(review);
            booking.ReviewSubmitted = true;
            _context.Bookings.Update(booking);
            _context.SaveChanges();

            return RedirectToAction("ReviewSuccess");
        }

        [HttpGet]
        public IActionResult ReviewSuccess()
        {
            return View();
        }

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
                        bookingId = activeBooking?.Id
                    };
                })
                .ToList();

            return Json(nearbySlots);
        }
        [HttpGet]
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

            var istNow = DateTime.UtcNow.AddHours(5.5); // ← ADD THIS

            ViewBag.Slot = slot;
            ViewBag.BookedRanges = bookedRanges;
            ViewBag.IstNow = istNow.ToString("yyyy-MM-ddTHH:mm"); // ← ADD THIS
            return View();
        }

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
            ViewBag.ExistingBookingId = booking.Id;
            ViewBag.ExtensionMode = true;
            ViewBag.ExtensionStart = booking.BookingTo;
            return View("Book");
        }

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

        [HttpGet]
        public IActionResult DebugTest()
        {
            return Content("ParkingController is working on Render.");
        }
    }

    public class NotifyMeRequest
    {
        public int SlotId { get; set; }
        public int BookingId { get; set; }
    }
}   