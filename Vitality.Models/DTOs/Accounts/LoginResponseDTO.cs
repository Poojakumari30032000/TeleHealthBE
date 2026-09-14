using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vitality.Models.DTOs.Subscriptions;

namespace DudeMeds.Models.DTOs.Accounts
{
    public class LoginResponseDTO
    {
        public string? Token { get; set; }
        public string? Guid { get; set; }
        public long? FacilityId { get; set; }
        public string? FacilityGuid { get; set; }
        public long? UserId { get; set; }
        public long? PatientId { get; set; }
        public string? FirstName { get; set; }
        public string? MiddleName { get; set; }
        public string? LastName { get; set; }
        public DateTime? DOB { get; set; }
        public string? Title { get; set; }
        public string? Gender { get; set; }
        public string? Email { get; set; }
        public string? ProfileUrl { get; set; }
        public string? Phone { get; set; }
        public string? AddressType { get; set; }
        public string? Address { get; set; }
        public int? StateId { get; set; }
        public int? CityId { get; set; }
        public string? ZipCode { get; set; }
        public string? Status { get; set; }
        public string? TaxId { get; set; }
        public string? Medicaid { get; set; }
        public string? CAQHId { get; set; }
        public string? NPI { get; set; }
        public string? License { get; set; }
        public string? SSN { get; set; }
        public string? DEA { get; set; }
        public long? LoginId { get; set; }
        public int? RoleId { get; set; }
        public string? RoleName { get; set; }

        public string? RoleTitle { get; set; }
        public long? OrganizationId { get; set; }
        public bool? IsFirstUse { get; set; }
        public bool? IsFirstQuestionaire { get; set; }

        public SaveSubscriptionRequestDTO? Subscription { get; set; }

        public string? ErrorMessage { get; set; }
        public string? ErrorCode { get; set; }
    }
}
