using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.DropDown
{
    public class GetAllInTakeFormProductsDropDownResponseDTO
    {
        public long? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public long? ConditionId { get; set; }
        public string? ConditionName { get; set; }
        public long? ProductId { get; set; }
        public string? ProductName { get; set; }
        public string? ProductType { get; set; }
        public string? DrugType { get; set; }
        public long? DrugVarientId { get; set; }
        public string? DrugVarientName { get; set; }
        public decimal? Price { get; set; }
        public int? Quantity { get; set; }
        public string? QuantityUnit {  get; set; }
        public string? ImageURL { get; set; }
    }
}
