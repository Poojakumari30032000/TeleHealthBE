using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.DropDown
{
    public class GetAllFacilitiesDropDownResponseDTO
    {
        public long? FacilityId { get; set; }
        public string? Titlelong { get; set; }
        public string? Titleshort { get; set; }
        public string? Guid { get; set; }
        public long? OrganizationId { get; set; }
        public string? OrganizationName { get; set; }
    }
}
