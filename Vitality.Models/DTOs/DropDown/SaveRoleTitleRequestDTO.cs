namespace Vitality.Models.DTOs.DropDown
{
    public class SaveRoleTitleRequestDTO
    {
        public int RoleTitleId { get; set; }
        public string? RoleTitleName { get; set; }
        public bool? IsActive { get; set; }
    }
}
