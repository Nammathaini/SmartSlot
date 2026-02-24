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

        public async Task SendReviewSms(string toNumber, int bookingId)
        {
            Console.WriteLine($"📞 Attempting to send SMS to: {toNumber}");
            // ✅ Safety check
            if (string.IsNullOrWhiteSpace(toNumber))
                throw new ArgumentException("Phone number is empty.");

            // ✅ Normalize number (remove spaces)
            toNumber = toNumber.Trim();

            // ✅ Add country code if missing
            if (!toNumber.StartsWith("+91"))
            {
                toNumber = "+91" + toNumber;
            }

            // ✅ Initialize Twilio
            TwilioClient.Init(_settings.AccountSid, _settings.AuthToken);

            // ✅ Review link (change when deployed)
            var reviewLink = $"https://smartslot-fkc6.onrender.com/Parking/Review/{bookingId}"; 

            // ✅ Send SMS (Async version)
            await MessageResource.CreateAsync(
                body: $"SmartSlot: How was your parking experience? Rate here: {reviewLink}",
                from: new PhoneNumber(_settings.FromNumber),
                to: new PhoneNumber(toNumber)
            );
        }
    }
}