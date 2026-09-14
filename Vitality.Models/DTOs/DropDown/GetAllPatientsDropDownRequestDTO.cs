using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.DropDown
{
    public class GetAllPatientsDropDownRequestDTO
    {
        public long? FacilityId { get; set; }
        public long? ProviderId { get; set; }
    }
}
