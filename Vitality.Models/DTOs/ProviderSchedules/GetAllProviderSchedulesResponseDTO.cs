using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.ProviderSchedules
{
    public class GetAllProviderSchedulesResponseDTO
    {
        public long? ProviderScheduleId { get; set; }
        public string? Title { get; set; }
        public long? ProviderId {  get; set; }
        public string? ProviderName { get; set; }
        public DateTime? StartDate  { get; set; }
        public bool? IsRecurrence {  get; set; }
        public int? SlotCount { get; set; }
        public int? Duration { get; set; }
    }
}
