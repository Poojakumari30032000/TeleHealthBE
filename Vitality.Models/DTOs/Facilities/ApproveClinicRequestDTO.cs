namespace Vitality.Models.DTOs.Facilities
{
    public class ApproveClinicRequestDTO
    {
        public long FacilityId { get; set; }
        public bool? CanViewChannels { get; set; }
        public bool? IsBillable { get; set; }
        public int PaymentModeId { get; set; }
    }
}
