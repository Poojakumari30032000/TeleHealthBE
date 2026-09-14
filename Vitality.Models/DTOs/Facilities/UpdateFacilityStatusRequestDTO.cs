using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.Facilities
{
    public class UpdateFacilityStatusRequestDTO
    {
        public long? FacilityId {  get; set; }
        public string? Status { get; set; }
    }
}
