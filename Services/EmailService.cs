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

        // ── Slot Added Confirmation (to owner) ──
        public async Task SendSlotAddedEmail(string toEmail, string ownerName, string vehicleType,
            double pricePerHour, DateTime availableFrom, DateTime availableTo, string paymentMode,
            string qrToken = "")
        {
            var dashboardLink = "https://smartslot-sc9u.onrender.com/Parking/Dashboard";

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
  .qr-box{{background:#f5f3ff;border:1px solid #c4b5fd;border-radius:10px;padding:18px 20px;margin:20px 0;}}
  .qr-box h3{{color:#5b21b6;font-size:15px;margin:0 0 8px;}}
  .qr-box p{{color:#6d28d9;font-size:13px;line-height:1.7;margin:0 0 14px;}}
  .qr-btn{{display:inline-block;padding:11px 24px;background:#7c3aed;color:white;text-decoration:none;border-radius:8px;font-weight:600;font-size:14px;}}
  .footer{{background:#f9f9f9;padding:18px 32px;text-align:center;color:#aaa;font-size:12px;border-top:1px solid #eee;}}
</style></head>
<body><div class='container'>
  <div class='header'><h1>🅿 SmartSlot</h1><p>Your parking slot has been listed!</p></div>
  <div class='body'>
    <span class='badge'>✅ Slot Added Successfully</span>
    <h2 style='color:#111;font-size:18px;margin-bottom:6px;'>Hi {ownerName},</h2>
    <p style='color:#555;font-size:14px;margin-bottom:20px;'>Your parking slot has been successfully listed on SmartSlot.</p>
    <div class='detail-row'><span class='detail-label'>Vehicle Type</span><span class='detail-value'>{vehicleType}</span></div>
    <div class='detail-row'><span class='detail-label'>Price Per Hour</span><span class='detail-value'>₹{pricePerHour}/hr</span></div>
    <div class='detail-row'><span class='detail-label'>Available From</span><span class='detail-value'>{availableFrom:dd MMM yyyy, hh:mm tt}</span></div>
    <div class='detail-row'><span class='detail-label'>Available To</span><span class='detail-value'>{availableTo:dd MMM yyyy, hh:mm tt}</span></div>
    <div class='detail-row'><span class='detail-label'>Payment Mode</span><span class='detail-value'>{paymentMode}</span></div>
    <div class='qr-box'>
      <h3>📷 QR Code Generated for Your Slot!</h3>
      <p>
        A unique QR code has been generated for your slot and is available in your Dashboard.<br/><br/>
        <strong>What to do:</strong> Print or display this QR code at your parking spot so customers can scan it to confirm their exit.<br/><br/>
        ⚠️ Customers who do not scan the QR within <strong>15 minutes</strong> after their booking ends will be penalised ₹20/hour.
      </p>
      <a href='{dashboardLink}' class='qr-btn'>📊 View QR in Dashboard →</a>
    </div>
  </div>
  <div class='footer'>2025 SmartSlot. All rights reserved.</div>
</div></body></html>";

            await SendEmail(toEmail, ownerName, "✅ Your Parking Slot is Now Live — SmartSlot", html);
        }

        // ── ✅ NEW: Owner Notification when slot is booked by a customer ──
        public async Task SendOwnerBookingNotificationEmail(
            string toEmail, string ownerName,
            string customerName, string customerPhone, string customerEmail,
            string vehicleType, string vehicleNumber,
            double pricePerHour, double totalAmount,
            DateTime bookingFrom, DateTime bookingTo,
            string paymentMode)
        {
            var html = $@"<!DOCTYPE html><html><head><meta charset='utf-8'/>
<style>
  body{{font-family:'Segoe UI',sans-serif;background:#f4f4f4;margin:0;padding:0;}}
  .container{{max-width:520px;margin:30px auto;background:white;border-radius:12px;overflow:hidden;box-shadow:0 2px 12px rgba(0,0,0,0.1);}}
  .header{{background:linear-gradient(135deg,#0f766e,#0d9488);padding:30px;text-align:center;}}
  .header h1{{color:white;margin:0;font-size:24px;}}
  .header p{{color:rgba(255,255,255,0.85);margin:6px 0 0;font-size:14px;}}
  .body{{padding:28px 32px;}}
  .badge{{display:inline-block;background:#ccfbf1;color:#0f766e;padding:4px 12px;border-radius:20px;font-size:12px;font-weight:600;margin-bottom:20px;}}
  .section-title{{font-size:12px;font-weight:700;text-transform:uppercase;letter-spacing:1px;color:#888;margin:20px 0 10px;}}
  .detail-row{{display:flex;justify-content:space-between;padding:11px 0;border-bottom:1px solid #f0f0f0;}}
  .detail-row:last-child{{border-bottom:none;}}
  .detail-label{{color:#888;font-size:13px;}}
  .detail-value{{color:#111;font-size:14px;font-weight:600;}}
  .total-box{{background:#f0fdf4;border:1px solid #86efac;border-radius:8px;padding:16px;text-align:center;margin:20px 0;}}
  .total-box .amount{{font-size:28px;font-weight:800;color:#16a34a;}}
  .total-box .label{{color:#555;font-size:13px;margin-top:4px;}}
  .info-note{{background:#eff6ff;border:1px solid #bfdbfe;border-radius:8px;padding:14px;color:#1d4ed8;font-size:13px;line-height:1.6;margin-top:16px;}}
  .footer{{background:#f9f9f9;padding:18px 32px;text-align:center;color:#aaa;font-size:12px;border-top:1px solid #eee;}}
</style></head>
<body><div class='container'>
  <div class='header'><h1>🅿 SmartSlot</h1><p>Your slot has been booked!</p></div>
  <div class='body'>
    <span class='badge'>🎉 New Booking Received</span>
    <h2 style='color:#111;font-size:18px;margin-bottom:6px;'>Hi {ownerName},</h2>
    <p style='color:#555;font-size:14px;margin-bottom:4px;'>Your parking slot has been successfully booked by a customer. Here are the details:</p>

    <div class='section-title'>Customer Details</div>
    <div class='detail-row'><span class='detail-label'>Customer Name</span><span class='detail-value'>{customerName}</span></div>
    <div class='detail-row'><span class='detail-label'>Customer Phone</span><span class='detail-value'>{customerPhone}</span></div>
    <div class='detail-row'><span class='detail-label'>Customer Email</span><span class='detail-value'>{customerEmail}</span></div>
    <div class='detail-row'><span class='detail-label'>Vehicle Number</span><span class='detail-value'>{vehicleNumber}</span></div>

    <div class='section-title'>Booking Details</div>
    <div class='detail-row'><span class='detail-label'>Vehicle Type</span><span class='detail-value'>{vehicleType}</span></div>
    <div class='detail-row'><span class='detail-label'>Booking From</span><span class='detail-value'>{bookingFrom:dd MMM yyyy, hh:mm tt}</span></div>
    <div class='detail-row'><span class='detail-label'>Booking To</span><span class='detail-value'>{bookingTo:dd MMM yyyy, hh:mm tt}</span></div>
    <div class='detail-row'><span class='detail-label'>Rate</span><span class='detail-value'>₹{pricePerHour}/hr</span></div>
    <div class='detail-row'><span class='detail-label'>Payment Mode</span><span class='detail-value'>{paymentMode}</span></div>

    <div class='total-box'>
      <div class='amount'>₹{totalAmount:F2}</div>
      <div class='label'>Total Amount to Collect</div>
    </div>

    <div class='info-note'>
      ℹ️ The customer has been notified. Please ensure your slot is ready and the QR code is displayed at the spot for exit verification.
    </div>
  </div>
  <div class='footer'>2025 SmartSlot. All rights reserved.</div>
</div></body></html>";

            await SendEmail(toEmail, ownerName, "🎉 Your Slot Has Been Booked — SmartSlot", html);
        }

        // ── ✅ UPDATED: Customer Booking Confirmation — redesigned with location ──
        public async Task SendBookingConfirmationEmail(string toEmail, string customerName,
            string ownerName, string ownerPhone, string vehicleType, string vehicleNumber,
            double pricePerHour, double totalAmount, DateTime bookingFrom, DateTime bookingTo,
            string paymentMode, string qrToken = "",
            double latitude = 0, double longitude = 0)
        {
            // ✅ Times are already IST from DB — no AddHours needed
            var locationLink = (latitude != 0 && longitude != 0)
                ? $"https://www.google.com/maps?q={latitude},{longitude}"
                : "";

            var locationRow = !string.IsNullOrEmpty(locationLink)
                ? $"<div class='detail-row'><span class='detail-label'>Slot Location</span><span class='detail-value'><a href='{locationLink}' style='color:#2563eb;'>View on Map →</a></span></div>"
                : "";

            var html = $@"<!DOCTYPE html><html><head><meta charset='utf-8'/>
<style>
  body{{font-family:'Segoe UI',sans-serif;background:#f4f4f4;margin:0;padding:0;}}
  .container{{max-width:520px;margin:30px auto;background:white;border-radius:12px;overflow:hidden;box-shadow:0 2px 12px rgba(0,0,0,0.1);}}
  .header{{background:linear-gradient(135deg,#2563eb,#1d4ed8);padding:30px;text-align:center;}}
  .header h1{{color:white;margin:0;font-size:24px;}}
  .header p{{color:rgba(255,255,255,0.85);margin:6px 0 0;font-size:14px;}}
  .body{{padding:28px 32px;}}
  .body h2{{color:#111;font-size:18px;margin-bottom:6px;}}
  .body p{{color:#555;font-size:14px;margin-bottom:20px;}}
  .detail-row{{display:flex;justify-content:space-between;padding:12px 0;border-bottom:1px solid #f0f0f0;}}
  .detail-row:last-child{{border-bottom:none;}}
  .detail-label{{color:#888;font-size:13px;}}
  .detail-value{{color:#111;font-size:14px;font-weight:600;}}
  .badge{{display:inline-block;background:#dbeafe;color:#2563eb;padding:4px 12px;border-radius:20px;font-size:12px;font-weight:600;margin-bottom:20px;}}
  .section-title{{font-size:12px;font-weight:700;text-transform:uppercase;letter-spacing:1px;color:#888;margin:20px 0 10px;}}
  .total-box{{background:#f0fdf4;border:1px solid #86efac;border-radius:8px;padding:16px;text-align:center;margin:20px 0;}}
  .total-box .amount{{font-size:28px;font-weight:800;color:#16a34a;}}
  .total-box .label{{color:#555;font-size:13px;margin-top:4px;}}
  .qr-notice{{background:#faf5ff;border:1px solid #d8b4fe;border-radius:10px;padding:16px 18px;margin-top:20px;}}
  .qr-notice h4{{color:#7c3aed;font-size:14px;margin:0 0 8px;}}
  .qr-notice p{{color:#6d28d9;font-size:13px;line-height:1.7;margin:0;}}
  .footer{{background:#f9f9f9;padding:18px 32px;text-align:center;color:#aaa;font-size:12px;border-top:1px solid #eee;}}
</style></head>
<body><div class='container'>
  <div class='header'><h1>🅿 SmartSlot</h1><p>Booking Confirmed!</p></div>
  <div class='body'>
    <span class='badge'>✅ Booking Confirmed</span>
    <h2>Hi {customerName},</h2>
    <p>Your parking slot has been successfully booked. Here are your details:</p>

    <div class='section-title'>Owner & Slot Info</div>
    <div class='detail-row'><span class='detail-label'>Owner Name</span><span class='detail-value'>{ownerName}</span></div>
    <div class='detail-row'><span class='detail-label'>Owner Phone</span><span class='detail-value'>{ownerPhone}</span></div>
    <div class='detail-row'><span class='detail-label'>Vehicle Type</span><span class='detail-value'>{vehicleType}</span></div>
    <div class='detail-row'><span class='detail-label'>Payment Mode</span><span class='detail-value'>{paymentMode}</span></div>
    {locationRow}

    <div class='section-title'>Your Booking</div>
    <div class='detail-row'><span class='detail-label'>Your Vehicle No.</span><span class='detail-value'>{vehicleNumber}</span></div>
    <div class='detail-row'><span class='detail-label'>Booking From</span><span class='detail-value'>{bookingFrom:dd MMM yyyy, hh:mm tt}</span></div>
    <div class='detail-row'><span class='detail-label'>Booking To</span><span class='detail-value'>{bookingTo:dd MMM yyyy, hh:mm tt}</span></div>
    <div class='detail-row'><span class='detail-label'>Rate</span><span class='detail-value'>₹{pricePerHour}/hr</span></div>

    <div class='total-box'>
      <div class='amount'>₹{totalAmount:F2}</div>
      <div class='label'>Total Amount</div>
    </div>

    <div class='qr-notice'>
      <h4>📷 QR Exit — Important</h4>
      <p>
        A QR code is generated for this slot and displayed at the parking spot by the owner.<br/><br/>
        When your booking ends, you will receive a personal scan link via email — use it to scan the QR at the slot to <strong>confirm your exit</strong>.<br/><br/>
        ⚠️ If exit is not confirmed within <strong>15 minutes</strong> of your booking ending, a penalty of <strong>₹20/hour</strong> will be applied.
      </p>
    </div>
    <p style='color:#888;font-size:12px;text-align:center;margin-top:16px;'>Please arrive on time. Contact the owner if you need assistance.</p>
  </div>
  <div class='footer'>You are receiving this because you booked a slot on SmartSlot.<br/>2025 SmartSlot. All rights reserved.</div>
</div></body></html>";

            await SendEmail(toEmail, customerName, "✅ Booking Confirmed — SmartSlot", html);
        }

        // ── Slot Available Notification ──
        public async Task SendSlotAvailableEmail(string toEmail, string customerName,
            string ownerName, string vehicleType, double pricePerHour, DateTime availableTo)
        {
            var html = $@"<!DOCTYPE html><html><head><meta charset='utf-8'/>
<style>
  body{{font-family:'Segoe UI',sans-serif;background:#f4f4f4;margin:0;padding:0;}}
  .container{{max-width:520px;margin:30px auto;background:white;border-radius:12px;overflow:hidden;box-shadow:0 2px 12px rgba(0,0,0,0.1);}}
  .header{{background:linear-gradient(135deg,#f59e0b,#d97706);padding:30px;text-align:center;}}
  .header h1{{color:white;margin:0;font-size:24px;}}
  .header p{{color:rgba(255,255,255,0.85);margin:6px 0 0;font-size:14px;}}
  .body{{padding:28px 32px;}}
  .detail-row{{display:flex;justify-content:space-between;padding:12px 0;border-bottom:1px solid #f0f0f0;}}
  .detail-row:last-child{{border-bottom:none;}}
  .detail-label{{color:#888;font-size:13px;}}
  .detail-value{{color:#111;font-size:14px;font-weight:600;}}
  .badge{{display:inline-block;background:#fef3c7;color:#d97706;padding:4px 12px;border-radius:20px;font-size:12px;font-weight:600;margin-bottom:20px;}}
  .cta{{display:block;margin:24px auto 0;padding:12px 28px;background:#2563eb;color:white;text-decoration:none;border-radius:8px;font-weight:600;text-align:center;font-size:15px;}}
  .footer{{background:#f9f9f9;padding:18px 32px;text-align:center;color:#aaa;font-size:12px;border-top:1px solid #eee;}}
</style></head>
<body><div class='container'>
  <div class='header'><h1>🅿 SmartSlot</h1><p>A slot is now available!</p></div>
  <div class='body'>
    <span class='badge'>Slot Now Available</span>
    <h2 style='color:#111;font-size:18px;margin-bottom:6px;'>Hi {customerName},</h2>
    <p style='color:#555;font-size:14px;margin-bottom:20px;'>The parking slot you requested is now free. Book it quickly!</p>
    <div class='detail-row'><span class='detail-label'>Owner</span><span class='detail-value'>{ownerName}</span></div>
    <div class='detail-row'><span class='detail-label'>Vehicle Type</span><span class='detail-value'>{vehicleType}</span></div>
    <div class='detail-row'><span class='detail-label'>Price Per Hour</span><span class='detail-value'>₹{pricePerHour}/hr</span></div>
    <div class='detail-row'><span class='detail-label'>Available Until</span><span class='detail-value'>{availableTo:dd MMM yyyy, hh:mm tt}</span></div>
    <a href='https://smartslot-sc9u.onrender.com/Parking/Search' class='cta'>Book Now →</a>
  </div>
  <div class='footer'>2025 SmartSlot. All rights reserved.</div>
</div></body></html>";

            await SendEmail(toEmail, customerName, "🅿 Parking Slot Now Available — SmartSlot", html);
        }

        // ── Password Reset ──
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
  <div class='header'><h1>🅿 SmartSlot</h1><p>Password Reset Request</p></div>
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

        // ── Review Email ──
        public async Task SendReviewEmail(string toEmail, string customerName, int bookingId)
        {
            var reviewLink = $"https://smartslot-sc9u.onrender.com/Parking/Review/{bookingId}";

            var html = $@"<!DOCTYPE html><html><head><meta charset='utf-8'/>
<style>
  body{{font-family:'Segoe UI',sans-serif;background:#f4f4f4;margin:0;padding:0;}}
  .container{{max-width:520px;margin:30px auto;background:white;border-radius:12px;overflow:hidden;box-shadow:0 2px 12px rgba(0,0,0,0.1);}}
  .header{{background:linear-gradient(135deg,#7c3aed,#5b21b6);padding:30px;text-align:center;}}
  .header h1{{color:white;margin:0;font-size:24px;}}
  .header p{{color:rgba(255,255,255,0.85);margin:6px 0 0;font-size:14px;}}
  .body{{padding:28px 32px;text-align:center;}}
  .body h2{{color:#111;font-size:18px;margin-bottom:8px;}}
  .body p{{color:#555;font-size:14px;line-height:1.6;margin-bottom:20px;}}
  .stars{{font-size:32px;margin:16px 0;}}
  .cta{{display:inline-block;padding:14px 32px;background:#7c3aed;color:white;text-decoration:none;border-radius:8px;font-weight:700;font-size:16px;}}
  .footer{{background:#f9f9f9;padding:18px 32px;text-align:center;color:#aaa;font-size:12px;border-top:1px solid #eee;}}
</style></head>
<body><div class='container'>
  <div class='header'><h1>🅿 SmartSlot</h1><p>How was your parking experience?</p></div>
  <div class='body'>
    <div class='stars'>⭐⭐⭐⭐⭐</div>
    <h2>Hi {customerName},</h2>
    <p>Your parking session has ended. We'd love to hear your feedback! It only takes 10 seconds.</p>
    <a href='{reviewLink}' class='cta'>Rate Your Experience →</a>
    <p style='color:#aaa;font-size:12px;margin-top:20px;'>Thank you for choosing SmartSlot!</p>
  </div>
  <div class='footer'>2025 SmartSlot. All rights reserved.</div>
</div></body></html>";

            await SendEmail(toEmail, customerName, "How was your SmartSlot experience? ⭐", html);
        }

        // ── 1-Hour Alert Email ──
        public async Task SendOneHourAlertEmail(string toEmail, string customerName, DateTime bookingTo, int bookingId)
        {
            // ✅ FIX: bookingTo already stored as IST in DB — no AddHours needed
            string formattedTime = bookingTo.ToString("hh:mm tt, dd MMM yyyy");
            var extendLink = $"https://smartslot-sc9u.onrender.com/Parking/Extend/{bookingId}";

            var html = $@"<!DOCTYPE html><html><head><meta charset='utf-8'/>
<style>
  body{{font-family:'Segoe UI',sans-serif;background:#f4f4f4;margin:0;padding:0;}}
  .container{{max-width:520px;margin:30px auto;background:white;border-radius:12px;overflow:hidden;box-shadow:0 2px 12px rgba(0,0,0,0.1);}}
  .header{{background:linear-gradient(135deg,#dc2626,#b91c1c);padding:30px;text-align:center;}}
  .header h1{{color:white;margin:0;font-size:24px;}}
  .header p{{color:rgba(255,255,255,0.85);margin:6px 0 0;font-size:14px;}}
  .body{{padding:28px 32px;text-align:center;}}
  .body h2{{color:#111;font-size:18px;margin-bottom:8px;}}
  .body p{{color:#555;font-size:14px;line-height:1.6;}}
  .time-box{{background:#fef2f2;border:2px solid #fca5a5;border-radius:10px;padding:20px;margin:20px 0;}}
  .time-box .time{{font-size:28px;font-weight:800;color:#dc2626;}}
  .time-box .label{{color:#888;font-size:13px;margin-top:4px;}}
  .warning{{background:#fff7ed;border:1px solid #fed7aa;border-radius:8px;padding:14px;color:#c2410c;font-size:13px;margin-top:16px;line-height:1.6;}}
  .extend-btn{{display:block;margin:16px auto 0;padding:12px 28px;background:#2563eb;color:white;text-decoration:none;border-radius:8px;font-weight:700;font-size:15px;text-align:center;}}
  .footer{{background:#f9f9f9;padding:18px 32px;text-align:center;color:#aaa;font-size:12px;border-top:1px solid #eee;}}
</style></head>
<body><div class='container'>
  <div class='header'><h1>🅿 SmartSlot</h1><p>⚠ Parking Expiry Reminder</p></div>
  <div class='body'>
    <h2>Hi {customerName},</h2>
    <p>Your parking slot expires in about 1 hour. Please make sure to clear the slot on time — or extend below.</p>
    <div class='time-box'>
      <div class='time'>{formattedTime}</div>
      <div class='label'>Your parking ends at (IST)</div>
    </div>
    <div class='warning'>⚠ If exit QR is not scanned within 15 minutes of booking end, a ₹20/hour penalty will apply.</div>
    <a href='{extendLink}' class='extend-btn'>🔄 Extend My Booking →</a>
  </div>
  <div class='footer'>2025 SmartSlot. All rights reserved.</div>
</div></body></html>";

            await SendEmail(toEmail, customerName, "⚠ Your SmartSlot parking ends in 1 hour!", html);
        }

        // ── EXIT SCAN EMAIL ──
        public async Task SendExitScanEmail(string toEmail, string customerName, DateTime bookingTo, string scanLink, int bookingId)
        {
            // ✅ FIX: bookingTo already stored as IST in DB — no AddHours needed
            string formattedTime = bookingTo.ToString("hh:mm tt, dd MMM yyyy");

            var html = $@"<!DOCTYPE html><html><head><meta charset='utf-8'/>
<style>
  body{{font-family:'Segoe UI',sans-serif;background:#f4f4f4;margin:0;padding:0;}}
  .container{{max-width:520px;margin:30px auto;background:white;border-radius:12px;overflow:hidden;box-shadow:0 2px 12px rgba(0,0,0,0.1);}}
  .header{{background:linear-gradient(135deg,#7c3aed,#4f46e5);padding:30px;text-align:center;}}
  .header h1{{color:white;margin:0;font-size:24px;}}
  .header p{{color:rgba(255,255,255,0.85);margin:6px 0 0;font-size:14px;}}
  .body{{padding:28px 32px;text-align:center;}}
  .body h2{{color:#111;font-size:18px;margin-bottom:8px;}}
  .body p{{color:#555;font-size:14px;line-height:1.6;margin-bottom:20px;}}
  .qr-icon{{font-size:52px;margin:16px 0;}}
  .scan-btn{{display:block;padding:16px 28px;background:linear-gradient(90deg,#7c3aed,#4f46e5);color:white;text-decoration:none;border-radius:10px;font-weight:700;font-size:16px;text-align:center;margin:0 auto 16px;}}
  .penalty-box{{background:#fef2f2;border:1px solid #fca5a5;border-radius:8px;padding:14px;color:#dc2626;font-size:13px;margin-top:16px;line-height:1.6;}}
  .steps{{text-align:left;margin:20px 0;}}
  .step{{display:flex;align-items:flex-start;gap:12px;padding:8px 0;border-bottom:1px solid #f0f0f0;font-size:13px;color:#444;}}
  .step:last-child{{border-bottom:none;}}
  .step-num{{width:24px;height:24px;background:#4f46e5;color:white;border-radius:50%;display:flex;align-items:center;justify-content:center;font-size:12px;font-weight:700;flex-shrink:0;}}
  .lock-note{{background:#f0fdf4;border:1px solid #86efac;border-radius:8px;padding:12px;font-size:12px;color:#15803d;margin-top:12px;line-height:1.6;}}
  .footer{{background:#f9f9f9;padding:18px 32px;text-align:center;color:#aaa;font-size:12px;border-top:1px solid #eee;}}
</style></head>
<body><div class='container'>
  <div class='header'><h1>🅿 SmartSlot</h1><p>📷 Exit Verification Required</p></div>
  <div class='body'>
    <div class='qr-icon'>📷</div>
    <h2>Hi {customerName},</h2>
    <p>Your parking booking ended at <strong>{formattedTime} (IST)</strong>. Please confirm your exit by scanning the QR code at the slot.</p>
    <div class='steps'>
      <div class='step'><div class='step-num'>1</div><span>Tap the button below — this link is personal to you</span></div>
      <div class='step'><div class='step-num'>2</div><span>Point your camera at the QR code at the slot, or upload the QR image</span></div>
      <div class='step'><div class='step-num'>3</div><span>Tap <strong>Confirm Exit</strong> — the slot is immediately freed ✅</span></div>
    </div>
    <a href='{scanLink}' class='scan-btn'>📷 Scan QR to Confirm Exit →</a>
    <div class='lock-note'>🔒 This link is unique to your booking. No one else can use it to scan or confirm your exit.</div>
    <div class='penalty-box'>
      ⚠ <strong>Important:</strong> If exit is not confirmed within <strong>15 minutes</strong> of your booking ending, a penalty of <strong>₹20 per hour</strong> will be applied.
    </div>
  </div>
  <div class='footer'>Booking #{bookingId} · 2025 SmartSlot. All rights reserved.</div>
</div></body></html>";

            await SendEmail(toEmail, customerName, "📷 Confirm Your Exit — SmartSlot", html);
        }

        // ── Slot Free Notification (Notify Me) ──
        public async Task SendSlotFreeNotificationEmail(
            string toEmail, string customerName, string ownerName,
            decimal pricePerHour, string vehicleType, int slotId)
        {
            var bookUrl = $"https://smartslot-sc9u.onrender.com/Parking/Book/{slotId}";

            var body = $@"<!DOCTYPE html><html><head><meta charset='utf-8'/>
<style>
  body{{margin:0;padding:0;background:#0F172A;font-family:'Segoe UI',Arial,sans-serif;}}
  .wrap{{max-width:520px;margin:40px auto;background:rgba(20,30,48,0.98);border:1px solid rgba(255,255,255,0.08);border-radius:20px;overflow:hidden;}}
  .header{{background:linear-gradient(135deg,#10b981,#059669);padding:32px 36px 24px;text-align:center;}}
  .header h1{{margin:0;color:#fff;font-size:22px;font-weight:800;}}
  .header p{{margin:6px 0 0;color:rgba(255,255,255,0.8);font-size:13px;}}
  .icon{{font-size:40px;margin-bottom:12px;}}
  .body{{padding:28px 36px;}}
  .hi{{font-size:16px;color:#f1f5f9;margin-bottom:6px;}}
  .msg{{font-size:14px;color:#94A3B8;line-height:1.6;margin-bottom:22px;}}
  .slot-box{{background:rgba(16,185,129,0.08);border:1px solid rgba(16,185,129,0.2);border-radius:12px;padding:18px 20px;margin-bottom:22px;}}
  .slot-row{{display:flex;justify-content:space-between;margin-bottom:8px;}}
  .slot-row:last-child{{margin-bottom:0;}}
  .slot-label{{font-size:11px;color:#64748B;text-transform:uppercase;letter-spacing:0.5px;font-weight:600;}}
  .slot-val{{font-size:13px;color:#f1f5f9;font-weight:600;}}
  .slot-val.green{{color:#10b981;}}
  .btn{{display:block;text-align:center;background:linear-gradient(90deg,#10b981,#059669);color:#fff;text-decoration:none;padding:14px 24px;border-radius:12px;font-size:15px;font-weight:700;margin-bottom:14px;}}
  .warn{{font-size:11px;color:#64748B;text-align:center;line-height:1.5;}}
  .footer{{padding:16px 36px 24px;text-align:center;font-size:11px;color:#475569;border-top:1px solid rgba(255,255,255,0.05);}}
</style></head>
<body><div class='wrap'>
  <div class='header'><div class='icon'>🟢</div><h1>Slot is Free!</h1><p>The parking slot you were waiting for is now available</p></div>
  <div class='body'>
    <div class='hi'>Hi {customerName},</div>
    <p class='msg'>Great news! The parking slot you registered a notification for has just become available. Book it now before someone else does!</p>
    <div class='slot-box'>
      <div class='slot-row'><span class='slot-label'>Owner</span><span class='slot-val'>{ownerName}</span></div>
      <div class='slot-row'><span class='slot-label'>Vehicle Type</span><span class='slot-val'>{vehicleType}</span></div>
      <div class='slot-row'><span class='slot-label'>Price</span><span class='slot-val green'>₹{pricePerHour} / hr</span></div>
    </div>
    <a href='{bookUrl}' class='btn'>Book This Slot Now →</a>
    <p class='warn'>⚡ Act fast — slots fill up quickly.</p>
  </div>
  <div class='footer'>SmartSlot · Automated alert · You requested this notification</div>
</div></body></html>";

            await SendEmail(toEmail, customerName, "🟢 Parking Slot is Now Free — SmartSlot", body);
        }

        // ── 1-Hour Alert (Background Job version) ──
        public async Task SendOneHourAlert(
            string toEmail, string customerName, DateTime bookingTo,
            int bookingId, string slotOwner, string qrToken)
        {
            // ✅ FIX: bookingTo already stored as IST in DB — no AddHours needed
            string formattedTime = bookingTo.ToString("hh:mm tt, dd MMM yyyy");
            var extendUrl = $"https://smartslot-sc9u.onrender.com/Parking/Extend/{bookingId}";

            var body = $@"<!DOCTYPE html><html><head><meta charset='utf-8'/>
<style>
  body{{margin:0;padding:0;background:#0F172A;font-family:'Segoe UI',Arial,sans-serif;}}
  .wrap{{max-width:520px;margin:40px auto;background:rgba(20,30,48,0.98);border:1px solid rgba(255,255,255,0.08);border-radius:20px;overflow:hidden;}}
  .header{{background:linear-gradient(135deg,#F97316,#ea580c);padding:32px 36px 24px;text-align:center;}}
  .header h1{{margin:0;color:#fff;font-size:22px;font-weight:800;}}
  .header p{{margin:6px 0 0;color:rgba(255,255,255,0.8);font-size:13px;}}
  .icon{{font-size:40px;margin-bottom:12px;}}
  .body{{padding:28px 36px;}}
  .hi{{font-size:16px;color:#f1f5f9;margin-bottom:6px;}}
  .msg{{font-size:14px;color:#94A3B8;line-height:1.6;margin-bottom:22px;}}
  .time-box{{background:rgba(249,115,22,0.08);border:1px solid rgba(249,115,22,0.25);border-radius:12px;padding:16px 20px;text-align:center;margin-bottom:22px;}}
  .time-val{{font-size:22px;font-weight:800;color:#F97316;}}
  .time-label{{font-size:12px;color:#94A3B8;margin-top:4px;}}
  .btn{{display:block;text-align:center;background:linear-gradient(90deg,#F97316,#ea580c);color:#fff;text-decoration:none;padding:14px 24px;border-radius:12px;font-size:15px;font-weight:700;margin-bottom:10px;}}
  .footer{{padding:16px 36px 24px;text-align:center;font-size:11px;color:#475569;border-top:1px solid rgba(255,255,255,0.05);}}
</style></head>
<body><div class='wrap'>
  <div class='header'><div class='icon'>⏰</div><h1>1 Hour Remaining!</h1><p>Your parking booking is ending soon</p></div>
  <div class='body'>
    <div class='hi'>Hi {customerName},</div>
    <p class='msg'>Your parking at <strong style='color:#f1f5f9'>{slotOwner}'s</strong> slot ends in about 1 hour. Make sure to wrap up or extend your booking.</p>
    <div class='time-box'>
      <div class='time-val'>{formattedTime}</div>
      <div class='time-label'>Your booking ends at (IST)</div>
    </div>
    <a href='{extendUrl}' class='btn'>Extend My Booking →</a>
  </div>
  <div class='footer'>SmartSlot · Booking #{bookingId}</div>
</div></body></html>";

            await SendEmail(toEmail, customerName, "⏰ 1 Hour Left on Your Parking — SmartSlot", body);
        }

        // ── Penalty Email ──
        public async Task SendPenaltyEmail(string toEmail, string customerName, DateTime bookingTo, int bookingId, string slotOwner)
        {
            // ✅ FIX: bookingTo already stored as IST in DB — no AddHours needed
            string formattedTime = bookingTo.ToString("hh:mm tt");
            string formattedDate = bookingTo.ToString("dd MMM yyyy");

            var html = $@"<!DOCTYPE html><html><head><meta charset='utf-8'/>
<style>
  body{{font-family:'Segoe UI',sans-serif;background:#f4f4f4;margin:0;padding:0;}}
  .container{{max-width:520px;margin:30px auto;background:white;border-radius:12px;overflow:hidden;box-shadow:0 2px 12px rgba(0,0,0,0.1);}}
  .header{{background:linear-gradient(135deg,#dc2626,#991b1b);padding:30px;text-align:center;}}
  .header h1{{color:white;margin:0;font-size:24px;}}
  .header p{{color:rgba(255,255,255,0.85);margin:6px 0 0;font-size:14px;}}
  .body{{padding:28px 32px;text-align:center;}}
  .body h2{{color:#111;font-size:18px;margin-bottom:8px;}}
  .body p{{color:#555;font-size:14px;line-height:1.6;}}
  .warn-icon{{font-size:52px;margin:10px 0 16px;}}
  .penalty-box{{background:#fef2f2;border:2px solid #fca5a5;border-radius:10px;padding:20px;margin:20px 0;}}
  .penalty-box .amount{{font-size:28px;font-weight:800;color:#dc2626;}}
  .penalty-box .label{{color:#888;font-size:13px;margin-top:4px;}}
  .detail-row{{display:flex;justify-content:space-between;padding:10px 0;border-bottom:1px solid #f0f0f0;font-size:13px;text-align:left;}}
  .detail-row:last-of-type{{border-bottom:none;}}
  .detail-label{{color:#888;}}.detail-value{{color:#111;font-weight:600;}}
  .note{{background:#fff7ed;border:1px solid #fed7aa;border-radius:8px;padding:14px;color:#c2410c;font-size:12px;margin-top:16px;line-height:1.6;text-align:left;}}
  .footer{{background:#f9f9f9;padding:18px 32px;text-align:center;color:#aaa;font-size:12px;border-top:1px solid #eee;}}
</style></head>
<body><div class='container'>
  <div class='header'><h1>🅿 SmartSlot</h1><p>⚠ Penalty Notice</p></div>
  <div class='body'>
    <div class='warn-icon'>⚠️</div>
    <h2>Hi {customerName},</h2>
    <p>Your booking ended at <strong>{formattedTime}</strong> on <strong>{formattedDate}</strong>, but exit was not confirmed within the 15-minute grace period.</p>
    <div class='penalty-box'>
      <div class='amount'>₹20 / hour</div>
      <div class='label'>Penalty rate applied</div>
    </div>
    <div style='text-align:left;'>
      <div class='detail-row'><span class='detail-label'>Booking #</span><span class='detail-value'>{bookingId}</span></div>
      <div class='detail-row'><span class='detail-label'>Slot Owner</span><span class='detail-value'>{slotOwner}</span></div>
      <div class='detail-row'><span class='detail-label'>Booking Ended</span><span class='detail-value'>{formattedTime}, {formattedDate} (IST)</span></div>
      <div class='detail-row'><span class='detail-label'>Exit Confirmed</span><span class='detail-value' style='color:#dc2626;'>Not confirmed ✗</span></div>
    </div>
    <div class='note'>
      ℹ️ <strong>Why did this happen?</strong><br/>
      When your booking ends, you must scan the QR code at the slot to confirm exit. Since this was not done within 15 minutes, a penalty has been applied.<br/><br/>
      Please contact the slot owner <strong>{slotOwner}</strong> to resolve this.
    </div>
  </div>
  <div class='footer'>Booking #{bookingId} · 2025 SmartSlot. All rights reserved.</div>
</div></body></html>";

            await SendEmail(toEmail, customerName, "⚠ Penalty Applied — SmartSlot Exit Not Confirmed", html);
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