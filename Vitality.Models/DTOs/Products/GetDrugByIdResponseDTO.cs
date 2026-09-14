using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.Products
{
    public class GetDrugByIdResponseDTO
    {
        public long DrugId { get; set; }
        public long ProductId { get; set; }
        public long? CatalogId { get; set; }
        public string? CatalogName { get; set; }
        public long? CategoryId { get; set; }
        public string? Type { get; set; }
        public string? Name { get; set; }
        public string? BrandName { get; set; }
        public string? ShortName { get; set; }
        public string? LablerName { get; set; }
        public string? GenericName { get; set; }
        public long? PharmacyId { get; set; }
        public decimal? Price { get; set; }
        public decimal? ComparePrice { get; set; }
        public int? Quantity { get; set; }
        public string? QuantityUnit { get; set; }
        public int? Refills { get; set; }
        public string? Dose { get; set; }
        public string? Dosage { get; set; }
        public string? Strenght { get; set; }
        public string? Sig { get; set; }
        public string? ShippingFrequency { get; set; }
        public string? BillingFrequency { get; set; }
        public string? Packing { get; set; }
        public string? Form { get; set; }
        public string? UPC { get; set; }
        public string? PackageNDC { get; set; }
        public int? DosageOrdering { get; set; }
        public string? Instruction { get; set; }
        public string? RegularImageURL { get; set; }
        public string? TransparentBackgroundImageURL { get; set; }
        public string? VideoURL { get; set; }
        public string? Status { get; set; }
        public long? CreatedBy { get; set; }
        public string? CreatedByName { get; set; }
        public string? Guid { get; set; }

        public string? PackageSize { get; set; }
        public decimal? Markup { get; set; }
        public string? MarkupType { get; set; }
        public bool? ControlSubstance { get; set; }
        public string? DosageForm { get; set; }
        public string? ItemDesignatorID { get; set; }
        public bool? Refrigerated { get; set; }
        public decimal? SuggestedRetail { get; set; }

        public List<SaveDrugIngredientRequestDTO>? drugIngredient { get; set; }
        public List<SaveDrugVarientRequestDTO>? drugVarient { get; set; }
    }
}
