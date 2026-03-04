namespace SmartSlot.Models
{
    public class Booking
    {
        public int Id { get; set; }
        public int ParkingSlotId { get; set; }

        public string CustomerName { get; set; }
        public string CustomerPhone { get; set; }
        public string CustomerEmail { get; set; }

        public string VehicleNumber { get; set; }

        public DateTime BookingFrom { get; set; }
        public DateTime BookingTo { get; set; }

        public bool ReviewSmsSent { get; set; } = false;
        public bool ReviewSubmitted { get; set; } = false;
        public bool OneHourAlertSent { get; set; } = false;

        // ── Exit Verification System ──
        public bool ExitConfirmed { get; set; } = false;
        public DateTime? ExitConfirmedAt { get; set; }
        public bool ExitScanAlertSent { get; set; } = false;
        public bool PenaltyApplied { get; set; } = false;
    }
}
