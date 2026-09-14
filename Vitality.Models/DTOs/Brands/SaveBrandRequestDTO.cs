using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitality.Models.DTOs.Brands
{
    public class SaveBrandRequestDTO
    {
        public long BrandId { get; set; }
        public long? FacilityId { get; set; }
        public string? Logo { get; set; }
        public string? PrimaryColor { get; set; }
        public List<string>? PrimaryColorVariants { get; set; }
        public string? SecondaryColor { get; set; }
        public List<string>? ChartColors { get; set; }
        public string? FontStyle { get; set; }
    }
}
