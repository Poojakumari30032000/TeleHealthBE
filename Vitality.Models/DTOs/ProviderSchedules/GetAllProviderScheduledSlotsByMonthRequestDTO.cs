using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.ProviderSchedules
{
    public class GetAllProviderScheduledSlotsByMonthRequestDTO
    {
        public int ScheduledMonth { get; set; }
        public int ScheduledYear { get; set; }
        public long? FacilityId { get; set; }
        public long? ProviderId { get; set; }
    }
}
