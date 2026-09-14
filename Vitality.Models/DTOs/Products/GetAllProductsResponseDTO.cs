using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.Products
{
    public class GetAllProductsResponseDTO
    {
        public string? ProductGuid { get; set; }
        public long? ProductId {  get; set; }
        public string? ProductType { get; set; }
        public string? PharmacyName { get; set; }
        public string? GenericName { get; set; }
        public string? Status { get; set; }
        public string? Guid { get; set; }
        public int? IngrdientCount { get; set; }
        public long DrugId { get; set; }
        public long? PharmacyId { get; set; }

        public long? CategoryId { get; set; }
        public long? CatalogId { get; set; }
        public string? CatalogName { get; set; }

        public string? DrugType { get; set; }
        public string? Name { get; set; }
        public string? BrandName { get; set; }

        public string? DosageForm { get; set; }
        public string? Strenght { get; set; }
        public string? PackageSize { get; set; }
        public int? Quantity { get; set; }
        public string? QuantityUnit { get; set; }
        public int? Refills { get; set; }

        public decimal? Price { get; set; }
        public decimal? ComparePrice { get; set; }

        public bool? ControlSubstance { get; set; }
        public bool? Refrigerated { get; set; }
        public string? ItemDesignatorID { get; set; }

        public decimal? Markup { get; set; }

        public decimal? SuggestedRetail { get; set; }
        public decimal? CustomerSuggestedRetailPrice { get; set; }

    }

    public class GetAllBundleResponseDTO
    {
        public long? BundleId { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Status { get; set; }
        public string? RegularImageUrl { get; set; }
        public long? CategoryId { get; set; }
        public long? CatalogId { get; set; }
        public string? CatalogName { get; set; }
        public string? CategoryName { get; set; }
        public long? FacilityId { get; set; }
        public string? FacilityName { get; set; }

        public string? AssignedFacilities { get; set; }

        public List<long>? FacilityIds { get; set; }
        public long? Visits { get; set; }
        public decimal? Price { get; set; }
        public decimal? ComparePrice { get; set; }
    }

    public class GetAllDrugsForGlobalAdminResponseDTO
    {

        public long DrugId { get; set; }
        public long? ProductId { get; set; }
        public long? PharmacyId { get; set; }
        public long? CatalogId { get; set; }
        public string? CatalogName { get; set; }
        public long? CategoryId { get; set; }

        public string? DrugType { get; set; }
        public string? Name { get; set; }
        public string? BrandName { get; set; }
        public string? GenericName { get; set; }

        public string? DosageForm { get; set; }
        public string? Strenght { get; set; }
        public string? PackageSize { get; set; }
        public int? Quantity { get; set; }
        public string? QuantityUnit { get; set; }
        public int? Refills { get; set; }

        public decimal? Price { get; set; }
        public decimal? ComparePrice { get; set; }

        public bool? ControlSubstance { get; set; }
        public bool? Refrigerated { get; set; }
        public string? ItemDesignatorID { get; set; }
        public string? Status { get; set; }

        public decimal? Markup { get; set; }

        public long? PharmToGlobalId { get; set; }
        public decimal? PharmacyPrice { get; set; }
        public decimal? MarkupPercent { get; set; }
        public decimal? WholesalePrice { get; set; }
        public string? MarkupType { get; set; }

        public long? GAtoClinicIdGlobal { get; set; }
        public decimal? SuggestedRetail { get; set; }

        public long? ClinicToPatientId { get; set; }
        public long? GAtoClinicId { get; set; }
        public decimal? CustomerSuggestedRetailPrice { get; set; }
        public bool? IsCustom { get; set; }
    }

    public class GetDrugByIdResponse2DTO
    {

        public long DrugId { get; set; }
        public long? ProductId { get; set; }
        public long? CategoryId { get; set; }
        public long? PharmacyId { get; set; }
        public long? CatalogId { get; set; }
        public string? CatalogName { get; set; }
        public string? Type { get; set; }
        public string? Name { get; set; }
        public string? BrandName { get; set; }
        public string? GenericName { get; set; }
        public string? DosageForm { get; set; }
        public string? Strenght { get; set; }
        public string? PackageSize { get; set; }
        public int? Quantity { get; set; }
        public string? QuantityUnit { get; set; }
        public int? Refills { get; set; }
        public bool? ControlSubstance { get; set; }
        public bool? Refrigerated { get; set; }
        public string? ItemDesignatorID { get; set; }
        public string? Status { get; set; }

        public long? PharmToGlobalId { get; set; }
        public decimal? PharmacyPrice { get; set; }
        public decimal? MarkupPercent { get; set; }
        public decimal? WholesalePrice { get; set; }
        public string? MarkupType { get; set; }

        public long? GAtoClinicIdGlobal { get; set; }
        public decimal? SuggestedRetail { get; set; }

        public long? ClinicToPatientId { get; set; }
        public long? GAtoClinicId { get; set; }
        public decimal? CustomerSuggestedRetailPrice { get; set; }
        public bool? IsCustom { get; set; }
    }

    public class UpdateDrugRequestDTO
    {
        public long DrugId { get; set; }
        public long? CategoryId { get; set; }
        public string? Type { get; set; }
        public string? Name { get; set; }
        public string? BrandName { get; set; }
        public string? GenericName { get; set; }
        public string? DosageForm { get; set; }
        public string? Strenght { get; set; }
        public string? PackageSize { get; set; }
        public int? Quantity { get; set; }
        public string? QuantityUnit { get; set; }
        public int? Refills { get; set; }
        public bool? ControlSubstance { get; set; }
        public bool? Refrigerated { get; set; }
        public string? ItemDesignatorID { get; set; }
        public string? Status { get; set; }
    }

    public class GetBundleByIdResponse2DTO
    {
        public long BundleId { get; set; }
        public long? ProductId { get; set; }
        public long? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? RegularImageURL { get; set; }
        public decimal? Price { get; set; }
        public decimal? ComparePrice { get; set; }
        public string? Status { get; set; }
        public int? Visits { get; set; }

        public List<long>? FacilityIds { get; set; }
    }

    public class UpdateBundleRequestDTO
    {
        public long BundleId { get; set; }
        public long? CategoryId { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? RegularImageURL { get; set; }
        public decimal? Price { get; set; }
        public decimal? ComparePrice { get; set; }
        public string? Status { get; set; }
    }

}
