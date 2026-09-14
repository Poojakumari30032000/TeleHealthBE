using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class PT_MedicineSupply
    {
        public long MedicineSupplyId { get; set; }
        public long PrescriptionMedicineId { get; set; }
        public string? SupplyDesc { get; set; }
        public string? SupplyQuantity { get; set; }
        public string? SupplyItemDesignatorID { get; set; }
        public string? Name { get; set; }
        public string? Direction { get; set; }
        public decimal? WholesalePrice { get; set; }
        public decimal? TotalAmount { get; set; }
    }
}
