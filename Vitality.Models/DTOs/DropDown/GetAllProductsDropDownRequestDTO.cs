using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.DropDown
{
    public class GetAllProductsDropDownRequestDTO
    {
        public long? CategoryId { get; set; }
        public bool? IsBundle { get; set; }
        public long? BundleId { get; set; }
        public long? FacilityId { get; set; }
        public long? CatalogId { get; set; }
        public string? SearchTerm { get; set; }
    }
}
