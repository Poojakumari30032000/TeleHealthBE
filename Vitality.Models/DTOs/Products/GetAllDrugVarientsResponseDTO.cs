using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.Products
{
    public class GetAllDrugVarientsResponseDTO
    {
        public long DrugVarientId { get; set; }
        public long? DrugId { get; set; }
        public string? VarientName { get; set; }
        public decimal? Price { get; set; }
        public decimal? ComparePrice { get; set; }
        public string? Quantity { get; set; }
        public string? QuantityUnit { get; set; }
        public string? Refills { get; set; }
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
        public string? RegularImageURL { get; set; }
        public string? TransparentBackgroundImageURL { get; set; }
        public string? VideoURL { get; set; }
        public string? ItemDesignatorID { get; set; }
        public bool? Refrigerated { get; set; }
        public string? Status { get; set; }
    }
}
