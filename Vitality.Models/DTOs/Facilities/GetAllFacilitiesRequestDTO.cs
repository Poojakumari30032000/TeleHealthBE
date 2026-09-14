using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.Facilities
{
    public class GetAllFacilitiesRequestDTO
    {
        public string? Title { get; set; } = null;
        public string? Status { get; set; } = null;
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 100;
    }
}
