namespace SmartSlot.Models
{
    public class ParkingSlot
    {
        public int Id { get; set; }
        public string OwnerName { get; set; } = "";
        public string OwnerPhone { get; set; } = "";
        public bool IsBooked { get; set; } = false;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double PricePerHour { get; set; }
        public DateTime AvailableFrom { get; set; }
        public DateTime AvailableTo { get; set; }
        public string VehicleType { get; set; } = "";

        public string PaymentMode { get; set; } = "Cash";
        public string? OwnerUpiId { get; set; }

        // ── UPI QR Image (uploaded by owner) ──
        public string? UpiQrImagePath { get; set; }

        // ── Parking Slot Photo (file path) ──
        public string? ParkingImagePath { get; set; }

        public string? ParkingImageBase64 { get; set; }
        public int ParkingScore { get; set; } = 0;
        public string? ParkingBadge { get; set; } = "";
        public string? ParkingScoreDetails { get; set; } = "";

        // ── Owner tracking by UserId ──
        public int UserId { get; set; }

        // ── Exit Verification System ──
        public string ExitMethod { get; set; } = "Manual";
        public string? QrToken { get; set; }
    }
}
