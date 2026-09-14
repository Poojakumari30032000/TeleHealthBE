using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.ProviderSchedules
{
    public class GetProviderScheduledSlotTimesRequestDTO
    {
        public long? ProviderId { get; set; }
        public DateTime? SlotDate { get; set; }
    }
}
