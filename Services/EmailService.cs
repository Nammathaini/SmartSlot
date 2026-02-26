using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace SmartSlot.Services
{
    public class EmailService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey = "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";
        private readonly string _fromEmail = "ramsulochana08@gmail.com";
        private readonly string _fromName = "SmartSlot";

        public EmailService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        // ── Slot Added Confirmation ──
        public async Task SendSlotAddedEmail(string toEmail, string ownerName, string vehicleType,
            double pricePerHour, DateTime availableFrom, DateTime availableTo, string paymentMode)
        {
            var istFrom = availableFrom.AddHours(5.5);
            var istTo = availableTo.AddHours(5.5);

            var html = $@"<!DOCTYPE html><html><head><meta charset='utf-8'/>
<style>
  body{{font-family:'Segoe UI',sans-serif;background:#f4f4f4;margin:0;padding:0;}}
  .container{{max-width:520px;margin:30px auto;background:white;border-radius:12px;overflow:hidden;box-shadow:0 2px 12px rgba(0,0,0,0.1);}}
  .header{{background:linear-gradient(135deg,#16a34a,#15803d);padding:30px;text-align:center;}}
  .header h1{{color:white;margin:0;font-size:24px;}}
  .header p{{color:rgba(255,255,255,0.85);margin:6px 0 0;font-size:14px;}}
  .body{{padding:28px 32px;}}
  .detail-row{{display:flex;justify-content:space-between;padding:12px 0;border-bottom:1px solid #f0f0f0;}}
  .detail-row:last-child{{border-bottom:none;}}
  .detail-label{{color:#888;font-size:13px;}}
  .detail-value{{color:#111;font-size:14px;font-weight:600;}}
  .badge{{display:inline-block;background:#dcfce7;color:#16a34a;padding:4px 12px;border-radius:20px;font-size:12px;font-weight:600;margin-bottom:20px;}}
  .footer{{background:#f9f9f9;padding:18px 32px;text-align:center;color:#aaa;font-size:12px;border-top:1px solid #eee;}}
</style></head>
<body><div class='container'>
  <div class='header'><h1>P SmartSlot</h1><p>Your parking slot has been listed!</p></div>
  <div class='body'>
    <span class='badge'>Slot Added Successfully</span>
    <h2 style='color:#111;font-size:18px;margin-bottom:6px;'>Hi {ownerName},</h2>
    <p style='color:#555;font-size:14px;margin-bottom:20px;'>Your parking slot has been successfully listed on SmartSlot.</p>
    <div class='detail-row'><span class='detail-label'>Vehicle Type</span><span class='detail-value'>{vehicleType}</span></div>
    <div class='detail-row'><span class='detail-label'>Price Per Hour</span><span class='detail-value'>Rs.{pricePerHour}/hr</span></div>
    <div class='detail-row'><span class='detail-label'>Available From</span><span class='detail-value'>{istFrom:dd MMM yyyy, hh:mm tt}</span></div>
    <div class='detail-row'><span class='detail-label'>Available To</span><span class='detail-value'>{istTo:dd MMM yyyy, hh:mm tt}</span></div>
    <div class='detail-row'><span class='detail-label'>Payment Mode</span><span class='detail-value'>{paymentMode}</span></div>
  </div>
  <div class='footer'>2025 SmartSlot. All rights reserved.</div>
</div></body></html>";

            await SendEmail(toEmail, ownerName, "Your Parking Slot is Now Live - SmartSlot", html);
        }

        // ── Slot Available Notification ──
        public async Task SendSlotAvailableEmail(string toEmail, string customerName,
            string ownerName, string vehicleType, double pricePerHour, DateTime availableTo)
        {
            var istTo = availableTo.AddHours(5.5);

            var html = $@"<!DOCTYPE html><html><head><meta charset='utf-8'/>
<style>
  body{{font-family:'Segoe UI',sans-serif;background:#f4f4f4;margin:0;padding:0;}}
  .container{{max-width:520px;margin:30px auto;background:white;border-radius:12px;overflow:hidden;box-shadow:0 2px 12px rgba(0,0,0,0.1);}}
  .header{{background:linear-gradient(135deg,#2563eb,#1d4ed8);padding:30px;text-align:center;}}
  .header h1{{color:white;margin:0;font-size:24px;}}
  .body{{padding:28px 32px;}}
  .detail-row{{display:flex;justify-content:space-between;padding:12px 0;border-bottom:1px solid #f0f0f0;}}
  .detail-row:last-child{{border-bottom:none;}}
  .detail-label{{color:#888;font-size:13px;}}
  .detail-value{{color:#111;font-size:14px;font-weight:600;}}
  .badge{{display:inline-block;background:#dbeafe;color:#2563eb;padding:4px 12px;border-radius:20px;font-size:12px;font-weight:600;margin-bottom:20px;}}
  .cta{{display:block;margin:24px auto 0;padding:12px 28px;background:#2563eb;color:white;text-decoration:none;border-radius:8px;font-weight:600;text-align:center;font-size:15px;}}
  .footer{{background:#f9f9f9;padding:18px 32px;text-align:center;color:#aaa;font-size:12px;border-top:1px solid #eee;}}
</style></head>
<body><div class='container'>
  <div class='header'><h1>P SmartSlot</h1><p>A slot is now available!</p></div>
  <div class='body'>
    <span class='badge'>Slot Now Available</span>
    <h2 style='color:#111;font-size:18px;margin-bottom:6px;'>Hi {customerName},</h2>
    <p style='color:#555;font-size:14px;margin-bottom:20px;'>The parking slot you requested is now free. Book it quickly!</p>
    <div class='detail-row'><span class='detail-label'>Owner</span><span class='detail-value'>{ownerName}</span></div>
    <div class='detail-row'><span class='detail-label'>Vehicle Type</span><span class='detail-value'>{vehicleType}</span></div>
    <div class='detail-row'><span class='detail-label'>Price Per Hour</span><span class='detail-value'>Rs.{pricePerHour}/hr</span></div>
    <div class='detail-row'><span class='detail-label'>Available Until</span><span class='detail-value'>{istTo:dd MMM yyyy, hh:mm tt}</span></div>
    <a href='https://smartslot-fkc6.onrender.com/Parking/Search' class='cta'>Book Now →</a>
  </div>
  <div class='footer'>2025 SmartSlot. All rights reserved.</div>
</div></body></html>";

            await SendEmail(toEmail, customerName, "Parking Slot Now Available - SmartSlot", html);
        }

        // ── NEW: Password Reset Link ──
        public async Task SendPasswordResetEmail(string toEmail, string username, string resetLink)
        {
            var html = $@"<!DOCTYPE html><html><head><meta charset='utf-8'/>
<style>
  body{{font-family:'Segoe UI',sans-serif;background:#f4f4f4;margin:0;padding:0;}}
  .container{{max-width:520px;margin:30px auto;background:white;border-radius:12px;overflow:hidden;box-shadow:0 2px 12px rgba(0,0,0,0.1);}}
  .header{{background:linear-gradient(135deg,#3b82f6,#1d4ed8);padding:30px;text-align:center;}}
  .header h1{{color:white;margin:0;font-size:24px;}}
  .header p{{color:rgba(255,255,255,0.85);margin:6px 0 0;font-size:14px;}}
  .body{{padding:28px 32px;text-align:center;}}
  .body h2{{color:#111;font-size:18px;margin-bottom:8px;}}
  .body p{{color:#555;font-size:14px;line-height:1.6;margin-bottom:20px;}}
  .reset-btn{{display:inline-block;padding:14px 32px;background:#3b82f6;color:white;text-decoration:none;border-radius:8px;font-weight:700;font-size:16px;}}
  .warning{{color:#aaa;font-size:12px;margin-top:20px;}}
  .footer{{background:#f9f9f9;padding:18px 32px;text-align:center;color:#aaa;font-size:12px;border-top:1px solid #eee;}}
</style></head>
<body><div class='container'>
  <div class='header'><h1>P SmartSlot</h1><p>Password Reset Request</p></div>
  <div class='body'>
    <h2>Hi {username},</h2>
    <p>We received a request to reset your SmartSlot password.<br/>Click the button below to set a new password.</p>
    <a href='{resetLink}' class='reset-btn'>Reset My Password →</a>
    <p class='warning'>This link expires in 30 minutes.<br/>If you did not request this, please ignore this email.</p>
  </div>
  <div class='footer'>2025 SmartSlot. All rights reserved.</div>
</div></body></html>";

            await SendEmail(toEmail, username, "Reset Your SmartSlot Password", html);
        }

        // ── Shared send helper ──
        private async Task SendEmail(string toEmail, string toName, string subject, string html)
        {
            var payload = new
            {
                sender = new { email = _fromEmail, name = _fromName },
                to = new[] { new { email = toEmail, name = toName } },
                subject = subject,
                htmlContent = html
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email");
            request.Headers.Add("api-key", _apiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"📧 Brevo response: {response.StatusCode} - {body}");
        }
    }
}
