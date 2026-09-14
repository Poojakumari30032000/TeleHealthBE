using System.Collections.Generic;

namespace DudeMeds.Models.DTOs.ProviderHours
{

    public class SaveDayRequest
    {
        public long ProviderId { get; set; }
        public bool IsClosed { get; set; }
        public int? SlotDurationMinutes { get; set; }
        public List<TimeRangeWriteDto> TimeRanges { get; set; } = new List<TimeRangeWriteDto>();
    }

    public class TimeRangeWriteDto
    {

        public long? Id { get; set; }
        public string StartTime { get; set; } = "";
        public string EndTime { get; set; } = "";
        public int? SlotDurationMinutes { get; set; }
    }
}
