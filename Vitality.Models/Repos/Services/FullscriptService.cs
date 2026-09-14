using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Vitality.Models.EntityClasses;
using Vitality.Models.Repos;
using Vitality.Models.Repos.Interfaces;

namespace Vitality.Models.Repos.Services
{
    public class FullscriptService : BaseRepo, IFullscriptService
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _baseUrl;
        private readonly string _apiBaseUrl;

        public FullscriptService(IConfiguration configuration, HttpClient httpClient)
        {
            _configuration = configuration;
            _httpClient = httpClient;
            _clientId = _configuration["Fullscript:ClientId"]
                ?? throw new InvalidOperationException("Fullscript:ClientId is not configured.");
            _clientSecret = _configuration["Fullscript:ClientSecret"]
                ?? throw new InvalidOperationException("Fullscript:ClientSecret is not configured.");

            var environment = _configuration["Fullscript:Environment"]?.ToLowerInvariant() ?? "sandbox";
            var region = _configuration["Fullscript:Region"]?.ToLowerInvariant() ?? "us";

            var envPrefix = environment == "sandbox" ? "-snd" : "";
            _baseUrl = $"https://{region}{envPrefix}.fullscript.io";
            _apiBaseUrl = $"https://api-{region}{envPrefix}.fullscript.io";
        }

        public async Task<string> GetAuthorizationUrlAsync(string redirectUri, string? state = null)
        {
            var authUrl = $"{_baseUrl}/oauth/authorize";

            var queryParams = new Dictionary<string, string>
            {
                { "client_id", _clientId },
                { "redirect_uri", redirectUri },
                { "response_type", "code" },
                { "scope", "clinic:read clinic:write patients:write patients:treatment_plan_history" }
            };

            if (!string.IsNullOrWhiteSpace(state))
            {
                queryParams["state"] = state;
            }

            var queryString = string.Join("&", queryParams.Select(kvp =>
                $"{WebUtility.UrlEncode(kvp.Key)}={WebUtility.UrlEncode(kvp.Value)}"));

            return $"{authUrl}?{queryString}";
        }

        public async Task<bool> ExchangeAuthCodeForTokenAsync(string authCode, string redirectUri, long? facilityId = null)
        {
            try
            {
                var tokenUrl = $"{_apiBaseUrl}/api/oauth/token";

                var requestBody = new Dictionary<string, string>
                {
                    { "grant_type", "authorization_code" },
                    { "code", authCode },
                    { "redirect_uri", redirectUri },
                    { "client_id", _clientId },
                    { "client_secret", _clientSecret }
                };

                var content = new FormUrlEncodedContent(requestBody);
                var response = await _httpClient.PostAsync(tokenUrl, content);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {

                    throw new Exception($"Failed to exchange auth code: {response.StatusCode} - {responseBody}. Request URL: {tokenUrl}, Redirect URI: {redirectUri}");
                }

                using var jsonDoc = JsonDocument.Parse(responseBody);
                var root = jsonDoc.RootElement;

                JsonElement oauthElement;
                if (root.TryGetProperty("oauth", out oauthElement))
                {

                    root = oauthElement;
                }

                if (!root.TryGetProperty("access_token", out var accessTokenElement) &&
                    !root.TryGetProperty("accessToken", out accessTokenElement))
                {
                    throw new Exception($"Invalid token response from Fullscript. Response: {responseBody}");
                }

                var accessToken = accessTokenElement.GetString();
                var refreshToken = root.TryGetProperty("refresh_token", out var refreshTokenElement)
                    ? refreshTokenElement.GetString()
                    : (root.TryGetProperty("refreshToken", out refreshTokenElement) ? refreshTokenElement.GetString() : null);

                var expiresIn = root.TryGetProperty("expires_in", out var expiresInElement)
                    ? expiresInElement.GetInt32()
                    : (root.TryGetProperty("expiresIn", out expiresInElement) ? expiresInElement.GetInt32() : 7200);

                if (string.IsNullOrWhiteSpace(accessToken))
                {
                    throw new Exception($"Invalid token response from Fullscript - access_token is missing. Response: {responseBody}");
                }

                var expiresAt = DateTime.UtcNow.AddSeconds(expiresIn);

                var existingToken = await _db.Sys_FullscriptOAuths
                    .FirstOrDefaultAsync(t => t.FacilityId == facilityId && t.IsActive == true);

                if (existingToken != null)
                {
                    existingToken.AccessToken = accessToken;
                    existingToken.RefreshToken = refreshToken;
                    existingToken.TokenExpiresAt = expiresAt;
                    existingToken.ModifiedDate = DateTime.UtcNow;
                }
                else
                {
                    var newToken = new Sys_FullscriptOAuth
                    {
                        FacilityId = facilityId,
                        AccessToken = accessToken,
                        RefreshToken = refreshToken,
                        TokenExpiresAt = expiresAt,
                        CreatedDate = DateTime.UtcNow,
                        ModifiedDate = DateTime.UtcNow,
                        IsActive = true
                    };
                    _db.Sys_FullscriptOAuths.Add(newToken);
                }

                await _db.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error exchanging auth code for token: {ex.Message}", ex);
            }
        }

        public async Task<bool> RefreshAccessTokenAsync(long? facilityId = null)
        {
            try
            {

                var token = await _db.Sys_FullscriptOAuths
                    .FirstOrDefaultAsync(t => t.FacilityId == facilityId && t.IsActive == true);

                if (token == null || string.IsNullOrWhiteSpace(token.RefreshToken))
                {
                    return false;
                }

                var tokenUrl = $"{_apiBaseUrl}/api/oauth/token";

                var requestBody = new Dictionary<string, string>
                {
                    { "grant_type", "refresh_token" },
                    { "refresh_token", token.RefreshToken },
                    { "client_id", _clientId },
                    { "client_secret", _clientSecret }
                };

                var content = new FormUrlEncodedContent(requestBody);
                var response = await _httpClient.PostAsync(tokenUrl, content);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {

                    token.IsActive = false;
                    await _db.SaveChangesAsync();
                    return false;
                }

                using var jsonDoc = JsonDocument.Parse(responseBody);
                var root = jsonDoc.RootElement;

                if (root.TryGetProperty("oauth", out var oauthElement))
                {
                    root = oauthElement;
                }

                if (!root.TryGetProperty("access_token", out var accessTokenElement) &&
                    !root.TryGetProperty("accessToken", out accessTokenElement))
                {
                    return false;
                }

                var newAccessToken = accessTokenElement.GetString();
                var newRefreshToken = root.TryGetProperty("refresh_token", out var refreshTokenElement)
                    ? refreshTokenElement.GetString()
                    : (root.TryGetProperty("refreshToken", out refreshTokenElement) ? refreshTokenElement.GetString() : null);

                var expiresIn = root.TryGetProperty("expires_in", out var expiresInElement)
                    ? expiresInElement.GetInt32()
                    : (root.TryGetProperty("expiresIn", out expiresInElement) ? expiresInElement.GetInt32() : 7200);

                if (string.IsNullOrWhiteSpace(newAccessToken))
                {
                    return false;
                }

                var expiresAt = DateTime.UtcNow.AddSeconds(expiresIn);

                token.AccessToken = newAccessToken;
                token.RefreshToken = newRefreshToken ?? token.RefreshToken;
                token.TokenExpiresAt = expiresAt;
                token.ModifiedDate = DateTime.UtcNow;

                await _db.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<string?> GetValidAccessTokenAsync(long? facilityId = null)
        {

            var token = await _db.Sys_FullscriptOAuths
                .FirstOrDefaultAsync(t => t.FacilityId == facilityId && t.IsActive == true);

            if (token == null || string.IsNullOrWhiteSpace(token.AccessToken))
            {
                return null;
            }

            if (token.TokenExpiresAt.HasValue && token.TokenExpiresAt.Value <= DateTime.UtcNow.AddMinutes(5))
            {

                var refreshed = await RefreshAccessTokenAsync(facilityId);
                if (!refreshed)
                {
                    return null;
                }

                token = await _db.Sys_FullscriptOAuths
                    .FirstOrDefaultAsync(t => t.FacilityId == facilityId && t.IsActive == true);
            }

            return token?.AccessToken;
        }

        public async Task<string> GenerateSessionGrantTokenAsync(long? facilityId = null)
        {
            var accessToken = await GetValidAccessTokenAsync(facilityId);

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                throw new UnauthorizedAccessException("Fullscript is not connected. Please connect your account first.");
            }

            var sessionGrantUrl = $"{_apiBaseUrl}/api/clinic/embeddable/session_grants";

            using var request = new HttpRequestMessage(HttpMethod.Post, sessionGrantUrl);
            request.Headers.Add("Authorization", $"Bearer {accessToken}");
            request.Headers.Add("Accept", "application/json");

            var response = await _httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Failed to generate session grant token: {response.StatusCode} - {responseBody}");
            }

            using var jsonDoc = JsonDocument.Parse(responseBody);
            var root = jsonDoc.RootElement;

            string? secretToken = null;
            if (root.TryGetProperty("secret_token", out var secretTokenElement))
            {
                secretToken = secretTokenElement.GetString();
            }
            else if (root.TryGetProperty("secretToken", out secretTokenElement))
            {
                secretToken = secretTokenElement.GetString();
            }
            else if (root.TryGetProperty("data", out var dataElement))
            {

                if (dataElement.TryGetProperty("secret_token", out secretTokenElement))
                {
                    secretToken = secretTokenElement.GetString();
                }
                else if (dataElement.TryGetProperty("secretToken", out secretTokenElement))
                {
                    secretToken = secretTokenElement.GetString();
                }
            }

            if (string.IsNullOrWhiteSpace(secretToken))
            {
                throw new Exception($"Invalid session grant response from Fullscript. Response: {responseBody}");
            }

            return secretToken;
        }

        public async Task<bool> IsConnectedAsync(long? facilityId = null)
        {
            var token = await GetValidAccessTokenAsync(facilityId);
            return !string.IsNullOrWhiteSpace(token);
        }

        public async Task RevokeTokenAsync(long? facilityId = null)
        {

            var token = await _db.Sys_FullscriptOAuths
                .FirstOrDefaultAsync(t => t.FacilityId == facilityId && t.IsActive == true);

            if (token != null && !string.IsNullOrWhiteSpace(token.AccessToken))
            {
                try
                {
                    var revokeUrl = $"{_apiBaseUrl}/api/oauth/revoke";
                    using var request = new HttpRequestMessage(HttpMethod.Post, revokeUrl);
                    request.Headers.Add("Authorization", $"Bearer {token.AccessToken}");

                    await _httpClient.SendAsync(request);
                }
                catch
                {

                }

                token.IsActive = false;
                token.ModifiedDate = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }
        }

        private class FullscriptTokenResponse
        {
            public string AccessToken { get; set; } = "";
            public string? RefreshToken { get; set; }
            public int ExpiresIn { get; set; }
            public string? TokenType { get; set; }
        }

        private class FullscriptSessionGrantResponse
        {
            public string SecretToken { get; set; } = "";
        }
    }
}
