using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.Patients
{
    public class GetAllPatientsResponseDTO
    {
        public string? Guid { get; set; }
        public long PatientId { get; set; }
        public long? FacilityId { get; set; }
        public string? Name { get; set; }
        public DateTime? StartDate { get; set; }
        public string? MRN { get; set; }

        public decimal? Subscription { get; set; }
        public long? ProductId { get; set; }
        public string? ProductName { get; set; }

        public string? Email { get; set; }
        public string? Phone { get; set; }

        public int? TreatmentCount { get; set; }
        public int? PrescriptionCount { get; set; }
        public int? OrderCount { get; set; }

        public string? Location { get; set; }
        public string? Status { get; set; }

        public DateTime? LastOrder { get; set; }
        public string? LastVisitStatus { get; set; }

        public string? RefillStatus { get; set; }
        public DateTime? NextRefill { get; set; }
        public DateTime? FollowUp { get; set; }

        public long? ArchievedBy { get; set; }
        public DateTime? ArchievedAt { get; set; }
        public int? UnreadChat { get; set; }

        public string? DigitalProductStatus { get; set; }
        public decimal? DigitalProductPrice { get; set; }

        public DateTime? DOB { get; set; }
        public string? Address { get; set; }
        public string? Street { get; set; }
        public int? CityId { get; set; }
        public string? CityName { get; set; }
        public int? StateId { get; set; }
        public string? StateName { get; set; }
        public string? Zipcode { get; set; }
        public string? FacilityName { get; set; }
    }

}
