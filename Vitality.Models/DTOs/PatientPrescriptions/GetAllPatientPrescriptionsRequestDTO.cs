using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitality.Models.DTOs.PatientPrescriptions
{
    public class GetAllPatientPrescriptionsRequestDTO
    {
        public int PageSize { get; set; }
        public int PageNumber { get; set; }
        public string? FacilityGuid { get; set; }
        public long? FacilityId { get; set; }
        public long? ProviderId { get; set; }
        public string? Title { get; set; }
        public long? PatientId { get; set; }
        public string? Status { get; set; }
        public string? OrderStatus { get; set; }
    }
}
