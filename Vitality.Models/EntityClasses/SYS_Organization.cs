using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class SYS_Organization
    {
        public long OrganizationId { get; set; }
        public long? SubscriptionId { get; set; }
        public string? TitleLong { get; set; }
        public string? TitleShort { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Fax { get; set; }
        public string? BillingAddressType { get; set; }
        public string? Address { get; set; }
        public int? CityId { get; set; }
        public int? StateId { get; set; }
        public string? ZipCode { get; set; }
        public string? OtherAddress { get; set; }
        public int? OtherCityId { get; set; }
        public int? OtherStateId { get; set; }
        public string? OtherZipCode { get; set; }
        public string? FedearlTaxId { get; set; }
        public string? NPI { get; set; }
        public string? Status { get; set; }
        public string? OrganizationContactName { get; set; }
        public string? OrganizationContactEmail { get; set; }
        public string? OrganizationContactPhone { get; set; }
        public bool? IsActive { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
    }
}
