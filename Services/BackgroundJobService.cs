using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartSlot.Data;
using SmartSlot.Hubs;
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
                    var hub = scope.ServiceProvider.GetRequiredService<IHubContext<ParkingHub>>();
                    var pushService = scope.ServiceProvider.GetRequiredService<PushNotificationService>();
                    var now = DateTime.UtcNow;
                    var istNow = now.AddHours(5.5);
                    Console.WriteLine($"⏰ Background check — UTC: {now:HH:mm:ss}  IST: {istNow:HH:mm:ss}");

                    // ADD THESE:
                    Console.WriteLine($"📦 Bookings pending 1-hour alert: {context.Bookings.Count(b => !b.OneHourAlertSent)}");
                    Console.WriteLine($"📦 Bookings pending review email: {context.Bookings.Count(b => !b.ReviewSmsSent && b.BookingTo <= now)}");
                    Console.WriteLine($"📦 Bookings pending exit scan email: {context.Bookings.Count(b => !b.ExitScanAlertSent && !b.ExitConfirmed && b.BookingTo <= now)}");
                    Console.WriteLine($"📦 Bookings pending penalty check: {context.Bookings.Count(b => !b.ExitConfirmed && !b.PenaltyApplied && b.BookingTo <= now.AddMinutes(-15))}");
                    Console.WriteLine($"🔔 Pending notify requests: {context.SlotNotifyRequests.Count(n => !n.NotificationSent)}");
                    await RunJobsAsync(context, emailService, hub, pushService);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"❌ Background job error: {ex.Message}");
                }
                await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
            }
        }


        private async Task RunJobsAsync(
            ApplicationDbContext context,
            EmailService emailService,
            IHubContext<ParkingHub> hub,
            PushNotificationService pushService)
        {
            // ✅ FIX: Use real UTC now — bookings are stored as UTC in DB
            var now = DateTime.UtcNow;
            var istNow = now.AddHours(5.5); // only used for display in logs/emails
            Console.WriteLine($"⏰ Background check — UTC: {now:HH:mm:ss}  IST: {istNow:HH:mm:ss}");

            // ── 1. One-hour alert ─────────────────────────────────────────────
            var pendingOneHour = context.Bookings
                .Where(b => !b.OneHourAlertSent)
                .ToList();

            foreach (var booking in pendingOneHour)
            {
                var alertTime = booking.BookingTo.AddHours(-1);

                if (booking.BookingTo <= now)
                {
                    booking.OneHourAlertSent = true;
                    context.Bookings.Update(booking);
                    continue;
                }

                if (now >= alertTime && now < booking.BookingTo)
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(booking.CustomerEmail))
                        {
                            // Show IST time in email
                            await emailService.SendOneHourAlertEmail(
                                toEmail: booking.CustomerEmail,
                                customerName: booking.CustomerName,
                                bookingTo: booking.BookingTo.AddHours(5.5),
                                bookingId: booking.Id);
                            Console.WriteLine($"📧 1-hour alert sent → Booking #{booking.Id}");
                        }

                        var customer = context.Users.FirstOrDefault(u => u.PhoneNumber == booking.CustomerPhone);
                        if (customer != null)
                        {
                            await pushService.SendToUserAsync(
                                customer.Id,
                                "⏰ 1 Hour Left!",
                                $"Your parking ends at {booking.BookingTo.AddHours(5.5):hh:mm tt}. Tap to extend if needed.",
                                $"/Parking/Extend/{booking.Id}");
                        }

                        booking.OneHourAlertSent = true;
                        context.Bookings.Update(booking);
                    }
                    catch (Exception ex) { Console.WriteLine($"📧 1-hour alert failed #{booking.Id}: {ex.Message}"); }
                }
            }

            // ── 2. Review email ───────────────────────────────────────────────
            var reviewPending = context.Bookings
                .Where(b => !b.ReviewSmsSent && b.BookingTo <= now)
                .ToList();

            foreach (var booking in reviewPending)
            {
                try
                {
                    if (!string.IsNullOrEmpty(booking.CustomerEmail))
                    {
                        await emailService.SendReviewEmail(
                            toEmail: booking.CustomerEmail,
                            customerName: booking.CustomerName,
                            bookingId: booking.Id);
                        Console.WriteLine($"📧 Review email sent → Booking #{booking.Id}");
                    }

                    var customer = context.Users.FirstOrDefault(u => u.PhoneNumber == booking.CustomerPhone);
                    if (customer != null)
                    {
                        await pushService.SendToUserAsync(
                            customer.Id,
                            "⭐ Rate Your Experience",
                            "How was your parking? Tap to leave a quick review.",
                            $"/Parking/Review/{booking.Id}");
                    }

                    booking.ReviewSmsSent = true;
                    context.Bookings.Update(booking);
                }
                catch (Exception ex) { Console.WriteLine($"📧 Review email failed #{booking.Id}: {ex.Message}"); }
            }

            // ── 3. Exit scan alert ────────────────────────────────────────────
            var exitScanPending = context.Bookings
                .Where(b =>
                    !b.ExitScanAlertSent &&
                    !b.ExitConfirmed &&
                    b.BookingTo <= now &&
                    b.BookingTo >= now.AddMinutes(-15))
                .ToList();

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
                            bookingTo: booking.BookingTo.AddHours(5.5), // show IST in email
                            scanLink: scanLink,
                            bookingId: booking.Id);
                        Console.WriteLine($"📧 Exit scan email sent → Booking #{booking.Id}");
                    }

                    var customer = context.Users.FirstOrDefault(u => u.PhoneNumber == booking.CustomerPhone);
                    if (customer != null && slot != null)
                    {
                        await pushService.SendToUserAsync(
                            customer.Id,
                            "🚗 Time to Exit!",
                            "Your booking has ended. Tap to scan QR and confirm exit.",
                            $"/Parking/ExitScan?token={slot.QrToken}&bid={booking.Id}");
                    }

                    booking.ExitScanAlertSent = true;
                    context.Bookings.Update(booking);
                }
                catch (Exception ex) { Console.WriteLine($"📧 Exit scan email failed #{booking.Id}: {ex.Message}"); }
            }

            // ── 4. Penalty — email only, NO push ─────────────────────────────
            var penaltyPending = context.Bookings
                .Where(b =>
                    !b.ExitConfirmed &&
                    !b.PenaltyApplied &&
                    b.BookingTo <= now.AddMinutes(-15))
                .ToList();

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
                            bookingTo: booking.BookingTo.AddHours(5.5), // show IST in email
                            bookingId: booking.Id,
                            slotOwner: slot?.OwnerName ?? "Owner");
                    }
                    booking.PenaltyApplied = true;
                    context.Bookings.Update(booking);
                    Console.WriteLine($"⚠ Penalty applied → Booking #{booking.Id}");
                }
                catch (Exception ex) { Console.WriteLine($"⚠ Penalty failed #{booking.Id}: {ex.Message}"); }
            }

            // ── 5. Slot-free notification (Notify Me) ─────────────────────────
            var notifyPending = context.SlotNotifyRequests
                .Where(n => !n.NotificationSent)
                .ToList();

            foreach (var waiter in notifyPending)
            {
                bool stillBooked = context.Bookings.Any(b =>
                    b.ParkingSlotId == waiter.ParkingSlotId &&
                    !b.ExitConfirmed &&
                    b.BookingFrom <= now &&
                    b.BookingTo > now);

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
                        slotId: slot.Id);

                    var waitingUser = context.Users.FirstOrDefault(u => u.Email == waiter.CustomerEmail);
                    if (waitingUser != null)
                    {
                        await pushService.SendToUserAsync(
                            waitingUser.Id,
                            "🟢 Slot Available!",
                            $"{slot.OwnerName}'s slot is now free. Tap to book it!",
                            $"/Parking/Book/{slot.Id}");
                    }

                    waiter.NotificationSent = true;
                    waiter.SentAt = DateTime.UtcNow;
                    context.SlotNotifyRequests.Update(waiter);
                    Console.WriteLine($"📧 Slot-free alert sent → {waiter.CustomerEmail} Slot #{waiter.ParkingSlotId}");
                }
                catch (Exception ex) { Console.WriteLine($"📧 Slot-free alert failed: {ex.Message}"); }
            }

            context.SaveChanges();

            // ── 6. SignalR — auto-free expired slots ──────────────────────────
            var staleBookedSlots = context.ParkingSlots
                .Where(s => s.IsBooked)
                .ToList()
                .Where(s =>
                {
                    bool hasActiveBooking = context.Bookings.Any(b =>
                        b.ParkingSlotId == s.Id &&
                        b.BookingFrom <= now &&
                        b.BookingTo > now &&
                        !b.ExitConfirmed);
                    return !hasActiveBooking;
                })
                .ToList();

            foreach (var slot in staleBookedSlots)
            {
                try
                {
                    slot.IsBooked = false;
                    context.ParkingSlots.Update(slot);

                    await hub.Clients.Group("map").SendAsync("SlotUpdated", new
                    {
                        slotId = slot.Id,
                        isBooked = false
                    });

                    var owner = context.Users.FirstOrDefault(u => u.PhoneNumber == slot.OwnerPhone);
                    if (owner != null)
                    {
                        await pushService.SendToUserAsync(
                            owner.Id,
                            "🅿️ Slot Now Available",
                            "Your parking slot is now free and visible to customers.",
                            "/Parking/Dashboard");
                    }

                    Console.WriteLine($"📡 SignalR: Slot #{slot.Id} auto-freed");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"📡 SignalR auto-free failed Slot #{slot.Id}: {ex.Message}");
                }
            }

            if (staleBookedSlots.Any())
                context.SaveChanges();
        }
    }
}