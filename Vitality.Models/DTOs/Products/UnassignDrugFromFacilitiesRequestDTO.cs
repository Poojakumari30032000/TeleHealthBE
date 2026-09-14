using System.Collections.Generic;

namespace Vitality.Models.DTOs.Products
{
    public class UnassignDrugFromFacilitiesRequestDTO
    {
        public long DrugId { get; set; }
        public List<long> FacilityIds { get; set; } = new();
    }
}
