using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class PT_PrescriptionMedicine
    {
        public long PrescriptionMedicineId { get; set; }
        public long PatientPrescriptionId { get; set; }
        public string MedicineName { get; set; } = null!;
        public long? DrugId { get; set; }
        public long? CatalogId { get; set; }
        public string? DaysSupplies { get; set; }
        public string? Injection { get; set; }
        public string? InjectionQuantity { get; set; }
        public string? Needle { get; set; }
        public string? NeedleQuantity { get; set; }
        public string? Direction { get; set; }
        public string? Instruction { get; set; }
        public string? Quantity { get; set; }
        public string? ItemDesignatorID { get; set; }
        public string? strenght { get; set; }
        public string? DosageForm { get; set; }
        public string? PackageSize { get; set; }
        public bool? ControlSubstance { get; set; }
        public string? CourierMethod { get; set; }
        public decimal? WholesalePrice { get; set; }
        public decimal? TotalAmount { get; set; }

        public virtual PT_PatientPrescription PatientPrescription { get; set; } = null!;
    }
}
