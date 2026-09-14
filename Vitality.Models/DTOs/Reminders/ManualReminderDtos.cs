namespace Vitality.Models.DTOs.Reminders;

public sealed class ManualReminderResultDto
{
    public bool EmailSent { get; set; }
    public string Message { get; set; } = string.Empty;
}
