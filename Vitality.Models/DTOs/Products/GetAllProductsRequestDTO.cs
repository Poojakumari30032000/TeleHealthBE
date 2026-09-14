using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.Products
{
    public class GetAllProductsRequestDTO
    {
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public string? Title { get; set; }
        public string? Status { get; set; }
        public bool? ControlSubstance { get; set; }

        public long? FacilityId { get; set; }
        public long? CatalogId { get; set; }

    }
    public class GetAllBundlesRequestDTO
    {
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public string? Title { get; set; }
        public string? Status { get; set; }
        public long? FacilityId { get; set; }
        public long? CategoryId { get; set; }
    }
}
