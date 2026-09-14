using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.PatientTreatments
{
    public class GetPatientTreatmentByIdResponseDTO
    {
        public long PatientTreatmentId { get; set; }
        public long? FacilityId { get; set; }
        public long? PatientId { get; set; }
        public long? ProductId { get; set; }
        public long? DrugVarientId { get; set; }
        public List<SavePatientTreatmentInTakeRequestDTO>? InTakeForm { get; set; }
        public long? ProviderScheduledSlotId { get; set; }
        public string? Status { get; set; }
    }
}
