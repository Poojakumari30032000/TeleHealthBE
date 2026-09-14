using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.DropDown
{
    public class GetAllProviderRequestDTO
    {
        public bool? IsSupervisor {  get; set; }
        public bool? IsAssign {  get; set; }
        public long? FacilityId { get; set; }
        public long? CategoryId { get; set; }
    }
}
