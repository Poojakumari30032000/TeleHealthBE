using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.DropDown
{
    public class GetAllPatientOrdersDropDownResponseDTO
    {
        public long? PatientOrderId { get; set; }
        public DateTime? OrderDate { get; set; }
    }
}
