using Microsoft.AspNetCore.Mvc;
using SmartSlot.Services;
using SmartSlot.Models;

namespace SmartSlot.Controllers
{
    public class VerificationController : Controller
    {
        private readonly VerificationService _verificationService;

        public VerificationController(VerificationService verificationService)
        {
            _verificationService = verificationService;
        }

        [HttpGet]
        [Route("Verification/Upload/{slotId}")]
        public IActionResult Upload(int slotId)
        {
            ViewBag.SlotId = slotId;
            return View();
        }

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

            // Read images
            var rcBytes = await ReadImage(rcImage);
            var dlBytes = await ReadImage(dlImage);
            var vehicleBytes = await ReadImage(vehicleImage);

            // For now simulate OCR extraction
            // In next step we will add real OCR
            var simulatedRCText = "Name: RAMSULOCHANA\nVehicle: TN01AB1234";
            var simulatedDLText = "Name: RAMSULOCHANA";
            var simulatedVehicleText = "TN01AB1234";

            // Extract data
            var plateFromRC = _verificationService.ExtractPlateNumber(simulatedRCText);
            var plateFromVehicle = _verificationService.ExtractPlateNumber(simulatedVehicleText);
            var nameFromRC = _verificationService.ExtractName(simulatedRCText);
            var nameFromDL = _verificationService.ExtractName(simulatedDLText);

            // Compare
            bool plateMatch = _verificationService.IsMatch(plateFromRC, plateFromVehicle);
            bool nameMatch = _verificationService.IsMatch(nameFromRC, nameFromDL);

            // Check EXIF (basic)
            bool hasExif = rcImage.Length > 10000;
            bool normalSize = vehicleImage.Length < 10000000;

            // Risk score
            int riskScore = _verificationService.CalculateRiskScore(
                plateMatch, nameMatch, hasExif, normalSize);

            // Send to result page
            var result = new VerificationData
            {
                ExtractedPlateFromRC = plateFromRC,
                ExtractedPlateFromVehicle = plateFromVehicle,
                ExtractedNameFromRC = nameFromRC,
                ExtractedNameFromDL = nameFromDL,
                PlateMatch = plateMatch,
                NameMatch = nameMatch,
                RiskScore = riskScore,
                VerificationStatus = riskScore >= 70 ? "Verified" : "Review Required"
            };

            ViewBag.SlotId = slotId;
            TempData["SlotId"] = slotId;
            return View("Result", result);
        }

        private async Task<byte[]> ReadImage(IFormFile file)
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            return ms.ToArray();
        }
    }
}