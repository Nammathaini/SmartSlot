using Microsoft.AspNetCore.Mvc;
using SmartSlot.Models;
using SmartSlot.Services;
using SmartSlot.Data;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace SmartSlot.Controllers
{
    public class VerificationController : Controller
    {
        private readonly VerificationService _verificationService;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly ApplicationDbContext _context;

        public VerificationController(
            VerificationService verificationService,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            ApplicationDbContext context)
        {
            _verificationService = verificationService;
            _configuration = configuration;
            _httpClient = httpClientFactory.CreateClient();
            _context = context;
        }

        // ========================= UPLOAD PAGE =========================
        [HttpGet]
        [Route("Verification/Upload/{id}")]
        public IActionResult Upload(int id)
        {
            var slot = _context.ParkingSlots.Find(id);
            if (slot == null) return NotFound();
            ViewBag.SlotId = id;
            return View();
        }

        // ========================= VERIFY PROCESS =========================
        [HttpPost]
        public async Task<IActionResult> Verify(
            IFormFile rcImage,
            IFormFile dlImage,
            IFormFile vehicleImage,
            int slotId)
        {
            Console.WriteLine("==================================================");
            Console.WriteLine($"[VERIFY] START — SlotId:{slotId}");
            Console.WriteLine($"[VERIFY] RC:{rcImage?.FileName} ({rcImage?.Length} bytes)");
            Console.WriteLine($"[VERIFY] DL:{dlImage?.FileName} ({dlImage?.Length} bytes)");
            Console.WriteLine($"[VERIFY] Vehicle:{vehicleImage?.FileName} ({vehicleImage?.Length} bytes)");

            if (rcImage == null || dlImage == null || vehicleImage == null)
            {
                ViewBag.Error = "Please upload all 3 images!";
                ViewBag.SlotId = slotId;
                return View("Upload");
            }

            var rcBytes = await ReadImage(rcImage);
            var dlBytes = await ReadImage(dlImage);
            var vehicleBytes = await ReadImage(vehicleImage);

            // ── Compress images before OCR (fixes E500 on Render free tier) ──
            var rcCompressed = CompressImage(rcBytes, 1024, 75);
            var dlCompressed = CompressImage(dlBytes, 1024, 75);
            var vehicleCompressed = CompressImage(vehicleBytes, 1024, 75);

            Console.WriteLine($"[VERIFY] RC compressed: {rcBytes.Length} → {rcCompressed.Length} bytes");
            Console.WriteLine($"[VERIFY] DL compressed: {dlBytes.Length} → {dlCompressed.Length} bytes");
            Console.WriteLine($"[VERIFY] Vehicle compressed: {vehicleBytes.Length} → {vehicleCompressed.Length} bytes");

            // ── ANPR ──────────────────────────────────────────────────────
            Console.WriteLine("[ANPR] Calling PlateRecognizer...");
            var plateFromVehicle = await ExtractPlateANPR(vehicleCompressed);
            Console.WriteLine($"[ANPR] Result: '{plateFromVehicle}'");

            // ── OCR RC ────────────────────────────────────────────────────
            Console.WriteLine("[OCR-RC] Sending compressed RC to OCR.space...");
            var rcText = await ExtractTextOCR(rcCompressed, "RC");
            var plateFromRC = _verificationService.ExtractPlateNumber(rcText ?? "");
            var nameFromRC = _verificationService.ExtractName(rcText ?? "");
            Console.WriteLine($"[OCR-RC] Plate:'{plateFromRC}' Name:'{nameFromRC}'");

            // ── OCR DL ────────────────────────────────────────────────────
            Console.WriteLine("[OCR-DL] Sending compressed DL to OCR.space...");
            var dlText = await ExtractTextOCR(dlCompressed, "DL");
            var nameFromDL = _verificationService.ExtractName(dlText ?? "");
            Console.WriteLine($"[OCR-DL] Name:'{nameFromDL}'");

            // ── Compare ───────────────────────────────────────────────────
            bool plateMatch = _verificationService.IsMatch(plateFromRC, plateFromVehicle);
            bool nameMatch = _verificationService.IsMatch(nameFromRC, nameFromDL);
            bool hasExif = rcImage.Length > 10000;
            bool normalSize = vehicleImage.Length < 10000000;
            int riskScore = _verificationService.CalculateRiskScore(plateMatch, nameMatch, hasExif, normalSize);

            Console.WriteLine($"[VERIFY] plateMatch={plateMatch} nameMatch={nameMatch} riskScore={riskScore}");
            Console.WriteLine("==================================================");

            var result = new VerificationData
            {
                ExtractedPlateFromRC = string.IsNullOrEmpty(plateFromRC) ? "Not detected" : plateFromRC,
                ExtractedPlateFromVehicle = string.IsNullOrEmpty(plateFromVehicle) ? "Not detected" : plateFromVehicle,
                ExtractedNameFromRC = string.IsNullOrEmpty(nameFromRC) ? "Not detected" : nameFromRC,
                ExtractedNameFromDL = string.IsNullOrEmpty(nameFromDL) ? "Not detected" : nameFromDL,
                PlateMatch = plateMatch,
                NameMatch = nameMatch,
                RiskScore = riskScore,
                DebugLog = new List<string>
                {
                    $"RC raw OCR ({rcText?.Length ?? 0} chars): {Truncate(rcText?.Replace("\n"," | "), 300)}",
                    $"DL raw OCR ({dlText?.Length ?? 0} chars): {Truncate(dlText?.Replace("\n"," | "), 300)}",
                    $"Plate from RC: '{plateFromRC}' | ANPR: '{plateFromVehicle}' | Match: {plateMatch}",
                    $"Name from RC: '{nameFromRC}' | DL: '{nameFromDL}' | Match: {nameMatch}",
                    $"Risk Score: {riskScore}"
                },
                VerificationStatus =
                    riskScore >= 30 ? "Verified" :
                    (!plateMatch && !nameMatch) ? "Name & Plate Mismatch - Review Required" :
                    !plateMatch ? "Plate Mismatch - Review Required" :
                    !nameMatch ? "Name Mismatch - Review Required" :
                                                  "Review Required"
            };

            if (riskScore >= 30)
            {
                HttpContext.Session.SetInt32("VerifiedSlotId", slotId);
                return RedirectToAction("Book", "Parking", new { id = slotId });
            }

            TempData["SlotId"] = slotId;
            ViewBag.SlotId = slotId;
            return View("Result", result);
        }

        // ========================= IMAGE COMPRESSION =========================
        /// <summary>
        /// Resize image to maxWidth and compress to JPEG at given quality.
        /// Keeps it well under 500KB so OCR.space free tier doesn't choke.
        /// </summary>
        private byte[] CompressImage(byte[] input, int maxWidth = 1024, int quality = 75)
        {
            try
            {
                using var image = SixLabors.ImageSharp.Image.Load(input);

                // Only downscale, never upscale
                if (image.Width > maxWidth)
                    image.Mutate(x => x.Resize(maxWidth, 0)); // 0 = auto height

                using var ms = new MemoryStream();
                image.Save(ms, new JpegEncoder { Quality = quality });
                var compressed = ms.ToArray();
                Console.WriteLine($"[COMPRESS] {input.Length} → {compressed.Length} bytes (max:{maxWidth}px, q:{quality})");
                return compressed;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[COMPRESS] Failed, using original: {ex.Message}");
                return input; // fallback to original
            }
        }

        // ========================= ANPR =========================
        private async Task<string> ExtractPlateANPR(byte[] imageBytes)
        {
            try
            {
                var apiToken = _configuration["PlateRecognizer:ApiToken"];
                Console.WriteLine($"[ANPR] Token present: {!string.IsNullOrEmpty(apiToken)}");

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Token {apiToken}");

                var content = new MultipartFormDataContent();
                content.Add(new ByteArrayContent(imageBytes), "upload", "vehicle.jpg");

                var response = await _httpClient.PostAsync(
                    "https://api.platerecognizer.com/v1/plate-reader/", content);

                var json = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[ANPR] Status:{response.StatusCode} Response:{json}");
                return ParseANPRResponse(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ANPR] EXCEPTION: {ex.Message}");
                return "";
            }
        }

        private string ParseANPRResponse(string json)
        {
            try
            {
                var start = json.IndexOf("\"plate\":\"") + 9;
                if (start < 9) return "";
                var end = json.IndexOf("\"", start);
                return json.Substring(start, end - start).ToUpper();
            }
            catch { return ""; }
        }

        // ========================= OCR =========================
        private async Task<string> ExtractTextOCR(byte[] imageBytes, string label = "")
        {
            try
            {
                var apiKey = _configuration["OcrSpace:ApiKey"];
                Console.WriteLine($"[OCR-{label}] Key present:{!string.IsNullOrEmpty(apiKey)} Size:{imageBytes.Length} bytes");

                var base64 = Convert.ToBase64String(imageBytes);
                var dataUrl = $"data:image/jpeg;base64,{base64}";
                Console.WriteLine($"[OCR-{label}] Base64 length:{base64.Length}");

                // Try Engine 2 first (better accuracy)
                var result = await CallOcrApi(dataUrl, apiKey, "2", label);

                // If Engine 2 fails → fallback to Engine 1
                if (string.IsNullOrEmpty(result))
                {
                    Console.WriteLine($"[OCR-{label}] Engine 2 failed, trying Engine 1...");
                    result = await CallOcrApi(dataUrl, apiKey, "1", label);
                }

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OCR-{label}] EXCEPTION: {ex.Message}");
                return "";
            }
        }

        private async Task<string> CallOcrApi(string dataUrl, string apiKey, string engine, string label)
        {
            try
            {
                var formData = new MultipartFormDataContent();
                formData.Add(new StringContent(apiKey ?? ""), "apikey");
                formData.Add(new StringContent(dataUrl), "base64Image");
                formData.Add(new StringContent("eng"), "language");
                formData.Add(new StringContent("false"), "isOverlayRequired");
                formData.Add(new StringContent(engine), "OCREngine");
                formData.Add(new StringContent("true"), "scale");
                formData.Add(new StringContent("true"), "detectOrientation");

                var response = await _httpClient.PostAsync(
                    "https://api.ocr.space/parse/image", formData);

                var json = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[OCR-{label}-E{engine}] Status:{response.StatusCode}");
                Console.WriteLine($"[OCR-{label}-E{engine}] Response:{json}");

                if (json.Contains("\"IsErroredOnProcessing\":true"))
                {
                    Console.WriteLine($"[OCR-{label}-E{engine}] IsErroredOnProcessing=true — ENGINE FAILED");
                    return "";
                }

                return ParseOCRResponse(json, $"{label}-E{engine}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OCR-{label}-E{engine}] EXCEPTION: {ex.Message}");
                return "";
            }
        }

        private string ParseOCRResponse(string json, string label = "")
        {
            try
            {
                var start = json.IndexOf("\"ParsedText\":\"") + 14;
                if (start < 14)
                {
                    Console.WriteLine($"[OCR-{label}] ParsedText not found");
                    return "";
                }

                var end = start;
                while (end < json.Length)
                {
                    if (json[end] == '"' && json[end - 1] != '\\') break;
                    end++;
                }

                var raw = json.Substring(start, end - start);
                raw = raw.Replace("\\r\\n", "\n")
                         .Replace("\\R\\N", "\n")
                         .Replace("\\n", "\n")
                         .Replace("\\t", " ")
                         .Replace("\\r", "\n");

                Console.WriteLine($"[OCR-{label}] Parsed text:\n{raw}");
                return raw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OCR-{label}] Parse error: {ex.Message}");
                return "";
            }
        }

        // ========================= IMAGE READER =========================
        private async Task<byte[]> ReadImage(IFormFile file)
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            return ms.ToArray();
        }

        private string Truncate(string s, int max) =>
            string.IsNullOrEmpty(s) ? "EMPTY" :
            s.Length <= max ? s : s.Substring(0, max) + "...";
    }
}
