using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vitality.Models.EntityClasses;
using Vitality.Models.Repos.Services.Schedules;

namespace Vitality.Models.Schedulers
{

    public class ProviderHoursMaterializationService : BackgroundService
    {
        private const string LockName = "SCHEDULER:ProviderHoursMaterialization";

        private readonly ILogger<ProviderHoursMaterializationService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly SchedulerSettings _settings;

        public ProviderHoursMaterializationService(
            ILogger<ProviderHoursMaterializationService> logger,
            IServiceScopeFactory scopeFactory,
            IOptions<SchedulerSettings> settings)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _settings = settings?.Value ?? new SchedulerSettings();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            TimeSpan tickInterval;
            if (_settings.ProviderHoursMaterializationIntervalMinutes is int qaMinutes && qaMinutes > 0)
            {
                tickInterval = TimeSpan.FromMinutes(qaMinutes);
                _logger.LogWarning(
                    "ProviderHoursMaterializationService: using QA minutes-based interval ({Minutes}m). This should NOT be set in production.",
                    qaMinutes);
            }
            else
            {
                var intervalHours = Math.Max(1, _settings.ProviderHoursMaterializationIntervalHours);
                tickInterval = TimeSpan.FromHours(intervalHours);
            }

            var horizonDays = Math.Max(1, _settings.ProviderHoursHorizonDays);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<MainContext>();

                    var connectionString = db.Database.GetDbConnection().ConnectionString;
                    await using var distributedLock = await SchedulerDistributedLock
                        .TryAcquireAsync(connectionString, LockName, stoppingToken).ConfigureAwait(false);
                    if (distributedLock is null)
                    {
                        _logger.LogInformation("ProviderHoursMaterializationService: another instance holds {Lock} — skipping this iteration.", LockName);
                    }
                    else
                    {
                        var materializer = scope.ServiceProvider.GetRequiredService<SlotMaterializer>();

                        var today = DateTime.UtcNow.Date;
                        var horizonEnd = today.AddDays(horizonDays);

                        var activeProviderIds = await db.UR_ProviderWeeklyTemplates
                            .AsNoTracking()
                            .Where(t => t.IsActive == true)
                            .Select(t => t.ProviderId)
                            .ToListAsync(stoppingToken);

                        _logger.LogInformation(
                            "ProviderHoursMaterializationService: materializing horizon [{From:yyyy-MM-dd}, {To:yyyy-MM-dd}] for {Count} provider(s).",
                            today, horizonEnd, activeProviderIds.Count);

                        foreach (var providerId in activeProviderIds)
                        {
                            try
                            {
                                await materializer.MaterializeHorizonAsync(providerId, today, horizonEnd, stoppingToken);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "ProviderHoursMaterializationService: materialization failed for provider {ProviderId}.", providerId);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ProviderHoursMaterializationService: top-level iteration failed.");
                }

                try
                {
                    await Task.Delay(tickInterval, stoppingToken);
                }
                catch (TaskCanceledException) { break; }
            }
        }
    }
}
