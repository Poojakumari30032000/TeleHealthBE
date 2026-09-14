using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.Products
{
    public class GetProductInfoByNameResponseDTO
    {
        public long? ProductId { get; set; }
        public string? ProductName { get; set; }
        public string? ProductType { get; set; }
        public long? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public string? DrugType { get; set; }
        public decimal? Price { get; set; }
        public int? Quantity { get; set; }
        public string? QuantityUnit { get; set; }
        public int? Refills { get; set; }
        public string? Dose { get; set; }
        public string? Dosage { get; set; }
        public string? Strenght { get; set; }
        public string? ShippingFrequency { get; set; }
        public string? BillingFrequency { get; set; }
        public string? RegularImageURL { get; set; }
    }
}
