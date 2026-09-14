using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.ProviderSchedules
{
    public class SaveRescheduleProviderScheduledSlotRequestDTO
    {
        public long? ProviderScheduledSlotId { get; set; }
        public DateTime? StartDate { get; set; }
        public string? StartTime { get; set; }
        public string? EndTime { get; set; }
        public int? Duration { get; set; }
    }
}
