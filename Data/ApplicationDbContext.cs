using Microsoft.EntityFrameworkCore;
using SmartSlot.Models;

namespace SmartSlot.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<ParkingSlot> ParkingSlots { get; set; }
        public DbSet<Booking> Bookings { get; set; }
        public DbSet<Review> Reviews { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<SlotNotifyRequest> SlotNotifyRequests { get; set; }

        // ✅ NEW: Password reset tokens
        public DbSet<PasswordResetToken> PasswordResetTokens { get; set; }
    }
}
