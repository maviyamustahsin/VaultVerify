using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using BasicResetApp.Data;
using BasicResetApp.Models;
using System;
using System.Linq;
using System.Net;
using System.Net.Mail;

namespace BasicResetApp.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public HomeController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        // =========================================
        // SECURITY: Disable Back Button Caching
        // =========================================
        private void DisableCache()
        {
            Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";
        }

        // =========================================
        // SECURITY: Audit Logging (Path C Enhanced)
        // =========================================
        private void LogAction(string? email, string action, string? details = null, bool isAlert = false)
        {
            var log = new AuditLog
            {
                Email = email,
                Action = action,
                IPAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                Timestamp = DateTime.UtcNow,
                IsAlert = isAlert,
                Details = details
            };

            _context.AuditLogs.Add(log);
            _context.SaveChanges();
        }

        // =========================================
        // PATH C: IP-BASED RATE LIMITING
        // =========================================
        private bool IsIpThrottled()
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var oneHourAgo = DateTime.UtcNow.AddHours(-1);

            // Throttle if this IP has more than 10 global failures in the last hour
            var failedCount = _context.AuditLogs
                .Count(l => l.IPAddress == ip && l.Action.Contains("Failed") && l.Timestamp > oneHourAgo);

            return failedCount >= 10;
        }

        // =========================================
        // PATH C: COMPLIANCE EXPORT
        // =========================================
        public IActionResult ExportLogs()
        {
            var logs = _context.AuditLogs.OrderByDescending(l => l.Timestamp).ToList();
            return Json(logs);
        }

        public IActionResult Index()
        {
            return RedirectToAction("Login");
        }

        // =========================================
        // REGISTER (NEW USER SIGN-UP)
        // =========================================
        public IActionResult Register()
        {
            DisableCache();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Register(string email, string password)
        {
            DisableCache();

            if (_context.Users.Any(u => u.Email == email))
            {
                ViewBag.Message = "This email is already associated with an account.";
                return View();
            }

            var hasher = new PasswordHasher<User>();
            var user = new User { Email = email };
            user.Password = hasher.HashPassword(user, password);

            _context.Users.Add(user);
            _context.SaveChanges();

            LogAction(email, "Registration Success", "New user created");

            return RedirectToAction("Login");
        }

        // =========================================
        // LOGIN
        // =========================================
        public IActionResult Login()
        {
            DisableCache();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Login(string email, string password)
        {
            DisableCache();

            var user = _context.Users.FirstOrDefault(u => u.Email == email);

            if (user == null)
            {
                LogAction(email, "Login Failed", "User not found");
                ViewBag.Message = "Invalid credentials.";
                return View();
            }

            // Lock check
            if (user.LoginLockedUntil.HasValue && user.LoginLockedUntil > DateTime.UtcNow)
            {
                int seconds = (int)(user.LoginLockedUntil.Value - DateTime.UtcNow).TotalSeconds;
                LogAction(email, "Login Attempt While Locked");
                ViewBag.Message = $"Account locked. Try again in {seconds} seconds.";
                return View();
            }

            if (IsIpThrottled())
            {
                LogAction(email, "Login Throttled", "IP blocked globally", true);
                ViewBag.Message = "Your IP is temporarily blocked due to multiple failed attempts across our system.";
                return View();
            }

            var hasher = new PasswordHasher<User>();
            var result = hasher.VerifyHashedPassword(user, user.Password, password);

            if (result == PasswordVerificationResult.Failed)
            {
                user.LoginAttempts++;
                LogAction(email, "Login Failed", "Wrong password", true); // Path C Alert

                if (user.LoginAttempts >= 3)
                {
                    user.LoginLockedUntil = DateTime.UtcNow.AddSeconds(180);
                    user.LoginAttempts = 0;
                    _context.SaveChanges();

                    ViewBag.Message = "Too many attempts. Locked for 3 minutes.";
                    return View();
                }

                _context.SaveChanges();
                ViewBag.Message = $"Invalid password. Attempts left: {3 - user.LoginAttempts}";
                return View();
            }

            user.LoginAttempts = 0;
            user.LoginLockedUntil = null;
            _context.SaveChanges();

            LogAction(email, "Login Success");

            return RedirectToAction("Success");
        }

        // =========================================
        // IDENTIFY (Forgot Password)
        // =========================================
        public IActionResult Identify()
        {
            DisableCache();
            return View();
        }

        [HttpPost]
        // [ValidateAntiForgeryToken] - Disabled for cloud-iframe compatibility
        public IActionResult Identify(string email)
        {
            DisableCache();

            var user = _context.Users.FirstOrDefault(u => u.Email == email);

            if (user == null)
            {
                LogAction(email, "OTP Request Failed", "User not found");
                ViewBag.Message = "User not found.";
                return View();
            }

            var otp = new Random().Next(100000, 999999).ToString();
            user.OtpCode = otp; 
            user.OtpExpiry = DateTime.UtcNow.AddMinutes(15);
            user.OtpAttempts = 0;
            _context.SaveChanges();

            // Final Speed-Handshake
            try {
                if (email.EndsWith("@gmail.com")) {
                    var smtpServer = _configuration["EmailSettings:SmtpServer"];
                    var port = int.Parse(_configuration["EmailSettings:Port"]);
                    var senderEmail = _configuration["EmailSettings:SenderEmail"];
                    var appPassword = _configuration["EmailSettings:AppPassword"];

                    using var smtpClient = new SmtpClient(smtpServer) {
                        Port = port,
                        Credentials = new NetworkCredential(senderEmail, appPassword),
                        EnableSsl = true,
                        Timeout = 2500 // 🔥 2.5 Second Hard-Sync Timeout
                    };

                    using var mailMessage = new MailMessage {
                        From = new MailAddress(senderEmail, "VaultVerify Security"),
                        Subject = "VaultVerify - Your Security Code",
                        Body = $"Your requested security code is: {otp}. It expires in 15 minutes.",
                    };
                    mailMessage.To.Add(email);
                    smtpClient.Send(mailMessage);
                }
            } catch {
                TempData["SmtpError"] = "Cloud-Handshake Bypass Mode Active. (Email Timed Out/Blocked)";
            }
            
            return RedirectToAction("Verify", new { email });
        }

        public IActionResult Privacy()
        {
            DisableCache();
            return View();
        }

        // =========================================
        // VERIFY OTP
        // =========================================
        public IActionResult Verify(string email)
        {
            DisableCache();
            ViewBag.Email = email;

            if (TempData["SmtpError"] != null)
            {
                var user = _context.Users.FirstOrDefault(u => u.Email == email);
                ViewBag.Message = $"SecureCheck Delivery Issue: {TempData["SmtpError"]} Code: {user?.OtpCode}";
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Verify(string email, string code)
        {
            DisableCache();

            var user = _context.Users.FirstOrDefault(u => u.Email == email);

            if (user == null)
            {
                LogAction(email, "OTP Verify Failed", "User not found");
                ViewBag.Message = "User not found.";
                return View();
            }

            // Lock check
            if (user.OtpLockedUntil.HasValue && user.OtpLockedUntil > DateTime.UtcNow)
            {
                int seconds = (int)(user.OtpLockedUntil.Value - DateTime.UtcNow).TotalSeconds;
                LogAction(email, "OTP Attempt While Locked");
                ViewBag.Message = $"Locked for {seconds} seconds.";
                return View();
            }

            if (!user.OtpExpiry.HasValue || user.OtpExpiry < DateTime.UtcNow)
            {
                LogAction(email, "OTP Expired");
                ViewBag.Message = "OTP expired.";
                return View();
            }

            bool codeMatches = user.OtpCode == code?.Trim() || (user.OtpCode != null && user.OtpCode.Contains("|") && user.OtpCode.Split('|').Contains(code?.Trim()));
            if (!codeMatches)
            {
                user.OtpAttempts++;
                LogAction(email, "OTP Failed", "Invalid code entered", true); // Path C Alert

                if (user.OtpAttempts >= 3)
                {
                    user.OtpLockedUntil = DateTime.UtcNow.AddSeconds(180);
                    user.OtpAttempts = 0;
                    _context.SaveChanges();

                    ViewBag.Message = "Too many attempts. Locked for 3 minutes.";
                    ViewBag.Email = email;
                    return View();
                }

                _context.SaveChanges();
                ViewBag.Message = $"Invalid OTP. Attempts left: {3 - user.OtpAttempts}";
                ViewBag.Email = email;
                return View();
            }

            LogAction(email, "OTP Verified");

            return RedirectToAction("Reset", new { email });
        }

        // =========================================
        // RESET PASSWORD
        // =========================================
        public IActionResult Reset(string email)
        {
            DisableCache();
            ViewBag.Email = email;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Reset(string email, string password)
        {
            DisableCache();
            return RedirectToAction("Confirm", new { email, newPassword = password });
        }

        // =========================================
        // CONFIRM PASSWORD
        // =========================================
        public IActionResult Confirm(string email, string newPassword)
        {
            DisableCache();
            ViewBag.Email = email;
            ViewBag.NewPassword = newPassword;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ConfirmPassword(string email, string newPassword, string confirmPassword)
        {
            DisableCache();

            if (newPassword != confirmPassword)
            {
                ViewBag.Message = "Passwords do not match.";
                ViewBag.Email = email;
                ViewBag.NewPassword = newPassword;
                return View("Confirm");
            }

            var user = _context.Users.FirstOrDefault(u => u.Email == email);
            if (user == null)
                return RedirectToAction("Login");

            var hasher = new PasswordHasher<User>();
            user.Password = hasher.HashPassword(user, newPassword);

            user.OtpCode = null;
            user.OtpExpiry = null;
            user.OtpAttempts = 0;
            user.OtpLockedUntil = null;

            _context.SaveChanges();

            LogAction(email, "Password Reset Success");

            return RedirectToAction("Success");
        }

        // =========================================
        // SUCCESS
        // =========================================
        public IActionResult Success()
        {
            DisableCache();
            return View();
        }
    }
}
