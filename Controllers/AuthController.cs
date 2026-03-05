using BCrypt.Net;
using Microsoft.AspNetCore.Mvc;
using SmartSlot.Data;
using SmartSlot.Models;
using SmartSlot.Services;
using System.Linq;
using System.Text.RegularExpressions;

namespace SmartSlot.Controllers
{
    public class AuthController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly SmsService _smsService;
        private readonly EmailService _emailService;

        public AuthController(ApplicationDbContext context, SmsService smsService, EmailService emailService)
        {
            _context = context;
            _smsService = smsService;
            _emailService = emailService;
        }

        public IActionResult Index() => View();

        // ── Sign In ──
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

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Index", "Parking");
        }

        // ── Feature 9: Check username availability (AJAX) ──
        [HttpGet]
        public IActionResult CheckUsername(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return Json(new { available = false, reason = "empty" });

            if (!IsValidUsernameFormat(username, out string formatError))
                return Json(new { available = false, reason = formatError });

            bool taken = _context.Users.Any(u => u.Username.ToLower() == username.ToLower());
            return Json(new { available = !taken, reason = taken ? "taken" : "ok" });
        }

        private bool IsValidUsernameFormat(string username, out string error)
        {
            error = string.Empty;
            if (username.Length < 4 || username.Length > 30) { error = "length"; return false; }
            if (!Regex.IsMatch(username, @"^[a-zA-Z0-9_.]+$")) { error = "chars"; return false; }
            if (!Regex.IsMatch(username, @"^[a-zA-Z]")) { error = "start"; return false; }
            if (Regex.IsMatch(username, @"__|\.\.")) { error = "double"; return false; }
            if (Regex.IsMatch(username, @"[_.]$")) { error = "end"; return false; }
            return true;
        }

        // ── Sign Up Step 1 ──
        [HttpGet]
        public IActionResult Signup() => View();

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
            catch (Exception ex) { ViewBag.Error = ex.Message; return View(); }

            return RedirectToAction("VerifyOtp");
        }

        // ── Sign Up Step 2: Verify OTP ──
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
            var stored = HttpContext.Session.GetString("OtpCode");
            var expStr = HttpContext.Session.GetString("OtpExpiry");

            if (string.IsNullOrEmpty(phone) || string.IsNullOrEmpty(stored))
            { ViewBag.Error = "Session expired. Please start again."; ViewBag.Phone = phone; return View(); }

            if (DateTime.TryParse(expStr, out var expiry) && DateTime.UtcNow > expiry)
            { ViewBag.Error = "OTP has expired. Please request a new one."; ViewBag.Phone = phone; return View(); }

            if (otp != stored)
            { ViewBag.Error = "Invalid OTP. Please try again."; ViewBag.Phone = phone; return View(); }

            HttpContext.Session.SetString("OtpVerified", "true");
            return RedirectToAction("SignupDetails");
        }

        [HttpPost]
        public async Task<IActionResult> ResendOtp()
        {
            var phone = HttpContext.Session.GetString("OtpPhone");
            if (string.IsNullOrEmpty(phone)) return RedirectToAction("Signup");
            var otp = new Random().Next(100000, 999999).ToString();
            HttpContext.Session.SetString("OtpCode", otp);
            HttpContext.Session.SetString("OtpExpiry", DateTime.UtcNow.AddMinutes(10).ToString("o"));
            try { await _smsService.SendOtpSms(phone, otp); }
            catch { TempData["Error"] = "Failed to resend OTP."; }
            return RedirectToAction("VerifyOtp");
        }

        // ── Sign Up Step 3: Details ──
        [HttpGet]
        public IActionResult SignupDetails()
        {
            var verified = HttpContext.Session.GetString("OtpVerified");
            var phone = HttpContext.Session.GetString("OtpPhone");
            if (verified != "true" || string.IsNullOrEmpty(phone)) return RedirectToAction("Signup");
            ViewBag.Phone = phone;
            return View();
        }

        [HttpPost]
        public IActionResult SignupDetails(string email, string username, string password, string? confirmPassword)
        {
            var verified = HttpContext.Session.GetString("OtpVerified");
            var phone = HttpContext.Session.GetString("OtpPhone");
            if (verified != "true" || string.IsNullOrEmpty(phone)) return RedirectToAction("Signup");

            if (!IsValidUsernameFormat(username, out string formatError))
            { ViewBag.Error = $"Username invalid ({formatError})."; ViewBag.Phone = phone; return View(); }

            if (_context.Users.Any(u => u.Username.ToLower() == username.ToLower()))
            { ViewBag.Error = "Username already taken."; ViewBag.Phone = phone; return View(); }

            if (_context.Users.Any(u => u.Email == email))
            { ViewBag.Error = "Email already registered."; ViewBag.Phone = phone; return View(); }

            if (password.Length < 6)
            { ViewBag.Error = "Password must be at least 6 characters."; ViewBag.Phone = phone; return View(); }

            if (!string.IsNullOrEmpty(confirmPassword) && password != confirmPassword)
            { ViewBag.Error = "Passwords do not match."; ViewBag.Phone = phone; return View(); }

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

        // ── Forgot Password ──
        [HttpGet]
        public IActionResult ForgotPassword() => View();

        [HttpPost]
        public async Task<IActionResult> ForgotPasswordEmail(string email)
        {
            var user = _context.Users.FirstOrDefault(u => u.Email == email);
            if (user == null) { ViewBag.Error = "No account found with this email."; return View("ForgotPassword"); }
            await SendResetLink(user);
            ViewBag.Success = $"Password reset link sent to {MaskEmail(email)}.";
            return View("ForgotPassword");
        }

        [HttpPost]
        public async Task<IActionResult> ForgotPasswordPhone(string phone)
        {
            var user = _context.Users.FirstOrDefault(u => u.PhoneNumber == phone);
            if (user == null) { ViewBag.Error = "No account found with this phone."; return View("ForgotPassword"); }
            if (string.IsNullOrEmpty(user.Email)) { ViewBag.Error = "No email linked to this account."; return View("ForgotPassword"); }
            await SendResetLink(user);
            ViewBag.Success = $"Password reset link sent to {MaskEmail(user.Email)}.";
            return View("ForgotPassword");
        }

        private async Task SendResetLink(User user)
        {
            var oldTokens = _context.PasswordResetTokens.Where(t => t.PhoneNumber == user.PhoneNumber && !t.Used).ToList();
            _context.PasswordResetTokens.RemoveRange(oldTokens);
            var token = Guid.NewGuid().ToString("N");
            _context.PasswordResetTokens.Add(new PasswordResetToken
            {
                Token = token,
                PhoneNumber = user.PhoneNumber,
                ExpiresAt = DateTime.UtcNow.AddMinutes(30),
                Used = false
            });
            _context.SaveChanges();
            var resetLink = $"https://smartslot-sc9u.onrender.com/Auth/ResetPasswordByToken?token={token}";
            try { await _emailService.SendPasswordResetEmail(user.Email!, user.Username, resetLink); }
            catch (Exception ex) { Console.WriteLine($"Reset email failed: {ex.Message}"); }
        }

        private string MaskEmail(string email)
        {
            var parts = email.Split('@');
            if (parts.Length != 2) return email;
            var name = parts[0];
            var masked = name.Length <= 2 ? new string('*', name.Length)
                : name[0] + new string('*', name.Length - 2) + name[^1];
            return masked + "@" + parts[1];
        }

        [HttpGet]
        public IActionResult ResetPasswordByToken(string token)
        {
            var resetToken = _context.PasswordResetTokens.FirstOrDefault(t => t.Token == token && !t.Used);
            if (resetToken == null || resetToken.ExpiresAt < DateTime.UtcNow) { ViewBag.TokenInvalid = true; return View(); }
            ViewBag.Token = token;
            return View();
        }

        [HttpPost]
        public IActionResult ResetPasswordByToken(string token, string newPassword, string confirmPassword)
        {
            var resetToken = _context.PasswordResetTokens.FirstOrDefault(t => t.Token == token && !t.Used);
            if (resetToken == null || resetToken.ExpiresAt < DateTime.UtcNow) { ViewBag.TokenInvalid = true; return View(); }
            if (newPassword != confirmPassword) { ViewBag.Error = "Passwords do not match."; ViewBag.Token = token; return View(); }
            if (newPassword.Length < 6) { ViewBag.Error = "Min 6 characters."; ViewBag.Token = token; return View(); }
            var user = _context.Users.FirstOrDefault(u => u.PhoneNumber == resetToken.PhoneNumber);
            if (user == null) { ViewBag.TokenInvalid = true; return View(); }
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            resetToken.Used = true;
            _context.SaveChanges();
            ViewBag.PasswordReset = true;
            return View();
        }

        [HttpGet]
        public IActionResult ResetPassword() => View();

        [HttpPost]
        public async Task<IActionResult> ResetPassword(string phone)
        {
            var user = _context.Users.FirstOrDefault(u => u.PhoneNumber == phone);
            if (user == null) { ViewBag.Error = "No account found."; return View(); }
            var otp = new Random().Next(100000, 999999).ToString();
            HttpContext.Session.SetString("ResetPhone", phone);
            HttpContext.Session.SetString("ResetOtpCode", otp);
            HttpContext.Session.SetString("ResetOtpExpiry", DateTime.UtcNow.AddMinutes(10).ToString("o"));
            try { await _smsService.SendOtpSms(phone, otp); }
            catch { ViewBag.Error = "Failed to send OTP."; return View(); }
            return RedirectToAction("ResetPasswordVerify");
        }

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
            var stored = HttpContext.Session.GetString("ResetOtpCode");
            var expStr = HttpContext.Session.GetString("ResetOtpExpiry");
            if (string.IsNullOrEmpty(phone) || string.IsNullOrEmpty(stored)) { ViewBag.Error = "Session expired."; ViewBag.Phone = phone; return View(); }
            if (DateTime.TryParse(expStr, out var expiry) && DateTime.UtcNow > expiry) { ViewBag.Error = "OTP expired."; ViewBag.Phone = phone; return View(); }
            if (otp != stored) { ViewBag.Error = "Invalid OTP."; ViewBag.Phone = phone; return View(); }
            HttpContext.Session.SetString("ResetVerified", "true");
            return RedirectToAction("ResetPasswordNew");
        }

        [HttpPost]
        public async Task<IActionResult> ResendResetOtp()
        {
            var phone = HttpContext.Session.GetString("ResetPhone");
            if (string.IsNullOrEmpty(phone)) return RedirectToAction("ResetPassword");
            var otp = new Random().Next(100000, 999999).ToString();
            HttpContext.Session.SetString("ResetOtpCode", otp);
            HttpContext.Session.SetString("ResetOtpExpiry", DateTime.UtcNow.AddMinutes(10).ToString("o"));
            try { await _smsService.SendOtpSms(phone, otp); } catch { TempData["Error"] = "Failed to resend OTP."; }
            return RedirectToAction("ResetPasswordVerify");
        }

        [HttpGet]
        public IActionResult ResetPasswordNew()
        {
            var verified = HttpContext.Session.GetString("ResetVerified");
            var phone = HttpContext.Session.GetString("ResetPhone");
            if (verified != "true" || string.IsNullOrEmpty(phone)) return RedirectToAction("ResetPassword");
            ViewBag.Phone = phone;
            return View();
        }

        [HttpPost]
        public IActionResult ResetPasswordNew(string newPassword, string confirmPassword)
        {
            var verified = HttpContext.Session.GetString("ResetVerified");
            var phone = HttpContext.Session.GetString("ResetPhone");
            if (verified != "true" || string.IsNullOrEmpty(phone)) return RedirectToAction("ResetPassword");
            if (newPassword != confirmPassword) { ViewBag.Error = "Passwords do not match."; ViewBag.Phone = phone; return View(); }
            if (newPassword.Length < 6) { ViewBag.Error = "Min 6 characters."; ViewBag.Phone = phone; return View(); }
            var user = _context.Users.FirstOrDefault(u => u.PhoneNumber == phone);
            if (user == null) return RedirectToAction("ResetPassword");
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            _context.SaveChanges();
            HttpContext.Session.Remove("ResetPhone");
            HttpContext.Session.Remove("ResetOtpCode");
            HttpContext.Session.Remove("ResetOtpExpiry");
            HttpContext.Session.Remove("ResetVerified");
            TempData["Success"] = "Password updated successfully.";
            return RedirectToAction("Signin");
        }

        [HttpPost]
        public IActionResult DeleteAccount()
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId)) return RedirectToAction("Signin");
            var user = _context.Users.FirstOrDefault(u => u.Id.ToString() == userId);
            if (user != null) { _context.Users.Remove(user); _context.SaveChanges(); }
            HttpContext.Session.Clear();
            return RedirectToAction("Index");
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index");
        }
    }
}
