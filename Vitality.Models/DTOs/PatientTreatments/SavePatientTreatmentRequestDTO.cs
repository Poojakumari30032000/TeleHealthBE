using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.PatientTreatments
{
    public class SavePatientTreatmentRequestDTO
    {
        public long PatientTreatmentId { get; set; }
        public string? FacilityGuid { get; set; }
        public long? PatientId { get; set; }
        public long? ProductId { get; set; }
        public long? FacilityId { get; set; }
        public long? UserId { get; set; }
        public List<SavePatientTreatmentInTakeRequestDTO>? InTakeForm { get; set; }
        public long? ProviderScheduledSlotId { get; set; }
        public bool? IsIntakeForm { get; set; }
    }
    public class SavePatientTreatmentInTakeRequestDTO
    {
        public string? Question { get; set; }
        public string? Answer { get; set; }
        public string? ConsentHtml { get; set; }
        public string? OtherText { get; set; }
        public string? Type { get; set; }
    }

    public sealed class UpdateTreatmentStatusRequest
    {
        public long PatientTreatmentId { get; set; }
        public string TreatmentStatus { get; set; } = default!;
        public long UserId { get; set; }
    }
}
