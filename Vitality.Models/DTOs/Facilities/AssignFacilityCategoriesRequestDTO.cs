using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitality.Models.DTOs.Facilities
{
    public class AssignFacilityCategoriesRequestDTO
    {
        public long FacilityId { get; set; }
        public List<long> CategoryIds { get; set; } = new();
    }

    public class UnassignFacilityCategoriesRequestDTO
    {
        public long FacilityId { get; set; }
        public List<long> CategoryIds { get; set; } = new();
    }

    public class GetAssignedCategoryResponseDTO
    {
        public long CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public string? CategoryDescription { get; set; }
        public string? ImageURL { get; set; }
    }
}
