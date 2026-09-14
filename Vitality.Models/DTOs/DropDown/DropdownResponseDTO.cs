using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.DropDown
{
    public class DropdownResponseDTO
    {
        public long Id { get; set; }
        public string? Name { get; set; }
        public string? ShortName { get; set; }
    }
}
