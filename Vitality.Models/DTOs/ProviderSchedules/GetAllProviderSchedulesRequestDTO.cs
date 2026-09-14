using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.ProviderSchedules
{

    public class GetAllProviderSchedulesRequestDTO
    {
        public long? FacilityId { get; set; }
        public long? ProviderId { get; set; }
        public long? ServiceId { get; set; }
        public string? Title { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int PageSize { get; set; }
        public int PageNumber { get; set; }

        public int? ClientTimezoneOffsetMinutes { get; set; }
    }

}
