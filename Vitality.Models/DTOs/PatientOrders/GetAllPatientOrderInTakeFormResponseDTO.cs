using DudeMeds.Models.DTOs.PatientTreatments;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.PatientOrders
{
    public class GetAllPatientOrderInTakeFormResponseDTO
    {
        public string? QuestionaireName { get; set; }
        public List<SavePatientTreatmentInTakeRequestDTO>? InTakeForm { get; set; }
    }
}
