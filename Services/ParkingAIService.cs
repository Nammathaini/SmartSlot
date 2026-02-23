using System.Text;
using System.Text.Json;
using static System.Runtime.InteropServices.JavaScript.JSType;

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
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Key {apiKey}");

                var response = await _httpClient.PostAsync(
                    "https://api.clarifai.com/v2/users/uzpsxpq02fgi/apps/moderation/models/general-image-recognition/versions/aa7f35c01e0642fda5cf400f543e7c40/outputs",
                    content);

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
                var doc = JsonDocument.Parse(responseJson);

                if (!doc.RootElement.TryGetProperty("outputs", out var outputs))
                    return (0, "❌ API Error", "No outputs found");

                if (outputs.GetArrayLength() == 0)
                    return (0, "❌ API Error", "Empty outputs from Clarifai");

                var firstOutput = outputs[0];

                if (!firstOutput.TryGetProperty("data", out var data))
                    return (0, "❌ API Error", "No data in output");

                if (!data.TryGetProperty("concepts", out var concepts))
                    return (0, "❌ API Error", "No concepts found");

                var labelList = new List<string>();

                bool wallDetected = false;
                bool indoorDetected = false;
                bool outdoorDetected = false;
                bool spaceDetected = false;
                bool parkingDetected = false;

                foreach (var concept in concepts.EnumerateArray())
                {
                    var name = concept.GetProperty("name")
                        .GetString()?.ToLower() ?? "";
                    var confidence = concept.GetProperty("value").GetDouble();

                    labelList.Add(name);

                    // Priority 1 — Wall/Structure presence — 30 points
                    if (!wallDetected &&
                        (name.Contains("wall") ||
                        name.Contains("concrete") ||
                        name.Contains("brick") ||
                        name.Contains("building") ||
                        name.Contains("architecture") ||
                        name.Contains("ceiling") ||
                        name.Contains("pillar") ||
                        name.Contains("column") ||
                        name.Contains("structure")) && confidence > 0.5)
                    {
                        score += 30;
                        detectedFeatures.Add($"🧱 Wall/Structure:{(int)(confidence * 100)}%");
                        wallDetected = true;
                    }

                    // Priority 2 — Indoor parking — 25 points
                    if (!indoorDetected && !outdoorDetected &&
                        (name.Contains("indoor") ||
                        name.Contains("garage") ||
                        name.Contains("basement") ||
                        name.Contains("inside") ||
                        name.Contains("ceiling") ||
                        name.Contains("room")) && confidence > 0.5)
                    {
                        score += 25;
                        detectedFeatures.Add($"🏢 Indoor Parking:{(int)(confidence * 100)}%");
                        indoorDetected = true;
                    }

                    // Priority 2 — Outdoor parking — 15 points
                    if (!outdoorDetected && !indoorDetected &&
                        (name.Contains("outdoor") ||
                        name.Contains("outside") ||
                        name.Contains("open") ||
                        name.Contains("street") ||
                        name.Contains("road") ||
                        name.Contains("lot")) && confidence > 0.5)
                    {
                        score += 20;
                        detectedFeatures.Add($"🌳 Outdoor Parking:{(int)(confidence * 100)}%");
                        outdoorDetected = true;
                    }

                    // Priority 3 — Space quantity — 25 points
                    if (!spaceDetected &&
                        (name.Contains("empty") ||
                        name.Contains("space") ||
                        name.Contains("area") ||
                        name.Contains("floor") ||
                        name.Contains("ground") ||
                        name.Contains("asphalt") ||
                        name.Contains("pavement")) && confidence > 0.5)
                    {
                        score += 25;
                        detectedFeatures.Add($"📐 Space Available:{(int)(confidence * 100)}%");
                        spaceDetected = true;
                    }

                    // Bonus — Parking confirmed — 20 points
                    if (!parkingDetected &&
                        (name.Contains("parking") ||
                        name.Contains("car park") ||
                        name.Contains("garage")) && confidence > 0.5)
                    {
                        score += 20;
                        detectedFeatures.Add($"🅿️ Parking Confirmed:{(int)(confidence * 100)}%");
                        parkingDetected = true;
                    }
                }

                // Cap at 100
                score = Math.Min(score, 100);

                // Give minimum 30 if at least something detected
                if (labelList.Count > 0 && score == 0)
                {
                    score = 30;
                    detectedFeatures.Add("🏠 Basic space detected");
                }

                // Not valid at all
                if (labelList.Count == 0)
                {
                    return (0, "❌ Could not analyze",
                        "Please upload a clearer photo");
                }
            }
            catch (Exception ex)
            {
                return (0, "❌ Analysis Failed", ex.Message);
            }

            string badge;
            if (score >= 90)
                badge = "🥇 Premium";
            else if (score >= 80)
                badge = "🥈 Gold";
            else if (score >= 65)
                badge = "🥉 Silver";
            else if (score >= 30)
                badge = "🏠 Basic";
            else
                badge = "❌ Not Recommended";

            var details = detectedFeatures.Count > 0
                ? string.Join("|", detectedFeatures)
                : "Basic parking detected";

            return (score, badge, details);
        }

    }
}