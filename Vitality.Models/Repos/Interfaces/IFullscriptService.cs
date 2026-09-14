using System.Threading.Tasks;

namespace Vitality.Models.Repos.Interfaces
{
    public interface IFullscriptService
    {
        Task<string> GetAuthorizationUrlAsync(string redirectUri, string? state = null);
        Task<bool> ExchangeAuthCodeForTokenAsync(string authCode, string redirectUri, long? facilityId = null);
        Task<bool> RefreshAccessTokenAsync(long? facilityId = null);
        Task<string?> GetValidAccessTokenAsync(long? facilityId = null);
        Task<string> GenerateSessionGrantTokenAsync(long? facilityId = null);
        Task<bool> IsConnectedAsync(long? facilityId = null);
        Task RevokeTokenAsync(long? facilityId = null);
    }
}
