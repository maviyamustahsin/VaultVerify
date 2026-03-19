using System;

namespace BasicResetApp.Models
{
    public class AuditLog
    {
        public int Id { get; set; }

        public string? Email { get; set; }

        public string Action { get; set; } = string.Empty;

        public string? IPAddress { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public bool IsAlert { get; set; } = false;

        public string? Details { get; set; }
    }
}
