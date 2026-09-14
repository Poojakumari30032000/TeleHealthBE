using System.Threading.Tasks;

namespace Vitality.Models.Repos.Interfaces
{
    public interface ISquareOAuthService
    {

        Task<string> GetAuthorizationUrlAsync(long facilityId, string redirectUri);

        Task<bool> ExchangeCodeForTokenAsync(string code, string redirectUri, string state);

        Task<bool> RefreshAccessTokenAsync(long facilityId);

        Task<string?> GetValidAccessTokenAsync(long facilityId);

        Task<bool> IsConnectedAsync(long facilityId);

        Task DisconnectAsync(long facilityId);

        string CreateGoToken(long facilityId);

        bool TryConsumeGoToken(string? tok, out long facilityId);
    }
}
