using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitality.Models.DTOs.Patients
{
    public class GetPatientDetailsResponseDTO
    {
        public long? PatientId { get; set; }
        public long? UserId { get; set; }
        public string? MRN { get; set; }
        public DateTime? CreatedDate { get; set; }
        public string? CreatedBy { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? PhoneType { get; set; }
        public string? Address { get; set; }
        public string? Gender { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public long? PharmacyId { get; set; }
        public string? PharmacyName { get; set; }
        public long? FacilityId { get; set; }
        public string? FacilityName { get; set; }
        public long? ProviderId { get; set; }
        public string? ProviderName { get; set; }
        public string? VisitStatus { get; set; }
        public int? NumberOfOrders { get; set; }
        public int? Treatments { get; set; }
        public decimal? LastOrderPrice { get; set; }
        public decimal? Height { get; set; }
        public decimal? Weight { get; set; }
        public decimal? BMI { get; set; }
        public DateTime? DateStarted { get; set; }
        public DateTime? LastOrderDate { get; set; }
        public DateTime? NextVisitDate { get; set; }
        public DateTime? NextShippingDate { get; set; }
        public string? PatientPicture { get; set; }
        public string? IdPicture { get; set; }

        public List<GetPatientDocumentResponseDTO> Documents { get; set; } = new List<GetPatientDocumentResponseDTO>();
    }
}
