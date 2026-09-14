using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class SYS_UserDetail
    {
        public SYS_UserDetail()
        {
            SYS_UserCards = new HashSet<SYS_UserCard>();
            Sys_InvoicePayments = new HashSet<Sys_InvoicePayment>();
            Sys_Invoices = new HashSet<Sys_Invoice>();
        }

        public long UserId { get; set; }
        public string? FirstName { get; set; }
        public string? MiddleName { get; set; }
        public string? LastName { get; set; }
        public DateTime? DOB { get; set; }
        public string? Title { get; set; }
        public int? RoleTitleId { get; set; }
        public string? Gender { get; set; }
        public string? Email { get; set; }
        public string? ProviderType { get; set; }
        public string? Phone { get; set; }
        public string? AddressType { get; set; }
        public string? Address { get; set; }
        public int? StateId { get; set; }
        public int? CityId { get; set; }
        public string? ZipCode { get; set; }
        public int? LicenseStateId { get; set; }
        public string? Status { get; set; }
        public string? TaxId { get; set; }
        public string? Medicaid { get; set; }
        public string? CAQHId { get; set; }
        public string? NPI { get; set; }
        public string? License { get; set; }
        public string? SSN { get; set; }
        public string? DEA { get; set; }
        public bool? IsSupervisorRequired { get; set; }
        public long? OrganizationId { get; set; }
        public long? LoginId { get; set; }
        public bool? IsActive { get; set; }
        public DateTime? CreatedDate { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public long? ModifiedBy { get; set; }
        public string? Guid { get; set; }
        public bool? IsFirstUse { get; set; }
        public bool? IsFirstQuestionaire { get; set; }
        public string? ProfileUrl { get; set; }
        public string? Bio { get; set; }
        public string? PatientPicture { get; set; }
        public string? IdPicture { get; set; }

        public string? StripePlatformCustomerId { get; set; }

        public virtual SYS_Login? Login { get; set; }
        public virtual ICollection<SYS_UserCard> SYS_UserCards { get; set; }
        public virtual ICollection<Sys_InvoicePayment> Sys_InvoicePayments { get; set; }
        public virtual ICollection<Sys_Invoice> Sys_Invoices { get; set; }
    }
}
