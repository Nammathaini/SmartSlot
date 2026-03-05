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
            var now = DateTime.UtcNow;
            var istNow = now.AddHours(5.5);
            Console.WriteLine($"⏰ Background check — UTC: {now:HH:mm:ss}  IST: {istNow:HH:mm:ss}");

            // ── 1. One-hour alert ─────────────────────────────────────────────
            var pendingOneHour = context.Bookings
                .Where(b => !b.OneHourAlertSent && !b.IsCancelled && !b.ExitConfirmed)
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

            // ── 2. Review email — only after exit confirmed, not cancelled ────
            var reviewPending = context.Bookings
                .Where(b => !b.ReviewSmsSent && !b.IsCancelled && b.ExitConfirmed)
                .ToList();

            Console.WriteLine($"📦 Review pending count: {reviewPending.Count}");

            foreach (var booking in reviewPending)
            {
                try
                {
                    Console.WriteLine($"📧 Attempting review email → Booking #{booking.Id} Email:{booking.CustomerEmail}");
                    if (!string.IsNullOrEmpty(booking.CustomerEmail))
                    {
                        await emailService.SendReviewEmail(
                            toEmail: booking.CustomerEmail,
                            customerName: booking.CustomerName,
                            bookingId: booking.Id);
                        Console.WriteLine($"✅ Review email sent → Booking #{booking.Id}");
                    }
                    else
                    {
                        Console.WriteLine($"⚠️ Review email skipped — no email for Booking #{booking.Id}");
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
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Review email failed #{booking.Id}: {ex.Message}");
                    Console.WriteLine($"❌ Review email stack: {ex.StackTrace}");
                }
            }

            // ── 3. Exit scan alert — fires once at BookingTo ──────────────────
            var exitScanPending = context.Bookings
                .Where(b =>
                    !b.ExitScanAlertSent &&
                    !b.ExitConfirmed &&
                    !b.IsCancelled &&
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
                            bookingTo: booking.BookingTo.AddHours(5.5),
                            scanLink: scanLink,
                            bookingId: booking.Id);
                        Console.WriteLine($"📧 Exit scan email sent → Booking #{booking.Id}");
                    }
                    var customer2 = context.Users.FirstOrDefault(u => u.PhoneNumber == booking.CustomerPhone);
                    if (customer2 != null && context.ParkingSlots.Find(booking.ParkingSlotId) is { } sl)
                    {
                        await pushService.SendToUserAsync(
                            customer2.Id,
                            "🚗 Time to Exit!",
                            "Your booking has ended. Tap to scan QR and confirm exit.",
                            $"/Parking/ExitScan?token={sl.QrToken}&bid={booking.Id}");
                    }
                    booking.ExitScanAlertSent = true;
                    context.Bookings.Update(booking);
                }
                catch (Exception ex) { Console.WriteLine($"📧 Exit scan email failed #{booking.Id}: {ex.Message}"); }
            }

            // ── 4. Penalty — starts 15 mins after BookingTo, rate = 2x PricePerHour ──
            // ✅ Slot stays BOOKED until QR scan — penalty accumulates over time
            // ✅ PenaltyApplied flag = penalty has been CALCULATED and saved (not that it's done)
            var penaltyPending = context.Bookings
                .Where(b =>
                    !b.ExitConfirmed &&
                    !b.IsCancelled &&
                    b.BookingTo <= now.AddMinutes(-15)) // grace period = 15 mins
                .ToList();

            foreach (var booking in penaltyPending)
            {
                try
                {
                    var slot = context.ParkingSlots.Find(booking.ParkingSlotId);
                    if (slot == null) continue;

                    // Calculate how many minutes overdue (after 15-min grace)
                    var overdueMinutes = (now - booking.BookingTo).TotalMinutes - 15;
                    if (overdueMinutes < 0) overdueMinutes = 0;

                    // Penalty = 2x PricePerHour, calculated per minute
                    var penaltyRate = slot.PricePerHour * 2.0; // per hour
                    var penaltyAmount = Math.Round((overdueMinutes / 60.0) * penaltyRate, 2);

                    // Save penalty amount to booking
                    booking.PenaltyAmount = (decimal)penaltyAmount;
                    booking.PenaltyApplied = true;
                    context.Bookings.Update(booking);

                    // Send penalty email only once (first time penalty kicks in)
                    if (!booking.PenaltyEmailSent && overdueMinutes >= 1)
                    {
                        if (!string.IsNullOrEmpty(booking.CustomerEmail))
                        {
                            await emailService.SendPenaltyEmail(
                                toEmail: booking.CustomerEmail,
                                customerName: booking.CustomerName,
                                bookingTo: booking.BookingTo.AddHours(5.5),
                                bookingId: booking.Id,
                                slotOwner: slot.OwnerName ?? "Owner");
                        }
                        booking.PenaltyEmailSent = true;
                        context.Bookings.Update(booking);
                        Console.WriteLine($"⚠️ Penalty started → Booking #{booking.Id} Overdue:{overdueMinutes:F0}m Amount:₹{penaltyAmount}");
                    }
                    else
                    {
                        Console.WriteLine($"⚠️ Penalty updated → Booking #{booking.Id} Overdue:{overdueMinutes:F0}m Amount:₹{penaltyAmount}");
                    }
                }
                catch (Exception ex) { Console.WriteLine($"⚠️ Penalty failed #{booking.Id}: {ex.Message}"); }
            }

            // ── 5. Owner exit notification — slot now free after customer exit ─
            var exitConfirmedBookings = context.Bookings
                .Where(b => b.ExitConfirmed && !b.IsCancelled && !b.OwnerExitNotified)
                .ToList();

            foreach (var booking in exitConfirmedBookings)
            {
                try
                {
                    var slot = context.ParkingSlots.Find(booking.ParkingSlotId);
                    if (slot != null)
                    {
                        // ✅ hasPenalty = true if exit was confirmed after grace period
                        bool hasPenalty = booking.PenaltyApplied && booking.PenaltyAmount > 0;
                        var exitIst = (booking.ExitConfirmedAt ?? DateTime.UtcNow).AddHours(5.5);

                        var owner = context.Users.FirstOrDefault(u => u.Id == slot.UserId);
                        if (owner != null && !string.IsNullOrEmpty(owner.Email))
                        {
                            await emailService.SendOwnerSlotFreeEmail(
                                toEmail: owner.Email,
                                ownerName: slot.OwnerName,
                                customerName: booking.CustomerName,
                                customerPhone: booking.CustomerPhone,
                                vehicleNumber: booking.VehicleNumber,
                                bookingFrom: booking.BookingFrom.AddHours(5.5),
                                bookingTo: booking.BookingTo.AddHours(5.5),
                                exitConfirmedAt: exitIst,
                                hasPenalty: hasPenalty);
                            Console.WriteLine($"📧 Owner exit notification sent → Booking #{booking.Id} Owner:{owner.Email} Penalty:{hasPenalty}");
                        }

                        // ✅ Send customer exit confirmed email (with penalty notice if late)
                        if (!string.IsNullOrEmpty(booking.CustomerEmail))
                        {
                            await emailService.SendCustomerExitConfirmedEmail(
                                toEmail: booking.CustomerEmail,
                                customerName: booking.CustomerName,
                                ownerName: slot.OwnerName ?? "Owner",
                                vehicleNumber: booking.VehicleNumber,
                                bookingFrom: booking.BookingFrom.AddHours(5.5),
                                bookingTo: booking.BookingTo.AddHours(5.5),
                                exitConfirmedAt: exitIst,
                                hasPenalty: hasPenalty);
                            Console.WriteLine($"📧 Customer exit confirmed email sent → Booking #{booking.Id} Penalty:{hasPenalty}");
                        }
                        if (owner != null)
                        {
                            await pushService.SendToUserAsync(
                                owner.Id,
                                "🅿️ Slot Now Free",
                                $"{booking.CustomerName} confirmed exit. Your slot is now available for new bookings!",
                                "/Parking/Dashboard");
                        }
                    }
                    booking.OwnerExitNotified = true;
                    context.Bookings.Update(booking);
                }
                catch (Exception ex) { Console.WriteLine($"❌ Owner exit notification failed #{booking.Id}: {ex.Message}"); }
            }

            // ── 6. Slot-free notification (Notify Me) ─────────────────────────
            var notifyPending = context.SlotNotifyRequests
                .Where(n => !n.NotificationSent)
                .ToList();

            foreach (var waiter in notifyPending)
            {
                // ✅ Slot is only free when NO active unconfirmed booking exists
                bool stillBooked = context.Bookings.Any(b =>
                    b.ParkingSlotId == waiter.ParkingSlotId &&
                    !b.ExitConfirmed &&
                    !b.IsCancelled);

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

            // ── 7. SignalR — ONLY free slots where ExitConfirmed = true ────────
            // ✅ REMOVED time-based auto-free — slot stays booked until QR scan
            var staleBookedSlots = context.ParkingSlots
                .Where(s => s.IsBooked)
                .ToList()
                .Where(s =>
                {
                    // Only free if ALL bookings for this slot are either ExitConfirmed or Cancelled
                    bool hasActiveBooking = context.Bookings.Any(b =>
                        b.ParkingSlotId == s.Id &&
                        !b.ExitConfirmed &&
                        !b.IsCancelled);
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

                    var owner = context.Users.FirstOrDefault(u => u.Id == slot.UserId);
                    if (owner != null)
                    {
                        await pushService.SendToUserAsync(
                            owner.Id,
                            "🅿️ Slot Now Available",
                            "Your parking slot is now free and visible to customers.",
                            "/Parking/Dashboard");
                    }

                    Console.WriteLine($"📡 SignalR: Slot #{slot.Id} freed after exit confirmed");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"📡 SignalR free failed Slot #{slot.Id}: {ex.Message}");
                }
            }

            if (staleBookedSlots.Any())
                context.SaveChanges();
        }
    }
}
