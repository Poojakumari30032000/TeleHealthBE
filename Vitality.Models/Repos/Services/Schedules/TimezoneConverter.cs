using System;
using NodaTime;
using NodaTime.TimeZones;

namespace Vitality.Models.Repos.Services.Schedules
{

    public class TimezoneConverter
    {

        private static readonly ZoneLocalMappingResolver Resolver =
            Resolvers.CreateMappingResolver(Resolvers.ReturnEarlier, Resolvers.ReturnStartOfIntervalAfter);

        public DateTime LocalToUtc(DateTime localDate, TimeSpan localTimeOfDay, string ianaTimezone)
        {
            var zone = DateTimeZoneProviders.Tzdb[ianaTimezone];
            var local = new LocalDateTime(
                localDate.Year, localDate.Month, localDate.Day,
                localTimeOfDay.Hours, localTimeOfDay.Minutes, localTimeOfDay.Seconds);

            var zoned = zone.ResolveLocal(local, Resolver);
            return zoned.ToInstant().ToDateTimeUtc();
        }

        public DateTime UtcToLocal(DateTime utc, string ianaTimezone)
        {
            if (utc.Kind != DateTimeKind.Utc)
                utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);

            var zone = DateTimeZoneProviders.Tzdb[ianaTimezone];
            var instant = Instant.FromDateTimeUtc(utc);
            var local = instant.InZone(zone).LocalDateTime;
            return new DateTime(local.Year, local.Month, local.Day, local.Hour, local.Minute, local.Second, DateTimeKind.Unspecified);
        }

        public bool IsValidTimezone(string ianaTimezone)
        {
            if (string.IsNullOrWhiteSpace(ianaTimezone)) return false;
            try
            {
                _ = DateTimeZoneProviders.Tzdb[ianaTimezone];
                return true;
            }
            catch (DateTimeZoneNotFoundException)
            {
                return false;
            }
        }
    }
}
