using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vitality.Models.DTOs.PrescriptionMedicines;
using Vitality.Models.EntityClasses;

namespace Vitality.Models.Repos.Interfaces
{
    public interface IPrescriptionMedicinesRepo
    {
        bool Create(long patientPrescriptionId, string medicineName);
        bool CreateMany(long patientPrescriptionId, List<PrescriptionMedicinesRequestDTO> medicines);

        public PrescriptionMedicineItemDTO? GetById(long prescriptionMedicineId);
        public List<PrescriptionMedicineItemDTO> GetMedicinesByPrescriptionId(long patientPrescriptionId);
        bool UpdateName(long prescriptionMedicineId, string medicineName);
        bool UpdateMedicine(PrescriptionMedicinesRequestDTO dto);
        bool Delete(long prescriptionMedicineId);
    }

}
