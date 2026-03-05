using System;
using System.ComponentModel.DataAnnotations;

namespace SmartSlot.Models
{
    public class PushSubscription
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        [Required]
        [MaxLength(1024)]
        public string Endpoint { get; set; } = string.Empty;

        [Required]
        [MaxLength(512)]
        public string P256dh { get; set; } = string.Empty;

        [Required]
        [MaxLength(256)]
        public string Auth { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
