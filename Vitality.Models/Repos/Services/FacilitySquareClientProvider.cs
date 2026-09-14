using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Square;
using Vitality.Models.Repos;
using Vitality.Models.Repos.Interfaces;

public class FacilitySquareClientProvider : BaseRepo, IFacilitySquareClientProvider
{
    private readonly IMemoryCache _cache;
    private readonly Microsoft.Extensions.Configuration.IConfiguration _cfg;

    public FacilitySquareClientProvider(IMemoryCache cache, Microsoft.Extensions.Configuration.IConfiguration cfg)
    {
        _cache = cache; _cfg = cfg;
    }

    public async Task<SquareClient> GetAsync(long facilityId, CancellationToken ct = default)
    {
        return await _cache.GetOrCreateAsync($"sq:client:{facilityId}", async entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromMinutes(30);

            var cred = await _db.Sys_FacilitySquareCreds
                .Where(x => x.FacilityId == facilityId && x.IsActive == true)
                .OrderByDescending(x => x.FacilitySquareCredId)
                .Select(x => new { x.AccessToken, x.ApplicationId, x.LocationId })
                .FirstOrDefaultAsync(ct);

            if (cred == null || string.IsNullOrWhiteSpace(cred.AccessToken))
                throw new InvalidOperationException($"No active Square credentials for facility {facilityId}.");

            var envStr = _cfg.GetValue<string>("Square:Environment", "production");

            var env = string.Equals(envStr, "sandbox", StringComparison.OrdinalIgnoreCase)
                ? Square.Environment.Sandbox
                : Square.Environment.Production;

            return new SquareClient.Builder()
                .Environment(env)
                .AccessToken(cred.AccessToken)
                .Build();
        });
    }

    public void Invalidate(long facilityId) => _cache.Remove($"sq:client:{facilityId}");
}
