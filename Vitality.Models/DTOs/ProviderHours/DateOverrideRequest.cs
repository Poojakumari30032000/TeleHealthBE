using System;
using System.Collections.Generic;

namespace DudeMeds.Models.DTOs.ProviderHours
{

    public class DateOverrideRequest
    {
        public long ProviderId { get; set; }
        public DateTime Date { get; set; }
        public bool IsClosed { get; set; }
        public string? Note { get; set; }
        public int? SlotDurationMinutes { get; set; }
        public List<TimeRangeWriteDto> TimeRanges { get; set; } = new List<TimeRangeWriteDto>();
    }
}
