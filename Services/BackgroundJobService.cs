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
            var istNow = DateTime.UtcNow.AddHours(5.5);
            Console.WriteLine($"⏰ Background check at (IST): {istNow:MM/dd/yyyy HH:mm:ss}");

            // ── 1. One-hour alert ─────────────────────────────────────────────
            var pendingOneHour = context.Bookings
                .Where(b => !b.OneHourAlertSent)
                .ToList();

            foreach (var booking in pendingOneHour)
            {
                var alertTime = booking.BookingTo.AddHours(-1);

                if (booking.BookingTo <= istNow)
                {
                    booking.OneHourAlertSent = true;
                    context.Bookings.Update(booking);
                    continue;
                }

                if (istNow >= alertTime && istNow < booking.BookingTo)
                {
                    try
                    {
                        // Email
                        if (!string.IsNullOrEmpty(booking.CustomerEmail))
                        {
                            await emailService.SendOneHourAlertEmail(
                                toEmail: booking.CustomerEmail,
                                customerName: booking.CustomerName,
                                bookingTo: booking.BookingTo,
                                bookingId: booking.Id);
                            Console.WriteLine($"📧 1-hour alert sent → Booking #{booking.Id}");
                        }

                        // ✅ Push → taps to /Parking/Extend/{bookingId}
                        var customer = context.Users.FirstOrDefault(u => u.PhoneNumber == booking.CustomerPhone);
                        if (customer != null)
                        {
                            await pushService.SendToUserAsync(
                                customer.Id,
                                "⏰ 1 Hour Left!",
                                $"Your parking ends at {booking.BookingTo:hh:mm tt}. Tap to extend if needed.",
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
                .Where(b => !b.ReviewSmsSent && b.BookingTo <= istNow)
                .ToList();

            foreach (var booking in reviewPending)
            {
                try
                {
                    // Email
                    if (!string.IsNullOrEmpty(booking.CustomerEmail))
                    {
                        await emailService.SendReviewEmail(
                            toEmail: booking.CustomerEmail,
                            customerName: booking.CustomerName,
                            bookingId: booking.Id);
                        Console.WriteLine($"📧 Review email sent → Booking #{booking.Id}");
                    }

                    // ✅ Push → taps to /Parking/Review/{bookingId}
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
                    b.BookingTo <= istNow &&
                    b.BookingTo >= istNow.AddMinutes(-15))
                .ToList();

            foreach (var booking in exitScanPending)
            {
                try
                {
                    var slot = context.ParkingSlots.Find(booking.ParkingSlotId);

                    // Email
                    if (slot != null && !string.IsNullOrEmpty(booking.CustomerEmail))
                    {
                        var scanLink = $"https://smartslot-sc9u.onrender.com/Parking/ExitScan?token={slot.QrToken}&bid={booking.Id}";
                        await emailService.SendExitScanEmail(
                            toEmail: booking.CustomerEmail,
                            customerName: booking.CustomerName,
                            bookingTo: booking.BookingTo,
                            scanLink: scanLink,
                            bookingId: booking.Id);
                        Console.WriteLine($"📧 Exit scan email sent → Booking #{booking.Id}");
                    }

                    // ✅ Push → taps directly to /Parking/ExitScan?token=...&bid=...
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

            // ── 4. Penalty check — email only, NO push ────────────────────────
            var penaltyPending = context.Bookings
                .Where(b =>
                    !b.ExitConfirmed &&
                    !b.PenaltyApplied &&
                    b.BookingTo <= istNow.AddMinutes(-15))
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
                            bookingTo: booking.BookingTo,
                            bookingId: booking.Id,
                            slotOwner: slot?.OwnerName ?? "Owner");
                    }
                    // ❌ No push notification for penalty
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
                    b.BookingFrom <= istNow &&
                    b.BookingTo > istNow);

                if (stillBooked) continue;

                var slot = context.ParkingSlots.Find(waiter.ParkingSlotId);
                if (slot == null) continue;

                try
                {
                    // Email
                    await emailService.SendSlotFreeNotificationEmail(
                        toEmail: waiter.CustomerEmail,
                        customerName: waiter.CustomerName,
                        ownerName: slot.OwnerName,
                        pricePerHour: (decimal)slot.PricePerHour,
                        vehicleType: slot.VehicleType,
                        slotId: slot.Id);

                    // ✅ Push → taps to /Parking/Book/{slotId} (that exact slot)
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
                        b.BookingFrom <= istNow &&
                        b.BookingTo > istNow &&
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

                    // SignalR — update map in real time
                    await hub.Clients.Group("map").SendAsync("SlotUpdated", new
                    {
                        slotId = slot.Id,
                        isBooked = false
                    });

                    // ✅ Push to owner → taps to /Parking/Dashboard
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
