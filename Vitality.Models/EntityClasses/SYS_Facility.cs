using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class SYS_Facility
    {
        public SYS_Facility()
        {
            PD_FacilityBundlePrices = new HashSet<PD_FacilityBundlePrice>();
            PD_FacilityCategories = new HashSet<PD_FacilityCategory>();
            SYS_QuestionnaireFacilityJsons = new HashSet<SYS_QuestionnaireFacilityJson>();
            Sys_FacilitySquareCreds = new HashSet<Sys_FacilitySquareCred>();
            Sys_FacilityStripeConnects = new HashSet<Sys_FacilityStripeConnect>();
            Sys_FullscriptOAuths = new HashSet<Sys_FullscriptOAuth>();
            Sys_InvoicePayments = new HashSet<Sys_InvoicePayment>();
        }

        public long FacilityId { get; set; }
        public long? SubscriptionPlanId { get; set; }
        public string? TitleLong { get; set; }
        public string? TitleShort { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Fax { get; set; }
        public string? Address { get; set; }
        public int? CityId { get; set; }
        public int? StateId { get; set; }
        public string? ZipCode { get; set; }
        public string? BillingAddressType { get; set; }
        public string? BillingAddress { get; set; }
        public int? BillingCityId { get; set; }
        public int? BillingStateId { get; set; }
        public string? BillingZipCode { get; set; }
        public string? FedearlTaxId { get; set; }
        public string? NPI { get; set; }
        public string? Status { get; set; }
        public string? FacilityContactName { get; set; }
        public string? FacilityContactEmail { get; set; }
        public string? FacilityContactPhone { get; set; }
        public long? OrganizationId { get; set; }
        public bool? IsActive { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public string? Guid { get; set; }
        public bool? CanViewChannels { get; set; }
        public bool? IsExternal { get; set; }
        public int? PaymentModeId { get; set; }
        public bool? IsBillable { get; set; }
        public bool? IsApproved { get; set; }

        public virtual ICollection<PD_FacilityBundlePrice> PD_FacilityBundlePrices { get; set; }
        public virtual ICollection<PD_FacilityCategory> PD_FacilityCategories { get; set; }
        public virtual ICollection<SYS_QuestionnaireFacilityJson> SYS_QuestionnaireFacilityJsons { get; set; }
        public virtual ICollection<Sys_FacilitySquareCred> Sys_FacilitySquareCreds { get; set; }
        public virtual ICollection<Sys_FacilityStripeConnect> Sys_FacilityStripeConnects { get; set; }
        public virtual ICollection<Sys_FullscriptOAuth> Sys_FullscriptOAuths { get; set; }
        public virtual ICollection<Sys_InvoicePayment> Sys_InvoicePayments { get; set; }
    }
}
