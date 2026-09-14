using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Vitality.Models.Repos.Zoom
{
    public sealed class ZoomTokenProvider : IZoomTokenProvider
    {
        private readonly IMemoryCache _cache;
        private readonly ZoomOptions _opts;
        private const string CacheKey = "zoom_access_token";

        public ZoomTokenProvider(IMemoryCache cache, IOptions<ZoomOptions> opts)
        { _cache = cache; _opts = opts.Value; }

        public async Task<string> GetAccessTokenAsync(CancellationToken ct = default)
        {
            if (!_opts.Enabled) return string.Empty;
            if (_cache.TryGetValue(CacheKey, out string token) && !string.IsNullOrWhiteSpace(token))
                return token;

            using var http = new HttpClient { BaseAddress = new Uri("https://zoom.us/") };
            var req = new HttpRequestMessage(HttpMethod.Post,
                $"oauth/token?grant_type=account_credentials&account_id={Uri.EscapeDataString(_opts.AccountId)}");

            var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_opts.ClientId}:{_opts.ClientSecret}"));
            req.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
            req.Content = new StringContent(string.Empty);
            req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

            using var res = await http.SendAsync(req, ct);
            res.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            token = doc.RootElement.GetProperty("access_token").GetString()!;
            var expires = doc.RootElement.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 3500;

            _cache.Set(CacheKey, token, TimeSpan.FromSeconds(Math.Max(60, expires - 60)));
            return token;
        }
    }
}
