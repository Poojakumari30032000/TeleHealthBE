namespace Vitality.Models.DTOs.Facilities
{
    public class UpdateFacilityBillingRequestDTO
    {
        public long FacilityId { get; set; }
        public bool IsBillable { get; set; }
    }
}
