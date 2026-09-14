using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.Users
{
    public class SaveUserRequestDTO
    {
        public long UserId { get; set; }
        public long? FacilityId { get; set; }
        public string? FirstName { get; set; }
        public string? MiddleName { get; set; }
        public string? LastName { get; set; }
        public DateTime? DOB { get; set; }
        public string? Title { get; set; }
        public string? Gender { get; set; }
        public string? Email { get; set; }
        public string? ProviderType { get; set; }
        public string? Phone { get; set; }
        public string? AddressType { get; set; }
        public string? Address { get; set; }
        public int? StateId { get; set; }
        public int? CityId { get; set; }
        public string? ZipCode { get; set; }
        public string? TaxId { get; set; }
        public string? Medicaid { get; set; }
        public string? CAQHId { get; set; }
        public string? NPI { get; set; }
        public string? License { get; set; }
        public string? SSN { get; set; }
        public string? DEA { get; set; }
        public List<long>? CategoryId { get; set; }
        public bool? IsSupervisorRequired { get; set; }
        public List<long>? SupervisorId { get; set; }
        public int? RoleId { get; set; }
        public int? RoleTitleId { get; set; }
        public List<ProviderStateLicenseDTO>? ProviderLicense {  get; set; }
        public string? ProfileUrl { get; set; }
        public string? Bio { get; set; }
        public string? PatientPicture { get; set; }
        public string? IdPicture { get; set; }
    }

    public class ProviderStateLicenseDTO
    {
        public long? StateId { set; get; }
        public string? StateLicense { set; get; }
    }
}
