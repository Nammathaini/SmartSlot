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
            var istNow = utcNow.AddHours(5.5);
            Console.WriteLine($"⏰ Background check at (IST): {istNow:MM/dd/yyyy HH:mm:ss}");

            // ── 1. One-hour alert ─────────────────────────────────────────
            var pendingOneHour = context.Bookings
                .Where(b => !b.OneHourAlertSent)
                .ToList();

            Console.WriteLine($"📦 Bookings pending 1-hour alert: {pendingOneHour.Count}");

            foreach (var booking in pendingOneHour)
            {
                var alertTime = booking.BookingTo.AddHours(-1);
                Console.WriteLine($"🔎 Booking {booking.Id} — BookingTo: {booking.BookingTo:MM/dd/yyyy HH:mm:ss}, AlertTime: {alertTime:MM/dd/yyyy HH:mm:ss}, istNow: {istNow:MM/dd/yyyy HH:mm:ss}");

                // Already expired — just mark done so it stops showing in logs
                if (booking.BookingTo <= utcNow)
                {
                    Console.WriteLine($"⏭ Booking {booking.Id} already expired — marking sent to clear from queue");
                    booking.OneHourAlertSent = true;
                    context.Bookings.Update(booking);
                    continue;
                }

                // Send only within the 1-hour window before booking ends
                if (utcNow >= alertTime && utcNow < booking.BookingTo)
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(booking.CustomerEmail))
                        {
                            await emailService.SendOneHourAlertEmail(
                                toEmail: booking.CustomerEmail,
                                customerName: booking.CustomerName,
                                bookingTo: booking.BookingTo,
                                bookingId: booking.Id
                            );
                            Console.WriteLine($"📧 1-hour alert sent → Booking #{booking.Id}");
                        }
                        booking.OneHourAlertSent = true;
                        context.Bookings.Update(booking);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"📧 1-hour alert failed #{booking.Id}: {ex.Message}");
                    }
                }
            }

            // ── 2. Review email ───────────────────────────────────────────
            var reviewPending = context.Bookings
                .Where(b => !b.ReviewSmsSent && b.BookingTo <= utcNow)
                .ToList();

            Console.WriteLine($"📦 Bookings pending review email: {reviewPending.Count}");

            foreach (var booking in reviewPending)
            {
                try
                {
                    if (!string.IsNullOrEmpty(booking.CustomerEmail))
                    {
                        await emailService.SendReviewEmail(
                            toEmail: booking.CustomerEmail,
                            customerName: booking.CustomerName,
                            bookingId: booking.Id
                        );
                        Console.WriteLine($"📧 Review email sent → Booking #{booking.Id}");
                    }
                    booking.ReviewSmsSent = true;
                    context.Bookings.Update(booking);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"📧 Review email failed #{booking.Id}: {ex.Message}");
                }
            }

            // ── 3. Exit scan alert ────────────────────────────────────────
            var exitScanPending = context.Bookings
                .Where(b =>
                    !b.ExitScanAlertSent &&
                    !b.ExitConfirmed &&
                    b.BookingTo <= utcNow &&
                    b.BookingTo >= utcNow.AddMinutes(-15))
                .ToList();

            Console.WriteLine($"📦 Bookings pending exit scan email: {exitScanPending.Count}");

            foreach (var booking in exitScanPending)
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
                        Console.WriteLine($"📧 Exit scan email sent → Booking #{booking.Id}");
                    }
                    booking.ExitScanAlertSent = true;
                    context.Bookings.Update(booking);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"📧 Exit scan email failed #{booking.Id}: {ex.Message}");
                }
            }

            // ── 4. Penalty check ──────────────────────────────────────────
            var penaltyPending = context.Bookings
                .Where(b =>
                    !b.ExitConfirmed &&
                    !b.PenaltyApplied &&
                    b.BookingTo <= utcNow.AddMinutes(-15))
                .ToList();

            Console.WriteLine($"📦 Bookings pending penalty check: {penaltyPending.Count}");

            foreach (var booking in penaltyPending)
            {
                try
                {
                    var slot = context.ParkingSlots.Find(booking.ParkingSlotId);
                    if (!string.IsNullOrEmpty(booking.CustomerEmail))
                    {
                        await emailService.SendPenaltyEmail(
                            toEmail: booking.CustomerEmail,
                            customerName: booking.CustomerName,
                            bookingTo: booking.BookingTo,
                            bookingId: booking.Id,
                            slotOwner: slot?.OwnerName ?? "Owner"
                        );
                    }
                    booking.PenaltyApplied = true;
                    context.Bookings.Update(booking);
                    Console.WriteLine($"⚠ Penalty applied → Booking #{booking.Id}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠ Penalty failed #{booking.Id}: {ex.Message}");
                }
            }

            // ── 5. Slot-free notification (Notify Me) ─────────────────────
            var notifyPending = context.SlotNotifyRequests
                .Where(n => !n.NotificationSent)
                .ToList();

            Console.WriteLine($"🔔 Pending notify requests: {notifyPending.Count}");

            foreach (var waiter in notifyPending)
            {
                bool stillBooked = context.Bookings.Any(b =>
                    b.ParkingSlotId == waiter.ParkingSlotId &&
                    !b.ExitConfirmed &&
                    b.BookingFrom <= utcNow &&
                    b.BookingTo > utcNow);

                if (stillBooked) continue;

                var slot = context.ParkingSlots.Find(waiter.ParkingSlotId);
                if (slot == null) continue;

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
                    Console.WriteLine($"📧 Slot-free alert sent → {waiter.CustomerEmail} for Slot #{waiter.ParkingSlotId}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"📧 Slot-free alert failed: {ex.Message}");
                }
            }

            context.SaveChanges();
        }
    }
}
