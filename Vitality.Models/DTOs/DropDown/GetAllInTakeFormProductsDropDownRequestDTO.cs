using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.DropDown
{
    public class GetAllInTakeFormProductsDropDownRequestDTO
    {
        public string? FacilityGuid { get; set; }
        public long? CategoryId { get; set; }
    }
}
