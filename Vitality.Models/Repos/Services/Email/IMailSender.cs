namespace Vitality.Services.Email;

public interface IMailSender
{
    Task SendAsync(MailTemplateModel message, CancellationToken cancellationToken = default);
}
