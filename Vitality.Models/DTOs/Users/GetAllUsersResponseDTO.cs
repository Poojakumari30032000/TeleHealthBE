using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.Users
{
    public class GetAllUsersResponseDTO
    {
        public string? Guid { get; set; }
        public long UserId { get; set; }
        public string? UserName { get; set; }
        public DateTime? DOB { get; set; }
        public string? Email { get; set; }
        public string? ProviderType { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public int? StateId { get; set; }
        public string? StateName { get; set; }
        public int? CityId { get; set; }
        public string? CityName { get; set; }
        public string? ZipCode { get; set; }
        public string? Status { get; set; }
        public List<long>? CategoryId { get; set; }
        public List<string>? CategoryName { get; set; }
        public bool? IsSupervisorRequired { get; set; }
        public long? SupervisorId { get; set; }
        public string? SupervisorName { get; set; }
        public int? RoleId { get; set; }
        public int? RoleTitleId { get; set; }
        public string? RoleTitleName { get; set; }
        public string? UserRole {  get; set; }
        public List<ProviderStateLicenseDTO>? ProviderLicense { get; set; }

    }

    public class ProviderLicenseDTO
    {
        public long? StateId { get; set; }
        public string? StateLicense { get; set; }
    }

}
