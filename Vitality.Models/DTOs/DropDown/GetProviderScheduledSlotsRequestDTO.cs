using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.DropDown
{
    public class GetProviderScheduledSlotsRequestDTO
    {
        public long? CategoryId { get; set; }
        public DateTime? Date {  get; set; }
        public long? FacilityId { get; set; }
        public long? ProviderId { get; set; }

        public DateTime? ClientCurrentTime { get; set; }

        public int? ClientTimezoneOffsetMinutes { get; set; }
    }
    public class GetProviderScheduledSlotsByProviderRequestDTO
    {
        public long? ProviderId { get; set; }
        public DateTime? Date { get; set; }
        public long? FacilityId { get; set; }
        public int? ClientTimezoneOffsetMinutes { get; set; }
    }
}
