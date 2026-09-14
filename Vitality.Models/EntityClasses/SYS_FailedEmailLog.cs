namespace Vitality.Models.EntityClasses;

public partial class SYS_FailedEmailLog
{
    public long FailedEmailLogId { get; set; }
    public string ToEmail { get; set; } = null!;
    public string? ToName { get; set; }
    public string Subject { get; set; } = null!;
    public string ErrorMessage { get; set; } = null!;
    public string? ExceptionType { get; set; }
    public DateTime FailedAtUtc { get; set; }
}
