namespace SmartSlot.Models
{
    public class SlotNotifyRequest
    {
        public int Id { get; set; }
        public int ParkingSlotId { get; set; }
        public int BookingId { get; set; }       // The booking they're waiting on
        public string CustomerEmail { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public bool NotificationSent { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
