using Twilio.Clients;
using Twilio.Rest.Api.V2010.Account;

namespace Vitality.Services.Sms;

public sealed class TwilioClientWrapper : ITwilioClientWrapper
{
    private readonly ITwilioRestClient _client;

    public TwilioClientWrapper(ITwilioRestClient client)
    {
        _client = client;
    }

    public async Task<MessageResource> SendMessageAsync(CreateMessageOptions options, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await MessageResource.CreateAsync(options, _client).ConfigureAwait(false);
    }
}
