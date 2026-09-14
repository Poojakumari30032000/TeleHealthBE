using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.PatientTreatments
{
    public class GetPatientTreatmentVisitHistoryResponseDTO
    {
        public string? VisitGuid { get; set; }
        public DateTime? PrescribedAt { get; set; }
        public string? FollowUp {  get; set; }
        public string? Status { get; set; }
        public string? VisitType { get; set; }
    }
}
