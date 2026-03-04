using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartSlot.Data;
using SmartSlot.Services;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SmartSlot.Services
{
    public class BackgroundJobService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<BackgroundJobService> _logger;

        public BackgroundJobService(IServiceScopeFactory scopeFactory, ILogger<BackgroundJobService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("⏰ BackgroundJobService started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();

                    await RunJobsAsync(context, emailService);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"❌ Background job error: {ex.Message}");
                }

                await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
            }
        }

        private async Task RunJobsAsync(ApplicationDbContext context, EmailService emailService)
        {
            var utcNow = DateTime.UtcNow;

            // ── 1. One-hour alert ──────────────────────────────────────────
            var soonBookings = context.Bookings
                .Where(b =>
                    !b.OneHourAlertSent &&
                    !b.ExitConfirmed &&
                    b.BookingTo > utcNow &&
                    b.BookingTo <= utcNow.AddHours(1))
                .ToList();

            foreach (var booking in soonBookings)
            {
                try
                {
                    if (!string.IsNullOrEmpty(booking.CustomerEmail))
                    {
                        var slot = context.ParkingSlots.Find(booking.ParkingSlotId);
                        await emailService.SendOneHourAlert(
                            toEmail: booking.CustomerEmail,
                            customerName: booking.CustomerName,
                            bookingTo: booking.BookingTo,
                            bookingId: booking.Id,
                            slotOwner: slot?.OwnerName ?? "Owner",
                            qrToken: slot?.QrToken ?? ""
                        );
                        booking.OneHourAlertSent = true;
                        context.Bookings.Update(booking);
                        Console.WriteLine($"📧 1-hour alert sent → Booking #{booking.Id}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"📧 1-hour alert failed #{booking.Id}: {ex.Message}");
                }
            }

            // ── 2. Exit scan alert (booking just ended, not yet exited) ───
            var justEnded = context.Bookings
                .Where(b =>
                    !b.ExitScanAlertSent &&
                    !b.ExitConfirmed &&
                    b.BookingTo <= utcNow &&
                    b.BookingTo >= utcNow.AddMinutes(-10))
                .ToList();

            foreach (var booking in justEnded)
            {
                try
                {
                    var slot = context.ParkingSlots.Find(booking.ParkingSlotId);
                    if (slot != null && !string.IsNullOrEmpty(booking.CustomerEmail))
                    {
                        var scanLink = $"https://smartslot-sc9u.onrender.com/Parking/ExitScan?token={slot.QrToken}&bid={booking.Id}";
                        await emailService.SendExitScanEmail(
                            toEmail: booking.CustomerEmail,
                            customerName: booking.CustomerName,
                            bookingTo: booking.BookingTo,
                            scanLink: scanLink,
                            bookingId: booking.Id
                        );
                        booking.ExitScanAlertSent = true;
                        context.Bookings.Update(booking);
                        Console.WriteLine($"📧 Exit scan email sent → Booking #{booking.Id}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"📧 Exit scan email failed #{booking.Id}: {ex.Message}");
                }
            }

            // ── 3. Slot-free notification (Notify Me feature) ─────────────
            // Find bookings that just ended OR exit was confirmed, where someone is waiting
            var freedSlotIds = context.Bookings
                .Where(b =>
                    b.ExitConfirmed ||
                    (b.BookingTo <= utcNow && !b.ExitConfirmed))
                .Select(b => b.ParkingSlotId)
                .Distinct()
                .ToList();

            foreach (var slotId in freedSlotIds)
            {
                // Check if the slot is actually free now (no active booking)
                bool stillBooked = context.Bookings.Any(b =>
                    b.ParkingSlotId == slotId &&
                    !b.ExitConfirmed &&
                    b.BookingFrom <= utcNow &&
                    b.BookingTo > utcNow);

                if (stillBooked) continue;

                // Find pending notify requests for this slot
                var waiters = context.SlotNotifyRequests
                    .Where(n => n.ParkingSlotId == slotId && !n.NotificationSent)
                    .ToList();

                if (!waiters.Any()) continue;

                var slot = context.ParkingSlots.Find(slotId);
                if (slot == null) continue;

                foreach (var waiter in waiters)
                {
                    try
                    {
                        await emailService.SendSlotFreeNotificationEmail(
                            toEmail: waiter.CustomerEmail,
                            customerName: waiter.CustomerName,
                            ownerName: slot.OwnerName,
                            pricePerHour: (decimal)slot.PricePerHour,
                            vehicleType: slot.VehicleType,
                            slotId: slot.Id
                        );
                        waiter.NotificationSent = true;
                        waiter.SentAt = utcNow;
                        context.SlotNotifyRequests.Update(waiter);
                        Console.WriteLine($"📧 Slot-free alert sent → {waiter.CustomerEmail} for Slot #{slotId}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"📧 Slot-free alert failed: {ex.Message}");
                    }
                }
            }

            context.SaveChanges();
        }
    }
}
