using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.DropDown
{
    public class GetProviderScheduledSlotsResponseDTO
    {
        public long? ProviderScheduledSlotId { get; set; }
        public DateTime? SlotDate { get; set; }
        public string? StartTime { get; set; }
        public string? EndTime { get; set; }
        public int? Duration { get; set; }
        public long? ProviderId { get; set; }
        public long? FacilityId { get; set; }
        public string? ProviderName { get; set; }
    }
}
