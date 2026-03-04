using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using SmartSlot.Data;
using SmartSlot.Models;

namespace SmartSlot.Services
{
    public class ReviewBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;

        public ReviewBackgroundService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Console.WriteLine("🔥 ReviewBackgroundService Started");

            while (!stoppingToken.IsCancellationRequested)
            {
                var istNow = DateTime.UtcNow.AddHours(5.5);
                Console.WriteLine($"⏰ Background check at (IST): {istNow}");

                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();

                        // ══════════════════════════════════════════════
                        // 1️⃣  1-HOUR ALERT EMAIL
                        // ══════════════════════════════════════════════
                        var alertPending = context.Bookings
                            .Where(b => !b.OneHourAlertSent)
                            .ToList();

                        Console.WriteLine($"📦 Bookings pending 1-hour alert: {alertPending.Count}");

                        foreach (var booking in alertPending)
                        {
                            var alertTime = booking.BookingTo.AddHours(-1);
                            Console.WriteLine($"🔎 Booking {booking.Id} — BookingTo: {booking.BookingTo}, AlertTime: {alertTime}, istNow: {istNow}");

                            if (istNow >= alertTime && istNow < booking.BookingTo)
                            {
                                try
                                {
                                    await emailService.SendOneHourAlertEmail(
                                        booking.CustomerEmail,
                                        booking.CustomerName,
                                        booking.BookingTo,
                                        booking.Id
                                    );
                                    booking.OneHourAlertSent = true;
                                    context.Bookings.Update(booking);
                                    await context.SaveChangesAsync();
                                    Console.WriteLine($"📨 1-hour alert EMAIL sent for Booking {booking.Id}");
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"❌ 1-hour Alert Email Error: {ex.Message}");
                                }
                            }
                        }

                        // ══════════════════════════════════════════════
                        // 2️⃣  REVIEW EMAIL (after booking ends)
                        // ══════════════════════════════════════════════
                        var reviewPending = context.Bookings
                            .Where(b => !b.ReviewSmsSent && b.BookingTo <= istNow)
                            .ToList();

                        Console.WriteLine($"📦 Bookings pending review email: {reviewPending.Count}");

                        foreach (var booking in reviewPending)
                        {
                            try
                            {
                                await emailService.SendReviewEmail(
                                    booking.CustomerEmail,
                                    booking.CustomerName,
                                    booking.Id
                                );
                                booking.ReviewSmsSent = true;
                                context.Bookings.Update(booking);
                                await context.SaveChangesAsync();
                                Console.WriteLine($"📨 Review EMAIL sent for Booking {booking.Id}");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"❌ Review Email Error: {ex.Message}");
                            }
                        }

                        // ══════════════════════════════════════════════
                        // 3️⃣  EXIT SCAN EMAIL (QR Exit slots only)
                        //     Fires when booking ends + exit not confirmed
                        //     Link = token + bid so only the right customer can use it
                        // ══════════════════════════════════════════════
                        var exitScanPending = context.Bookings
                            .Where(b =>
                                !b.ExitScanAlertSent &&
                                !b.ExitConfirmed &&
                                b.BookingTo <= istNow)
                            .ToList();

                        Console.WriteLine($"📦 Bookings pending exit scan email: {exitScanPending.Count}");

                        foreach (var booking in exitScanPending)
                        {
                            try
                            {
                                var slot = context.ParkingSlots
                                    .FirstOrDefault(s => s.Id == booking.ParkingSlotId);

                                // Skip non-QR slots, mark done so we don't retry
                                if (slot == null || slot.ExitMethod != "QR" || string.IsNullOrEmpty(slot.QrToken))
                                {
                                    booking.ExitScanAlertSent = true;
                                    context.Bookings.Update(booking);
                                    await context.SaveChangesAsync();
                                    Console.WriteLine($"⏭ Booking {booking.Id} — not a QR slot, skip.");
                                    continue;
                                }

                                if (string.IsNullOrEmpty(booking.CustomerEmail))
                                {
                                    booking.ExitScanAlertSent = true;
                                    context.Bookings.Update(booking);
                                    await context.SaveChangesAsync();
                                    Console.WriteLine($"⚠ Booking {booking.Id} — no email, skip.");
                                    continue;
                                }

                                // Include bid in link so ExitScan page can verify the customer
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
                                await context.SaveChangesAsync();
                                Console.WriteLine($"📨 Exit scan EMAIL sent for Booking {booking.Id} → {booking.CustomerEmail}");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"❌ Exit Scan Email Error for Booking {booking.Id}: {ex.Message}");
                            }
                        }

                        // ══════════════════════════════════════════════
                        // 4️⃣  PENALTY CHECK — 15 min grace period
                        //     (changed from 30 min to 15 min)
                        // ══════════════════════════════════════════════
                        var penaltyPending = context.Bookings
                            .Where(b =>
                                !b.ExitConfirmed &&
                                !b.PenaltyApplied &&
                                b.BookingTo <= istNow.AddMinutes(-15))  // ← 15 min grace
                            .ToList();

                        Console.WriteLine($"📦 Bookings pending penalty check: {penaltyPending.Count}");

                        foreach (var booking in penaltyPending)
                        {
                            try
                            {
                                var slot = context.ParkingSlots
                                    .FirstOrDefault(s => s.Id == booking.ParkingSlotId);

                                // Only penalise QR Exit slots
                                if (slot == null || slot.ExitMethod != "QR")
                                {
                                    booking.PenaltyApplied = true;
                                    context.Bookings.Update(booking);
                                    await context.SaveChangesAsync();
                                    continue;
                                }

                                booking.PenaltyApplied = true;
                                context.Bookings.Update(booking);
                                await context.SaveChangesAsync();

                                Console.WriteLine($"⚠ Penalty applied for Booking {booking.Id} — exit not confirmed 15 mins after BookingTo.");

                                if (!string.IsNullOrEmpty(booking.CustomerEmail))
                                {
                                    await emailService.SendPenaltyEmail(
                                        toEmail: booking.CustomerEmail,
                                        customerName: booking.CustomerName,
                                        bookingTo: booking.BookingTo,
                                        bookingId: booking.Id,
                                        slotOwner: slot.OwnerName
                                    );
                                    Console.WriteLine($"📨 Penalty EMAIL sent for Booking {booking.Id}");
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"❌ Penalty Check Error for Booking {booking.Id}: {ex.Message}");
                            }
                        }

                        // ══════════════════════════════════════════════
                        // 5️⃣  SLOT AVAILABILITY NOTIFICATIONS
                        // ══════════════════════════════════════════════
                        var pendingNotifications = context.SlotNotifyRequests
                            .Where(n => !n.NotificationSent)
                            .ToList();

                        Console.WriteLine($"🔔 Pending notify requests: {pendingNotifications.Count}");

                        foreach (var notify in pendingNotifications)
                        {
                            var waitedBooking = context.Bookings
                                .FirstOrDefault(b => b.Id == notify.BookingId);

                            if (waitedBooking == null || waitedBooking.BookingTo > istNow)
                                continue;

                            var newActiveBooking = context.Bookings
                                .Where(b =>
                                    b.ParkingSlotId == notify.ParkingSlotId &&
                                    b.Id != notify.BookingId &&
                                    b.BookingFrom <= istNow &&
                                    b.BookingTo > istNow &&
                                    !b.ExitConfirmed)
                                .FirstOrDefault();

                            if (newActiveBooking != null)
                            {
                                Console.WriteLine($"⏭ Slot {notify.ParkingSlotId} still booked, skipping notify.");
                                continue;
                            }

                            try
                            {
                                var slot = context.ParkingSlots
                                    .FirstOrDefault(s => s.Id == notify.ParkingSlotId);

                                if (slot != null && !string.IsNullOrEmpty(notify.CustomerEmail))
                                {
                                    await emailService.SendSlotAvailableEmail(
                                        toEmail: notify.CustomerEmail,
                                        customerName: notify.CustomerName,
                                        ownerName: slot.OwnerName,
                                        vehicleType: slot.VehicleType,
                                        pricePerHour: slot.PricePerHour,
                                        availableTo: slot.AvailableTo
                                    );

                                    notify.NotificationSent = true;
                                    context.SlotNotifyRequests.Update(notify);
                                    await context.SaveChangesAsync();
                                    Console.WriteLine($"📧 Slot available email sent to {notify.CustomerEmail}");
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"❌ Slot notify email error: {ex.Message}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"🔥 BackgroundService Error: {ex.Message}");
                }

                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }
}
