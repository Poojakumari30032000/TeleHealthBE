using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.Products
{
    public class GetAllProductsForShoppingResponseDTO
    {
        public long? ProductId { get; set; }
        public string? ProductName { get; set; }
        public string? ProductType { get; set; }
        public string? ProductURL { get; set; }
        public decimal? ProductPrice { get; set; }
        public decimal? ComparePrice { get; set; }
        public int? Quantity { get; set; }
        public string? Dose { get; set; }
        public string? Dosage { get; set; }
        public string? RegularImageURL { get; set; }
        public string? GenericName { get; set; }
        public long? CategoryId { get; set; }
        public long? DrugId { get; set; }
        public long? BundleId { get; set; }
    }
}
