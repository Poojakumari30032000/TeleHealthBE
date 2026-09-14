using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.Patients
{
    public class SavePatientRequestDTO
    {
        public long PatientId { get; set; }
        public long? FacilityId { get; set; }
        public string? FacilityGuid { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? Gender { get; set; }
        public DateTime? DOB { get; set; }
        public string? Address { get; set; }
        public string? Street { get; set; }
        public int? CityId { get; set; }
        public int? StateId { get; set; }
        public string? Zipcode { get; set; }
        public string? Phone { get; set; }
        public string? Password { get; set; }
        public string? PatientPicture { get; set; }
        public string? IdPicture { get; set; }
    }
}
