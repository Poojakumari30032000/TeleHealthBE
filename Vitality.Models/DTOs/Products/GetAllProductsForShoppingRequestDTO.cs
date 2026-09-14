using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.Products
{
    public class GetAllProductsForShoppingRequestDTO
    {
        public long? OrganizationId { get; set; }
        public string? Title { get; set; }
        public long? CategoryId { get; set; }
        public string? ProductType { get; set; }
    }
}
