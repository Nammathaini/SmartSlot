using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SmartSlot.Models;

namespace SmartSlot.Data
{
    // ✅ IDataProtectionKeyContext — stores DataProtection keys in PostgreSQL
    // Prevents users being logged out on every Render redeploy
    public class ApplicationDbContext : DbContext, IDataProtectionKeyContext
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
        public DbSet<PasswordResetToken> PasswordResetTokens { get; set; }

        // ✅ Browser Web Push subscriptions (VAPID)
        public DbSet<PushSubscription> PushSubscriptions { get; set; }

        // ✅ Required by IDataProtectionKeyContext
        public DbSet<DataProtectionKey> DataProtectionKeys { get; set; }
    }
}
