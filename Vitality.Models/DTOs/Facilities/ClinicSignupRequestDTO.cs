using DudeMeds.Models.DTOs.Facilities;

namespace Vitality.Models.DTOs.Facilities
{
    public class ClinicSignupRequestDTO : SaveFacilityRequestDTO
    {
        public string? Password { get; set; }
        public string? ConfirmPassword { get; set; }
    }
}
