using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.PatientTreatments
{
    public class GetAllPatientTreatmentsResponseDTO
    {
        public long? PatientTreatmentId { get; set; }
        public long? PatientId { get; set; }
        public string? PatientName { get; set; }
        public DateTime? TreatmentStartDate { get; set; }
        public string? PatientMRN { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public int? OrderCount { get; set; }
        public string? Location { get; set; }
        public string? TreatmentStatus { get; set; }
        public string? VisitStatus { get; set; }
        public DateTime? LastOrder {  get; set; }
        public DateTime? NextShippingDate { get; set; }
        public string? Status { get; set; }
        public string? FacilityName { get; set; }
        public bool? Refill { get; set; }

    }
}
