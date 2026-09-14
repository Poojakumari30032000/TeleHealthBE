using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.ProviderSchedules
{
    public class GetAllProviderScheduledSlotsByMonthResponseDTO
    {
        public long? ProviderScheduledSlotId { get; set; }
        public long? ProviderId { get; set; }
        public string? ProviderName { get; set; }
        public DateTime? Date { get; set; }
        public string? StartTime { get; set; }
        public string? EndTime { get; set; }
        public int? Duration { get; set; }
        public bool? IsAppointment { get; set; }
    }
}
