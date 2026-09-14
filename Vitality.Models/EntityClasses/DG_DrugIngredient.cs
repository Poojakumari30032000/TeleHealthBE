using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class DG_DrugIngredient
    {
        public long DrugIngredientId { get; set; }
        public long? DrugId { get; set; }
        public string? IngredientName { get; set; }
        public string? IngredientStrength { get; set; }
        public string? Status { get; set; }
        public bool? IsActive { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
    }
}
