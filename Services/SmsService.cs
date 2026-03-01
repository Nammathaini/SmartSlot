using Microsoft.Extensions.Options;
using SmartSlot.Models;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace SmartSlot.Services
{
    public class SmsService
    {
        private readonly TwilioSettings _settings;

        public SmsService(IOptions<TwilioSettings> settings)
        {
            _settings = settings.Value;
        }

        // 🔐 OTP SMS — for Signup & Forgot Password only
        public async Task SendOtpSms(string toNumber, string otp)
        {
            Console.WriteLine($"📞 Sending OTP to: {toNumber}");

            if (string.IsNullOrWhiteSpace(toNumber))
                throw new ArgumentException("Phone number is empty.");

            toNumber = NormalizePhoneNumber(toNumber);

            TwilioClient.Init(_settings.AccountSid, _settings.AuthToken);

            var message = await MessageResource.CreateAsync(
                body: $"Your SmartSlot verification code is: {otp}. Valid for 10 minutes. Do not share this code.",
                from: new PhoneNumber(_settings.FromNumber),
                to: new PhoneNumber(toNumber)
            );

            Console.WriteLine($"✅ OTP SMS sent | SID: {message.Sid} | Status: {message.Status}");

            if (message.ErrorCode != null)
                Console.WriteLine($"❌ Twilio Error: {message.ErrorCode} - {message.ErrorMessage}");
        }

        // 🔧 Phone Normalization
        private string NormalizePhoneNumber(string toNumber)
        {
            toNumber = toNumber.Trim();
            if (!toNumber.StartsWith("+91"))
                toNumber = "+91" + toNumber;
            return toNumber;
        }
    }
}