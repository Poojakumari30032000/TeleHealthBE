using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DudeMeds.Models.DTOs.ProviderHours;

namespace Vitality.Models.Repos.Services.Validators
{

    public class ProviderHoursValidator
    {
        public ValidationResult ValidateDay(SaveDayRequest req, IReadOnlyList<DateTime> bookedSlotStartsLocal, int? templateDefaultDurationMinutes)
        {
            var result = new ValidationResult();

            if (req.IsClosed)
            {
                if (req.TimeRanges != null && req.TimeRanges.Count > 0)
                    result.Add(ValidationCode.ClosedDayMustHaveNoRanges, "A closed day cannot have time ranges.");

                if (bookedSlotStartsLocal != null && bookedSlotStartsLocal.Count > 0)
                {
                    result.Add(
                        ValidationCode.CannotCloseDayWithBookings,
                        $"Cannot mark this day closed: {bookedSlotStartsLocal.Count} booked slot(s) exist. Create a per-date override or cancel the bookings first.");
                }
                return result;
            }

            ValidateRangesShape(req.TimeRanges, req.SlotDurationMinutes ?? templateDefaultDurationMinutes, result);
            ValidateBookedSlotsStillFit(req.TimeRanges, bookedSlotStartsLocal, result);
            return result;
        }

        public ValidationResult ValidateDateOverride(DateOverrideRequest req, IReadOnlyList<DateTime> bookedSlotStartsLocal, int? templateDefaultDurationMinutes)
        {
            var result = new ValidationResult();

            if (req.IsClosed)
            {
                if (bookedSlotStartsLocal != null && bookedSlotStartsLocal.Count > 0)
                {
                    result.Add(
                        ValidationCode.CannotCloseDateWithBookings,
                        $"Cannot close this date: {bookedSlotStartsLocal.Count} booked slot(s) exist. Cancel the bookings first.");
                }
                return result;
            }

            ValidateRangesShape(req.TimeRanges, req.SlotDurationMinutes ?? templateDefaultDurationMinutes, result);
            ValidateBookedSlotsStillFit(req.TimeRanges, bookedSlotStartsLocal, result);
            return result;
        }

        private static void ValidateRangesShape(List<TimeRangeWriteDto> ranges, int? slotDurationMinutes, ValidationResult result)
        {
            if (ranges == null || ranges.Count == 0)
            {
                result.Add(ValidationCode.OpenDayMustHaveRanges, "An open day must have at least one time range.");
                return;
            }

            var parsed = new List<(TimeSpan Start, TimeSpan End, int Index)>();
            for (var i = 0; i < ranges.Count; i++)
            {
                var r = ranges[i];
                if (!TryParseTime(r.StartTime, out var start) || !TryParseEndTime(r.EndTime, out var end))
                {
                    result.Add(ValidationCode.InvalidTimeFormat, $"Range #{i + 1} has an invalid time (expected HH:mm).");
                    continue;
                }
                if (end <= start)
                {
                    result.Add(ValidationCode.EndNotAfterStart, $"Range #{i + 1}: end time must be after start time.");
                    continue;
                }

                var duration = r.SlotDurationMinutes ?? slotDurationMinutes ?? 0;
                if (duration > 0)
                {
                    var totalMinutes = (int)(end - start).TotalMinutes;
                    if (totalMinutes % duration != 0)
                    {
                        result.Add(
                            ValidationCode.DurationDoesNotDivide,
                            $"Range #{i + 1}: slot duration {duration} min does not divide evenly into the {totalMinutes}-min range.");
                    }
                }

                parsed.Add((start, end, i + 1));
            }

            var ordered = parsed.OrderBy(p => p.Start).ToList();
            for (var i = 1; i < ordered.Count; i++)
            {
                if (ordered[i].Start < ordered[i - 1].End)
                {
                    result.Add(
                        ValidationCode.RangesOverlap,
                        $"Ranges #{ordered[i - 1].Index} and #{ordered[i].Index} overlap.");
                }
            }
        }

        private static void ValidateBookedSlotsStillFit(List<TimeRangeWriteDto> ranges, IReadOnlyList<DateTime> bookedSlotStartsLocal, ValidationResult result)
        {
            if (bookedSlotStartsLocal == null || bookedSlotStartsLocal.Count == 0) return;
            if (ranges == null || ranges.Count == 0) return;

            var parsedRanges = ranges
                .Where(r => TryParseTime(r.StartTime, out _) && TryParseEndTime(r.EndTime, out _))
                .Select(r =>
                {
                    TryParseTime(r.StartTime, out var s);
                    TryParseEndTime(r.EndTime, out var e);
                    return (Start: s, End: e);
                })
                .ToList();

            foreach (var bookedLocal in bookedSlotStartsLocal)
            {
                var tod = bookedLocal.TimeOfDay;
                var fits = parsedRanges.Any(r => tod >= r.Start && tod < r.End);
                if (!fits)
                {
                    result.Add(
                        ValidationCode.WouldOrphanBookedSlot,
                        $"Cannot save: the booked slot at {bookedLocal:yyyy-MM-dd HH:mm} would fall outside the new ranges. Cancel the booking first or keep a range that covers it.");
                }
            }
        }

        private static bool TryParseTime(string s, out TimeSpan ts)
        {

            return TimeSpan.TryParseExact(s, new[] { @"hh\:mm", @"hh\:mm\:ss", @"h\:mm" }, CultureInfo.InvariantCulture, out ts);
        }

        private static bool TryParseEndTime(string s, out TimeSpan ts)
        {
            if (!TryParseTime(s, out ts)) return false;
            if (ts == TimeSpan.Zero) ts = TimeSpan.FromHours(24);
            return true;
        }
    }

    public class ValidationResult
    {
        public List<ValidationError> Errors { get; } = new List<ValidationError>();
        public bool IsValid => Errors.Count == 0;

        public void Add(ValidationCode code, string message) => Errors.Add(new ValidationError(code, message));
    }

    public record ValidationError(ValidationCode Code, string Message);

    public enum ValidationCode
    {
        ClosedDayMustHaveNoRanges,
        OpenDayMustHaveRanges,
        EndNotAfterStart,
        DurationDoesNotDivide,
        RangesOverlap,
        InvalidTimeFormat,
        WouldOrphanBookedSlot,
        CannotCloseDayWithBookings,
        CannotCloseDateWithBookings,
        InvalidTimezone,
        InvalidDayOfWeek
    }
}
