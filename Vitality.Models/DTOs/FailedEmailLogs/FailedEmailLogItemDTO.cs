using System;

namespace Vitality.Models.DTOs.FailedEmailLogs;

public class FailedEmailLogItemDTO
{
    public long FailedEmailLogId { get; set; }
    public string ToEmail { get; set; } = string.Empty;
    public string? ToName { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public string? ExceptionType { get; set; }
    public DateTime FailedAtUtc { get; set; }
}
