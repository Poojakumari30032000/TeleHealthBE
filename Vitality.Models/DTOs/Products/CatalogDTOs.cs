using System.Collections.Generic;

namespace DudeMeds.Models.DTOs.Products
{
    public class SaveCatalogRequestDTO
    {
        public long CatalogId { get; set; }
        public string? CatalogName { get; set; }
        public string? Description { get; set; }
        public List<long>? FacilityIds { get; set; }
    }

    public class GetAllCatalogsRequestDTO
    {
        public long? FacilityId { get; set; }
        public string? SearchText { get; set; }
    }

    public class CatalogResponseDTO
    {
        public long CatalogId { get; set; }
        public string CatalogName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public bool IsSystemDefined { get; set; }
        public List<long> FacilityIds { get; set; } = new List<long>();
    }

    public class UpdateCatalogStatusRequestDTO
    {
        public long CatalogId { get; set; }
        public bool IsActive { get; set; }
    }
}
