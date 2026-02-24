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
                var istNow = DateTime.UtcNow.AddHours(5.5);
                Console.WriteLine($"⏰ Background check at (IST): {istNow}");

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
                            Console.WriteLine($"   BookingTo (stored): {booking.BookingTo}");
                            Console.WriteLine($"   Current IST time:   {istNow}");

                            if (booking.BookingTo <= istNow)
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
                                Console.WriteLine($"⏳ Booking {booking.Id} not yet expired. BookingTo: {booking.BookingTo}, IST Now: {istNow}");
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