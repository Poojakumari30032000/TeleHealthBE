using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Vitality.Models.EntityClasses;
using Vitality.Models.Repos;
using Vitality.Models.Repos.Interfaces;

namespace Vitality.Models.Repos.Services
{
    public class SquareOAuthService : BaseRepo, ISquareOAuthService
    {
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _cache;
        private readonly IFacilitySquareClientProvider _clientProvider;
        private readonly ILogger<SquareOAuthService> _logger;

        private const string CacheKeyPrefix = "sq:oauth:state:";
        private const string GoTokenPrefix = "sq:go:";
        private static readonly TimeSpan StateExpiry = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan GoTokenExpiry = TimeSpan.FromMinutes(2);

        private const string Scopes = "MERCHANT_PROFILE_READ PAYMENTS_READ PAYMENTS_WRITE CUSTOMERS_READ CUSTOMERS_WRITE";

        private const string ConnectSandbox = "https://connect.squareupsandbox.com";
        private const string ConnectProduction = "https://connect.squareup.com";

        public SquareOAuthService(
            IConfiguration config,
            IHttpClientFactory httpClientFactory,
            IMemoryCache cache,
            IFacilitySquareClientProvider clientProvider,
            ILogger<SquareOAuthService> logger)
        {
            _config = config;
            _httpClientFactory = httpClientFactory;
            _cache = cache;
            _clientProvider = clientProvider;
            _logger = logger;
        }

        private HttpClient Http() => _httpClientFactory.CreateClient();

        private string GetClientId() =>
            _config["Square:OAuth:ClientId"] ?? _config["Square:ApplicationId"]
            ?? throw new InvalidOperationException("Square OAuth ClientId (or Square:ApplicationId) is not configured.");

        private string GetClientSecret() =>
            _config["Square:OAuth:ClientSecret"]
            ?? throw new InvalidOperationException("Square:OAuth:ClientSecret is not configured.");

        private bool IsSandbox() =>
            string.Equals(_config["Square:Environment"] ?? "", "sandbox", StringComparison.OrdinalIgnoreCase);

        private string GetConnectBaseUrl() =>
            IsSandbox() ? ConnectSandbox : ConnectProduction;

        public async Task<string> GetAuthorizationUrlAsync(long facilityId, string redirectUri)
        {
            var redirect = (redirectUri ?? "").Trim().TrimEnd('/');
            if (string.IsNullOrWhiteSpace(redirect))
                throw new InvalidOperationException("Square OAuth redirect_uri is required. Set Square:OAuth:RedirectUri and add the exact URL in Developer Dashboard OAuth page.");

            var clientId = GetClientId();
            var state = $"{facilityId}:{Guid.NewGuid():N}";
            _cache.Set(CacheKeyPrefix + state, facilityId, StateExpiry);

            var baseUrl = GetConnectBaseUrl();
            var path = "/oauth2/authorize";

            var scopeValue = string.Join(" ", Scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries));

            var query = new Dictionary<string, string>
            {
                { "client_id", clientId },
                { "scope", scopeValue },
                { "state", state },
                { "redirect_uri", redirect },
                { "locale", "en-US" }
            };
            if (!IsSandbox())
                query["session"] = "false";

            var qs = string.Join("&", query.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
            var url = $"{baseUrl}{path}?{qs}";

            _logger.LogInformation("Square OAuth initiate facility {FacilityId} sandbox={Sandbox} base={Base} redirect_uri={RedirectUri}.", facilityId, IsSandbox(), baseUrl, redirect);
            if (IsSandbox())
                _logger.LogInformation("Square Sandbox: Open the Sandbox Dashboard (squareupsandbox.com) in another tab first, then use the auth URL. See https://developer.squareup.com/docs/oauth-api/walkthrough.");
            return await Task.FromResult(url);
        }

        public async Task<bool> ExchangeCodeForTokenAsync(string code, string redirectUri, string state)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                _logger.LogWarning("Square OAuth callback: missing code.");
                return false;
            }

            long facilityId;
            if (string.IsNullOrWhiteSpace(state) || !_cache.TryGetValue(CacheKeyPrefix + state, out var cached) || cached == null)
            {
                _logger.LogWarning("Square OAuth callback: invalid or expired state.");
                return false;
            }

            facilityId = (long)cached;
            _cache.Remove(CacheKeyPrefix + state);

            var clientId = GetClientId();
            var clientSecret = GetClientSecret();
            var baseUrl = GetConnectBaseUrl();
            var tokenUrl = $"{baseUrl}/oauth2/token";

            var form = new Dictionary<string, string>
            {
                { "grant_type", "authorization_code" },
                { "code", code },
                { "redirect_uri", redirectUri },
                { "client_id", clientId },
                { "client_secret", clientSecret }
            };
            using var content = new FormUrlEncodedContent(form);
            using var http = Http();
            using var response = await http.PostAsync(tokenUrl, content);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Square OAuth token exchange failed. Status {StatusCode}, Body: {Body}", response.StatusCode, body);
                return false;
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var accessToken = root.TryGetProperty("access_token", out var at) ? at.GetString() : null;
            var refreshToken = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;
            var merchantId = root.TryGetProperty("merchant_id", out var mid) ? mid.GetString() : null;
            DateTime? expiresAt = null;
            if (root.TryGetProperty("expires_at", out var ea))
            {
                if (ea.ValueKind == JsonValueKind.String && DateTime.TryParse(ea.GetString(), out var dt))
                    expiresAt = dt;
            }

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                _logger.LogError("Square OAuth token response missing access_token.");
                return false;
            }

            string? locationId = null;
            try
            {
                locationId = await FetchFirstLocationIdAsync(accessToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch Square locations for merchant. LocationId will be null.");
            }

            var applicationId = GetClientId();

            var existing = await _db.Sys_FacilitySquareCreds
                .Where(c => c.FacilityId == facilityId && c.IsActive == true)
                .ToListAsync();
            foreach (var e in existing)
                e.IsActive = false;
            await _db.SaveChangesAsync();

            var cred = new Sys_FacilitySquareCred
            {
                FacilityId = facilityId,
                ApplicationId = applicationId,
                LocationId = locationId ?? "",
                AccessToken = accessToken,
                RefreshToken = refreshToken ?? "",
                TokenExpiresAt = expiresAt,
                MerchantId = merchantId,
                IsActive = true
            };
            _db.Sys_FacilitySquareCreds.Add(cred);
            await _db.SaveChangesAsync();

            _clientProvider.Invalidate(facilityId);
            _logger.LogInformation("Square OAuth connected for facility {FacilityId}, LocationId {LocationId}.", facilityId, locationId);
            return true;
        }

        private async Task<string?> FetchFirstLocationIdAsync(string accessToken)
        {
            var baseUrl = GetConnectBaseUrl();
            var url = $"{baseUrl}/v2/locations";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var http = Http();
            using var res = await http.SendAsync(req);
            res.EnsureSuccessStatusCode();
            var json = await res.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("locations", out var locs) || locs.ValueKind != JsonValueKind.Array)
                return null;
            foreach (var loc in locs.EnumerateArray())
            {
                if (loc.TryGetProperty("id", out var id))
                    return id.GetString();
            }
            return null;
        }

        public async Task<bool> RefreshAccessTokenAsync(long facilityId)
        {
            var cred = await _db.Sys_FacilitySquareCreds
                .FirstOrDefaultAsync(c => c.FacilityId == facilityId && c.IsActive == true);
            if (cred == null || string.IsNullOrWhiteSpace(cred.RefreshToken))
            {
                _logger.LogWarning("No active Square OAuth cred or refresh token for facility {FacilityId}.", facilityId);
                return false;
            }

            var clientId = GetClientId();
            var clientSecret = GetClientSecret();
            var baseUrl = GetConnectBaseUrl();
            var tokenUrl = $"{baseUrl}/oauth2/token";

            var form = new Dictionary<string, string>
            {
                { "grant_type", "refresh_token" },
                { "refresh_token", cred.RefreshToken },
                { "client_id", clientId },
                { "client_secret", clientSecret }
            };
            using var content = new FormUrlEncodedContent(form);
            using var http = Http();
            using var response = await http.PostAsync(tokenUrl, content);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Square OAuth refresh failed for facility {FacilityId}. Status {StatusCode}, Body: {Body}",
                    facilityId, response.StatusCode, body);
                cred.IsActive = false;
                await _db.SaveChangesAsync();
                _clientProvider.Invalidate(facilityId);
                return false;
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var accessToken = root.TryGetProperty("access_token", out var at) ? at.GetString() : null;
            var refreshToken = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;
            DateTime? expiresAt = null;
            if (root.TryGetProperty("expires_at", out var ea) && ea.ValueKind == JsonValueKind.String && DateTime.TryParse(ea.GetString(), out var dt))
                expiresAt = dt;

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                _logger.LogError("Square OAuth refresh response missing access_token.");
                return false;
            }

            cred.AccessToken = accessToken;
            if (!string.IsNullOrWhiteSpace(refreshToken))
                cred.RefreshToken = refreshToken;
            if (expiresAt.HasValue)
                cred.TokenExpiresAt = expiresAt;
            await _db.SaveChangesAsync();
            _clientProvider.Invalidate(facilityId);
            _logger.LogInformation("Square OAuth token refreshed for facility {FacilityId}.", facilityId);
            return true;
        }

        public async Task<string?> GetValidAccessTokenAsync(long facilityId)
        {
            var cred = await _db.Sys_FacilitySquareCreds
                .Where(c => c.FacilityId == facilityId && c.IsActive == true)
                .OrderByDescending(c => c.FacilitySquareCredId)
                .FirstOrDefaultAsync();
            if (cred == null || string.IsNullOrWhiteSpace(cred.AccessToken))
                return null;

            var expiresAt = cred.TokenExpiresAt;
            if (expiresAt.HasValue && expiresAt.Value <= DateTime.UtcNow.AddMinutes(5))
            {
                var ok = await RefreshAccessTokenAsync(facilityId);
                if (!ok) return null;
                cred = await _db.Sys_FacilitySquareCreds
                    .Where(c => c.FacilityId == facilityId && c.IsActive == true)
                    .OrderByDescending(c => c.FacilitySquareCredId)
                    .FirstOrDefaultAsync();
            }

            return cred?.AccessToken;
        }

        public async Task<bool> IsConnectedAsync(long facilityId)
        {
            var cred = await _db.Sys_FacilitySquareCreds
                .Where(c => c.FacilityId == facilityId && c.IsActive == true)
                .AnyAsync();
            return cred;
        }

        public async Task DisconnectAsync(long facilityId)
        {
            var list = await _db.Sys_FacilitySquareCreds
                .Where(c => c.FacilityId == facilityId && c.IsActive == true)
                .ToListAsync();
            foreach (var c in list)
                c.IsActive = false;
            await _db.SaveChangesAsync();
            _clientProvider.Invalidate(facilityId);
            _logger.LogInformation("Square OAuth disconnected for facility {FacilityId}.", facilityId);
        }

        public string CreateGoToken(long facilityId)
        {
            var tok = Guid.NewGuid().ToString("N");
            _cache.Set(GoTokenPrefix + tok, facilityId, GoTokenExpiry);
            return tok;
        }

        public bool TryConsumeGoToken(string? tok, out long facilityId)
        {
            facilityId = 0;
            if (string.IsNullOrWhiteSpace(tok)) return false;
            if (!_cache.TryGetValue(GoTokenPrefix + tok, out var v) || v == null) return false;
            _cache.Remove(GoTokenPrefix + tok);
            facilityId = (long)v;
            return true;
        }
    }
}
