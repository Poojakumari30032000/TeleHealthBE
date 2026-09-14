using System;
using Vitality.Models.Repos.Services.Schedules;
using Xunit;

namespace Vitality.Models.Tests.ProviderHours;

public class TimezoneConverterTests
{
    private readonly TimezoneConverter _tz = new();

    [Fact]
    public void IsValidTimezone_recognizes_known_zones()
    {
        Assert.True(_tz.IsValidTimezone("America/New_York"));
        Assert.True(_tz.IsValidTimezone("Etc/UTC"));
        Assert.True(_tz.IsValidTimezone("Pacific/Auckland"));
        Assert.False(_tz.IsValidTimezone("Not/A_Zone"));
        Assert.False(_tz.IsValidTimezone(""));
        Assert.False(_tz.IsValidTimezone(null!));
    }

    [Fact]
    public void New_York_winter_offset_is_minus_5()
    {

        var utc = _tz.LocalToUtc(new DateTime(2026, 1, 15), new TimeSpan(10, 0, 0), "America/New_York");
        Assert.Equal(new DateTime(2026, 1, 15, 15, 0, 0, DateTimeKind.Utc), utc);
    }

    [Fact]
    public void New_York_summer_offset_is_minus_4()
    {

        var utc = _tz.LocalToUtc(new DateTime(2026, 7, 15), new TimeSpan(10, 0, 0), "America/New_York");
        Assert.Equal(new DateTime(2026, 7, 15, 14, 0, 0, DateTimeKind.Utc), utc);
    }

    [Fact]
    public void Spring_forward_gap_resolves_to_end_of_gap()
    {

        var utc = _tz.LocalToUtc(new DateTime(2026, 3, 8), new TimeSpan(2, 30, 0), "America/New_York");
        Assert.Equal(new DateTime(2026, 3, 8, 7, 0, 0, DateTimeKind.Utc), utc);
    }

    [Fact]
    public void Fall_back_ambiguity_resolves_to_earlier_offset()
    {

        var utc = _tz.LocalToUtc(new DateTime(2026, 11, 1), new TimeSpan(1, 30, 0), "America/New_York");
        Assert.Equal(new DateTime(2026, 11, 1, 5, 30, 0, DateTimeKind.Utc), utc);
    }

    [Fact]
    public void UtcToLocal_roundtrips_an_unambiguous_local()
    {
        var date = new DateTime(2026, 7, 15);
        var time = new TimeSpan(14, 0, 0);
        var utc = _tz.LocalToUtc(date, time, "America/New_York");
        var local = _tz.UtcToLocal(utc, "America/New_York");
        Assert.Equal(date, local.Date);
        Assert.Equal(time, local.TimeOfDay);
    }

    [Fact]
    public void Auckland_winter_offset_is_plus_12_in_July()
    {

        var utc = _tz.LocalToUtc(new DateTime(2026, 7, 15), new TimeSpan(10, 0, 0), "Pacific/Auckland");
        Assert.Equal(new DateTime(2026, 7, 14, 22, 0, 0, DateTimeKind.Utc), utc);
    }

    [Fact]
    public void Utc_zone_is_passthrough()
    {
        var utc = _tz.LocalToUtc(new DateTime(2026, 5, 20), new TimeSpan(9, 30, 0), "Etc/UTC");
        Assert.Equal(new DateTime(2026, 5, 20, 9, 30, 0, DateTimeKind.Utc), utc);
    }
}
