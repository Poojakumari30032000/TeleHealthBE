using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.Products
{
    public class GetProductInfoByNameRequestDTO
    {
        public string? FacilityGuid { get; set; }
        public string? ProductName { get; set; }
    }
}
