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

        public EmailService(HttpClient httpClient) { _httpClient = httpClient; }

        // ── Slot Added ──
        public async Task SendSlotAddedEmail(string toEmail, string ownerName, string vehicleType,
            double pricePerHour, DateTime availableFrom, DateTime availableTo, string paymentMode, string qrToken = "")
        {
            var dashboardLink = "https://smartslot-sc9u.onrender.com/Parking/Dashboard";
            var html = $@"<!DOCTYPE html><html><head><meta charset='utf-8'/>
<style>body{{font-family:'Segoe UI',sans-serif;background:#f4f4f4;margin:0;padding:0;}}.container{{max-width:520px;margin:30px auto;background:white;border-radius:12px;overflow:hidden;box-shadow:0 2px 12px rgba(0,0,0,0.1);}}.header{{background:linear-gradient(135deg,#16a34a,#15803d);padding:30px;text-align:center;}}.header h1{{color:white;margin:0;font-size:24px;}}.header p{{color:rgba(255,255,255,0.85);margin:6px 0 0;font-size:14px;}}.body{{padding:28px 32px;}}.detail-row{{display:flex;justify-content:space-between;padding:12px 0;border-bottom:1px solid #f0f0f0;}}.detail-row:last-child{{border-bottom:none;}}.detail-label{{color:#888;font-size:13px;}}.detail-value{{color:#111;font-size:14px;font-weight:600;}}.badge{{display:inline-block;background:#dcfce7;color:#16a34a;padding:4px 12px;border-radius:20px;font-size:12px;font-weight:600;margin-bottom:20px;}}.qr-box{{background:#f5f3ff;border:1px solid #c4b5fd;border-radius:10px;padding:18px 20px;margin:20px 0;}}.qr-box h3{{color:#5b21b6;font-size:15px;margin:0 0 8px;}}.qr-box p{{color:#6d28d9;font-size:13px;line-height:1.7;margin:0 0 14px;}}.qr-btn{{display:inline-block;padding:11px 24px;background:#7c3aed;color:white;text-decoration:none;border-radius:8px;font-weight:600;font-size:14px;}}.footer{{background:#f9f9f9;padding:18px 32px;text-align:center;color:#aaa;font-size:12px;border-top:1px solid #eee;}}</style></head>
<body><div class='container'>
  <div class='header'><h1>SmartSlot</h1><p>Your parking slot has been listed!</p></div>
  <div class='body'>
    <span class='badge'>Slot Added Successfully</span>
    <h2 style='color:#111;font-size:18px;margin-bottom:6px;'>Hi {ownerName},</h2>
    <p style='color:#555;font-size:14px;margin-bottom:20px;'>Your parking slot has been successfully listed on SmartSlot.</p>
    <div class='detail-row'><span class='detail-label'>Vehicle Type</span><span class='detail-value'>{vehicleType}</span></div>
    <div class='detail-row'><span class='detail-label'>Price Per Hour</span><span class='detail-value'>Rs.{pricePerHour}/hr</span></div>
    <div class='detail-row'><span class='detail-label'>Available From</span><span class='detail-value'>{availableFrom:dd MMM yyyy, hh:mm tt}</span></div>
    <div class='detail-row'><span class='detail-label'>Available To</span><span class='detail-value'>{availableTo:dd MMM yyyy, hh:mm tt}</span></div>
    <div class='detail-row'><span class='detail-label'>Payment Mode</span><span class='detail-value'>{paymentMode}</span></div>
    <div class='qr-box'>
      <h3>QR Code Generated for Your Slot!</h3>
      <p>A unique QR code has been generated for your slot and is available in your Dashboard.<br/><br/>
      <strong>What to do:</strong> Display this QR code at your parking spot so customers can scan it to confirm their exit.<br/><br/>
      Customers who do not scan the QR within <strong>15 minutes</strong> after their booking ends will be penalised Rs.20/hour.</p>
      <a href='{dashboardLink}' class='qr-btn'>View QR in Dashboard</a>
    </div>
  </div>
  <div class='footer'>2025 SmartSlot. All rights reserved.</div>
</div></body></html>";
            await SendEmail(toEmail, ownerName, "Your Parking Slot is Now Live - SmartSlot", html);
        }

        // ── Owner Notified When Slot is Booked ──
        public async Task SendOwnerBookingNotificationEmail(
            string toEmail, string ownerName,
            string customerName, string customerPhone, string customerEmail,
            string vehicleType, string vehicleNumber,
            double pricePerHour, double totalAmount,
            DateTime bookingFrom, DateTime bookingTo, string paymentMode)
        {
            var html = $@"<!DOCTYPE html><html><head><meta charset='utf-8'/>
<style>body{{font-family:'Segoe UI',sans-serif;background:#f4f4f4;margin:0;padding:0;}}.container{{max-width:520px;margin:30px auto;background:white;border-radius:12px;overflow:hidden;box-shadow:0 2px 12px rgba(0,0,0,0.1);}}.header{{background:linear-gradient(135deg,#0f766e,#0d9488);padding:30px;text-align:center;}}.header h1{{color:white;margin:0;font-size:24px;}}.header p{{color:rgba(255,255,255,0.85);margin:6px 0 0;font-size:14px;}}.body{{padding:28px 32px;}}.badge{{display:inline-block;background:#ccfbf1;color:#0f766e;padding:4px 12px;border-radius:20px;font-size:12px;font-weight:600;margin-bottom:20px;}}.section-title{{font-size:12px;font-weight:700;text-transform:uppercase;letter-spacing:1px;color:#888;margin:20px 0 10px;}}.detail-row{{display:flex;justify-content:space-between;padding:11px 0;border-bottom:1px solid #f0f0f0;}}.detail-row:last-child{{border-bottom:none;}}.detail-label{{color:#888;font-size:13px;}}.detail-value{{color:#111;font-size:14px;font-weight:600;}}.total-box{{background:#f0fdf4;border:1px solid #86efac;border-radius:8px;padding:16px;text-align:center;margin:20px 0;}}.total-box .amount{{font-size:28px;font-weight:800;color:#16a34a;}}.total-box .label{{color:#555;font-size:13px;margin-top:4px;}}.info-note{{background:#eff6ff;border:1px solid #bfdbfe;border-radius:8px;padding:14px;color:#1d4ed8;font-size:13px;line-height:1.6;margin-top:16px;}}.footer{{background:#f9f9f9;padding:18px 32px;text-align:center;color:#aaa;font-size:12px;border-top:1px solid #eee;}}</style></head>
<body><div class='container'>
  <div class='header'><h1>SmartSlot</h1><p>Your slot has been booked!</p></div>
  <div class='body'>
    <span class='badge'>New Booking Received</span>
    <h2 style='color:#111;font-size:18px;margin-bottom:6px;'>Hi {ownerName},</h2>
    <p style='color:#555;font-size:14px;'>Your parking slot has been booked by a customer.</p>
    <div class='section-title'>Customer Details</div>
    <div class='detail-row'><span class='detail-label'>Name</span><span class='detail-value'>{customerName}</span></div>
    <div class='detail-row'><span class='detail-label'>Phone</span><span class='detail-value'>{customerPhone}</span></div>
    <div class='detail-row'><span class='detail-label'>Email</span><span class='detail-value'>{customerEmail}</span></div>
    <div class='detail-row'><span class='detail-label'>Vehicle No.</span><span class='detail-value'>{vehicleNumber}</span></div>
    <div class='section-title'>Booking Details</div>
    <div class='detail-row'><span class='detail-label'>Vehicle Type</span><span class='detail-value'>{vehicleType}</span></div>
    <div class='detail-row'><span class='detail-label'>From</span><span class='detail-value'>{bookingFrom:dd MMM yyyy, hh:mm tt}</span></div>
    <div class='detail-row'><span class='detail-label'>To</span><span class='detail-value'>{bookingTo:dd MMM yyyy, hh:mm tt}</span></div>
    <div class='detail-row'><span class='detail-label'>Rate</span><span class='detail-value'>Rs.{pricePerHour}/hr</span></div>
    <div class='detail-row'><span class='detail-label'>Payment Mode</span><span class='detail-value'>{paymentMode}</span></div>
    <div class='total-box'><div class='amount'>Rs.{totalAmount:F0}</div><div class='label'>Total Amount to Collect</div></div>
    <div class='info-note'>Please ensure your slot is ready and the QR code is displayed at the spot for exit verification.</div>
  </div>
  <div class='footer'>2025 SmartSlot. All rights reserved.</div>
</div></body></html>";
            await SendEmail(toEmail, ownerName, "Your Slot Has Been Booked - SmartSlot", html);
        }

        // ── Owner Notified When Customer Exit is Confirmed ──
        public async Task SendOwnerSlotFreeEmail(
            string toEmail, string ownerName,
            string customerName, string customerPhone,
            string vehicleNumber, DateTime bookingFrom, DateTime bookingTo, DateTime exitConfirmedAt)
        {
            var dashboardLink = "https://smartslot-sc9u.onrender.com/Parking/Dashboard";
            var html = $@"<!DOCTYPE html><html><head><meta charset='utf-8'/>
<style>body{{font-family:'Segoe UI',sans-serif;background:#f4f4f4;margin:0;padding:0;}}.container{{max-width:520px;margin:30px auto;background:white;border-radius:12px;overflow:hidden;box-shadow:0 2px 12px rgba(0,0,0,0.1);}}.header{{background:linear-gradient(135deg,#10b981,#059669);padding:30px;text-align:center;}}.header h1{{color:white;margin:0;font-size:24px;}}.header p{{color:rgba(255,255,255,0.85);margin:6px 0 0;font-size:14px;}}.body{{padding:28px 32px;}}.badge{{display:inline-block;background:#dcfce7;color:#16a34a;padding:4px 12px;border-radius:20px;font-size:12px;font-weight:600;margin-bottom:20px;}}.detail-row{{display:flex;justify-content:space-between;padding:11px 0;border-bottom:1px solid #f0f0f0;}}.detail-row:last-child{{border-bottom:none;}}.detail-label{{color:#888;font-size:13px;}}.detail-value{{color:#111;font-size:14px;font-weight:600;}}.info-note{{background:#f0fdf4;border:1px solid #86efac;border-radius:8px;padding:14px;color:#16a34a;font-size:13px;line-height:1.6;margin-top:16px;}}.btn{{display:block;text-align:center;background:linear-gradient(90deg,#10b981,#059669);color:white;text-decoration:none;padding:13px;border-radius:10px;font-size:14px;font-weight:700;margin-top:20px;}}.footer{{background:#f9f9f9;padding:18px 32px;text-align:center;color:#aaa;font-size:12px;border-top:1px solid #eee;}}</style></head>
<body><div class='container'>
  <div class='header'><h1>SmartSlot</h1><p>Your Slot is Now Free!</p></div>
  <div class='body'>
    <span class='badge'>Exit Confirmed ✅</span>
    <h2 style='color:#111;font-size:18px;margin-bottom:6px;'>Hi {ownerName},</h2>
    <p style='color:#555;font-size:14px;'>Your previous customer has confirmed their exit by scanning the QR code you displayed at your parking slot. Your slot is now free and available for new bookings!</p>
    <div class='detail-row'><span class='detail-label'>Customer Name</span><span class='detail-value'>{customerName}</span></div>
    <div class='detail-row'><span class='detail-label'>Customer Phone</span><span class='detail-value'>{customerPhone}</span></div>
    <div class='detail-row'><span class='detail-label'>Vehicle No.</span><span class='detail-value'>{vehicleNumber}</span></div>
    <div class='detail-row'><span class='detail-label'>Booking From</span><span class='detail-value'>{bookingFrom:dd MMM yyyy, hh:mm tt}</span></div>
    <div class='detail-row'><span class='detail-label'>Booking To</span><span class='detail-value'>{bookingTo:dd MMM yyyy, hh:mm tt}</span></div>
    <div class='detail-row'><span class='detail-label'>Exit Confirmed At</span><span class='detail-value'>{exitConfirmedAt:dd MMM yyyy, hh:mm tt}</span></div>
    <div class='info-note'>🅿️ Your slot is now visible to other customers on SmartSlot and ready for new bookings.</div>
    <a href='{dashboardLink}' class='btn'>View Dashboard</a>
  </div>
  <div class='footer'>2025 SmartSlot. All rights reserved.</div>
</div></body></html>";
            await SendEmail(toEmail, ownerName, "Your Slot is Now Free - Customer Exit Confirmed", html);
        }

        // ── Customer Booking Confirmation (with Cancel button) ──
        public async Task SendBookingConfirmationEmail(string toEmail, string customerName,
            string ownerName, string ownerPhone, string vehicleType, string vehicleNumber,
            double pricePerHour, double totalAmount, DateTime bookingFrom, DateTime bookingTo,
            string paymentMode, string qrToken = "",
            double latitude = 0, double longitude = 0, int bookingId = 0)
        {
            var locationLink = (latitude != 0 && longitude != 0)
                ? $"https://www.google.com/maps?q={latitude},{longitude}" : "";
            var locationRow = !string.IsNullOrEmpty(locationLink)
                ? $"<div class='detail-row'><span class='detail-label'>Slot Location</span><span class='detail-value'><a href='{locationLink}' style='color:#2563eb;'>View on Map</a></span></div>" : "";

            var cancelLink = $"https://smartslot-sc9u.onrender.com/Parking/CancelBooking?bookingId={bookingId}&token={qrToken}";

            var html = $@"<!DOCTYPE html><html><head><meta charset='utf-8'/>
<style>body{{font-family:'Segoe UI',sans-serif;background:#f4f4f4;margin:0;padding:0;}}.container{{max-width:520px;margin:30px auto;background:white;border-radius:12px;overflow:hidden;box-shadow:0 2px 12px rgba(0,0,0,0.1);}}.header{{background:linear-gradient(135deg,#2563eb,#1d4ed8);padding:30px;text-align:center;}}.header h1{{color:white;margin:0;font-size:24px;}}.header p{{color:rgba(255,255,255,0.85);margin:6px 0 0;font-size:14px;}}.body{{padding:28px 32px;}}.body h2{{color:#111;font-size:18px;margin-bottom:6px;}}.body p{{color:#555;font-size:14px;margin-bottom:20px;}}.detail-row{{display:flex;justify-content:space-between;padding:12px 0;border-bottom:1px solid #f0f0f0;}}.detail-row:last-child{{border-bottom:none;}}.detail-label{{color:#888;font-size:13px;}}.detail-value{{color:#111;font-size:14px;font-weight:600;}}.badge{{display:inline-block;background:#dbeafe;color:#2563eb;padding:4px 12px;border-radius:20px;font-size:12px;font-weight:600;margin-bottom:20px;}}.section-title{{font-size:12px;font-weight:700;text-transform:uppercase;letter-spacing:1px;color:#888;margin:20px 0 10px;}}.total-box{{background:#f0fdf4;border:1px solid #86efac;border-radius:8px;padding:16px;text-align:center;margin:20px 0;}}.total-box .amount{{font-size:28px;font-weight:800;color:#16a34a;}}.total-box .label{{color:#555;font-size:13px;margin-top:4px;}}.qr-notice{{background:#faf5ff;border:1px solid #d8b4fe;border-radius:10px;padding:16px 18px;margin-top:20px;}}.qr-notice h4{{color:#7c3aed;font-size:14px;margin:0 0 8px;}}.qr-notice p{{color:#6d28d9;font-size:13px;line-height:1.7;margin:0;}}.cancel-box{{background:#fef2f2;border:1px solid #fca5a5;border-radius:10px;padding:16px 18px;margin-top:16px;text-align:center;}}.cancel-box p{{color:#888;font-size:12px;margin:0 0 10px;}}.cancel-btn{{display:inline-block;padding:10px 24px;background:#dc2626;color:white;text-decoration:none;border-radius:8px;font-weight:600;font-size:13px;}}.footer{{background:#f9f9f9;padding:18px 32px;text-align:center;color:#aaa;font-size:12px;border-top:1px solid #eee;}}</style></head>
<body><div class='container'>
  <div class='header'><h1>SmartSlot</h1><p>Booking Confirmed!</p></div>
  <div class='body'>
    <span class='badge'>Booking Confirmed</span>
    <h2>Hi {customerName},</h2>
    <p>You successfully booked a parking slot .</p>
    <div class='section-title'>Owner &amp; Slot Info</div>
    <div class='detail-row'><span class='detail-label'>Owner Name: </span><span class='detail-value'>{ownerName}</span></div>
    <div class='detail-row'><span class='detail-label'>Owner Phone: </span><span class='detail-value'>{ownerPhone}</span></div>
    <div class='detail-row'><span class='detail-label'>Vehicle Type: </span><span class='detail-value'>{vehicleType}</span></div>
    <div class='detail-row'><span class='detail-label'>Payment Mode: </span><span class='detail-value'>{paymentMode}</span></div>
    {locationRow}
    <div class='section-title'>Your Booking</div>
    <div class='detail-row'><span class='detail-label'>Vehicle No.  </span><span class='detail-value'>{vehicleNumber}</span></div>
    <div class='detail-row'><span class='detail-label'>From: </span><span class='detail-value'>{bookingFrom:dd MMM yyyy, hh:mm tt}</span></div>
    <div class='detail-row'><span class='detail-label'>To: </span><span class='detail-value'>{bookingTo:dd MMM yyyy, hh:mm tt}</span></div>
    <div class='detail-row'><span class='detail-label'>Rate: </span><span class='detail-value'>Rs.{pricePerHour}/hr</span></div>
    <div class='total-box'><div class='amount'>Rs.{totalAmount:F0}</div><div class='label'>Total Amount</div></div>
    <div class='qr-notice'>
      <h4>QR Exit - Important</h4>
      <p>A QR code is generated for this slot and displayed at the parking spot by the owner.<br/><br/>
      When your booking ends, you will receive a personal scan link via email - use it to scan the QR at the slot to <strong>confirm your exit</strong>.<br/><br/>
      If exit is not confirmed within <strong>15 minutes</strong> of your booking ending, a penalty of <strong>Rs.20/hour</strong> will be applied.</p>
    </div>
    <div class='cancel-box'>
      <p>Need to cancel? Tap below to cancel your booking. The slot will be immediately freed for others.</p>
      <a href='{cancelLink}' class='cancel-btn'>Cancel This Booking</a>
    </div>
  </div>
  <div class='footer'>Booking #{bookingId} | 2025 SmartSlot. All rights reserved.</div>
</div></body></html>";
            await SendEmail(toEmail, customerName, "Booking Confirmed - SmartSlot", html);
        }

        // ── Booking Cancelled Confirmation to Customer ──
        public async Task SendBookingCancelledEmail(string toEmail, string customerName,
            string ownerName, string vehicleNumber, DateTime bookingFrom, DateTime bookingTo, string paymentMode)
        {
            var html = $@"<!DOCTYPE html><html><head><meta charset='utf-8'/>
<style>body{{font-family:'Segoe UI',sans-serif;background:#f4f4f4;margin:0;padding:0;}}.container{{max-width:520px;margin:30px auto;background:white;border-radius:12px;overflow:hidden;box-shadow:0 2px 12px rgba(0,0,0,0.1);}}.header{{background:linear-gradient(135deg,#6b7280,#4b5563);padding:30px;text-align:center;}}.header h1{{color:white;margin:0;font-size:24px;}}.header p{{color:rgba(255,255,255,0.85);margin:6px 0 0;font-size:14px;}}.body{{padding:28px 32px;}}.detail-row{{display:flex;justify-content:space-between;padding:12px 0;border-bottom:1px solid #f0f0f0;}}.detail-label{{color:#888;font-size:13px;}}.detail-value{{color:#111;font-size:14px;font-weight:600;}}.footer{{background:#f9f9f9;padding:18px 32px;text-align:center;color:#aaa;font-size:12px;border-top:1px solid #eee;}}</style></head>
<body><div class='container'>
  <div class='header'><h1>SmartSlot</h1><p>Booking Cancelled</p></div>
  <div class='body'>
    <h2 style='color:#111;font-size:18px;margin-bottom:6px;'>Hi {customerName},</h2>
    <p style='color:#555;font-size:14px;margin-bottom:20px;'>Your booking has been successfully cancelled. The slot is now free for others.</p>
    <div class='detail-row'><span class='detail-label'>Owner</span><span class='detail-value'>{ownerName}</span></div>
    <div class='detail-row'><span class='detail-label'>Vehicle No.</span><span class='detail-value'>{vehicleNumber}</span></div>
    <div class='detail-row'><span class='detail-label'>Was Booked From</span><span class='detail-value'>{bookingFrom:dd MMM yyyy, hh:mm tt}</span></div>
    <div class='detail-row'><span class='detail-label'>Was Booked To</span><span class='detail-value'>{bookingTo:dd MMM yyyy, hh:mm tt}</span></div>
    <div class='detail-row'><span class='detail-label'>Payment Mode</span><span class='detail-value'>{paymentMode}</span></div>
    <p style='color:#888;font-size:13px;margin-top:20px;'>Thank you for letting us know. We hope to see you again on SmartSlot!</p>
  </div>
  <div class='footer'>2025 SmartSlot. All rights reserved.</div>
</div></body></html>";
            await SendEmail(toEmail, customerName, "Your Booking Has Been Cancelled - SmartSlot", html);
        }

        // ── Owner Notified When Booking is Cancelled ──
        public async Task SendOwnerCancelNotificationEmail(
            string toEmail, string ownerName,
            string customerName, string customerPhone,
            string vehicleNumber, DateTime bookingFrom, DateTime bookingTo, string paymentMode)
        {
            var html = $@"<!DOCTYPE html><html><head><meta charset='utf-8'/>
<style>body{{font-family:'Segoe UI',sans-serif;background:#f4f4f4;margin:0;padding:0;}}.container{{max-width:520px;margin:30px auto;background:white;border-radius:12px;overflow:hidden;box-shadow:0 2px 12px rgba(0,0,0,0.1);}}.header{{background:linear-gradient(135deg,#dc2626,#991b1b);padding:30px;text-align:center;}}.header h1{{color:white;margin:0;font-size:24px;}}.header p{{color:rgba(255,255,255,0.85);margin:6px 0 0;font-size:14px;}}.body{{padding:28px 32px;}}.badge{{display:inline-block;background:#fee2e2;color:#dc2626;padding:4px 12px;border-radius:20px;font-size:12px;font-weight:600;margin-bottom:20px;}}.detail-row{{display:flex;justify-content:space-between;padding:11px 0;border-bottom:1px solid #f0f0f0;}}.detail-row:last-child{{border-bottom:none;}}.detail-label{{color:#888;font-size:13px;}}.detail-value{{color:#111;font-size:14px;font-weight:600;}}.info-note{{background:#f0fdf4;border:1px solid #86efac;border-radius:8px;padding:14px;color:#16a34a;font-size:13px;line-height:1.6;margin-top:16px;}}.footer{{background:#f9f9f9;padding:18px 32px;text-align:center;color:#aaa;font-size:12px;border-top:1px solid #eee;}}</style></head>
<body><div class='container'>
  <div class='header'><h1>SmartSlot</h1><p>Booking Cancelled</p></div>
  <div class='body'>
    <span class='badge'>Booking Cancelled</span>
    <h2 style='color:#111;font-size:18px;margin-bottom:6px;'>Hi {ownerName},</h2>
    <p style='color:#555;font-size:14px;'>A customer has cancelled their booking for your parking slot.</p>
    <div class='detail-row'><span class='detail-label'>Customer Name</span><span class='detail-value'>{customerName}</span></div>
    <div class='detail-row'><span class='detail-label'>Customer Phone</span><span class='detail-value'>{customerPhone}</span></div>
    <div class='detail-row'><span class='detail-label'>Vehicle No.</span><span class='detail-value'>{vehicleNumber}</span></div>
    <div class='detail-row'><span class='detail-label'>Was Booked From</span><span class='detail-value'>{bookingFrom:dd MMM yyyy, hh:mm tt}</span></div>
    <div class='detail-row'><span class='detail-label'>Was Booked To</span><span class='detail-value'>{bookingTo:dd MMM yyyy, hh:mm tt}</span></div>
    <div class='detail-row'><span class='detail-label'>Payment Mode</span><span class='detail-value'>{paymentMode}</span></div>
    <div class='info-note'>✅ Your slot is now free and visible to other customers on SmartSlot.</div>
  </div>
  <div class='footer'>2025 SmartSlot. All rights reserved.</div>
</div></body></html>";
            await SendEmail(toEmail, ownerName, "Booking Cancelled - SmartSlot", html);
        }

        // ── Slot Available Notification ──
        public async Task SendSlotAvailableEmail(string toEmail, string customerName,
            string ownerName, string vehicleType, double pricePerHour, DateTime availableTo)
        {
            var html = $@"<!DOCTYPE html><html><head><meta charset='utf-8'/>
<style>body{{font-family:'Segoe UI',sans-serif;background:#f4f4f4;margin:0;padding:0;}}.container{{max-width:520px;margin:30px auto;background:white;border-radius:12px;overflow:hidden;box-shadow:0 2px 12px rgba(0,0,0,0.1);}}.header{{background:linear-gradient(135deg,#f59e0b,#d97706);padding:30px;text-align:center;}}.header h1{{color:white;margin:0;font-size:24px;}}.body{{padding:28px 32px;}}.detail-row{{display:flex;justify-content:space-between;padding:12px 0;border-bottom:1px solid #f0f0f0;}}.detail-label{{color:#888;font-size:13px;}}.detail-value{{color:#111;font-size:14px;font-weight:600;}}.cta{{display:block;margin:24px auto 0;padding:12px 28px;background:#2563eb;color:white;text-decoration:none;border-radius:8px;font-weight:600;text-align:center;}}.footer{{background:#f9f9f9;padding:18px 32px;text-align:center;color:#aaa;font-size:12px;border-top:1px solid #eee;}}</style></head>
<body><div class='container'>
  <div class='header'><h1>SmartSlot</h1></div>
  <div class='body'>
    <h2 style='color:#111;font-size:18px;'>Hi {customerName},</h2>
    <p style='color:#555;font-size:14px;'>The parking slot you requested is now free. Book it quickly!</p>
    <div class='detail-row'><span class='detail-label'>Owner</span><span class='detail-value'>{ownerName}</span></div>
    <div class='detail-row'><span class='detail-label'>Vehicle Type</span><span class='detail-value'>{vehicleType}</span></div>
    <div class='detail-row'><span class='detail-label'>Price Per Hour</span><span class='detail-value'>Rs.{pricePerHour}/hr</span></div>
    <div class='detail-row'><span class='detail-label'>Available Until</span><span class='detail-value'>{availableTo:dd MMM yyyy, hh:mm tt}</span></div>
    <a href='https://smartslot-sc9u.onrender.com/Parking/Search' class='cta'>Book Now</a>
  </div>
  <div class='footer'>2025 SmartSlot. All rights reserved.</div>
</div></body></html>";
            await SendEmail(toEmail, customerName, "Parking Slot Now Available - SmartSlot", html);
        }

        // ── Password Reset ──
        public async Task SendPasswordResetEmail(string toEmail, string username, string resetLink)
        {
            var html = $@"<!DOCTYPE html><html><head><meta charset='utf-8'/>
<style>body{{font-family:'Segoe UI',sans-serif;background:#f4f4f4;margin:0;padding:0;}}.container{{max-width:520px;margin:30px auto;background:white;border-radius:12px;overflow:hidden;}}.header{{background:linear-gradient(135deg,#3b82f6,#1d4ed8);padding:30px;text-align:center;}}.header h1{{color:white;margin:0;font-size:24px;}}.body{{padding:28px 32px;text-align:center;}}.reset-btn{{display:inline-block;padding:14px 32px;background:#3b82f6;color:white;text-decoration:none;border-radius:8px;font-weight:700;font-size:16px;}}.footer{{background:#f9f9f9;padding:18px 32px;text-align:center;color:#aaa;font-size:12px;border-top:1px solid #eee;}}</style></head>
<body><div class='container'>
  <div class='header'><h1>SmartSlot</h1></div>
  <div class='body'>
    <h2 style='color:#111;'>Hi {username},</h2>
    <p style='color:#555;font-size:14px;'>Click the button below to reset your SmartSlot password. This link expires in 30 minutes.</p>
    <a href='{resetLink}' class='reset-btn'>Reset My Password</a>
    <p style='color:#aaa;font-size:12px;margin-top:20px;'>If you did not request this, please ignore this email.</p>
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
<style>body{{font-family:'Segoe UI',sans-serif;background:#f4f4f4;margin:0;padding:0;}}.container{{max-width:520px;margin:30px auto;background:white;border-radius:12px;overflow:hidden;}}.header{{background:linear-gradient(135deg,#7c3aed,#5b21b6);padding:30px;text-align:center;}}.header h1{{color:white;margin:0;font-size:24px;}}.body{{padding:28px 32px;text-align:center;}}.cta{{display:inline-block;padding:14px 32px;background:#7c3aed;color:white;text-decoration:none;border-radius:8px;font-weight:700;font-size:16px;}}.footer{{background:#f9f9f9;padding:18px 32px;text-align:center;color:#aaa;font-size:12px;border-top:1px solid #eee;}}</style></head>
<body><div class='container'>
  <div class='header'><h1>SmartSlot</h1><p>How was your parking experience?</p></div>
  <div class='body'>
    <div style='font-size:32px;margin:16px 0;'>&#11088;&#11088;&#11088;&#11088;&#11088;</div>
    <h2 style='color:#111;'>Hi {customerName},</h2>
    <p style='color:#555;font-size:14px;'>Your parking session has ended. We would love to hear your feedback!</p>
    <a href='{reviewLink}' class='cta'>Rate Your Experience</a>
  </div>
  <div class='footer'>2025 SmartSlot. All rights reserved.</div>
</div></body></html>";
            await SendEmail(toEmail, customerName, "How was your SmartSlot experience?", html);
        }

        // ── 1-Hour Alert ──
        public async Task SendOneHourAlertEmail(string toEmail, string customerName, DateTime bookingTo, int bookingId)
        {
            string formattedTime = bookingTo.ToString("hh:mm tt, dd MMM yyyy");
            var extendLink = $"https://smartslot-sc9u.onrender.com/Parking/Extend/{bookingId}";
            var html = $@"<!DOCTYPE html><html><head><meta charset='utf-8'/>
<style>body{{font-family:'Segoe UI',sans-serif;background:#f4f4f4;margin:0;padding:0;}}.container{{max-width:520px;margin:30px auto;background:white;border-radius:12px;overflow:hidden;}}.header{{background:linear-gradient(135deg,#dc2626,#b91c1c);padding:30px;text-align:center;}}.header h1{{color:white;margin:0;font-size:24px;}}.body{{padding:28px 32px;text-align:center;}}.time-box{{background:#fef2f2;border:2px solid #fca5a5;border-radius:10px;padding:20px;margin:20px 0;}}.time-box .time{{font-size:28px;font-weight:800;color:#dc2626;}}.time-box .label{{color:#888;font-size:13px;margin-top:4px;}}.warning{{background:#fff7ed;border:1px solid #fed7aa;border-radius:8px;padding:14px;color:#c2410c;font-size:13px;margin-top:16px;line-height:1.6;}}.extend-btn{{display:block;margin:16px auto 0;padding:12px 28px;background:#2563eb;color:white;text-decoration:none;border-radius:8px;font-weight:700;font-size:15px;text-align:center;}}.footer{{background:#f9f9f9;padding:18px 32px;text-align:center;color:#aaa;font-size:12px;border-top:1px solid #eee;}}</style></head>
<body><div class='container'>
  <div class='header'><h1>SmartSlot</h1><p>Parking Expiry Reminder</p></div>
  <div class='body'>
    <h2 style='color:#111;'>Hi {customerName},</h2>
    <p style='color:#555;font-size:14px;'>Your parking slot expires in about 1 hour. Please make sure to clear the slot on time.</p>
    <div class='time-box'><div class='time'>{formattedTime}</div><div class='label'>Your parking ends at (IST)</div></div>
    <div class='warning'>If exit QR is not scanned within 15 minutes of booking end, a Rs.20/hour penalty will apply.</div>
    <a href='{extendLink}' class='extend-btn'>Extend My Booking</a>
  </div>
  <div class='footer'>2025 SmartSlot. All rights reserved.</div>
</div></body></html>";
            await SendEmail(toEmail, customerName, "Your SmartSlot parking ends in 1 hour!", html);
        }

        // ── Exit Scan Email ──
        public async Task SendExitScanEmail(string toEmail, string customerName, DateTime bookingTo, string scanLink, int bookingId)
        {
            string formattedTime = bookingTo.ToString("hh:mm tt, dd MMM yyyy");
            var html = $@"<!DOCTYPE html><html><head><meta charset='utf-8'/>
<style>body{{font-family:'Segoe UI',sans-serif;background:#f4f4f4;margin:0;padding:0;}}.container{{max-width:520px;margin:30px auto;background:white;border-radius:12px;overflow:hidden;}}.header{{background:linear-gradient(135deg,#7c3aed,#4f46e5);padding:30px;text-align:center;}}.header h1{{color:white;margin:0;font-size:24px;}}.body{{padding:28px 32px;text-align:center;}}.scan-btn{{display:block;padding:16px 28px;background:linear-gradient(90deg,#7c3aed,#4f46e5);color:white;text-decoration:none;border-radius:10px;font-weight:700;font-size:16px;text-align:center;margin:0 auto 16px;}}.penalty-box{{background:#fef2f2;border:1px solid #fca5a5;border-radius:8px;padding:14px;color:#dc2626;font-size:13px;margin-top:16px;line-height:1.6;}}.footer{{background:#f9f9f9;padding:18px 32px;text-align:center;color:#aaa;font-size:12px;border-top:1px solid #eee;}}</style></head>
<body><div class='container'>
  <div class='header'><h1>SmartSlot</h1><p>Exit Verification Required</p></div>
  <div class='body'>
    <div style='font-size:52px;margin:16px 0;'>&#128247;</div>
    <h2 style='color:#111;'>Hi {customerName},</h2>
    <p style='color:#555;font-size:14px;'>Your parking booking ended at <strong>{formattedTime} (IST)</strong>. Please confirm your exit by scanning the QR code at the slot.</p>
    <a href='{scanLink}' class='scan-btn'>Scan QR to Confirm Exit</a>
    <div class='penalty-box'>If exit is not confirmed within <strong>15 minutes</strong> of your booking ending, a penalty of <strong>Rs.20 per hour</strong> will be applied.</div>
  </div>
  <div class='footer'>Booking #{bookingId} | 2025 SmartSlot. All rights reserved.</div>
</div></body></html>";
            await SendEmail(toEmail, customerName, "Confirm Your Exit - SmartSlot", html);
        }

        // ── Slot Free Notification ──
        public async Task SendSlotFreeNotificationEmail(
            string toEmail, string customerName, string ownerName,
            decimal pricePerHour, string vehicleType, int slotId)
        {
            var bookUrl = $"https://smartslot-sc9u.onrender.com/Parking/Book/{slotId}";
            var html = $@"<!DOCTYPE html><html><head><meta charset='utf-8'/>
<style>body{{font-family:'Segoe UI',sans-serif;background:#f4f4f4;margin:0;padding:0;}}.container{{max-width:520px;margin:30px auto;background:white;border-radius:12px;overflow:hidden;}}.header{{background:linear-gradient(135deg,#10b981,#059669);padding:30px;text-align:center;}}.header h1{{color:white;margin:0;font-size:24px;}}.body{{padding:28px 32px;}}.detail-row{{display:flex;justify-content:space-between;padding:12px 0;border-bottom:1px solid #f0f0f0;}}.detail-label{{color:#888;font-size:13px;}}.detail-value{{color:#111;font-size:14px;font-weight:600;}}.btn{{display:block;text-align:center;background:linear-gradient(90deg,#10b981,#059669);color:white;text-decoration:none;padding:14px;border-radius:12px;font-size:15px;font-weight:700;margin-top:20px;}}.footer{{background:#f9f9f9;padding:18px 32px;text-align:center;color:#aaa;font-size:12px;border-top:1px solid #eee;}}</style></head>
<body><div class='container'>
  <div class='header'><h1>SmartSlot</h1><p>Slot is Free!</p></div>
  <div class='body'>
    <h2 style='color:#111;font-size:18px;'>Hi {customerName},</h2>
    <p style='color:#555;font-size:14px;'>The parking slot you were waiting for is now available!</p>
    <div class='detail-row'><span class='detail-label'>Owner</span><span class='detail-value'>{ownerName}</span></div>
    <div class='detail-row'><span class='detail-label'>Vehicle Type</span><span class='detail-value'>{vehicleType}</span></div>
    <div class='detail-row'><span class='detail-label'>Price</span><span class='detail-value' style='color:#10b981;'>Rs.{pricePerHour}/hr</span></div>
    <a href='{bookUrl}' class='btn'>Book This Slot Now</a>
  </div>
  <div class='footer'>2025 SmartSlot. All rights reserved.</div>
</div></body></html>";
            await SendEmail(toEmail, customerName, "Parking Slot is Now Free - SmartSlot", html);
        }

        // ── 1-Hour Alert (Background job version) ──
        public async Task SendOneHourAlert(string toEmail, string customerName, DateTime bookingTo,
            int bookingId, string slotOwner, string qrToken)
        {
            string formattedTime = bookingTo.ToString("hh:mm tt, dd MMM yyyy");
            var extendUrl = $"https://smartslot-sc9u.onrender.com/Parking/Extend/{bookingId}";
            var html = $@"<!DOCTYPE html><html><head><meta charset='utf-8'/>
<style>body{{font-family:'Segoe UI',sans-serif;background:#f4f4f4;margin:0;padding:0;}}.container{{max-width:520px;margin:30px auto;background:white;border-radius:12px;overflow:hidden;}}.header{{background:linear-gradient(135deg,#F97316,#ea580c);padding:30px;text-align:center;}}.header h1{{color:white;margin:0;font-size:24px;}}.body{{padding:28px 32px;text-align:center;}}.time-box{{background:rgba(249,115,22,0.08);border:1px solid rgba(249,115,22,0.25);border-radius:12px;padding:16px;margin:20px 0;}}.time-val{{font-size:22px;font-weight:800;color:#F97316;}}.btn{{display:block;background:linear-gradient(90deg,#F97316,#ea580c);color:white;text-decoration:none;padding:14px;border-radius:12px;font-size:15px;font-weight:700;margin-top:10px;}}.footer{{background:#f9f9f9;padding:18px 32px;text-align:center;color:#aaa;font-size:12px;border-top:1px solid #eee;}}</style></head>
<body><div class='container'>
  <div class='header'><h1>SmartSlot</h1><p>1 Hour Remaining!</p></div>
  <div class='body'>
    <h2 style='color:#111;'>Hi {customerName},</h2>
    <p style='color:#555;font-size:14px;'>Your parking at <strong>{slotOwner}'s</strong> slot ends in about 1 hour.</p>
    <div class='time-box'><div class='time-val'>{formattedTime}</div><div style='font-size:12px;color:#888;margin-top:4px;'>Your booking ends at (IST)</div></div>
    <a href='{extendUrl}' class='btn'>Extend My Booking</a>
  </div>
  <div class='footer'>SmartSlot | Booking #{bookingId}</div>
</div></body></html>";
            await SendEmail(toEmail, customerName, "1 Hour Left on Your Parking - SmartSlot", html);
        }

        // ── Penalty Email ──
        public async Task SendPenaltyEmail(string toEmail, string customerName, DateTime bookingTo, int bookingId, string slotOwner)
        {
            string formattedTime = bookingTo.ToString("hh:mm tt");
            string formattedDate = bookingTo.ToString("dd MMM yyyy");
            var html = $@"<!DOCTYPE html><html><head><meta charset='utf-8'/>
<style>body{{font-family:'Segoe UI',sans-serif;background:#f4f4f4;margin:0;padding:0;}}.container{{max-width:520px;margin:30px auto;background:white;border-radius:12px;overflow:hidden;}}.header{{background:linear-gradient(135deg,#dc2626,#991b1b);padding:30px;text-align:center;}}.header h1{{color:white;margin:0;font-size:24px;}}.body{{padding:28px 32px;text-align:center;}}.penalty-box{{background:#fef2f2;border:2px solid #fca5a5;border-radius:10px;padding:20px;margin:20px 0;}}.penalty-box .amount{{font-size:28px;font-weight:800;color:#dc2626;}}.detail-row{{display:flex;justify-content:space-between;padding:10px 0;border-bottom:1px solid #f0f0f0;font-size:13px;text-align:left;}}.detail-label{{color:#888;}}.detail-value{{color:#111;font-weight:600;}}.footer{{background:#f9f9f9;padding:18px 32px;text-align:center;color:#aaa;font-size:12px;border-top:1px solid #eee;}}</style></head>
<body><div class='container'>
  <div class='header'><h1>SmartSlot</h1><p>Penalty Notice</p></div>
  <div class='body'>
    <div style='font-size:52px;margin:10px 0 16px;'>&#9888;&#65039;</div>
    <h2 style='color:#111;'>Hi {customerName},</h2>
    <p style='color:#555;font-size:14px;'>Your booking ended at <strong>{formattedTime}</strong> on <strong>{formattedDate}</strong>, but exit was not confirmed within the 15-minute grace period.</p>
    <div class='penalty-box'><div class='amount'>Rs.20 / hour</div><div style='color:#888;font-size:13px;margin-top:4px;'>Penalty rate applied</div></div>
    <div style='text-align:left;'>
      <div class='detail-row'><span class='detail-label'>Booking #</span><span class='detail-value'>{bookingId}</span></div>
      <div class='detail-row'><span class='detail-label'>Slot Owner</span><span class='detail-value'>{slotOwner}</span></div>
      <div class='detail-row'><span class='detail-label'>Booking Ended</span><span class='detail-value'>{formattedTime}, {formattedDate} (IST)</span></div>
    </div>
    <p style='color:#888;font-size:12px;margin-top:16px;'>Please contact the slot owner <strong>{slotOwner}</strong> to resolve this.</p>
  </div>
  <div class='footer'>Booking #{bookingId} | 2025 SmartSlot. All rights reserved.</div>
</div></body></html>";
            await SendEmail(toEmail, customerName, "Penalty Applied - SmartSlot Exit Not Confirmed", html);
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