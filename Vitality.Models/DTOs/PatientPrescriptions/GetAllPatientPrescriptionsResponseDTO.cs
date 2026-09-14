using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.PatientPrescriptions
{
    public class GetAllPatientPrescriptionsResponseDTO
    {
        public long? PatientPrescriptionId { get; set; }
        public long? PatientId { get; set; }
        public string? PatientName { get; set; }
        public DateTime? Date { get; set; }
        public string? PrescriptionStatus {  get; set; }
        public string? RefillStatus { get; set; }
        public int? RefillRemaining {  get; set; }
        public DateTime? NextRefilldate { get; set; }
        public DateTime? ExpirationDate { get; set; }
        public bool? IsSendToDudeMeds { get; set; }
        public DateTime? SendAt { get; set; }
        public string? Pharmacy {  get; set; }
        public string? OrderStatus { get; set; }
        public string? FacilityName {  get; set; }

    }
}
