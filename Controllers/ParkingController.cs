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
        private readonly IWebHostEnvironment _env;

        public ParkingController(
            ApplicationDbContext context,
            DistanceService distanceService,
            ParkingAIService parkingAIService,
            SmsService smsService,
            EmailService emailService,
            IWebHostEnvironment env)
        {
            _context = context;
            _distanceService = distanceService;
            _parkingAIService = parkingAIService;
            _smsService = smsService;
            _emailService = emailService;
            _env = env;
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

            // ✅ UPI QR image upload removed — owner uses UPI ID only (deep link flow)
            // UpiQrImagePath is no longer used

            slot.ExitMethod = "QR";
            slot.QrToken = Guid.NewGuid().ToString("N");
            Console.WriteLine($"📷 QR Exit — Token generated: {slot.QrToken}");

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
                        paymentMode: slot.PaymentMode,
                        qrToken: slot.QrToken
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

            // ✅ DB stores IST — save as-is, no UTC conversion
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
                OneHourAlertSent = false,
                ExitConfirmed = false,
                ExitScanAlertSent = false,
                PenaltyApplied = false
            };

            _context.Bookings.Add(booking);
            _context.SaveChanges();

            Console.WriteLine($"✅ Booking saved — Id:{booking.Id} From:{BookingFrom} To:{BookingTo}");

            try
            {
                if (user != null && !string.IsNullOrEmpty(user.Email))
                {
                    await _emailService.SendBookingConfirmationEmail(
                        toEmail: user.Email,
                        customerName: booking.CustomerName,
                        ownerName: slot.OwnerName,
                        ownerPhone: slot.OwnerPhone,
                        vehicleType: slot.VehicleType,
                        vehicleNumber: booking.VehicleNumber,
                        pricePerHour: slot.PricePerHour,
                        totalAmount: (double)totalAmount,
                        bookingFrom: BookingFrom,
                        bookingTo: BookingTo,
                        paymentMode: slot.PaymentMode,
                        qrToken: slot.QrToken
                    );
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"📧 Booking email failed: {ex.Message}");
            }

            // ✅ TempData stored as strings (cannot serialize double)
            TempData["PaymentMode"] = slot.PaymentMode;
            TempData["OwnerUpiId"] = slot.OwnerUpiId ?? "";
            TempData["OwnerName"] = slot.OwnerName;
            TempData["TotalAmount"] = totalAmount.ToString("F2");
            // ✅ UpiQrImagePath removed — no longer needed

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

            // ✅ DB stores IST — save as-is
            booking.BookingTo = BookingTo;
            booking.OneHourAlertSent = false;
            booking.ReviewSmsSent = false;
            booking.ExitConfirmed = false;
            booking.ExitScanAlertSent = false;
            booking.PenaltyApplied = false;
            _context.Bookings.Update(booking);
            _context.SaveChanges();

            try
            {
                var userId = HttpContext.Session.GetString("UserId");
                var user = _context.Users.FirstOrDefault(u => u.Id.ToString() == userId);

                if (user != null && !string.IsNullOrEmpty(user.Email))
                {
                    await _emailService.SendBookingConfirmationEmail(
                        toEmail: user.Email,
                        customerName: booking.CustomerName,
                        ownerName: slot.OwnerName,
                        ownerPhone: slot.OwnerPhone,
                        vehicleType: slot.VehicleType,
                        vehicleNumber: booking.VehicleNumber,
                        pricePerHour: slot.PricePerHour,
                        totalAmount: (double)totalAmount,
                        bookingFrom: booking.BookingFrom,
                        bookingTo: BookingTo,
                        paymentMode: slot.PaymentMode,
                        qrToken: slot.QrToken
                    );
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"📧 Extension email failed: {ex.Message}");
            }

            // ✅ Store as strings
            TempData["PaymentMode"] = slot.PaymentMode;
            TempData["OwnerUpiId"] = slot.OwnerUpiId ?? "";
            TempData["OwnerName"] = slot.OwnerName;
            TempData["TotalAmount"] = totalAmount.ToString("F2");
            // ✅ UpiQrImagePath removed

            return RedirectToAction("BookingSuccess");
        }

        public IActionResult BookingSuccess()
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Signin", "Auth");

            ViewBag.PaymentMode = TempData["PaymentMode"] as string ?? "";
            ViewBag.OwnerUpiId = TempData["OwnerUpiId"] as string ?? "";
            ViewBag.OwnerName = TempData["OwnerName"] as string ?? "";
            // ✅ Parse back from string
            ViewBag.TotalAmount = double.TryParse(TempData["TotalAmount"] as string, out double amt) ? amt : 0;
            // ✅ UpiQrImagePath removed from ViewBag — not needed

            return View();
        }

        // ══════════════════════════════════════════════
        //  QR EXIT SYSTEM
        // ══════════════════════════════════════════════

        [HttpGet]
        public IActionResult ExitScan(string token, int bid)
        {
            if (string.IsNullOrEmpty(token))
            {
                ViewBag.Error = "Invalid exit link. Please use the link sent in your email.";
                return View();
            }

            var slot = _context.ParkingSlots.FirstOrDefault(s => s.QrToken == token);
            if (slot == null)
            {
                ViewBag.Error = "QR code not recognised. Please contact the slot owner.";
                return View();
            }

            var booking = _context.Bookings.FirstOrDefault(b => b.Id == bid && b.ParkingSlotId == slot.Id);
            if (booking == null)
            {
                ViewBag.Error = "This link is not valid for your booking.";
                return View();
            }

            ViewBag.Token = token;
            ViewBag.BookingId = booking.Id;
            ViewBag.CustomerEmail = booking.CustomerEmail;
            ViewBag.SlotOwner = slot.OwnerName;
            // ✅ DB stores IST — no AddHours needed
            ViewBag.BookingTo = booking.BookingTo.ToString("hh:mm tt, dd MMM");
            return View();
        }

        [HttpPost]
        public IActionResult ConfirmExit([FromBody] ExitConfirmRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.Token))
                return Json(new { success = false, message = "Invalid request." });

            var slot = _context.ParkingSlots.FirstOrDefault(s => s.QrToken == request.Token);
            if (slot == null)
                return Json(new { success = false, message = "QR code not recognised." });

            var booking = _context.Bookings.FirstOrDefault(b =>
                b.Id == request.BookingId &&
                b.ParkingSlotId == slot.Id);

            if (booking == null)
                return Json(new { success = false, message = "Invalid booking." });

            if (!string.IsNullOrEmpty(request.CustomerEmail) &&
                !string.IsNullOrEmpty(booking.CustomerEmail) &&
                !string.Equals(request.CustomerEmail, booking.CustomerEmail, StringComparison.OrdinalIgnoreCase))
                return Json(new { success = false, message = "This link belongs to a different customer." });

            if (booking.ExitConfirmed)
                return Json(new { success = true, message = "Exit already confirmed. Safe journey!" });

            booking.ExitConfirmed = true;
            booking.ExitConfirmedAt = DateTime.Now;
            booking.PenaltyApplied = false;
            _context.Bookings.Update(booking);
            slot.IsBooked = false;
            _context.ParkingSlots.Update(slot);
            _context.SaveChanges();

            Console.WriteLine($"✅ Exit confirmed — Booking #{booking.Id}, Slot #{slot.Id}");
            return Json(new { success = true, message = "Exit confirmed! The slot is now free. Safe journey!" });
        }

        [HttpGet]
        public IActionResult ExitSuccess() => View();

        public async Task TriggerExitScanEmail(Booking booking, ParkingSlot slot)
        {
            try
            {
                if (!string.IsNullOrEmpty(booking.CustomerEmail) && !string.IsNullOrEmpty(slot.QrToken))
                {
                    var scanLink = $"https://smartslot-sc9u.onrender.com/Parking/ExitScan?token={slot.QrToken}&bid={booking.Id}";
                    await _emailService.SendExitScanEmail(
                        toEmail: booking.CustomerEmail,
                        customerName: booking.CustomerName,
                        bookingTo: booking.BookingTo,
                        scanLink: scanLink,
                        bookingId: booking.Id
                    );
                    booking.ExitScanAlertSent = true;
                    _context.Bookings.Update(booking);
                    _context.SaveChanges();
                    Console.WriteLine($"📧 Exit scan email sent — Booking #{booking.Id}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"📧 Exit scan email failed: {ex.Message}");
            }
        }

        // ✅ Route attribute catches /Parking/Review/26
        [HttpGet]
        [Route("Parking/Review/{id:int}")]
        public IActionResult Review(int id)
        {
            if (id <= 0) return Content("Invalid booking ID");
            var booking = _context.Bookings.FirstOrDefault(b => b.Id == id);
            if (booking == null) return Content("Invalid booking ID");
            if (booking.ReviewSubmitted) return RedirectToAction("ReviewSuccess");
            ViewBag.BookingId = booking.Id;
            ViewBag.ParkingSlotId = booking.ParkingSlotId;
            return View();
        }

        [HttpPost]
        public IActionResult SubmitReview(int BookingId, int ParkingSlotId, int Rating, string Comment)
        {
            var booking = _context.Bookings.FirstOrDefault(b => b.Id == BookingId);
            if (booking == null) return NotFound();
            if (booking.ReviewSubmitted) return RedirectToAction("ReviewSuccess");

            var review = new Review
            {
                ParkingSlotId = ParkingSlotId,
                BookingId = BookingId,
                Rating = Rating,
                Comment = Comment,
                CreatedAt = DateTime.Now
            };

            _context.Reviews.Add(review);
            booking.ReviewSubmitted = true;
            _context.Bookings.Update(booking);
            _context.SaveChanges();
            return RedirectToAction("ReviewSuccess");
        }

        [HttpGet]
        public IActionResult ReviewSuccess() => View();

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

            _context.SlotNotifyRequests.Add(new SlotNotifyRequest
            {
                ParkingSlotId = request.SlotId,
                BookingId = request.BookingId,
                CustomerEmail = user.Email,
                CustomerName = user.Username,
                NotificationSent = false,
                CreatedAt = DateTime.UtcNow
            });
            _context.SaveChanges();
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<JsonResult> AnalyzeParking(IFormFile image)
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return Json(new { error = "Unauthorized" });

            if (image == null)
                return Json(new { score = 0, badge = "No image", details = "No image uploaded" });

            using var ms = new MemoryStream();
            await image.CopyToAsync(ms);
            var (score, badge, details) = await _parkingAIService.AnalyzeParkingImage(ms.ToArray());
            return Json(new { score, badge, details });
        }

        public IActionResult Search()
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Signin", "Auth", new { returnUrl = HttpContext.Request.Path });
            return View();
        }

        [HttpGet]
        public JsonResult NearbySlots(double lat, double lon, double radius = 3, string fromTime = null, string toTime = null)
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return Json(new { error = "Unauthorized" });

            // ✅ DB stores IST — use UtcNow+5.5 so Render (UTC) compares correctly against IST dates
            var istNow = DateTime.UtcNow.AddHours(5.5);

            // ✅ Browser sends IST — parse directly, NO .AddHours(-5.5)
            DateTime? filterFrom = null;
            DateTime? filterTo = null;
            if (!string.IsNullOrEmpty(fromTime)) filterFrom = DateTime.Parse(fromTime);
            if (!string.IsNullOrEmpty(toTime)) filterTo = DateTime.Parse(toTime);

            Console.WriteLine($"[NearbySlots] istNow={istNow} filterFrom={filterFrom} filterTo={filterTo}");

            var allSlots = _context.ParkingSlots
                .Where(s => s.AvailableTo > istNow)
                .ToList();

            var nearbySlots = allSlots
                .Where(slot =>
                {
                    if (_distanceService.GetDistance(lat, lon, slot.Latitude, slot.Longitude) > radius)
                        return false;

                    if (filterFrom.HasValue && filterTo.HasValue)
                    {
                        if (slot.AvailableFrom > filterFrom.Value) return false;
                        if (slot.AvailableTo < filterTo.Value) return false;
                    }
                    return true;
                })
                .Select(slot =>
                {
                    bool isBooked;
                    if (filterFrom.HasValue && filterTo.HasValue)
                    {
                        isBooked = _context.Bookings.Any(b =>
                            b.ParkingSlotId == slot.Id &&
                            b.BookingFrom < filterTo.Value &&
                            b.BookingTo > filterFrom.Value &&
                            !b.ExitConfirmed);
                    }
                    else
                    {
                        isBooked = _context.Bookings.Any(b =>
                            b.ParkingSlotId == slot.Id &&
                            b.BookingFrom <= istNow &&
                            b.BookingTo > istNow &&
                            !b.ExitConfirmed);
                    }

                    var activeBookingInfo = _context.Bookings
                        .Where(b =>
                            b.ParkingSlotId == slot.Id &&
                            b.BookingFrom <= istNow &&
                            b.BookingTo > istNow &&
                            !b.ExitConfirmed)
                        .OrderByDescending(b => b.BookingFrom)
                        .FirstOrDefault();

                    var latestBooking = _context.Bookings
                        .Where(b => b.ParkingSlotId == slot.Id && !b.ExitConfirmed)
                        .OrderByDescending(b => b.BookingTo)
                        .FirstOrDefault();

                    DateTime displayAvailableFrom = slot.AvailableFrom;
                    if (latestBooking != null &&
                        latestBooking.BookingTo > slot.AvailableFrom &&
                        latestBooking.BookingTo < slot.AvailableTo)
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
                        isBooked = isBooked,
                        parkingScore = slot.ParkingScore,
                        parkingBadge = slot.ParkingBadge,
                        exitMethod = slot.ExitMethod,
                        averageRating = _context.Reviews
                            .Where(r => r.ParkingSlotId == slot.Id)
                            .Select(r => (double?)r.Rating)
                            .Average() ?? 0,
                        bookingFrom = activeBookingInfo?.BookingFrom,
                        bookingTo = activeBookingInfo?.BookingTo,
                        bookingId = activeBookingInfo?.Id
                    };
                })
                .ToList();

            Console.WriteLine($"[NearbySlots] Slots returned: {nearbySlots.Count}");
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
                .Where(b => b.ParkingSlotId == id && !b.ExitConfirmed)
                .Select(b => new { BookingFrom = b.BookingFrom, BookingTo = b.BookingTo })
                .ToList();

            ViewBag.Slot = slot;
            ViewBag.BookedRanges = bookedRanges;
            ViewBag.IstNow = DateTime.UtcNow.AddHours(5.5).ToString("yyyy-MM-ddTHH:mm");
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
                    b.BookingTo > booking.BookingTo &&
                    !b.ExitConfirmed)
                .OrderBy(b => b.BookingFrom)
                .FirstOrDefault();

            if (conflictingBooking != null)
            {
                TempData["ExtensionError"] = "Sorry, this slot is already reserved after your booking.";
                return RedirectToAction("Search");
            }

            ViewBag.Slot = slot;
            ViewBag.ExistingBookingId = booking.Id;
            ViewBag.ExtensionMode = true;
            ViewBag.ExtensionStart = booking.BookingTo;
            ViewBag.IstNow = DateTime.UtcNow.AddHours(5.5).ToString("yyyy-MM-ddTHH:mm");
            return View("Book");
        }

        [HttpGet]
        public IActionResult AvailableSlots(double lat, double lon, double radius = 3, string fromTime = null, string toTime = null)
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Signin", "Auth", new { returnUrl = HttpContext.Request.Path });

            ViewBag.Lat = lat; ViewBag.Lon = lon;
            ViewBag.Radius = radius;
            ViewBag.FromTime = fromTime; ViewBag.ToTime = toTime;
            return View();
        }

        [HttpGet]
        public IActionResult BookedSlots(double lat, double lon, double radius = 3, string fromTime = null, string toTime = null)
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Signin", "Auth", new { returnUrl = HttpContext.Request.Path });

            ViewBag.Lat = lat; ViewBag.Lon = lon;
            ViewBag.Radius = radius;
            ViewBag.FromTime = fromTime; ViewBag.ToTime = toTime;
            return View();
        }

        [HttpGet]
        public IActionResult DebugTest() =>
            Content("ParkingController is working on Render.");
    }

    public class NotifyMeRequest
    {
        public int SlotId { get; set; }
        public int BookingId { get; set; }
    }

    public class ExitConfirmRequest
    {
        public string Token { get; set; }
        public int BookingId { get; set; }
        public string CustomerEmail { get; set; }
    }
}
