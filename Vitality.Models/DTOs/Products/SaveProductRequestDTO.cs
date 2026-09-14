using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.Products
{
    public class SaveProductRequestDTO
    {
        public long ProductId { get; set; }
        public long? ConditionId { get; set; }
        public long? CategoryId { get; set; }
        public string? ProductType { get; set; }
        public string? ProductGroup { get; set; }
        public string? Status { get; set; }
        public SaveProductDrugsDTO? productDrugs { get; set; }
    }
    public class SaveProductDrugsDTO
    {
        public string? DrugName { get; set; }
        public string? BrandName { get; set; }
        public string? ShortName { get; set; }
        public string? LablerName { get; set; }
        public string? GenericName { get; set; }
        public string? DosageForm { get; set; }
        public decimal? ConsultPrice { get; set; }
        public List<SaveDrugIngredientRequestDTO>? drugIngradient{get; set;}
        public List<SaveDrugVarientRequestDTO>? drugVarient{get; set;}
    }
}
