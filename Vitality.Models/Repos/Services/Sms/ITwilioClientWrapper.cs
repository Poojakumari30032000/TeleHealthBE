using Twilio.Rest.Api.V2010.Account;

namespace Vitality.Services.Sms;

public interface ITwilioClientWrapper
{
    Task<MessageResource> SendMessageAsync(CreateMessageOptions options, CancellationToken cancellationToken = default);
}
