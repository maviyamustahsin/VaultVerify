using System;
using System.ComponentModel.DataAnnotations;

namespace BasicResetApp.Models
{
    public class User
    {
        public int Id { get; set; }

        // =========================
        // BASIC CREDENTIALS
        // =========================
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;

        // =========================
        // OTP SECURITY SYSTEM
        // =========================
        public string? OtpCode { get; set; }

        public DateTime? OtpExpiry { get; set; }

        public int OtpAttempts { get; set; } = 0;

        public DateTime? OtpLockedUntil { get; set; }

        // =========================
        // LOGIN SECURITY SYSTEM
        // =========================
        public int LoginAttempts { get; set; } = 0;

        public DateTime? LoginLockedUntil { get; set; }
    }
}
