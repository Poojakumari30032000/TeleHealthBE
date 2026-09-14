using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.ProviderSchedules
{
    public class GetAllProviderScheduledSlotsRequestDTO
    {
        public long? ProviderScheduleId { get; set; }
        public long? ProviderId { get; set; }
        public long? ServiceId { get; set; }
        public DateTime? Date {  get; set; }
        public int? Duration { get; set; }
        public string? AppointmentStatus { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 100;

        public int? ClientTimezoneOffsetMinutes { get; set; }
    }
}
