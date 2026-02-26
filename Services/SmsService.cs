using Microsoft.Extensions.Options;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;
using SmartSlot.Models;

namespace SmartSlot.Services
{
    public class SmsService
    {
        private readonly TwilioSettings _settings;

        public SmsService(IOptions<TwilioSettings> settings)
        {
            _settings = settings.Value;
        }

        // ⭐ EXISTING REVIEW SMS (UNCHANGED)
        public async Task SendReviewSms(string toNumber, int bookingId)
        {
            Console.WriteLine($"📞 Attempting to send Review SMS to: {toNumber}");

            if (string.IsNullOrWhiteSpace(toNumber))
                throw new ArgumentException("Phone number is empty.");

            toNumber = NormalizePhoneNumber(toNumber);

            TwilioClient.Init(_settings.AccountSid, _settings.AuthToken);

            var reviewLink = $"https://smartslot-fkc6.onrender.com/Parking/Review/{bookingId}";

            await MessageResource.CreateAsync(
                body: $"SmartSlot: How was your parking experience? Rate here: {reviewLink}",
                from: new PhoneNumber(_settings.FromNumber),
                to: new PhoneNumber(toNumber)
            );
        }

        // 🔔 1-HOUR ALERT SMS
        public async Task SendOneHourAlertSms(string toNumber, DateTime bookingTo)
        {
            Console.WriteLine($"📞 Attempting to send 1-hour alert SMS to: {toNumber}");

            if (string.IsNullOrWhiteSpace(toNumber))
                throw new ArgumentException("Phone number is empty.");

            toNumber = NormalizePhoneNumber(toNumber);

            TwilioClient.Init(_settings.AccountSid, _settings.AuthToken);

            string formattedTime = bookingTo.ToString("hh:mm tt");

            await MessageResource.CreateAsync(
                body: $"⚠ SmartSlot Reminder: Your parking ends at {formattedTime}. Kindly clear the parking slot within 1 hour, Otherwise you will be penalized.",
                from: new PhoneNumber(_settings.FromNumber),
                to: new PhoneNumber(toNumber)
            );
        }

        // 🔐 NEW: SEND OTP SMS
        public async Task SendOtpSms(string toNumber, string otp)
        {
            Console.WriteLine($"📞 Sending OTP to: {toNumber}");

            if (string.IsNullOrWhiteSpace(toNumber))
                throw new ArgumentException("Phone number is empty.");

            toNumber = NormalizePhoneNumber(toNumber);

            TwilioClient.Init(_settings.AccountSid, _settings.AuthToken);

            await MessageResource.CreateAsync(
                body: $"Your SmartSlot verification code is: {otp}. Valid for 10 minutes. Do not share this code.",
                from: new PhoneNumber(_settings.FromNumber),
                to: new PhoneNumber(toNumber)
            );
        }

        // 🔧 Shared Phone Normalization Logic
        private string NormalizePhoneNumber(string toNumber)
        {
            toNumber = toNumber.Trim();

            if (!toNumber.StartsWith("+91"))
            {
                toNumber = "+91" + toNumber;
            }

            return toNumber;
        }
    }
}
