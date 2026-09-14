using DudeMeds.Models.DTOs.PatientOrders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.PatientPrescriptions
{
    public class GetPatientPrescriptionInfoResponseDTO
    {
        public string? CreatedBy { get; set; }
        public DateTime? LastEditedDate { get; set; }
        public long? PatientId { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Address { get; set; }
        public string? PresImage { get; set; }
        public string? ControlledSubImage { get; set; }
        public string? Gender { get; set; }
        public DateTime? DOB { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? ZipCode { get; set; }
        public string? PharmacyName { get; set; }
        public string? MRN { get; set; }
        public long? PrescriptionId { get; set; }
        public DateTime? WrittenDate { get; set; }
        public DateTime? ExpirationDate { get; set; }
        public string? VisitStatus { get; set; }
        public string? SubscriptionStatus {  get; set; }
        public int? NumberOfRefills { get; set; }
        public int? RefillsRemaining { get; set; }
        public string? ProductVarient {  get; set; }
        public string? DrugName { get; set; }
        public string? PackageNDC { get; set; }
        public long? OrderId { get; set; }
        public DateTime? OrderDate { get; set; }
        public string? OrderStatus { get; set; }
        public string? SOAPNotes { get; set; }
        public string? ModifiedBy { get; set; }
        public long? TreatmentId { get; set; }
        public OrderProviderDTO? Provider { get; set; }
    }
}
