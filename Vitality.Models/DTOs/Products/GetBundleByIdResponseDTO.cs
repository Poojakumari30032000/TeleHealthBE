using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.Products
{
    public class GetBundleByIdResponseDTO
    {
        public long BundleId { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public long? DrugId { get; set; }
        public List<SaveDrugVarientsInBundlesDTO>? DrugVarientsInBundle { get; set; }
        public decimal? Price { get; set; }
        public decimal? ComparePrice { get; set; }
        public string? RegularImageURL { get; set; }
        public string? Status { get; set; }
        public long? CreatedBy { get; set; }
        public string? CreatedByName { get; set; }
        public long? CategoryId { get; set; }
        public int? visits { get; set; }

        public List<long>? FacilityIds { get; set; }
    }

}
