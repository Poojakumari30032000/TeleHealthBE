using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.PatientTreatments
{
    public class GetAllPatientTreatmentsRequestDTO
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 100;
        public string? FacilityGuid { get; set; }
        public long? PatientId { get; set; }
        public long? FacilityId { get; set; }
        public long? ProviderId { get; set; }
        public string? Title { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public int? ClientTimezoneOffsetMinutes { get; set; }
        public string? TreatmentStatus { get; set; }
        public string? VisitStatus { get; set; }
        public bool? Refill { get; set; }
    }
    public class PatientTreatmentSummaryDTO
    {
        public long TreatmentId { get; set; }
        public long? ProductId { get; set; }
        public string? BundleName { get; set; }
        public DateTime? StartDate { get; set; }
    }

}
