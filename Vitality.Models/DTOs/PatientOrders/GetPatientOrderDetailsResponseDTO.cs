using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.PatientOrders
{
    public class GetPatientOrderDetailsResponseDTO
    {
        public string? OrderStatus { get; set; }
        public DateTime? DateCreated { get; set; }
        public List<OrderProductListDTO>? ProductList { get; set; }
        public decimal? TotalAmount { get; set; }
    }
    public class OrderProductListDTO
    {
        public string? Item { get; set; }
        public decimal? Amount { get; set; }
        public decimal? Discount { get; set; }
    }
}
