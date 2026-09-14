using System;

namespace DudeMeds.Models.DTOs.Patients
{
    public class SaveManualPatientRequestDTO
    {
        public long FacilityId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
    }
}
