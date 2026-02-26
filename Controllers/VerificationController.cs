using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartSlot.Models;
using SmartSlot.Services;
using SmartSlot.Data;

namespace SmartSlot.Controllers
{
    public class VerificationController : Controller
    {
        private readonly VerificationService _verificationService;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly ApplicationDbContext _context; // ✅ FIXED

        public VerificationController(
            VerificationService verificationService,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            ApplicationDbContext context) // ✅ Inject DB
        {
            _verificationService = verificationService;
            _configuration = configuration;
            _httpClient = httpClientFactory.CreateClient();
            _context = context; // ✅ assign
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
            if (rcImage == null || dlImage == null || vehicleImage == null)
            {
                ViewBag.Error = "Please upload all 3 images!";
                ViewBag.SlotId = slotId;
                return View("Upload");
            }

            var rcBytes = await ReadImage(rcImage);
            var dlBytes = await ReadImage(dlImage);
            var vehicleBytes = await ReadImage(vehicleImage);

            // 🔥 ANPR - Extract plate from vehicle photo
            var plateFromVehicle = await ExtractPlateANPR(vehicleBytes);

            // 🔥 OCR - Extract plate and name from RC
            var rcText = await ExtractTextOCR(rcBytes);
            var plateFromRC = _verificationService.ExtractPlateNumber(rcText);
            var nameFromRC = _verificationService.ExtractName(rcText);

            // 🔥 OCR - Extract name from DL
            var dlText = await ExtractTextOCR(dlBytes);
            var nameFromDL = _verificationService.ExtractName(dlText);

            // 🔥 Compare
            bool plateMatch = _verificationService.IsMatch(plateFromRC, plateFromVehicle);
            bool nameMatch = _verificationService.IsMatch(nameFromRC, nameFromDL);

            // 🔥 Basic checks
            bool hasExif = rcImage.Length > 10000;
            bool normalSize = vehicleImage.Length < 10000000;

            // 🔥 Risk score
            int riskScore = _verificationService.CalculateRiskScore(
                plateMatch, nameMatch, hasExif, normalSize);

            var result = new VerificationData
            {
                ExtractedPlateFromRC = string.IsNullOrEmpty(plateFromRC) ? "Not detected" : plateFromRC,
                ExtractedPlateFromVehicle = string.IsNullOrEmpty(plateFromVehicle) ? "Not detected" : plateFromVehicle,
                ExtractedNameFromRC = string.IsNullOrEmpty(nameFromRC) ? "Not detected" : nameFromRC,
                ExtractedNameFromDL = string.IsNullOrEmpty(nameFromDL) ? "Not detected" : nameFromDL,
                PlateMatch = plateMatch,
                NameMatch = nameMatch,
                RiskScore = riskScore,
                VerificationStatus =
                    riskScore >= 30 ? "Verified" :
                    (!plateMatch && !nameMatch) ? "⚠ Name & Plate Mismatch - Review Required" :
                    !plateMatch ? "⚠ Plate Mismatch - Review Required" :
                    !nameMatch ? "⚠ Name Mismatch - Review Required" :
                    "Review Required",
            };

            // 🔥 ENTERPRISE SECURITY UPDATE
            if (riskScore >= 30)
            {
                // ✅ Store verification session
                HttpContext.Session.SetInt32("VerifiedSlotId", slotId);

                // ✅ Redirect to booking page
                return RedirectToAction("Book", "Parking", new { id = slotId });
            }

            // If not verified → show result page
            TempData["SlotId"] = slotId;
            ViewBag.SlotId = slotId;
            return View("Result", result);
        }

        // ========================= ANPR =========================
        private async Task<string> ExtractPlateANPR(byte[] imageBytes)
        {
            try
            {
                var apiToken = _configuration["PlateRecognizer:ApiToken"];
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Token {apiToken}");

                var content = new MultipartFormDataContent();
                content.Add(new ByteArrayContent(imageBytes), "upload", "vehicle.jpg");

                var response = await _httpClient.PostAsync(
                    "https://api.platerecognizer.com/v1/plate-reader/", content);

                var json = await response.Content.ReadAsStringAsync();
                return ParseANPRResponse(json);
            }
            catch
            {
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
            catch
            {
                return "";
            }
        }

        // ========================= OCR =========================
        private async Task<string> ExtractTextOCR(byte[] imageBytes)
        {
            try
            {
                var apiKey = _configuration["OcrSpace:ApiKey"];
                var base64 = Convert.ToBase64String(imageBytes);
                var dataUrl = $"data:image/jpeg;base64,{base64}";

                var formData = new MultipartFormDataContent();
                formData.Add(new StringContent(apiKey ?? ""), "apikey");
                formData.Add(new StringContent(dataUrl), "base64Image");
                formData.Add(new StringContent("eng"), "language");
                formData.Add(new StringContent("false"), "isOverlayRequired");
                formData.Add(new StringContent("2"), "OCREngine");
                formData.Add(new StringContent("true"), "scale");
                formData.Add(new StringContent("true"), "detectOrientation");

                var response = await _httpClient.PostAsync(
                    "https://api.ocr.space/parse/image", formData);

                var json = await response.Content.ReadAsStringAsync();
                Console.WriteLine("OCR RAW RESPONSE: " + json);
                return ParseOCRResponse(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine("OCR ERROR: " + ex.Message);
                return "";
            }
        }

        private string ParseOCRResponse(string json)
        {
            try
            {
                if (json.Contains("\"IsErroredOnProcessing\":true"))
                {
                    Console.WriteLine("OCR API ERROR IN RESPONSE: " + json);
                    return "";
                }

                var start = json.IndexOf("\"ParsedText\":\"") + 14;
                if (start < 14) return "";

                var end = start;
                while (end < json.Length)
                {
                    if (json[end] == '"' && json[end - 1] != '\\')
                        break;
                    end++;
                }

                var raw = json.Substring(start, end - start);

                raw = raw.Replace("\\r\\n", "\n")
                         .Replace("\\R\\N", "\n")
                         .Replace("\\n", "\n")
                         .Replace("\\t", " ")
                         .Replace("\\r", "\n");

                Console.WriteLine("OCR PARSED TEXT: " + raw);
                return raw;
            }
            catch (Exception ex)
            {
                Console.WriteLine("OCR PARSE ERROR: " + ex.Message);
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
    }
}