using System.Text;
using Newtonsoft.Json;

namespace SmartSlot.Services
{
    public class PhonePeService
    {
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _clientVersion;
        private readonly HttpClient _httpClient;

        public string LastTokenResponse { get; private set; } = "";
        public string LastPaymentResponse { get; private set; } = "";

        public PhonePeService(IConfiguration configuration)
        {
            _clientId = configuration["PhonePe:ClientId"];
            _clientSecret = configuration["PhonePe:ClientSecret"];
            _clientVersion = configuration["PhonePe:ClientVersion"];
            _httpClient = new HttpClient();
        }

        private async Task<string> GetAccessToken()
        {
            var tokenUrl = "https://api-preprod.phonepe.com/apis/pg-sandbox/v1/oauth/token";

            var body = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", _clientId),
                new KeyValuePair<string, string>("client_secret", _clientSecret),
                new KeyValuePair<string, string>("client_version", _clientVersion),
                new KeyValuePair<string, string>("grant_type", "client_credentials")
            });

            var response = await _httpClient.PostAsync(tokenUrl, body);
            var responseString = await response.Content.ReadAsStringAsync();
            LastTokenResponse = responseString;

            dynamic result = JsonConvert.DeserializeObject(responseString);
            return result?.access_token ?? "";
        }

        public async Task<string> InitiatePayment(
            string merchantOrderId,
            long amountInPaise,
            string ownerUpiId,
            string redirectUrl)
        {
            var token = await GetAccessToken();

            if (string.IsNullOrEmpty(token))
                return "";

            var payload = new
            {
                merchantOrderId = merchantOrderId,
                amount = amountInPaise,
                expireAfter = 1200,
                paymentFlow = new
                {
                    type = "PG_CHECKOUT",
                    message = "Parking slot payment",
                    merchantUrls = new
                    {
                        redirectUrl = redirectUrl
                    }
                }
            };

            var jsonPayload = JsonConvert.SerializeObject(payload);

            var request = new HttpRequestMessage(HttpMethod.Post,
                "https://api-preprod.phonepe.com/apis/pg-sandbox/checkout/v2/pay");

            request.Headers.Add("Authorization", $"O-Bearer {token}");
            request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            var responseString = await response.Content.ReadAsStringAsync();
            LastPaymentResponse = responseString;

            dynamic result = JsonConvert.DeserializeObject(responseString);
            return result?.redirectUrl ?? "";
        }
    }
}