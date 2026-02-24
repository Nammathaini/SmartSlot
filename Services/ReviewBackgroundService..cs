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
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        var smsService = scope.ServiceProvider.GetRequiredService<SmsService>();

                        var expiredBookings = context.Bookings
                            .Where(b => b.BookingTo <= DateTime.Now && !b.ReviewSmsSent)
                            .ToList();

                        foreach (var booking in expiredBookings)
                        {
                            try
                            {
                                if (!string.IsNullOrWhiteSpace(booking.CustomerPhone))
                                {
                                    await smsService.SendReviewSms(
                                        booking.CustomerPhone,
                                        booking.Id
                                    );

                                    booking.ReviewSmsSent = true;

                                    // Force EF tracking update
                                    context.Bookings.Update(booking);

                                    await context.SaveChangesAsync();
                                }
                                else
                                {
                                    Console.WriteLine(
                                        $"⚠️ Booking {booking.Id} skipped — No phone number."
                                    );

                                    booking.ReviewSmsSent = true;

                                    context.Bookings.Update(booking);

                                    await context.SaveChangesAsync();
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(
                                    $"❌ SMS failed for Booking {booking.Id}: {ex.Message}"
                                );
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