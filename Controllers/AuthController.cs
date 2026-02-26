using BCrypt.Net;
using Microsoft.AspNetCore.Mvc;
using SmartSlot.Data;
using SmartSlot.Models;
using SmartSlot.Services;
using System.Linq;

namespace SmartSlot.Controllers
{
    public class AuthController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly SmsService _smsService;

        public AuthController(ApplicationDbContext context, SmsService smsService)
        {
            _context = context;
            _smsService = smsService;
        }

        // =========================
        // FIRST PAGE
        // =========================
        public IActionResult Index()
        {
            return View();
        }

        // =========================
        // SIGN IN
        // =========================
        [HttpGet]
        public IActionResult Signin(string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        public IActionResult Signin(string username, string password, string? returnUrl)
        {
            var user = _context.Users.FirstOrDefault(u => u.Username == username);

            if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            {
                ViewBag.Error = "Invalid username or password";
                ViewBag.ReturnUrl = returnUrl;
                return View();
            }

            HttpContext.Session.SetString("UserId", user.Id.ToString());
            HttpContext.Session.SetString("Username", user.Username);

            if (!string.IsNullOrEmpty(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Index", "Parking");
        }

        // =========================
        // SIGN UP - STEP 1: Enter Phone
        // =========================
        [HttpGet]
        public IActionResult Signup()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Signup(string phone)
        {
            if (_context.Users.Any(u => u.PhoneNumber == phone))
            {
                ViewBag.Error = "This phone number is already registered.";
                return View();
            }

            var otp = new Random().Next(100000, 999999).ToString();
            var expiry = DateTime.UtcNow.AddMinutes(10);

            HttpContext.Session.SetString("OtpPhone", phone);
            HttpContext.Session.SetString("OtpCode", otp);
            HttpContext.Session.SetString("OtpExpiry", expiry.ToString("o"));

            try { await _smsService.SendOtpSms(phone, otp); }
            catch (Exception ex)
            {
                Console.WriteLine($"OTP SMS failed: {ex.Message}");
                ViewBag.Error = "Failed to send OTP. Please check your phone number.";
                return View();
            }

            return RedirectToAction("VerifyOtp");
        }

        // =========================
        // SIGN UP - STEP 2: Verify OTP
        // =========================
        [HttpGet]
        public IActionResult VerifyOtp()
        {
            var phone = HttpContext.Session.GetString("OtpPhone");
            if (string.IsNullOrEmpty(phone)) return RedirectToAction("Signup");
            ViewBag.Phone = phone;
            return View();
        }

        [HttpPost]
        public IActionResult VerifyOtp(string otp)
        {
            var phone = HttpContext.Session.GetString("OtpPhone");
            var storedOtp = HttpContext.Session.GetString("OtpCode");
            var expiryStr = HttpContext.Session.GetString("OtpExpiry");

            if (string.IsNullOrEmpty(phone) || string.IsNullOrEmpty(storedOtp))
            {
                ViewBag.Error = "Session expired. Please start again.";
                ViewBag.Phone = phone;
                return View();
            }

            if (DateTime.TryParse(expiryStr, out var expiry) && DateTime.UtcNow > expiry)
            {
                ViewBag.Error = "OTP has expired. Please request a new one.";
                ViewBag.Phone = phone;
                return View();
            }

            if (otp != storedOtp)
            {
                ViewBag.Error = "Invalid OTP. Please try again.";
                ViewBag.Phone = phone;
                return View();
            }

            HttpContext.Session.SetString("OtpVerified", "true");
            return RedirectToAction("SignupDetails");
        }

        [HttpPost]
        public async Task<IActionResult> ResendOtp()
        {
            var phone = HttpContext.Session.GetString("OtpPhone");
            if (string.IsNullOrEmpty(phone)) return RedirectToAction("Signup");

            var otp = new Random().Next(100000, 999999).ToString();
            var expiry = DateTime.UtcNow.AddMinutes(10);

            HttpContext.Session.SetString("OtpCode", otp);
            HttpContext.Session.SetString("OtpExpiry", expiry.ToString("o"));

            try { await _smsService.SendOtpSms(phone, otp); }
            catch (Exception ex)
            {
                Console.WriteLine($"Resend OTP failed: {ex.Message}");
                TempData["Error"] = "Failed to resend OTP. Please try again.";
            }

            return RedirectToAction("VerifyOtp");
        }

        // =========================
        // SIGN UP - STEP 3: Enter Details
        // =========================
        [HttpGet]
        public IActionResult SignupDetails()
        {
            var verified = HttpContext.Session.GetString("OtpVerified");
            var phone = HttpContext.Session.GetString("OtpPhone");

            if (verified != "true" || string.IsNullOrEmpty(phone))
                return RedirectToAction("Signup");

            ViewBag.Phone = phone;
            return View();
        }

        [HttpPost]
        public IActionResult SignupDetails(string email, string username, string password)
        {
            var verified = HttpContext.Session.GetString("OtpVerified");
            var phone = HttpContext.Session.GetString("OtpPhone");

            if (verified != "true" || string.IsNullOrEmpty(phone))
                return RedirectToAction("Signup");

            if (_context.Users.Any(u => u.Username == username))
            {
                ViewBag.Error = "Username already exists. Please choose another.";
                ViewBag.Phone = phone;
                return View();
            }

            if (_context.Users.Any(u => u.Email == email))
            {
                ViewBag.Error = "Email already registered.";
                ViewBag.Phone = phone;
                return View();
            }

            var user = new User
            {
                PhoneNumber = phone,
                Email = email,
                Username = username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password)
            };

            _context.Users.Add(user);
            _context.SaveChanges();

            HttpContext.Session.Remove("OtpPhone");
            HttpContext.Session.Remove("OtpCode");
            HttpContext.Session.Remove("OtpExpiry");
            HttpContext.Session.Remove("OtpVerified");

            HttpContext.Session.SetString("UserId", user.Id.ToString());
            HttpContext.Session.SetString("Username", user.Username);

            return RedirectToAction("Index", "Parking");
        }

        // =========================
        // RESET PASSWORD - STEP 1: Enter Phone
        // =========================
        [HttpGet]
        public IActionResult ResetPassword()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ResetPassword(string phone)
        {
            // Check phone exists in DB
            var user = _context.Users.FirstOrDefault(u => u.PhoneNumber == phone);
            if (user == null)
            {
                ViewBag.Error = "No account found with this phone number.";
                return View();
            }

            var otp = new Random().Next(100000, 999999).ToString();
            var expiry = DateTime.UtcNow.AddMinutes(10);

            HttpContext.Session.SetString("ResetPhone", phone);
            HttpContext.Session.SetString("ResetOtpCode", otp);
            HttpContext.Session.SetString("ResetOtpExpiry", expiry.ToString("o"));

            try { await _smsService.SendOtpSms(phone, otp); }
            catch (Exception ex)
            {
                Console.WriteLine($"Reset OTP failed: {ex.Message}");
                ViewBag.Error = "Failed to send OTP. Please try again.";
                return View();
            }

            return RedirectToAction("ResetPasswordVerify");
        }

        // =========================
        // RESET PASSWORD - STEP 2: Verify OTP
        // =========================
        [HttpGet]
        public IActionResult ResetPasswordVerify()
        {
            var phone = HttpContext.Session.GetString("ResetPhone");
            if (string.IsNullOrEmpty(phone)) return RedirectToAction("ResetPassword");
            ViewBag.Phone = phone;
            return View();
        }

        [HttpPost]
        public IActionResult ResetPasswordVerify(string otp)
        {
            var phone = HttpContext.Session.GetString("ResetPhone");
            var storedOtp = HttpContext.Session.GetString("ResetOtpCode");
            var expiryStr = HttpContext.Session.GetString("ResetOtpExpiry");

            if (string.IsNullOrEmpty(phone) || string.IsNullOrEmpty(storedOtp))
            {
                ViewBag.Error = "Session expired. Please start again.";
                ViewBag.Phone = phone;
                return View();
            }

            if (DateTime.TryParse(expiryStr, out var expiry) && DateTime.UtcNow > expiry)
            {
                ViewBag.Error = "OTP has expired. Please request a new one.";
                ViewBag.Phone = phone;
                return View();
            }

            if (otp != storedOtp)
            {
                ViewBag.Error = "Invalid OTP. Please try again.";
                ViewBag.Phone = phone;
                return View();
            }

            HttpContext.Session.SetString("ResetVerified", "true");
            return RedirectToAction("ResetPasswordNew");
        }

        [HttpPost]
        public async Task<IActionResult> ResendResetOtp()
        {
            var phone = HttpContext.Session.GetString("ResetPhone");
            if (string.IsNullOrEmpty(phone)) return RedirectToAction("ResetPassword");

            var otp = new Random().Next(100000, 999999).ToString();
            var expiry = DateTime.UtcNow.AddMinutes(10);

            HttpContext.Session.SetString("ResetOtpCode", otp);
            HttpContext.Session.SetString("ResetOtpExpiry", expiry.ToString("o"));

            try { await _smsService.SendOtpSms(phone, otp); }
            catch (Exception ex)
            {
                Console.WriteLine($"Resend Reset OTP failed: {ex.Message}");
                TempData["Error"] = "Failed to resend OTP. Please try again.";
            }

            return RedirectToAction("ResetPasswordVerify");
        }

        // =========================
        // RESET PASSWORD - STEP 3: Set New Password
        // =========================
        [HttpGet]
        public IActionResult ResetPasswordNew()
        {
            var verified = HttpContext.Session.GetString("ResetVerified");
            var phone = HttpContext.Session.GetString("ResetPhone");

            if (verified != "true" || string.IsNullOrEmpty(phone))
                return RedirectToAction("ResetPassword");

            ViewBag.Phone = phone;
            return View();
        }

        [HttpPost]
        public IActionResult ResetPasswordNew(string newPassword, string confirmPassword)
        {
            var verified = HttpContext.Session.GetString("ResetVerified");
            var phone = HttpContext.Session.GetString("ResetPhone");

            if (verified != "true" || string.IsNullOrEmpty(phone))
                return RedirectToAction("ResetPassword");

            if (newPassword != confirmPassword)
            {
                ViewBag.Error = "Passwords do not match.";
                ViewBag.Phone = phone;
                return View();
            }

            if (newPassword.Length < 6)
            {
                ViewBag.Error = "Password must be at least 6 characters.";
                ViewBag.Phone = phone;
                return View();
            }

            var user = _context.Users.FirstOrDefault(u => u.PhoneNumber == phone);
            if (user == null) return RedirectToAction("ResetPassword");

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            _context.SaveChanges();

            // Clear reset session
            HttpContext.Session.Remove("ResetPhone");
            HttpContext.Session.Remove("ResetOtpCode");
            HttpContext.Session.Remove("ResetOtpExpiry");
            HttpContext.Session.Remove("ResetVerified");

            TempData["Success"] = "Password updated successfully. Please sign in.";
            return RedirectToAction("Signin");
        }

        // =========================
        // DELETE ACCOUNT
        // =========================
        [HttpPost]
        public IActionResult DeleteAccount()
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
                return RedirectToAction("Signin");

            var user = _context.Users.FirstOrDefault(u => u.Id.ToString() == userId);
            if (user != null)
            {
                _context.Users.Remove(user);
                _context.SaveChanges();
            }

            HttpContext.Session.Clear();
            return RedirectToAction("Index");
        }

        // =========================
        // LOGOUT
        // =========================
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index");
        }
    }
}
