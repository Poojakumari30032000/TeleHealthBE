using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using DudeMeds.Models.DTOs.ProviderHours;
using DudeMeds.Models.Repos.Interfaces;
using Vitality.Models.EntityClasses;
using Vitality.Models.Repos;
using Vitality.Models.Repos.Services.Schedules;
using Vitality.Models.Repos.Services.Validators;

namespace DudeMeds.Models.Repos.Services
{
    public class ProviderHoursRepo : BaseRepo, IProviderHoursRepo
    {

        private const string DefaultTimezoneConfigKey = "ProviderHoursDefaults:Timezone";
        private const string FallbackTimezone = "Etc/UTC";

        private static readonly string[] NonOccupyingApptStatuses =
            { "Cancelled", "Canceled", "Declined", "Missed", "No Show", "NoShow", "Completed", "Done" };

        private readonly TimezoneConverter _tz;
        private readonly SlotMaterializer _materializer;
        private readonly ProviderHoursValidator _validator;
        private readonly IConfiguration _config;

        public ProviderHoursRepo(
            TimezoneConverter tz,
            SlotMaterializer materializer,
            ProviderHoursValidator validator,
            IConfiguration config)
        {
            _tz = tz;
            _materializer = materializer;
            _validator = validator;
            _config = config;
        }

        private async Task SyncTemplateToClientIfBlankAsync(long providerId, string? clientTimezone, CancellationToken ct)
        {
            var template = await _db.UR_ProviderWeeklyTemplates
                .Include(t => t.UR_ProviderDayHours)
                    .ThenInclude(dh => dh.UR_ProviderTimeRanges)
                .FirstOrDefaultAsync(t => t.ProviderId == providerId && t.IsActive == true, ct);
            if (template == null) return;

            var isBlank = template.UR_ProviderDayHours
                .All(dh => dh.IsClosed && (dh.UR_ProviderTimeRanges == null || dh.UR_ProviderTimeRanges.Count == 0));
            if (!isBlank) return;

            var desiredTz = ResolveTimezoneForNewTemplate(clientTimezone);
            const int desiredDuration = 10;
            var changed = false;

            if (!string.Equals(template.Timezone, desiredTz, StringComparison.Ordinal))
            {
                template.Timezone = desiredTz;
                changed = true;
            }
            if (template.DefaultSlotDurationMinutes != desiredDuration)
            {
                template.DefaultSlotDurationMinutes = desiredDuration;
                changed = true;
            }

            if (changed)
            {
                template.ModifiedDate = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
            }
        }

        private string ResolveTimezoneForNewTemplate(string? clientTimezone)
        {
            if (!string.IsNullOrWhiteSpace(clientTimezone) && _tz.IsValidTimezone(clientTimezone))
                return clientTimezone;

            var configured = _config[DefaultTimezoneConfigKey];
            if (!string.IsNullOrWhiteSpace(configured) && _tz.IsValidTimezone(configured))
                return configured;

            return FallbackTimezone;
        }

        public async Task<WeekViewDto> GetWeekAsync(long providerId, DateTime weekStartDate, long organizationId, string? clientTimezone = null, CancellationToken ct = default)
        {
            var weekStart = weekStartDate.Date;
            var weekEndExclusive = weekStart.AddDays(7);

            await EnsureTemplateExistsAsync(providerId, organizationId, createdByUserId: 0, clientTimezone, ct);

            await SyncTemplateToClientIfBlankAsync(providerId, clientTimezone, ct);

            var template = await _db.UR_ProviderWeeklyTemplates
                .AsNoTracking()
                .Include(t => t.UR_ProviderDayHours)
                    .ThenInclude(dh => dh.UR_ProviderTimeRanges)
                .Where(t => t.ProviderId == providerId && t.IsActive == true)
                .FirstOrDefaultAsync(ct);

            if (template == null) return EmptyWeek(providerId, weekStart);

            var overrides = await _db.UR_ProviderDateOverrides
                .AsNoTracking()
                .Include(o => o.UR_ProviderDateOverrideTimeRanges)
                .Where(o => o.ProviderId == providerId
                         && o.IsActive == true
                         && o.OverrideDate >= weekStart
                         && o.OverrideDate < weekEndExclusive)
                .ToListAsync(ct);
            var overrideByDate = overrides.ToDictionary(o => o.OverrideDate.Date);

            var weekStartUtc = _tz.LocalToUtc(weekStart, TimeSpan.Zero, template.Timezone);
            var weekEndUtc = _tz.LocalToUtc(weekEndExclusive, TimeSpan.Zero, template.Timezone);
            var bookedSlots = await _db.UR_ProviderScheduledSlots
                .AsNoTracking()
                .Where(s => s.ProviderId == providerId
                         && s.StartTimeUtc.HasValue
                         && s.StartTimeUtc.Value >= weekStartUtc
                         && s.StartTimeUtc.Value < weekEndUtc
                         && _db.PT_PatientAppointmentSlots.Any(a =>
                                a.ProviderScheduledSlotId == s.ProviderScheduledSlotId
                                && a.IsActive == true
                                && (a.Status == null || !NonOccupyingApptStatuses.Contains(a.Status))))
                .Select(s => s.StartTimeUtc!.Value)
                .ToListAsync(ct);

            var bookedLocalByDate = bookedSlots
                .Select(utc => _tz.UtcToLocal(utc, template.Timezone))
                .GroupBy(local => local.Date)
                .ToDictionary(g => g.Key, g => g.Select(d => d.TimeOfDay).ToList());

            var dayHoursByDow = template.UR_ProviderDayHours
                .ToDictionary(dh => (int)dh.DayOfWeek);

            var days = new List<DayHoursDto>(7);
            for (var i = 0; i < 7; i++)
            {
                var date = weekStart.AddDays(i);
                var dow = (byte)date.DayOfWeek;
                var dayDto = new DayHoursDto
                {
                    Date = date,
                    DayOfWeek = dow,
                    IsClosed = true,
                    IsOverride = false,
                    SlotDurationMinutes = null,
                    TimeRanges = new List<TimeRangeDto>(),
                    BookedSlotCount = bookedLocalByDate.TryGetValue(date, out var bookedList) ? bookedList.Count : 0
                };

                List<TimeRangeDto> ranges;
                int? duration;
                bool isClosed;

                if (overrideByDate.TryGetValue(date, out var ovr))
                {
                    dayDto.IsOverride = true;
                    dayDto.Note = ovr.Note;
                    isClosed = ovr.IsClosed;
                    duration = ovr.SlotDurationMinutes;
                    ranges = ovr.IsClosed
                        ? new List<TimeRangeDto>()
                        : ovr.UR_ProviderDateOverrideTimeRanges
                            .Where(r => r.IsActive != false)
                            .OrderBy(r => r.StartTimeLocal)
                            .Select(r => new TimeRangeDto
                            {
                                Id = r.ProviderDateOverrideTimeRangeId,
                                StartTime = r.StartTimeLocal.ToString(@"hh\:mm"),
                                EndTime = r.EndTimeLocal.ToString(@"hh\:mm"),
                                SlotDurationMinutes = ovr.SlotDurationMinutes,
                                HasBookings = RangeHasBookings(r.StartTimeLocal, r.EndTimeLocal, bookedList)
                            })
                            .ToList();
                }
                else if (dayHoursByDow.TryGetValue(dow, out var dh))
                {
                    isClosed = dh.IsClosed;
                    duration = dh.SlotDurationMinutes;
                    ranges = dh.IsClosed
                        ? new List<TimeRangeDto>()
                        : dh.UR_ProviderTimeRanges
                            .Where(r => r.IsActive != false)
                            .OrderBy(r => r.StartTimeLocal)
                            .Select(r => new TimeRangeDto
                            {
                                Id = r.ProviderTimeRangeId,
                                StartTime = r.StartTimeLocal.ToString(@"hh\:mm"),
                                EndTime = r.EndTimeLocal.ToString(@"hh\:mm"),
                                SlotDurationMinutes = dh.SlotDurationMinutes,
                                HasBookings = RangeHasBookings(r.StartTimeLocal, r.EndTimeLocal, bookedList)
                            })
                            .ToList();
                }
                else
                {
                    isClosed = true;
                    duration = null;
                    ranges = new List<TimeRangeDto>();
                }

                dayDto.IsClosed = isClosed;
                dayDto.SlotDurationMinutes = duration;
                dayDto.TimeRanges = ranges;
                days.Add(dayDto);
            }

            return new WeekViewDto
            {
                ProviderId = providerId,
                Timezone = template.Timezone,
                WeekStartDate = weekStart,
                DefaultSlotDurationMinutes = template.DefaultSlotDurationMinutes,
                Days = days
            };
        }

        private static bool RangeHasBookings(TimeSpan start, TimeSpan end, List<TimeSpan>? bookedTods)
        {
            if (bookedTods == null || bookedTods.Count == 0) return false;
            return bookedTods.Any(t => t >= start && t < end);
        }

        public async Task SaveDayAsync(long providerId, byte dayOfWeek, SaveDayRequest request, long userId, long organizationId, CancellationToken ct = default)
        {
            if (dayOfWeek > 6)
                throw new ProviderHoursValidationException(ValidationCode.InvalidDayOfWeek, "DayOfWeek must be 0..6 (Sunday..Saturday).");

            await EnsureTemplateExistsAsync(providerId, organizationId, userId, clientTimezone: null, ct);

            var template = await _db.UR_ProviderWeeklyTemplates
                .Include(t => t.UR_ProviderDayHours)
                    .ThenInclude(dh => dh.UR_ProviderTimeRanges)
                .Where(t => t.ProviderId == providerId && t.IsActive == true)
                .FirstAsync(ct);

            var horizonEnd = (template.LastMaterializedDate ?? DateTime.UtcNow.Date).AddDays(template.MaterializationHorizonDays);
            var currentDh = template.UR_ProviderDayHours.FirstOrDefault(dh => dh.DayOfWeek == dayOfWeek);

            using var tx = await _db.Database.BeginTransactionAsync(ct);

            await AutoProtectBookedDatesForDowAsync(template, currentDh, dayOfWeek, horizonEnd, userId, organizationId, ct);
            await _db.SaveChangesAsync(ct);

            var remainingBookedStartsLocal = await GetBookedLocalStartsAsync(
                providerId, template.Timezone, DateTime.UtcNow.Date, horizonEnd,
                excludeOverriddenDates: true, ct);
            var bookedOnThisDow = remainingBookedStartsLocal
                .Where(local => (byte)local.DayOfWeek == dayOfWeek)
                .ToList();

            var validation = _validator.ValidateDay(request, bookedOnThisDow, template.DefaultSlotDurationMinutes);
            if (!validation.IsValid)
                throw new ProviderHoursValidationException(validation);

            var dayHours = currentDh;
            if (dayHours == null)
            {
                dayHours = new UR_ProviderDayHours
                {
                    ProviderWeeklyTemplateId = template.ProviderWeeklyTemplateId,
                    DayOfWeek = dayOfWeek,
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = userId,
                    UR_ProviderTimeRanges = new HashSet<UR_ProviderTimeRange>()
                };
                template.UR_ProviderDayHours.Add(dayHours);
            }

            dayHours.IsClosed = request.IsClosed;
            dayHours.SlotDurationMinutes = request.SlotDurationMinutes;
            dayHours.ModifiedDate = DateTime.UtcNow;
            dayHours.ModifiedBy = userId;

            ReplaceTimeRanges(
                dayHours.UR_ProviderTimeRanges,
                request.TimeRanges,
                userId,
                createNew: () => new UR_ProviderTimeRange { IsActive = true, CreatedDate = DateTime.UtcNow, CreatedBy = userId });

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            var rematFrom = DateTime.UtcNow.Date;
            var rematTo = rematFrom.AddDays(template.MaterializationHorizonDays);
            await _materializer.MaterializeHorizonAsync(providerId, rematFrom, rematTo, ct);
        }

        private async Task AutoProtectBookedDatesForDowAsync(
            UR_ProviderWeeklyTemplate template,
            UR_ProviderDayHours? currentDh,
            byte dayOfWeek,
            DateTime horizonEnd,
            long userId,
            long organizationId,
            CancellationToken ct)
        {

            var bookedLocal = await GetBookedLocalStartsAsync(
                template.ProviderId, template.Timezone,
                DateTime.UtcNow.Date, horizonEnd,
                excludeOverriddenDates: false, ct);
            var bookedDatesOfDow = bookedLocal
                .Where(local => (byte)local.DayOfWeek == dayOfWeek)
                .Select(local => local.Date)
                .Distinct()
                .ToList();
            if (bookedDatesOfDow.Count == 0) return;

            var existingOverrideDates = await _db.UR_ProviderDateOverrides
                .Where(o => o.ProviderId == template.ProviderId
                         && o.IsActive == true
                         && bookedDatesOfDow.Contains(o.OverrideDate))
                .Select(o => o.OverrideDate)
                .ToListAsync(ct);
            var existingSet = existingOverrideDates.Select(d => d.Date).ToHashSet();

            foreach (var date in bookedDatesOfDow)
            {
                if (existingSet.Contains(date)) continue;

                var newOverride = new UR_ProviderDateOverride
                {
                    ProviderId = template.ProviderId,
                    OverrideDate = date,
                    IsClosed = currentDh?.IsClosed ?? true,
                    SlotDurationMinutes = currentDh?.SlotDurationMinutes,
                    Note = "Auto-protected: bookings existed when the weekly template changed.",
                    OrganizationId = organizationId,
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = userId,
                    UR_ProviderDateOverrideTimeRanges = new HashSet<UR_ProviderDateOverrideTimeRange>()
                };

                if (currentDh != null && !currentDh.IsClosed)
                {
                    foreach (var range in currentDh.UR_ProviderTimeRanges.Where(r => r.IsActive != false))
                    {
                        newOverride.UR_ProviderDateOverrideTimeRanges.Add(new UR_ProviderDateOverrideTimeRange
                        {
                            StartTimeLocal = range.StartTimeLocal,
                            EndTimeLocal = range.EndTimeLocal,
                            SortOrder = range.SortOrder,
                            IsActive = true,
                            CreatedDate = DateTime.UtcNow,
                            CreatedBy = userId
                        });
                    }
                }

                _db.UR_ProviderDateOverrides.Add(newOverride);
            }
        }

        public async Task SaveDateOverrideAsync(long providerId, DateOverrideRequest request, long userId, long organizationId, CancellationToken ct = default)
        {
            await EnsureTemplateExistsAsync(providerId, organizationId, userId, clientTimezone: null, ct);

            var template = await _db.UR_ProviderWeeklyTemplates
                .Where(t => t.ProviderId == providerId && t.IsActive == true)
                .FirstAsync(ct);

            var date = request.Date.Date;

            var bookedStartsLocal = await GetBookedLocalStartsAsync(providerId, template.Timezone, date, date.AddDays(1), excludeOverriddenDates: false, ct);

            var validation = _validator.ValidateDateOverride(request, bookedStartsLocal, template.DefaultSlotDurationMinutes);
            if (!validation.IsValid)
                throw new ProviderHoursValidationException(validation);

            var existing = await _db.UR_ProviderDateOverrides
                .Include(o => o.UR_ProviderDateOverrideTimeRanges)
                .FirstOrDefaultAsync(o => o.ProviderId == providerId && o.OverrideDate == date && o.IsActive == true, ct);

            if (existing == null)
            {
                existing = new UR_ProviderDateOverride
                {
                    ProviderId = providerId,
                    OverrideDate = date,
                    IsClosed = request.IsClosed,
                    Note = request.Note,
                    SlotDurationMinutes = request.SlotDurationMinutes,
                    OrganizationId = organizationId,
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = userId,
                    UR_ProviderDateOverrideTimeRanges = new HashSet<UR_ProviderDateOverrideTimeRange>()
                };
                _db.UR_ProviderDateOverrides.Add(existing);
            }
            else
            {
                existing.IsClosed = request.IsClosed;
                existing.Note = request.Note;
                existing.SlotDurationMinutes = request.SlotDurationMinutes;
                existing.ModifiedDate = DateTime.UtcNow;
                existing.ModifiedBy = userId;
            }

            ReplaceTimeRanges(
                existing.UR_ProviderDateOverrideTimeRanges,
                request.IsClosed ? new List<TimeRangeWriteDto>() : request.TimeRanges,
                userId,
                createNew: () => new UR_ProviderDateOverrideTimeRange { IsActive = true, CreatedDate = DateTime.UtcNow, CreatedBy = userId });

            using var tx = await _db.Database.BeginTransactionAsync(ct);
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            await _materializer.MaterializeHorizonAsync(providerId, date.AddDays(-1), date.AddDays(1), ct);
        }

        public async Task DeleteDateOverrideAsync(long providerId, DateTime date, long userId, long organizationId, CancellationToken ct = default)
        {
            var d = date.Date;
            var existing = await _db.UR_ProviderDateOverrides
                .Include(o => o.UR_ProviderDateOverrideTimeRanges)
                .FirstOrDefaultAsync(o => o.ProviderId == providerId && o.OverrideDate == d && o.IsActive == true, ct);
            if (existing == null) return;

            var template = await _db.UR_ProviderWeeklyTemplates.AsNoTracking()
                .FirstOrDefaultAsync(t => t.ProviderId == providerId && t.IsActive == true, ct);
            if (template == null) return;

            existing.IsActive = false;
            existing.ModifiedDate = DateTime.UtcNow;
            existing.ModifiedBy = userId;

            using var tx = await _db.Database.BeginTransactionAsync(ct);
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            await _materializer.MaterializeHorizonAsync(providerId, d.AddDays(-1), d.AddDays(1), ct);
        }

        public async Task EnsureTemplateExistsAsync(long providerId, long organizationId, long createdByUserId, string? clientTimezone = null, CancellationToken ct = default)
        {
            var existing = await _db.UR_ProviderWeeklyTemplates
                .AsNoTracking()
                .AnyAsync(t => t.ProviderId == providerId && t.IsActive == true, ct);
            if (existing) return;

            var tz = ResolveTimezoneForNewTemplate(clientTimezone);

            var template = new UR_ProviderWeeklyTemplate
            {
                ProviderId = providerId,
                Timezone = tz,
                DefaultSlotDurationMinutes = 10,
                MaterializationHorizonDays = 30,
                OrganizationId = organizationId,
                IsActive = true,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = createdByUserId,
                UR_ProviderDayHours = new HashSet<UR_ProviderDayHours>()
            };
            for (byte dow = 0; dow < 7; dow++)
            {
                template.UR_ProviderDayHours.Add(new UR_ProviderDayHours
                {
                    DayOfWeek = dow,
                    IsClosed = true,
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = createdByUserId,
                    UR_ProviderTimeRanges = new HashSet<UR_ProviderTimeRange>()
                });
            }
            _db.UR_ProviderWeeklyTemplates.Add(template);
            await _db.SaveChangesAsync(ct);
        }

        private async Task<List<DateTime>> GetBookedLocalStartsAsync(
            long providerId, string tz, DateTime fromLocalInclusive, DateTime toLocalExclusive,
            bool excludeOverriddenDates, CancellationToken ct)
        {
            var fromUtc = _tz.LocalToUtc(fromLocalInclusive.Date, TimeSpan.Zero, tz);
            var toUtc = _tz.LocalToUtc(toLocalExclusive.Date, TimeSpan.Zero, tz);

            var bookedUtcs = await _db.UR_ProviderScheduledSlots
                .AsNoTracking()
                .Where(s => s.ProviderId == providerId
                         && s.StartTimeUtc.HasValue
                         && s.StartTimeUtc.Value >= fromUtc
                         && s.StartTimeUtc.Value < toUtc
                         && _db.PT_PatientAppointmentSlots.Any(a =>
                                a.ProviderScheduledSlotId == s.ProviderScheduledSlotId
                                && a.IsActive == true
                                && (a.Status == null || !NonOccupyingApptStatuses.Contains(a.Status))))
                .Select(s => s.StartTimeUtc!.Value)
                .ToListAsync(ct);
            var bookedLocal = bookedUtcs.Select(utc => _tz.UtcToLocal(utc, tz)).ToList();

            if (!excludeOverriddenDates) return bookedLocal;

            var overrideDates = (await _db.UR_ProviderDateOverrides
                .AsNoTracking()
                .Where(o => o.ProviderId == providerId
                         && o.IsActive == true
                         && o.OverrideDate >= fromLocalInclusive.Date
                         && o.OverrideDate < toLocalExclusive.Date)
                .Select(o => o.OverrideDate)
                .ToListAsync(ct))
                .Select(d => d.Date)
                .ToHashSet();

            return bookedLocal.Where(l => !overrideDates.Contains(l.Date)).ToList();
        }

        private static void ReplaceTimeRanges<T>(
            ICollection<T> current,
            List<TimeRangeWriteDto> requested,
            long userId,
            Func<T> createNew)
            where T : class
        {
            var requestedIds = requested.Where(r => r.Id.HasValue && r.Id.Value > 0).Select(r => r.Id!.Value).ToHashSet();
            var toRemove = current.Where(r => GetId(r).HasValue && !requestedIds.Contains(GetId(r)!.Value)).ToList();
            foreach (var rem in toRemove) current.Remove(rem);

            foreach (var dto in requested)
            {
                var start = ParseTimeSpan(dto.StartTime);
                var end = ParseTimeSpan(dto.EndTime);
                T? row;
                if (dto.Id.HasValue && dto.Id.Value > 0)
                {
                    row = current.FirstOrDefault(r => GetId(r) == dto.Id.Value);
                    if (row == null) continue;
                    SetTimes(row, start, end, dto.SlotDurationMinutes, userId, isNew: false);
                }
                else
                {
                    row = createNew();
                    SetTimes(row, start, end, dto.SlotDurationMinutes, userId, isNew: true);
                    current.Add(row);
                }
            }
        }

        private static long? GetId<T>(T row) where T : class
        {
            return row switch
            {
                UR_ProviderTimeRange r => r.ProviderTimeRangeId,
                UR_ProviderDateOverrideTimeRange r => r.ProviderDateOverrideTimeRangeId,
                _ => null
            };
        }

        private static void SetTimes<T>(T row, TimeSpan start, TimeSpan end, int? slotDuration, long userId, bool isNew) where T : class
        {
            switch (row)
            {
                case UR_ProviderTimeRange r:
                    r.StartTimeLocal = start;
                    r.EndTimeLocal = end;
                    if (!isNew)
                    {
                        r.ModifiedDate = DateTime.UtcNow;
                        r.ModifiedBy = userId;
                    }
                    break;
                case UR_ProviderDateOverrideTimeRange r:
                    r.StartTimeLocal = start;
                    r.EndTimeLocal = end;
                    if (!isNew)
                    {
                        r.ModifiedDate = DateTime.UtcNow;
                        r.ModifiedBy = userId;
                    }
                    break;
            }
        }

        private static TimeSpan ParseTimeSpan(string s)
        {

            if (TimeSpan.TryParseExact(s, new[] { @"hh\:mm", @"hh\:mm\:ss", @"h\:mm" }, CultureInfo.InvariantCulture, out var ts))
                return ts;
            return TimeSpan.Parse(s, CultureInfo.InvariantCulture);
        }

        private static WeekViewDto EmptyWeek(long providerId, DateTime weekStart)
        {
            var days = new List<DayHoursDto>(7);
            for (var i = 0; i < 7; i++)
            {
                var date = weekStart.AddDays(i);
                days.Add(new DayHoursDto
                {
                    Date = date,
                    DayOfWeek = (byte)date.DayOfWeek,
                    IsClosed = true,
                    IsOverride = false,
                    TimeRanges = new List<TimeRangeDto>(),
                    BookedSlotCount = 0
                });
            }
            return new WeekViewDto
            {
                ProviderId = providerId,
                Timezone = FallbackTimezone,
                WeekStartDate = weekStart,
                DefaultSlotDurationMinutes = 10,
                Days = days
            };
        }
    }

    public class ProviderHoursValidationException : Exception
    {
        public ValidationResult Result { get; }

        public ProviderHoursValidationException(ValidationResult result)
            : base(result.Errors.FirstOrDefault()?.Message ?? "Validation failed.")
        {
            Result = result;
        }

        public ProviderHoursValidationException(ValidationCode code, string message)
            : base(message)
        {
            Result = new ValidationResult();
            Result.Add(code, message);
        }
    }
}
