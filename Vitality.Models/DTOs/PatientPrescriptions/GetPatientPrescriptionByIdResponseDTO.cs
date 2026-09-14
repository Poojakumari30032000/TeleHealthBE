using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.PatientPrescriptions
{
    public class GetPatientPrescriptionByIdResponseDTO
    {
        public long? PatientPrescriptionId { get; set; }
        public string? FacilityGuid { get; set; }
        public long? PatientId { get; set; }
        public string? PatientName { get; set; }
        public long? PatientTreatmentId { get; set; }
        public long? PatientOrderId { get; set; }
        public long? ProductId { get; set; }
        public string? PrescriptionInstruction { get; set; }
        public string? PrescriptionStatus { get; set; }
        public string? ProductName { get; set; }
        public string? ProductType { get; set; }
        public string? PatientOrderGuid { get; set; }
        public string? PatientTreatmentGuid { get; set; }
        public string? PatientPrescriptionGuid { get; set;}

    }
}
