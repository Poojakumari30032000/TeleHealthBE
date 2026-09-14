using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitality.Models.DTOs.Bundles
{
    public class GetAllBundlesByFacilityRequestDTO
    {
        public long FacilityId { get; set; }
        public int? CategoryId { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? Status { get; set; }
        public string? Title { get; set; }
        public string? ProductType { get; set; }
    }

    public class GetAllBundleByFacilityResponseDTO
    {
        public long BundleId { get; set; }
        public string? Name { get; set; }
        public long? CategoryId { get; set; }
        public string? CategoryName { get; set; }

        public decimal? Price { get; set; }
        public decimal? ComparePrice { get; set; }

        public decimal? ClinicPrice { get; set; }

        public string? Description { get; set; }
        public string? RegularImageUrl { get; set; }
        public string? Status { get; set; }

        public int? Duration { get; set; }
        public bool? IsRecurring { get; set; }
    }
    public class EditClinicBundlePriceRequestDTO
    {
        public long FacilityId { get; set; }
        public long BundleId { get; set; }
        public decimal ClinicPrice { get; set; }
        public bool? IsRecurring { get; set; }
        public long? UserId { get; set; }
    }
}
