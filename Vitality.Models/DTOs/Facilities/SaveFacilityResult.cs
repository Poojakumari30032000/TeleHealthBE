namespace Vitality.Models.DTOs.Facilities
{

    public class SaveFacilityResult
    {
        public string Message { get; set; } = string.Empty;
        public long? FacilityId { get; set; }

        public bool IsSuccess =>
            Message == "Facility Created Successfully" || Message == "Facility Updated Successfully";
    }
}
