using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitality.Models.DTOs.Products
{
    public class GetAllDrugsWithPricingResponseDTO
    {
        public long DrugId { get; set; }
        public long? CatalogId { get; set; }
        public string? CatalogName { get; set; }

        public string? PackageSize { get; set; }
        public decimal? Markup { get; set; }
        public bool? ControlSubstance { get; set; }
        public string? DosageForm { get; set; }
        public string? Name { get; set; }
        public string? Strenght { get; set; }
        public string? ItemDesignatorID { get; set; }
        public string? Status { get; set; }
        public bool? Refrigerated { get; set; }
        public decimal? ClinicSuggestedRetailPrice { get; set; }

        public decimal? SuggestedRetail { get; set; }
        public decimal? WholesalePrice { get; set; }
        public long? GAtoClinicId { get; set; }
        public string? MarkupType { get; set; }
        public bool? IsCustom { get; set; }

    }
}
