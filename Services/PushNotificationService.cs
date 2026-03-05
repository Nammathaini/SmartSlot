using Microsoft.Extensions.Configuration;
using SmartSlot.Data;
using System;
using System.Linq;
using System.Threading.Tasks;
using WebPush;

// ✅ FIX: alias SmartSlot model to avoid ambiguity with WebPush.PushSubscription
using AppPushSubscription = SmartSlot.Models.PushSubscription;

namespace SmartSlot.Services
{
    public class PushNotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly string _publicKey;
        private readonly string _privateKey;
        private readonly string _subject;

        public PushNotificationService(ApplicationDbContext context, IConfiguration config)
        {
            _context = context;
            _publicKey = config["Push:VapidPublicKey"] ?? "";
            _privateKey = config["Push:VapidPrivateKey"] ?? "";
            _subject = config["Push:VapidSubject"] ?? "mailto:ramsulochana08@gmail.com";
        }

        // ── Send push by int userId — matches User.Id ─────────────────────
        // ✅ FIX: added icon param to match ParkingController calls
        public async Task SendToUserAsync(
            int userId,
            string title,
            string body,
            string icon = "/images/icon-192.png",
            string url = "/Parking/Dashboard")
        {
            // ✅ FIX: UserId is int — compare correctly
            var subs = _context.PushSubscriptions
                .Where(s => s.UserId == userId)
                .ToList();

            foreach (var sub in subs)
                await SendAsync(sub, title, body, icon, url);
        }

        // ── Send push by string userId (BackgroundJobService uses this) ───
        public async Task SendToUserAsync(
            string userId,
            string title,
            string body,
            string url = "/Parking/Dashboard")
        {
            if (string.IsNullOrEmpty(userId)) return;
            if (!int.TryParse(userId, out int userIdInt)) return;
            await SendToUserAsync(userIdInt, title, body, "/images/icon-192.png", url);
        }

        // ── Send push by phone number ─────────────────────────────────────
        public async Task SendToPhoneAsync(
            string phone,
            string title,
            string body,
            string url = "/Parking/Dashboard")
        {
            if (string.IsNullOrEmpty(phone)) return;
            var user = _context.Users.FirstOrDefault(u => u.PhoneNumber == phone);
            if (user == null) return;
            await SendToUserAsync(user.Id, title, body, "/images/icon-192.png", url);
        }

        // ── Core send ─────────────────────────────────────────────────────
        // ✅ FIX: use AppPushSubscription alias — no ambiguity with WebPush.PushSubscription
        private async Task SendAsync(AppPushSubscription sub, string title, string body, string icon, string url)
        {
            try
            {
                var vapidDetails = new VapidDetails(_subject, _publicKey, _privateKey);
                var client = new WebPushClient();

                var payload = System.Text.Json.JsonSerializer.Serialize(new
                {
                    title,
                    body,
                    url,
                    icon,
                    badge = "/images/badge-72.png"
                });

                // ✅ FIX: use fully qualified WebPush.PushSubscription — no ambiguity
                var pushSub = new WebPush.PushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
                await client.SendNotificationAsync(pushSub, payload, vapidDetails);
                Console.WriteLine($"📲 Push sent → userId:{sub.UserId} | {title}");
            }
            catch (WebPushException ex) when (
                ex.StatusCode == System.Net.HttpStatusCode.Gone ||
                ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Subscription expired — remove it
                _context.PushSubscriptions.Remove(sub);
                _context.SaveChanges();
                Console.WriteLine($"🗑 Stale push subscription removed → userId:{sub.UserId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"📲 Push failed → userId:{sub.UserId}: {ex.Message}");
            }
        }
    }
}
