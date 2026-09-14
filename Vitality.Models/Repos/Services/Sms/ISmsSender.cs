namespace Vitality.Services.Sms;

public interface ISmsSender
{
    Task SendAsync(string toPhoneNumber, string message, CancellationToken cancellationToken = default);
}
