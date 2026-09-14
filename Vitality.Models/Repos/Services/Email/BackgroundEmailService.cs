using Microsoft.Extensions.Logging;
using Vitality.Services.Email;

namespace Vitality.Models.Repos.Services.Email;

public class BackgroundEmailService
{
    private readonly IMailSender _mailSender;
    private readonly ILogger<BackgroundEmailService> _logger;

    public BackgroundEmailService(IMailSender mailSender, ILogger<BackgroundEmailService> logger)
    {
        _mailSender = mailSender;
        _logger = logger;
    }

    public void SendInBackground(MailTemplateModel emailModel, CancellationToken ct = default)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await _mailSender.SendAsync(emailModel, ct);
                _logger.LogInformation("Background email sent successfully to {Email}", emailModel.ToEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending background email to {Email}", emailModel.ToEmail);
            }
        }, ct);
    }

    public void SendBatchInBackground(IEnumerable<MailTemplateModel> emailModels, CancellationToken ct = default)
    {
        _ = Task.Run(async () =>
        {
            var tasks = emailModels.Select(async emailModel =>
            {
                try
                {
                    await _mailSender.SendAsync(emailModel, ct);
                    _logger.LogInformation("Background batch email sent successfully to {Email}", emailModel.ToEmail);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending background batch email to {Email}", emailModel.ToEmail);
                }
            });

            await Task.WhenAll(tasks);
        }, ct);
    }
}
