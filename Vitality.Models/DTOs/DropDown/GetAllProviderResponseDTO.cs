using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.DropDown
{
    public class GetAllProviderResponseDTO
    {
        public long? ProviderId { get; set; }
        public string? Name { get; set; }
        public string? ProfileUrl { get; set; }
        public string? Bio { get; set; }
    }
}
