using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.Pharmacies
{
    public class GetAllPharmaciesRequestDTO
    {
        public int PageSize { get; set; }
        public int PageNumber { get; set; }
        public string? PharmacyName { get; set; }
        public string? Status { get; set; }
    }
}
