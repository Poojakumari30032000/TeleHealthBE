using Microsoft.Extensions.Logging;
using Vitality.Models.EntityClasses;

namespace Vitality.Services.Email;

public sealed class FailedEmailLoggingMailSender : IMailSender
{
    private readonly MailSender _inner;
    private readonly MainContext _db;
    private readonly ILogger<FailedEmailLoggingMailSender> _logger;

    public FailedEmailLoggingMailSender(
        MailSender inner,
        MainContext db,
        ILogger<FailedEmailLoggingMailSender> logger)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task SendAsync(MailTemplateModel message, CancellationToken cancellationToken = default)
    {
        try
        {
            await _inner.SendAsync(message, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Recipient}", message.ToEmail);

            try
            {
                var log = new SYS_FailedEmailLog
                {
                    ToEmail = message.ToEmail?.Trim() ?? "",
                    ToName = string.IsNullOrWhiteSpace(message.ToName) ? null : message.ToName.Trim(),
                    Subject = message.Subject?.Trim() ?? "",
                    ErrorMessage = ex.Message,
                    ExceptionType = ex.GetType().FullName,
                    FailedAtUtc = DateTime.UtcNow
                };
                _db.SYS_FailedEmailLogs.Add(log);
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception logEx)
            {
                _logger.LogError(logEx, "Failed to write failed email log to database for {Recipient}", message.ToEmail);
            }

            throw;
        }
    }
}
