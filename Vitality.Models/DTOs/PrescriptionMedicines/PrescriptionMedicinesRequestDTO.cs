using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Vitality.Models.DTOs.PrescriptionMedicines
{
    public class PrescriptionMedicinesRequestDTO
    {

        public long PrescriptionMedicineId { get; set; }
        public long PatientPrescriptionId { get; set; }
        public string MedicineName { get; set; } = null!;
        public long? DrugId { get; set; }
        public long? CatalogId { get; set; }
        public bool? IsCustom { get; set; }
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

        public List<MedicineSupplyItemDTO>? Supplies { get; set; }

    }
    public class MedicineSupplyItemDTO
    {

        public JsonElement SupplyDesc { get; set; }
        public string? SupplyQuantity { get; set; }
        public string? Name { get; set; }
        public string? Direction { get; set; }
        public string? SupplyItemDesignatorID { get; set; }
        public decimal? WholesalePrice { get; set; }
        public decimal? TotalAmount { get; set; }
    }

    public class CreatePrescriptionMedicineRequestDTO
    {
        public long PatientPrescriptionId { get; set; }
        public string MedicineName { get; set; } = string.Empty;
        public string? ItemDesignatorID { get; set; }

    }

    public class BulkCreatePrescriptionMedicineRequestDTO
    {
        public long PatientPrescriptionId { get; set; }
        public List<PrescriptionMedicinesRequestDTO>? Medicines { get; set; }
    }

    public class UpdatePrescriptionMedicineNameRequestDTO
    {
        public long PrescriptionMedicineId { get; set; }
        public string MedicineName { get; set; } = string.Empty;
        public string? ItemDesignatorID { get; set; }

    }

    public class MedicineSupplyDTO
    {
        public long MedicineSupplyId { get; set; }
        public long PrescriptionMedicineId { get; set; }
        public string? SupplyDesc { get; set; }
        public string? SupplyQuantity { get; set; }
        public string? Name { get; set; }
        public string? Direction { get; set; }
        public string? SupplyItemDesignatorID { get; set; }
        public decimal? WholesalePrice { get; set; }
        public decimal? TotalAmount { get; set; }
    }

    public class PrescriptionMedicineItemDTO
    {
        public long PrescriptionMedicineId { get; set; }
        public long PatientPrescriptionId { get; set; }
        public string MedicineName { get; set; } = string.Empty;
        public long? DrugId { get; set; }
        public long? CatalogId { get; set; }
        public string? CatalogName { get; set; }
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

        public List<MedicineSupplyDTO> Supplies { get; set; } = new();
    }

}
