using System;

namespace SmartSlot.Models
{
    public class PushSubscription
    {
        public int Id { get; set; }
        public int UserId { get; set; }        // ✅ int — matches User.Id
        public string Endpoint { get; set; }
        public string P256dh { get; set; }
        public string Auth { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
