using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.Products
{
    public class GetAllDrugIngredientsResponseDTO
    {
        public long DrugIngredientId { get; set; }
        public long? DurgId { get; set; }
        public string? IngredientName { get; set; }
        public string? IngredientStrength { get; set; }
        public string? Status { get; set; }
    }
}
