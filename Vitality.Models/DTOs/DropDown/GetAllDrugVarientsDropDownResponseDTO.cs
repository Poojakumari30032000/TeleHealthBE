using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.DropDown
{
    public class GetAllDrugVarientsDropDownResponseDTO
    {
        public long? DrugVarientId { get; set; }
        public string? VarientName { get; set; }
    }
}
