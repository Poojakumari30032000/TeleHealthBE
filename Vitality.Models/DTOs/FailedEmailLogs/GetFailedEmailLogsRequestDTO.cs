using System;

namespace Vitality.Models.DTOs.FailedEmailLogs;

public class GetFailedEmailLogsRequestDTO
{
    public DateTime? StartDateUtc { get; set; }
    public DateTime? EndDateUtc { get; set; }
    public string? ToEmail { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}
