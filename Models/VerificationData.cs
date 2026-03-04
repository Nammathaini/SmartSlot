namespace SmartSlot.Models
{
    public class VerificationData
    {
        public string ExtractedPlateFromRC { get; set; } = "";
        public string ExtractedPlateFromVehicle { get; set; } = "";
        public string ExtractedNameFromRC { get; set; } = "";
        public string ExtractedNameFromDL { get; set; } = "";
        public bool PlateMatch { get; set; }
        public bool NameMatch { get; set; }
        public int RiskScore { get; set; }
        public string VerificationStatus { get; set; } = "";
        public List<string> DebugLog { get; set; } = new List<string>();
    }
}