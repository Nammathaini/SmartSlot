using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmartSlot.Data;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using WebPush;

namespace SmartSlot.Services
{
    public class PushNotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _config;
        private readonly ILogger<PushNotificationService> _logger;

        // ✅ Reads from appsettings.json Push section (or Render env vars)
        private string VapidPublicKey => _config["Push:VapidPublicKey"] ?? "";
        private string VapidPrivateKey => _config["Push:VapidPrivateKey"] ?? "";
        private string VapidSubject => _config["Push:VapidSubject"] ?? "mailto:support@smartslot.in";

        public PushNotificationService(
            ApplicationDbContext context,
            IConfiguration config,
            ILogger<PushNotificationService> logger)
        {
            _context = context;
            _config = config;
            _logger = logger;
        }

        // ─────────────────────────────────────────────────────────────────
        //  Send push notification to ALL devices of a user
        //  Called from ParkingController after:
        //    - Slot added   → notifies owner
        //    - Booking confirmed → notifies customer
        // ─────────────────────────────────────────────────────────────────
        public async Task SendToUserAsync(
            int userId,
            string title,
            string body,
            string? icon = null,
            string? url = null)
        {
            var subs = _context.PushSubscriptions
                .Where(s => s.UserId == userId)
                .ToList();

            if (!subs.Any())
            {
                _logger.LogInformation("No push subscriptions for userId={UserId}", userId);
                return;
            }

            // Build JSON payload — sw.js reads these fields
            var payload = JsonSerializer.Serialize(new
            {
                title = title,
                body = body,
                icon = icon ?? "/images/icon-192.png",
                url = url ?? "/Parking/Dashboard",
                tag = "smartslot-" + userId  // replace old notif instead of stacking
            });

            var vapidDetails = new VapidDetails(VapidSubject, VapidPublicKey, VapidPrivateKey);
            var client = new WebPushClient();

            foreach (var sub in subs)
            {
                try
                {
                    var pushSub = new PushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
                    await client.SendNotificationAsync(pushSub, payload, vapidDetails);
                    _logger.LogInformation("📲 Push sent → userId={UserId}", userId);
                }
                catch (WebPushException ex) when (
                    ex.StatusCode == System.Net.HttpStatusCode.Gone ||
                    ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // Subscription expired/invalid — clean it up
                    _context.PushSubscriptions.Remove(sub);
                    _logger.LogWarning("🗑 Stale push sub removed → userId={UserId}", userId);
                }
                catch (Exception ex)
                {
                    _logger.LogError("❌ Push failed → userId={UserId}: {Msg}", userId, ex.Message);
                }
            }

            await _context.SaveChangesAsync();
        }

        public string GetPublicKey() => VapidPublicKey;
    }
}
