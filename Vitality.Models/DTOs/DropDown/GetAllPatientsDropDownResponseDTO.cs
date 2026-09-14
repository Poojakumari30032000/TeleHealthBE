using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.DropDown
{
    public class GetAllPatientsDropDownResponseDTO
    {
        public long? PatientId { get; set; }
        public string? PatientName { get; set; }
        public long? FacilityId { get; set; }
    }
}
