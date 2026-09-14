using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitality.Models.DTOs.ProductCategories
{

    public class BundleDTO
    {
        public long BundleId { get; set; }
        public string Name { get; set; }
        public string? Description { get; set; }
        public string BrandName { get; set; }
        public decimal Price { get; set; }
        public string Dosage { get; set; }
        public string QuantityUnit { get; set; }
        public bool? IsRecurring { get; set; }
        public int? Duration { get; set; }
    }
}
