using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vitality.Models.EntityClasses;
using Vitality.Models.Schedulers;

namespace Vitality.Models.Repos.Services.Schedules
{

    public class SlotMaterializer
    {
        private readonly MainContext _db;
        private readonly TimezoneConverter _tz;
        private readonly ILogger<SlotMaterializer>? _logger;

        public SlotMaterializer(MainContext db, TimezoneConverter tz, ILogger<SlotMaterializer>? logger = null)
        {
            _db = db;
            _tz = tz;
            _logger = logger;
        }

        public async Task<int> MaterializeHorizonAsync(
            long providerId,
            DateTime fromDateLocal,
            DateTime toDateLocal,
            CancellationToken ct = default)
        {
            var template = await _db.UR_ProviderWeeklyTemplates
                .AsNoTracking()
                .Include(t => t.UR_ProviderDayHours)
                    .ThenInclude(dh => dh.UR_ProviderTimeRanges)
                .Where(t => t.ProviderId == providerId && t.IsActive == true)
                .FirstOrDefaultAsync(ct);

            if (template == null)
            {
                _logger?.LogDebug("SlotMaterializer: no template for provider {ProviderId}; nothing to do.", providerId);
                return 0;
            }
            if (!_tz.IsValidTimezone(template.Timezone))
            {
                _logger?.LogError("SlotMaterializer: provider {ProviderId} has invalid timezone '{Tz}'; skipping.", providerId, template.Timezone);
                return 0;
            }

            var fromDate = fromDateLocal.Date;
            var toDate = toDateLocal.Date;
            var overrides = await _db.UR_ProviderDateOverrides
                .AsNoTracking()
                .Include(o => o.UR_ProviderDateOverrideTimeRanges)
                .Where(o => o.ProviderId == providerId
                         && o.IsActive == true
                         && o.OverrideDate >= fromDate
                         && o.OverrideDate <= toDate)
                .ToListAsync(ct);
            var overrideByDate = overrides.ToDictionary(o => o.OverrideDate.Date);

            var dayHoursByDow = template.UR_ProviderDayHours
                .Where(dh => dh.IsActive != false)
                .ToDictionary(dh => (int)dh.DayOfWeek);

            var deleteWindowStartUtc = _tz.LocalToUtc(fromDate.AddDays(-1), TimeSpan.Zero, template.Timezone);
            var deleteWindowEndUtc = _tz.LocalToUtc(toDate.AddDays(2), TimeSpan.Zero, template.Timezone);

            using var tx = await _db.Database.BeginTransactionAsync(ct);

            var stale = await _db.UR_ProviderScheduledSlots
                .Where(s => s.ProviderId == providerId
                         && s.Status == "Available"
                         && s.StartTimeUtc.HasValue
                         && s.StartTimeUtc.Value >= deleteWindowStartUtc
                         && s.StartTimeUtc.Value < deleteWindowEndUtc)
                .ToListAsync(ct);
            if (stale.Count > 0)
                _db.UR_ProviderScheduledSlots.RemoveRange(stale);

            var existingStarts = new HashSet<DateTime>(
                await _db.UR_ProviderScheduledSlots
                    .AsNoTracking()
                    .Where(s => s.ProviderId == providerId
                             && s.StartTimeUtc.HasValue
                             && s.StartTimeUtc.Value >= deleteWindowStartUtc
                             && s.StartTimeUtc.Value < deleteWindowEndUtc
                             && s.Status != "Available")
                    .Select(s => s.StartTimeUtc!.Value)
                    .ToListAsync(ct));

            var inserted = 0;
            for (var d = fromDate; d <= toDate; d = d.AddDays(1))
            {
                List<(TimeSpan Start, TimeSpan End)> ranges;
                int durationMinutes;
                string sourceType;
                long? sourceOverrideId;

                if (overrideByDate.TryGetValue(d, out var ovr))
                {
                    sourceType = "Override";
                    sourceOverrideId = ovr.ProviderDateOverrideId;
                    durationMinutes = ovr.SlotDurationMinutes ?? template.DefaultSlotDurationMinutes;
                    ranges = ovr.IsClosed
                        ? new List<(TimeSpan, TimeSpan)>()
                        : ovr.UR_ProviderDateOverrideTimeRanges
                            .Where(r => r.IsActive != false)
                            .OrderBy(r => r.StartTimeLocal)
                            .Select(r => (r.StartTimeLocal, r.EndTimeLocal))
                            .ToList();
                }
                else if (dayHoursByDow.TryGetValue((int)d.DayOfWeek, out var dh))
                {
                    sourceType = "Template";
                    sourceOverrideId = null;
                    durationMinutes = dh.SlotDurationMinutes ?? template.DefaultSlotDurationMinutes;
                    ranges = dh.IsClosed
                        ? new List<(TimeSpan, TimeSpan)>()
                        : dh.UR_ProviderTimeRanges
                            .Where(r => r.IsActive != false)
                            .OrderBy(r => r.StartTimeLocal)
                            .Select(r => (r.StartTimeLocal, r.EndTimeLocal))
                            .ToList();
                }
                else
                {

                    continue;
                }

                if (durationMinutes <= 0) continue;

                foreach (var (rangeStart, rangeEndRaw) in ranges)
                {

                    var rangeEnd = rangeEndRaw == TimeSpan.Zero ? TimeSpan.FromHours(24) : rangeEndRaw;
                    var totalMinutes = (int)(rangeEnd - rangeStart).TotalMinutes;
                    if (totalMinutes <= 0) continue;
                    var slotCount = totalMinutes / durationMinutes;

                    for (int i = 0; i < slotCount; i++)
                    {

                        var slotStartLocal = rangeStart.Add(TimeSpan.FromMinutes(i * durationMinutes));
                        var startUtc = _tz.LocalToUtc(d, slotStartLocal, template.Timezone);

                        var endUtc = startUtc.AddMinutes(durationMinutes);

                        if (existingStarts.Contains(startUtc))
                            continue;

                        _db.UR_ProviderScheduledSlots.Add(new UR_ProviderScheduledSlot
                        {
                            ProviderId = providerId,
                            FacilityId = null,
                            StartTimeUtc = startUtc,
                            EndTimeUtc = endUtc,
                            DurationMinutes = durationMinutes,
                            Status = "Available",
                            SourceType = sourceType,
                            SourceOverrideId = sourceOverrideId,

                            SlotDate = startUtc.Date,
                            StartTime = startUtc.TimeOfDay,
                            EndTime = endUtc.TimeOfDay,
                            Duration = durationMinutes,
                            IsActive = true,
                            CreatedDate = DateTime.UtcNow,
                            CreatedBy = 0,
                            OrganizationId = template.OrganizationId
                        });
                        inserted++;
                    }
                }
            }

            var trackedTemplate = await _db.UR_ProviderWeeklyTemplates
                .FirstOrDefaultAsync(t => t.ProviderWeeklyTemplateId == template.ProviderWeeklyTemplateId, ct);
            if (trackedTemplate != null)
            {
                if (!trackedTemplate.LastMaterializedDate.HasValue || trackedTemplate.LastMaterializedDate.Value < toDate)
                {
                    trackedTemplate.LastMaterializedDate = toDate;
                    trackedTemplate.ModifiedDate = DateTime.UtcNow;
                }
            }

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            _logger?.LogInformation(
                "SlotMaterializer: provider {ProviderId} [{From:yyyy-MM-dd}, {To:yyyy-MM-dd}] — deleted {Deleted}, inserted {Inserted}.",
                providerId, fromDate, toDate, stale.Count, inserted);

            return inserted;
        }

    }
}
