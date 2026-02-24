using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using SmartSlot.Data;

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
                Console.WriteLine($"⏰ Background check at: {DateTime.Now}");

                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        var smsService = scope.ServiceProvider.GetRequiredService<SmsService>();

                        var expiredBookings = context.Bookings
                            .Where(b => !b.ReviewSmsSent)
                            .ToList();

                        Console.WriteLine($"📦 Total bookings pending review: {expiredBookings.Count}");

                        foreach (var booking in expiredBookings)
                        {
                            Console.WriteLine($"🔎 Checking Booking ID: {booking.Id}");
                            Console.WriteLine($"   BookingTo: {booking.BookingTo}");
                            Console.WriteLine($"   CurrentTime: {DateTime.Now}");

                            if (booking.BookingTo <= DateTime.Now)
                            {
                                Console.WriteLine($"✅ Booking {booking.Id} expired. Sending SMS...");

                                try
                                {
                                    await smsService.SendReviewSms(booking.CustomerPhone, booking.Id);

                                    booking.ReviewSmsSent = true;
                                    context.Bookings.Update(booking);
                                    await context.SaveChangesAsync();

                                    Console.WriteLine($"📨 SMS sent for Booking {booking.Id}");
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"❌ SMS Error for Booking {booking.Id}: {ex.Message}");
                                }
                            }
                            else
                            {
                                Console.WriteLine($"⏳ Booking {booking.Id} not yet expired.");
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