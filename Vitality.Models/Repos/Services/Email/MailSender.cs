using System.Linq;
using System.Text.RegularExpressions;
using System.Text;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Vitality.Services.Email;

public sealed class MailSender : IMailSender
{
    private const string LegacyBrandName = "TelehealthUS";
    private const string PreviousBrandName = "ImpactHealthUSA";
    private const string NewBrandName = "TelehealthUS";
    private static readonly Regex LegacyBrandRegex = new(@"\bImpact Health\b(?!\s*USA\b)", RegexOptions.CultureInvariant);

    private readonly SmtpSettings _smtp;
    private readonly ILogger<MailSender> _logger;

    public MailSender(IOptions<SmtpSettings> smtpOptions, ILogger<MailSender> logger)
    {
        _smtp = smtpOptions.Value ?? throw new ArgumentNullException(nameof(smtpOptions));
        _logger = logger;
    }

    public async Task SendAsync(MailTemplateModel message, CancellationToken cancellationToken = default)
    {
        if (message is null)
        {
            throw new ArgumentNullException(nameof(message));
        }

        if (string.IsNullOrWhiteSpace(message.ToEmail))
        {
            throw new ArgumentException("Recipient email is required.", nameof(message));
        }

        if (string.IsNullOrWhiteSpace(message.Subject))
        {
            throw new ArgumentException("Subject is required.", nameof(message));
        }

        var normalizedMessage = NormalizeBranding(message);
        var mimeMessage = BuildMimeMessage(normalizedMessage);

        using var client = new SmtpClient();
        try
        {
            var secureSocketOptions = ResolveSocketOptions();
            await client.ConnectAsync(_smtp.Host, _smtp.Port, secureSocketOptions, cancellationToken).ConfigureAwait(false);

            var userName = string.IsNullOrWhiteSpace(_smtp.UserName) ? _smtp.FromEmail : _smtp.UserName;

            if (!string.IsNullOrWhiteSpace(userName))
            {
                client.AuthenticationMechanisms.Remove("XOAUTH2");
                await client.AuthenticateAsync(userName, _smtp.Password, cancellationToken).ConfigureAwait(false);
            }

            await client.SendAsync(mimeMessage, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Recipient}", normalizedMessage.ToEmail);
            throw;
        }
        finally
        {
            if (client.IsConnected)
            {
                await client.DisconnectAsync(true, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private MimeMessage BuildMimeMessage(MailTemplateModel message)
    {
        var mimeMessage = new MimeMessage();

        var fromName = string.IsNullOrWhiteSpace(_smtp.FromName) ? _smtp.FromEmail : _smtp.FromName;
        if (string.IsNullOrWhiteSpace(_smtp.FromEmail))
        {
            throw new InvalidOperationException("SMTP FromEmail is not configured.");
        }

        mimeMessage.From.Add(new MailboxAddress(fromName, _smtp.FromEmail));
        mimeMessage.To.Add(new MailboxAddress(message.ToName ?? string.Empty, message.ToEmail));
        mimeMessage.Subject = message.Subject;

        var builder = new BodyBuilder
        {
            HtmlBody = BuildHtmlBody(message),
            TextBody = BuildTextBody(message)
        };

        mimeMessage.Body = builder.ToMessageBody();
        return mimeMessage;
    }

    private string BuildHtmlBody(MailTemplateModel message)
    {
        var paragraphs = string.Join(
            string.Empty,
            message.BodyParagraphs.Select(p => $"<p style='margin:0 0 16px 0;color:#334155;font-size:16px;line-height:1.6;'>{p}</p>"));

        var highlight = string.IsNullOrWhiteSpace(message.HighlightText)
            ? string.Empty
            : $"<div style='background:#F4F7FF;border-radius:10px;padding:18px 20px;margin:0 0 24px 0;color:#1E293B;font-size:16px;line-height:1.6;'>{message.HighlightText}</div>";

        var button = string.IsNullOrWhiteSpace(message.ButtonText) || string.IsNullOrWhiteSpace(message.ButtonUrl)
            ? string.Empty
            : $"<div style='text-align:center;margin:32px 0;'><a href='{message.ButtonUrl}' style='background:#2563EB;color:#ffffff;padding:14px 28px;border-radius:999px;font-weight:600;text-decoration:none;display:inline-block;'>"
              + $"{message.ButtonText}</a></div>";

        var footer = string.IsNullOrWhiteSpace(message.FooterNote)
            ? "You're receiving this email because you have an account with TelehealthUS."
            : message.FooterNote;

        var preview = string.IsNullOrWhiteSpace(message.PreviewText) ? "&nbsp;" : message.PreviewText;

        return $@"
<!DOCTYPE html>
<html lang='en'>
  <head>
    <meta charset='utf-8' />
    <meta name='viewport' content='width=device-width, initial-scale=1' />
    <title>{message.Subject}</title>
  </head>
  <body style='margin:0;padding:0;background-color:#EEF2FF;font-family:""Segoe UI"",Roboto,Helvetica,Arial,sans-serif;color:#1F2937;'>
    <div style='display:none;max-height:0;overflow:hidden;color:transparent;opacity:0;'>{preview}</div>
    <table role='presentation' cellpadding='0' cellspacing='0' width='100%'>
      <tr>
        <td style='padding:32px 16px;'>
          <table role='presentation' cellpadding='0' cellspacing='0' width='100%' style='max-width:600px;margin:0 auto;background:#ffffff;border-radius:18px;box-shadow:0 20px 45px rgba(15,23,42,0.12);overflow:hidden;'>
            <tr>
              <td style='padding:36px 32px 24px;background:linear-gradient(135deg,#1D4ED8,#3B82F6);color:#ffffff;'>
                <div style='font-size:22px;font-weight:600;'>{_smtp.FromName}</div>
                <div style='margin-top:6px;font-size:14px;opacity:0.85;'>Healthcare that keeps up with you.</div>
              </td>
            </tr>
            <tr>
              <td style='padding:36px 32px 32px;'>
                <p style='margin:0 0 24px 0;color:#0F172A;font-size:20px;font-weight:600;'>{message.Greeting}</p>
                {paragraphs}
                {highlight}
                {button}
                <p style='margin:32px 0 0 0;color:#475569;font-size:15px;line-height:1.6;'>{footer}</p>
              </td>
            </tr>
            <tr>
              <td style='padding:20px 16px;background:#F8FAFC;color:#64748B;font-size:13px;text-align:center;'>
                &copy; {DateTime.UtcNow.Year} {_smtp.FromName}. All rights reserved.
              </td>
            </tr>
          </table>
        </td>
      </tr>
    </table>
  </body>
</html>";
    }

    private static MailTemplateModel NormalizeBranding(MailTemplateModel message)
    {
        return new MailTemplateModel
        {
            ToEmail = message.ToEmail,
            ToName = message.ToName,
            Subject = ReplaceBranding(message.Subject) ?? string.Empty,
            Greeting = ReplaceBranding(message.Greeting) ?? "Hi there,",
            PreviewText = ReplaceBranding(message.PreviewText),
            BodyParagraphs = message.BodyParagraphs?.Select(p => ReplaceBranding(p) ?? string.Empty).ToList() ?? new List<string>(),
            HighlightText = ReplaceBranding(message.HighlightText),
            ButtonText = ReplaceBranding(message.ButtonText),
            ButtonUrl = message.ButtonUrl,
            FooterNote = ReplaceBranding(message.FooterNote)
        };
    }

    private static string? ReplaceBranding(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        var normalized = text.Replace(PreviousBrandName, NewBrandName, StringComparison.Ordinal);
        normalized = LegacyBrandRegex.Replace(normalized, NewBrandName);
        return normalized.Replace($"{NewBrandName} USA", NewBrandName, StringComparison.Ordinal);
    }

    private static string BuildTextBody(MailTemplateModel message)
    {
        var builder = new StringBuilder();
        builder.AppendLine(message.Greeting);
        builder.AppendLine();

        foreach (var paragraph in message.BodyParagraphs)
        {
            builder.AppendLine(StripTags(paragraph));
            builder.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(message.HighlightText))
        {
            builder.AppendLine(StripTags(message.HighlightText));
            builder.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(message.ButtonText) && !string.IsNullOrWhiteSpace(message.ButtonUrl))
        {
            builder.AppendLine($"{message.ButtonText}: {message.ButtonUrl}");
            builder.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(message.FooterNote))
        {
            builder.AppendLine(StripTags(message.FooterNote));
        }

        return builder.ToString().TrimEnd();
    }

    private SecureSocketOptions ResolveSocketOptions()
    {
        if (_smtp.UseStartTls)
        {
            return SecureSocketOptions.StartTls;
        }

        if (_smtp.UseSsl)
        {
            return SecureSocketOptions.SslOnConnect;
        }

        return SecureSocketOptions.Auto;
    }

    private static string StripTags(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        Span<char> buffer = stackalloc char[value.Length];
        var index = 0;
        var inside = false;

        foreach (var c in value)
        {
            if (c == '<')
            {
                inside = true;
                continue;
            }

            if (c == '>')
            {
                inside = false;
                continue;
            }

            if (!inside)
            {
                buffer[index++] = c;
            }
        }

        return new string(buffer[..index]).Trim();
    }
}
