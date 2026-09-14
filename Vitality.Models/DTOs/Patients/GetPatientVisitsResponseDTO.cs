using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitality.Models.DTOs.Patients
{
    public class GetPatientVisitsResponseDTO
    {
        public long? VisitId { get; set; }
        public string? VisitGuid { get; set; }
        public DateTime? PrescribedAt { get; set; }
        public DateTime? FollowUp { get; set; }
        public string? Status { get; set; }
    }
}
