namespace SmartSlot.Models
{
    public class ParkingSlot
    {
        public int Id { get; set; }
        public string OwnerName { get; set; } = "";
        public string OwnerPhone { get; set; } = "";
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double PricePerHour { get; set; }
        public DateTime AvailableFrom { get; set; }
        public DateTime AvailableTo { get; set; }
        public string VehicleType { get; set; } = "";
        public bool IsBooked { get; set; } = false;
        public string PaymentMode { get; set; } = "Cash";
        public string? OwnerUpiId { get; set; }
        public string? ParkingImageBase64 { get; set; }
        public int ParkingScore { get; set; } = 0;
        public string? ParkingBadge { get; set; } = "";
        public string? ParkingScoreDetails { get; set; } = "";
    }
}