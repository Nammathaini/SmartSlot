using System.Text;
using System.Text.Json;

namespace SmartSlot.Services
{
    public class ParkingAIService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public ParkingAIService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task<(int score, string badge, string details)> AnalyzeParkingImage(byte[] imageBytes)
        {
            try
            {
                var apiKey = _configuration["Clarifai:ApiKey"];

                if (string.IsNullOrEmpty(apiKey))
                    return (0, "❌ Config Error", "Clarifai API Key missing");

                var base64Image = Convert.ToBase64String(imageBytes);

                var requestBody = new
                {
                    inputs = new[]
                    {
                        new
                        {
                            data = new
                            {
                                image = new { base64 = base64Image }
                            }
                        }
                    }
                };

                var json = JsonSerializer.Serialize(requestBody);

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Key {apiKey}");

                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    "https://api.clarifai.com/v2/users/uzpsxpq02fgi/apps/moderation/models/general-image-recognition/versions/aa7f35c01e0642fda5cf400f543e7c40/outputs",
                    content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorText = await response.Content.ReadAsStringAsync();
                    return (0, "❌ API Error", errorText);
                }

                var responseJson = await response.Content.ReadAsStringAsync();

                return CalculateParkingScore(responseJson);
            }
            catch (Exception ex)
            {
                return (0, "❌ Analysis Failed", ex.Message);
            }
        }

        private (int score, string badge, string details) CalculateParkingScore(string responseJson)
        {
            int score = 0;
            var detectedFeatures = new List<string>();

            try
            {
                if (string.IsNullOrEmpty(responseJson))
                    return (0, "❌ Failed", "Empty response from Clarifai");

                var doc = JsonDocument.Parse(responseJson);

                if (!doc.RootElement.TryGetProperty("outputs", out var outputs))
                    return (0, "❌ API Error", "No outputs found");

                if (outputs.GetArrayLength() == 0)
                    return (0, "❌ API Error", "Empty outputs");

                var firstOutput = outputs[0];

                if (!firstOutput.TryGetProperty("data", out var data))
                    return (0, "❌ API Error", "No data section");

                if (!data.TryGetProperty("concepts", out var concepts))
                    return (0, "❌ API Error", "No concepts found");

                bool wallDetected = false;
                bool indoorDetected = false;
                bool outdoorDetected = false;
                bool spaceDetected = false;
                bool parkingDetected = false;

                foreach (var concept in concepts.EnumerateArray())
                {
                    var name = concept.GetProperty("name").GetString()?.ToLower() ?? "";
                    var confidence = concept.GetProperty("value").GetDouble();

                    // Wall / Structure
                    if (!wallDetected &&
                        (name.Contains("wall") ||
                         name.Contains("concrete") ||
                         name.Contains("brick") ||
                         name.Contains("building") ||
                         name.Contains("architecture") ||
                         name.Contains("pillar") ||
                         name.Contains("column")) &&
                        confidence > 0.5)
                    {
                        score += 30;
                        detectedFeatures.Add($"🧱 Structure {(int)(confidence * 100)}%");
                        wallDetected = true;
                    }

                    // Indoor
                    if (!indoorDetected && !outdoorDetected &&
                        (name.Contains("indoor") ||
                         name.Contains("garage") ||
                         name.Contains("basement")) &&
                        confidence > 0.5)
                    {
                        score += 25;
                        detectedFeatures.Add($"🏢 Indoor {(int)(confidence * 100)}%");
                        indoorDetected = true;
                    }

                    // Outdoor
                    if (!outdoorDetected && !indoorDetected &&
                        (name.Contains("outdoor") ||
                         name.Contains("street") ||
                         name.Contains("road") ||
                         name.Contains("lot")) &&
                        confidence > 0.5)
                    {
                        score += 20;
                        detectedFeatures.Add($"🌳 Outdoor {(int)(confidence * 100)}%");
                        outdoorDetected = true;
                    }

                    // Space
                    if (!spaceDetected &&
                        (name.Contains("empty") ||
                         name.Contains("space") ||
                         name.Contains("area") ||
                         name.Contains("ground") ||
                         name.Contains("asphalt")) &&
                        confidence > 0.5)
                    {
                        score += 25;
                        detectedFeatures.Add($"📐 Space {(int)(confidence * 100)}%");
                        spaceDetected = true;
                    }

                    // Parking
                    if (!parkingDetected &&
                        name.Contains("parking") &&
                        confidence > 0.5)
                    {
                        score += 20;
                        detectedFeatures.Add($"🅿️ Parking {(int)(confidence * 100)}%");
                        parkingDetected = true;
                    }
                }

                score = Math.Min(score, 100);

                if (score == 0 && concepts.GetArrayLength() > 0)
                    score = 30;

            }
            catch (Exception ex)
            {
                return (0, "❌ Analysis Failed", ex.Message);
            }

            string badge =
                score >= 90 ? "🥇 Premium" :
                score >= 80 ? "🥈 Gold" :
                score >= 65 ? "🥉 Silver" :
                score >= 30 ? "🏠 Basic" :
                "❌ Not Recommended";

            string details = detectedFeatures.Count > 0
                ? string.Join(" | ", detectedFeatures)
                : "Basic parking detected";

            return (score, badge, details);
        }
    }
}