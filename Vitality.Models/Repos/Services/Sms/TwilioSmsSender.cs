using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace Vitality.Services.Sms;

public sealed class TwilioSmsSender : ISmsSender
{
    private const string LegacyBrandName = "TelehealthUS";
    private const string PreviousBrandName = "ImpactHealthUSA";
    private const string NewBrandName = "TelehealthUS";
    private static readonly Regex LegacyBrandRegex = new(@"\bImpact Health\b(?!\s*USA\b)", RegexOptions.CultureInvariant);

    private readonly TwilioSettings _settings;
    private readonly ITwilioClientWrapper _clientWrapper;
    private readonly ILogger<TwilioSmsSender> _logger;

    public TwilioSmsSender(
        IOptions<TwilioSettings> settings,
        ITwilioClientWrapper clientWrapper,
        ILogger<TwilioSmsSender> logger)
    {
        _settings = settings.Value ?? throw new ArgumentNullException(nameof(settings));
        _clientWrapper = clientWrapper ?? throw new ArgumentNullException(nameof(clientWrapper));
        _logger = logger;
    }

    public async Task SendAsync(string toPhoneNumber, string message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(toPhoneNumber))
        {
            throw new ArgumentException("Destination phone number is required.", nameof(toPhoneNumber));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Message body is required.", nameof(message));
        }

        if (string.IsNullOrWhiteSpace(_settings.FromNumber))
        {
            throw new InvalidOperationException("Twilio 'From' number is not configured.");
        }

        var sanitizedMessage = message.Trim()
            .Replace(PreviousBrandName, NewBrandName, StringComparison.Ordinal);
        sanitizedMessage = LegacyBrandRegex.Replace(sanitizedMessage, NewBrandName);
        sanitizedMessage = sanitizedMessage.Replace($"{NewBrandName} USA", NewBrandName, StringComparison.Ordinal);
        if (sanitizedMessage.Length > 1600)
        {
            throw new ArgumentException("SMS body cannot exceed 1600 characters.", nameof(message));
        }

        var options = new CreateMessageOptions(new PhoneNumber(toPhoneNumber))
        {
            From = new PhoneNumber(_settings.FromNumber),
            Body = sanitizedMessage
        };

        try
        {
            var response = await _clientWrapper.SendMessageAsync(options, cancellationToken).ConfigureAwait(false);

            if (response.ErrorCode.HasValue)
            {
                var errorMessage = $"Twilio error {response.ErrorCode}: {response.ErrorMessage}";
                _logger.LogError("Failed to send SMS to {PhoneNumber}. {ErrorMessage}", toPhoneNumber, errorMessage);
                throw new InvalidOperationException(errorMessage);
            }

            _logger.LogInformation("SMS sent to {PhoneNumber} with SID {Sid}", toPhoneNumber, response.Sid);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Unexpected error sending SMS to {PhoneNumber}", toPhoneNumber);
            throw;
        }
    }
}
