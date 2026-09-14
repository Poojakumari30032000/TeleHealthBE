using System;
using System.Collections.Generic;
using System.Linq;
using DudeMeds.Models.DTOs.ProviderHours;
using Vitality.Models.Repos.Services.Validators;
using Xunit;

namespace Vitality.Models.Tests.ProviderHours;

public class ProviderHoursValidatorTests
{
    private readonly ProviderHoursValidator _v = new();

    [Fact]
    public void Closed_day_with_no_ranges_passes()
    {
        var req = new SaveDayRequest { ProviderId = 1, IsClosed = true, TimeRanges = new List<TimeRangeWriteDto>() };
        var r = _v.ValidateDay(req, Array.Empty<DateTime>(), 30);
        Assert.True(r.IsValid);
    }

    [Fact]
    public void Closed_day_with_ranges_fails_with_correct_code()
    {
        var req = new SaveDayRequest
        {
            ProviderId = 1,
            IsClosed = true,
            TimeRanges = new List<TimeRangeWriteDto> { new() { StartTime = "09:00", EndTime = "10:00" } }
        };
        var r = _v.ValidateDay(req, Array.Empty<DateTime>(), 30);
        Assert.False(r.IsValid);
        Assert.Contains(r.Errors, e => e.Code == ValidationCode.ClosedDayMustHaveNoRanges);
    }

    [Fact]
    public void Open_day_with_no_ranges_fails()
    {
        var req = new SaveDayRequest { ProviderId = 1, IsClosed = false, TimeRanges = new List<TimeRangeWriteDto>() };
        var r = _v.ValidateDay(req, Array.Empty<DateTime>(), 30);
        Assert.False(r.IsValid);
        Assert.Contains(r.Errors, e => e.Code == ValidationCode.OpenDayMustHaveRanges);
    }

    [Fact]
    public void End_not_after_start_fails()
    {
        var req = new SaveDayRequest
        {
            ProviderId = 1,
            IsClosed = false,
            TimeRanges = new List<TimeRangeWriteDto> { new() { StartTime = "14:00", EndTime = "12:00" } }
        };
        var r = _v.ValidateDay(req, Array.Empty<DateTime>(), 30);
        Assert.False(r.IsValid);
        Assert.Contains(r.Errors, e => e.Code == ValidationCode.EndNotAfterStart);
    }

    [Fact]
    public void Ranges_that_dont_overlap_pass()
    {
        var req = new SaveDayRequest
        {
            ProviderId = 1,
            IsClosed = false,
            TimeRanges = new List<TimeRangeWriteDto>
            {
                new() { StartTime = "09:00", EndTime = "12:00" },
                new() { StartTime = "13:00", EndTime = "17:00" }
            }
        };
        var r = _v.ValidateDay(req, Array.Empty<DateTime>(), 30);
        Assert.True(r.IsValid);
    }

    [Fact]
    public void Touching_ranges_at_boundary_do_not_overlap()
    {

        var req = new SaveDayRequest
        {
            ProviderId = 1,
            IsClosed = false,
            TimeRanges = new List<TimeRangeWriteDto>
            {
                new() { StartTime = "09:00", EndTime = "12:00" },
                new() { StartTime = "12:00", EndTime = "14:00" }
            }
        };
        var r = _v.ValidateDay(req, Array.Empty<DateTime>(), 30);
        Assert.True(r.IsValid);
    }

    [Fact]
    public void Overlapping_ranges_fail()
    {
        var req = new SaveDayRequest
        {
            ProviderId = 1,
            IsClosed = false,
            TimeRanges = new List<TimeRangeWriteDto>
            {
                new() { StartTime = "09:00", EndTime = "12:00" },
                new() { StartTime = "11:00", EndTime = "13:00" }
            }
        };
        var r = _v.ValidateDay(req, Array.Empty<DateTime>(), 30);
        Assert.False(r.IsValid);
        Assert.Contains(r.Errors, e => e.Code == ValidationCode.RangesOverlap);
    }

    [Fact]
    public void Duration_must_divide_range_evenly()
    {

        var req = new SaveDayRequest
        {
            ProviderId = 1,
            IsClosed = false,
            SlotDurationMinutes = 30,
            TimeRanges = new List<TimeRangeWriteDto> { new() { StartTime = "09:00", EndTime = "10:10" } }
        };
        var r = _v.ValidateDay(req, Array.Empty<DateTime>(), 30);
        Assert.False(r.IsValid);
        Assert.Contains(r.Errors, e => e.Code == ValidationCode.DurationDoesNotDivide);
    }

    [Fact]
    public void Booked_slot_that_falls_outside_new_ranges_fails()
    {

        var bookedAt = new[] { DateTime.Today.AddHours(14).AddMinutes(30) };
        var req = new SaveDayRequest
        {
            ProviderId = 1,
            IsClosed = false,
            SlotDurationMinutes = 30,
            TimeRanges = new List<TimeRangeWriteDto> { new() { StartTime = "09:00", EndTime = "12:00" } }
        };
        var r = _v.ValidateDay(req, bookedAt, 30);
        Assert.False(r.IsValid);
        Assert.Contains(r.Errors, e => e.Code == ValidationCode.WouldOrphanBookedSlot);
    }

    [Fact]
    public void Booked_slot_that_still_fits_passes()
    {
        var bookedAt = new[] { DateTime.Today.AddHours(10).AddMinutes(30) };
        var req = new SaveDayRequest
        {
            ProviderId = 1,
            IsClosed = false,
            SlotDurationMinutes = 30,
            TimeRanges = new List<TimeRangeWriteDto> { new() { StartTime = "09:00", EndTime = "12:00" } }
        };
        var r = _v.ValidateDay(req, bookedAt, 30);
        Assert.True(r.IsValid);
    }

    [Fact]
    public void Cannot_close_day_with_existing_bookings()
    {
        var bookedAt = new[] { DateTime.Today.AddHours(10) };
        var req = new SaveDayRequest { ProviderId = 1, IsClosed = true, TimeRanges = new List<TimeRangeWriteDto>() };
        var r = _v.ValidateDay(req, bookedAt, 30);
        Assert.False(r.IsValid);
        Assert.Contains(r.Errors, e => e.Code == ValidationCode.CannotCloseDayWithBookings);
    }

    [Fact]
    public void Cannot_close_date_override_with_existing_bookings()
    {
        var bookedAt = new[] { DateTime.Today.AddHours(10) };
        var req = new DateOverrideRequest { ProviderId = 1, Date = DateTime.Today, IsClosed = true };
        var r = _v.ValidateDateOverride(req, bookedAt, 30);
        Assert.False(r.IsValid);
        Assert.Contains(r.Errors, e => e.Code == ValidationCode.CannotCloseDateWithBookings);
    }

    [Fact]
    public void Invalid_time_format_fails()
    {
        var req = new SaveDayRequest
        {
            ProviderId = 1,
            IsClosed = false,
            TimeRanges = new List<TimeRangeWriteDto> { new() { StartTime = "9am", EndTime = "5pm" } }
        };
        var r = _v.ValidateDay(req, Array.Empty<DateTime>(), 30);
        Assert.False(r.IsValid);
        Assert.Contains(r.Errors, e => e.Code == ValidationCode.InvalidTimeFormat);
    }

    [Fact]
    public void Range_ending_at_midnight_is_valid()
    {

        var req = new SaveDayRequest
        {
            ProviderId = 1,
            IsClosed = false,
            SlotDurationMinutes = 10,
            TimeRanges = new List<TimeRangeWriteDto> { new() { StartTime = "23:50", EndTime = "00:00" } }
        };
        var r = _v.ValidateDay(req, Array.Empty<DateTime>(), 10);
        Assert.True(r.IsValid);
    }

    [Fact]
    public void Two_adjacent_ranges_ending_at_midnight_do_not_overlap()
    {

        var req = new SaveDayRequest
        {
            ProviderId = 1,
            IsClosed = false,
            SlotDurationMinutes = 10,
            TimeRanges = new List<TimeRangeWriteDto>
            {
                new() { StartTime = "09:00", EndTime = "23:50" },
                new() { StartTime = "23:50", EndTime = "00:00" }
            }
        };
        var r = _v.ValidateDay(req, Array.Empty<DateTime>(), 10);
        Assert.True(r.IsValid);
    }

    [Fact]
    public void Full_day_range_midnight_to_midnight_divides_evenly_for_ten_min_slots()
    {

        var req = new SaveDayRequest
        {
            ProviderId = 1,
            IsClosed = false,
            SlotDurationMinutes = 10,
            TimeRanges = new List<TimeRangeWriteDto> { new() { StartTime = "00:00", EndTime = "00:00" } }
        };
        var r = _v.ValidateDay(req, Array.Empty<DateTime>(), 10);
        Assert.True(r.IsValid);
    }
}
