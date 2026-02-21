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

        public ParkingController(ApplicationDbContext context, DistanceService distanceService)
        {
            _context = context;
            _distanceService = distanceService;
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

        public IActionResult SlotAdded()
        {
            return View();
        }

        public IActionResult Search()
        {
            return View();
        }

        [HttpGet]
        public JsonResult NearbySlots(double lat, double lon, double radius = 6)
        {
            var allSlots = _context.ParkingSlots.ToList();
            var nearbySlots = allSlots.Where(slot =>
                _distanceService.GetDistance(lat, lon, slot.Latitude, slot.Longitude) <= radius
            ).ToList();
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
        public IActionResult ConfirmBooking(
            Booking booking,
            double totalAmount)
        {
            // Save booking
            _context.Bookings.Add(booking);

            // Mark slot as booked
            var slot = _context.ParkingSlots.Find(booking.ParkingSlotId);
            if (slot != null)
            {
                slot.IsBooked = true;
                _context.ParkingSlots.Update(slot);
            }
            _context.SaveChanges();

            // Payment handled by UPI deep link on booking page
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
    }
}