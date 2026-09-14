using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.DropDown
{
    public class GetAllProductsDropDownResponseDTO
    {
        public long? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public long? ProductId { get; set; }
        public string? ProductName { get; set; }
        public string? ProductType { get; set; }
        public string? DrugType { get; set; }

    }
    public class GetAllDrugsDropDownResponseDTO
    {
        public long? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public long? ProductId { get; set; }
        public long? DrugId { get; set; }
        public string? ProductName { get; set; }
        public string? ProductType { get; set; }
        public string? DrugType { get; set; }
        public string? DosageForm { get; set; }
        public string? PackageSize { get; set; }
        public decimal? Markup { get; set; }
        public bool? Control_Substance { get; set; }
        public bool? ControlSubstance { get; set; }
        public decimal? SuggestedRetail { get; set; }
        public decimal? Price { get; set; }
        public decimal? ComparePrice { get; set; }
        public string? Strenght { get; set; }
        public string? ItemDesignatorID { get; set; }
        public bool? Refrigerated { get; set; }
        public decimal? WholesalePrice { get; set; }

    }
}
