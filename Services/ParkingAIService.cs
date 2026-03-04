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
                {
                    Console.WriteLine("⚠️ Clarifai API key missing — using fallback score");
                    return FallbackScore(imageBytes);
                }

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

                // ── Build fresh HttpRequestMessage so headers are clean every call ──
                var request = new HttpRequestMessage(HttpMethod.Post,
                    "https://api.clarifai.com/v2/users/clarifai/apps/main/models/general-image-recognition/outputs");

                request.Headers.Clear();
                request.Headers.Add("Authorization", $"Key {apiKey}");
                request.Headers.Add("X-Clarifai-Client-Id", "smartslot-app");
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);
                var responseJson = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"🤖 Clarifai HTTP {(int)response.StatusCode}");

                // ── 401 / 403 = auth problem → fallback so image isn't blocked ──
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                    response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    Console.WriteLine("⚠️ Clarifai auth failed — using visual fallback");
                    return FallbackScore(imageBytes);
                }

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"❌ Clarifai non-success: {responseJson}");
                    return FallbackScore(imageBytes);
                }

                // ── Check Clarifai inner status code ──
                try
                {
                    var statusDoc = JsonDocument.Parse(responseJson);
                    if (statusDoc.RootElement.TryGetProperty("status", out var statusEl))
                    {
                        if (statusEl.TryGetProperty("code", out var codeEl))
                        {
                            var code = codeEl.GetInt32();
                            if (code != 10000)
                            {
                                var desc = statusEl.TryGetProperty("description", out var d)
                                    ? d.GetString() : "Unknown error";
                                Console.WriteLine($"⚠️ Clarifai status {code}: {desc} — using fallback");
                                return FallbackScore(imageBytes);
                            }
                        }
                    }
                }
                catch { /* continue */ }

                return CalculateParkingScore(responseJson);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ ParkingAI exception: {ex.Message}");
                return FallbackScore(null);
            }
        }

        // ── FALLBACK: when Clarifai is unavailable, give a reasonable passing score ──
        // Image was uploaded = user has something to show → give Basic (55) so it's not blocked
        private (int score, string badge, string details) FallbackScore(byte[]? imageBytes)
        {
            // If an image was provided (even if we couldn't analyse it), pass it as Basic
            if (imageBytes != null && imageBytes.Length > 0)
                return (55, "🥉 Silver", "AI analysis unavailable — slot accepted as Silver based on image upload");

            return (50, "🏠 Basic", "AI analysis unavailable — slot accepted as Basic");
        }

        private (int score, string badge, string details) CalculateParkingScore(string responseJson)
        {
            int score = 0;
            var detectedFeatures = new List<string>();

            try
            {
                if (string.IsNullOrEmpty(responseJson))
                    return FallbackScore(new byte[1]);

                var doc = JsonDocument.Parse(responseJson);

                if (!doc.RootElement.TryGetProperty("outputs", out var outputs) || outputs.GetArrayLength() == 0)
                    return FallbackScore(new byte[1]);

                var firstOutput = outputs[0];

                if (!firstOutput.TryGetProperty("data", out var data) ||
                    !data.TryGetProperty("concepts", out var concepts) ||
                    concepts.GetArrayLength() == 0)
                    return FallbackScore(new byte[1]);

                // Collect all concept names + confidences
                var conceptMap = new Dictionary<string, double>();
                foreach (var concept in concepts.EnumerateArray())
                {
                    var name = concept.GetProperty("name").GetString()?.ToLower() ?? "";
                    var confidence = concept.GetProperty("value").GetDouble();
                    conceptMap[name] = confidence;

                    Console.WriteLine($"  🏷 {name}: {confidence:P0}");
                }

                bool wallDetected = false;
                bool indoorDetected = false;
                bool outdoorDetected = false;
                bool spaceDetected = false;
                bool parkingDetected = false;
                bool vehicleDetected = false;
                bool homeDetected = false;

                // STRUCTURE / WALL
                var structureKeywords = new[] {
                    "wall","concrete","brick","building","architecture","pillar","column",
                    "ceiling","floor","pavement","driveway","gate","fence","door","shutter",
                    "roof","house","home","property","residential","real estate","facade",
                    "exterior","interior","tile","stone","surface","ground","courtyard"
                };
                foreach (var kv in conceptMap)
                {
                    if (!wallDetected && structureKeywords.Any(k => kv.Key.Contains(k)) && kv.Value > 0.30)
                    {
                        score += 25;
                        detectedFeatures.Add($"🧱 Structure {(int)(kv.Value * 100)}%");
                        wallDetected = true;
                    }
                }

                // HOME / RESIDENTIAL
                var homeKeywords = new[] {
                    "house","home","residential","property","real estate","suburb",
                    "neighborhood","bungalow","villa","dwelling","shed","garage","driveway"
                };
                foreach (var kv in conceptMap)
                {
                    if (!homeDetected && homeKeywords.Any(k => kv.Key.Contains(k)) && kv.Value > 0.25)
                    {
                        score += 20;
                        detectedFeatures.Add($"🏠 Residential {(int)(kv.Value * 100)}%");
                        homeDetected = true;
                    }
                }

                // INDOOR
                var indoorKeywords = new[] {
                    "indoor","garage","basement","interior","underground","carport","parking garage"
                };
                foreach (var kv in conceptMap)
                {
                    if (!indoorDetected && !outdoorDetected && indoorKeywords.Any(k => kv.Key.Contains(k)) && kv.Value > 0.30)
                    {
                        score += 20;
                        detectedFeatures.Add($"🏢 Indoor {(int)(kv.Value * 100)}%");
                        indoorDetected = true;
                    }
                }

                // OUTDOOR
                var outdoorKeywords = new[] {
                    "outdoor","street","road","lot","open","driveway","yard",
                    "alley","courtyard","lane","pathway","sidewalk","pavement"
                };
                foreach (var kv in conceptMap)
                {
                    if (!outdoorDetected && !indoorDetected && outdoorKeywords.Any(k => kv.Key.Contains(k)) && kv.Value > 0.30)
                    {
                        score += 15;
                        detectedFeatures.Add($"🌳 Outdoor {(int)(kv.Value * 100)}%");
                        outdoorDetected = true;
                    }
                }

                // SPACE
                var spaceKeywords = new[] {
                    "empty","space","area","ground","asphalt","concrete","floor",
                    "open space","land","plot","parking space","bay"
                };
                foreach (var kv in conceptMap)
                {
                    if (!spaceDetected && spaceKeywords.Any(k => kv.Key.Contains(k)) && kv.Value > 0.30)
                    {
                        score += 20;
                        detectedFeatures.Add($"📐 Space {(int)(kv.Value * 100)}%");
                        spaceDetected = true;
                    }
                }

                // VEHICLE PRESENCE
                var vehicleKeywords = new[] {
                    "car","vehicle","automobile","truck","van","motorcycle","bike",
                    "suv","sedan","hatchback","motor vehicle","transport"
                };
                foreach (var kv in conceptMap)
                {
                    if (!vehicleDetected && vehicleKeywords.Any(k => kv.Key.Contains(k)) && kv.Value > 0.30)
                    {
                        score += 15;
                        detectedFeatures.Add($"🚗 Vehicle {(int)(kv.Value * 100)}%");
                        vehicleDetected = true;
                    }
                }

                // PARKING LABEL (direct bonus)
                foreach (var kv in conceptMap)
                {
                    if (!parkingDetected && kv.Key.Contains("parking") && kv.Value > 0.25)
                    {
                        score += 15;
                        detectedFeatures.Add($"🅿️ Parking {(int)(kv.Value * 100)}%");
                        parkingDetected = true;
                    }
                }

                score = Math.Min(score, 100);

                // Generous fallback — don't reject if we couldn't match anything
                if (score == 0)
                {
                    score = 50;
                    detectedFeatures.Add("🏠 Basic parking area detected");
                }
                else if (score < 40)
                {
                    score = 45;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Score calculation error: {ex.Message}");
                return (55, "🥉 Silver", $"Analysis error — slot accepted: {ex.Message}");
            }

            string badge =
                score >= 90 ? "🥇 Premium" :
                score >= 75 ? "🥈 Gold" :
                score >= 55 ? "🥉 Silver" :
                score >= 40 ? "🏠 Basic" :
                              "❌ Poor";

            string details = detectedFeatures.Count > 0
                ? string.Join(" | ", detectedFeatures)
                : "Basic parking area detected";

            Console.WriteLine($"✅ Final score: {score} → {badge}");
            return (score, badge, details);
        }
    }
}
