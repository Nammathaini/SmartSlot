namespace SmartSlot.Models
{
    public class SlotNotifyRequest
    {
        public int Id { get; set; }
        public int ParkingSlotId { get; set; }
        public int BookingId { get; set; }
        public string CustomerEmail { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public bool NotificationSent { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? SentAt { get; set; }   // ← ADD THIS LINE
    }
}   