using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.Products
{
    public class SaveDigitalProductRequestDTO
    {
        public long DigitalProductId { get; set; }
        public string? Name { get; set; }
        public long? FacilityId { get; set; }
    }
}
