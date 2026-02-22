namespace SmartSlot.Models
{
    public class VerificationData
    {
        public int Id { get; set; }
        public int BookingId { get; set; }
        public string ExtractedPlateFromVehicle { get; set; } = "";
        public string ExtractedPlateFromRC { get; set; } = "";
        public string ExtractedNameFromRC { get; set; } = "";
        public string ExtractedNameFromDL { get; set; } = "";
        public bool PlateMatch { get; set; }
        public bool NameMatch { get; set; }
        public string VerificationStatus { get; set; } = "Pending";
        public int RiskScore { get; set; }
    }
}