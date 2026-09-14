using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.ProviderSchedules
{
    public class GetProviderScheduleByIdResponseDTO
    {
        public long ProviderScheduleId { get; set; }
        public long? FacilityId { get; set; }
        public string? Title { get; set; }
        public long? ProviderId { get; set; }
        public DateTime? SlotDate { get; set; }
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }
        public int? Duration { get; set; }
        public bool? IsRecurrence { get; set; }
        public int? RecurrenceWeek { get; set; }
        public List<string>? RecurrenceDays { get; set; }
        public int? RecurrenceEndType { get; set; }
        public DateTime? RecurrenceEndDate { get; set; }
        public int? RecurrenceEndSlot { get; set; }
    }
}
