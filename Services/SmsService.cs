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

```
    public SmsService(IOptions<TwilioSettings> settings)
    {
        _settings = settings.Value;
    }

    // OTP SMS — for Signup & Forgot Password only
    public async Task SendOtpSms(string toNumber, string otp)
    {
        try
        {
            Console.WriteLine($"Sending OTP to: {toNumber}");

            if (string.IsNullOrWhiteSpace(toNumber))
                throw new ArgumentException("Phone number is empty.");

            toNumber = NormalizePhoneNumber(toNumber);

            if (string.IsNullOrWhiteSpace(_settings.AccountSid))
                throw new Exception("Twilio AccountSid is empty");

            if (string.IsNullOrWhiteSpace(_settings.AuthToken))
                throw new Exception("Twilio AuthToken is empty");

            if (string.IsNullOrWhiteSpace(_settings.FromNumber))
                throw new Exception("Twilio FromNumber is empty");

            TwilioClient.Init(_settings.AccountSid, _settings.AuthToken);

            var message = await MessageResource.CreateAsync(
                body: $"Your SmartSlot verification code is: {otp}. Valid for 10 minutes. Do not share this code.",
                from: new PhoneNumber(_settings.FromNumber),
                to: new PhoneNumber(toNumber)
            );

            Console.WriteLine($"SMS Sent | SID: {message.Sid} | Status: {message.Status}");

            if (message.ErrorCode != null)
            {
                throw new Exception(
                    $"Twilio Error {message.ErrorCode}: {message.ErrorMessage}"
                );
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"TWILIO ERROR: {ex.Message}");
            throw;
        }
    }

    private string NormalizePhoneNumber(string toNumber)
    {
        toNumber = toNumber.Trim();

        if (!toNumber.StartsWith("+91"))
            toNumber = "+91" + toNumber;

        return toNumber;
    }
}
```

}
