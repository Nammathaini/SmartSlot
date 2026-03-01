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

                        // ── Alert + Review Emails ──
                        var activeBookings = context.Bookings
                            .Where(b => !b.ReviewSmsSent || !b.OneHourAlertSent)
                            .ToList();

                        Console.WriteLine($"📦 Total bookings pending alert/review: {activeBookings.Count}");

                        foreach (var booking in activeBookings)
                        {
                            Console.WriteLine($"🔎 Checking Booking ID: {booking.Id}");

                            // 🔔 1 HOUR BEFORE END ALERT
                            if (!booking.OneHourAlertSent &&
                                istNow >= booking.BookingTo.AddHours(-30) &&
                                istNow < booking.BookingTo)
                            {
                                try
                                {
                                    await emailService.SendOneHourAlertEmail(
     booking.CustomerEmail,
     booking.CustomerName,
     booking.BookingTo,
     booking.Id  // ← add this
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

                            // ⭐ AFTER BOOKING ENDS → SEND REVIEW EMAIL
                            if (!booking.ReviewSmsSent && booking.BookingTo <= istNow)
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
                        }

                        // ── Slot availability notifications ──
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
                                    b.BookingTo > istNow)
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