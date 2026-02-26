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
                        var smsService = scope.ServiceProvider.GetRequiredService<SmsService>();
                        var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();

                        // ── Existing: alert + review SMS ──
                        var activeBookings = context.Bookings
                            .Where(b => !b.ReviewSmsSent || !b.OneHourAlertSent)
                            .ToList();

                        Console.WriteLine($"📦 Total bookings pending alert/review: {activeBookings.Count}");

                        foreach (var booking in activeBookings)
                        {
                            Console.WriteLine($"🔎 Checking Booking ID: {booking.Id}");

                            // 🔔 1 HOUR BEFORE END ALERT
                            if (!booking.OneHourAlertSent &&
                                istNow >= booking.BookingTo.AddHours(-1) &&
                                istNow < booking.BookingTo)
                            {
                                try
                                {
                                    await smsService.SendOneHourAlertSms(
                                        booking.CustomerPhone,
                                        booking.BookingTo
                                    );
                                    booking.OneHourAlertSent = true;
                                    context.Bookings.Update(booking);
                                    await context.SaveChangesAsync();
                                    Console.WriteLine($"📨 1-hour alert SMS sent for Booking {booking.Id}");
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"❌ 1-hour Alert SMS Error: {ex.Message}");
                                }
                            }

                            // ⭐ AFTER BOOKING ENDS → SEND REVIEW LINK
                            if (!booking.ReviewSmsSent && booking.BookingTo <= istNow)
                            {
                                try
                                {
                                    await smsService.SendReviewSms(booking.CustomerPhone, booking.Id);
                                    booking.ReviewSmsSent = true;
                                    context.Bookings.Update(booking);
                                    await context.SaveChangesAsync();
                                    Console.WriteLine($"📨 Review SMS sent for Booking {booking.Id}");
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"❌ Review SMS Error: {ex.Message}");
                                }
                            }
                        }

                        // ── NEW: Slot availability notifications ──
                        var pendingNotifications = context.SlotNotifyRequests
                            .Where(n => !n.NotificationSent)
                            .ToList();

                        Console.WriteLine($"🔔 Pending notify requests: {pendingNotifications.Count}");

                        foreach (var notify in pendingNotifications)
                        {
                            // Check if the booking they were waiting on has ended
                            var waitedBooking = context.Bookings
                                .FirstOrDefault(b => b.Id == notify.BookingId);

                            if (waitedBooking == null || waitedBooking.BookingTo > istNow)
                                continue; // Booking not ended yet

                            // Check no new active booking exists for this slot
                            var newActiveBooking = context.Bookings
                                .Where(b =>
                                    b.ParkingSlotId == notify.ParkingSlotId &&
                                    b.Id != notify.BookingId &&
                                    b.BookingFrom <= istNow &&
                                    b.BookingTo > istNow)
                                .FirstOrDefault();

                            if (newActiveBooking != null)
                            {
                                // Slot is still booked by someone else — skip
                                Console.WriteLine($"⏭ Slot {notify.ParkingSlotId} still booked, skipping notify.");
                                continue;
                            }

                            // Slot is FREE — send email notification
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
