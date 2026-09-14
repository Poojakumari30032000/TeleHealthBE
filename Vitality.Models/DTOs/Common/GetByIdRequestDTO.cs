using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.Common
{
    public class GetByIdRequestDTO
    {
        public long Id { get; set; }
        public long? FacilityId { get; set; }

        public int? ClientTimezoneOffsetMinutes { get; set; }
    }
}
