using System;
using System.Collections.Generic;

namespace DudeMeds.Models.DTOs.ProviderHours
{

    public class WeekViewDto
    {
        public long ProviderId { get; set; }
        public string Timezone { get; set; } = "";
        public DateTime WeekStartDate { get; set; }
        public int DefaultSlotDurationMinutes { get; set; }
        public List<DayHoursDto> Days { get; set; } = new List<DayHoursDto>();
    }

    public class DayHoursDto
    {
        public DateTime Date { get; set; }

        public byte DayOfWeek { get; set; }
        public bool IsClosed { get; set; }

        public bool IsOverride { get; set; }

        public string? Note { get; set; }
        public int? SlotDurationMinutes { get; set; }
        public List<TimeRangeDto> TimeRanges { get; set; } = new List<TimeRangeDto>();
        public int BookedSlotCount { get; set; }
    }

    public class TimeRangeDto
    {
        public long? Id { get; set; }

        public string StartTime { get; set; } = "";
        public string EndTime { get; set; } = "";
        public int? SlotDurationMinutes { get; set; }

        public bool HasBookings { get; set; }
    }
}
