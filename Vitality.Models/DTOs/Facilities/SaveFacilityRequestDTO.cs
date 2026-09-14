using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.Facilities
{
    public class SaveFacilityRequestDTO
    {
        public long FacilityId { get; set; }
        public string? ClinicAdminFirstName { get; set; }
        public string? ClinicAdminLastName { get; set; }
        public long? SubscriptionPlanId { get; set; }
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
        public string? BillingAddress { get; set; }
        public int? BillingCityId { get; set; }
        public int? BillingStateId { get; set; }
        public string? BillingZipCode { get; set; }
        public string? FedearlTaxId { get; set; }
        public string? NPI { get; set; }
        public string? FacilityContactName { get; set; }
        public string? FacilityContactEmail { get; set; }
        public string? FacilityContactPhone { get; set; }
        public bool? CanViewChannels { get; set; }

        public int? PaymentModeId { get; set; }
        public bool? IsBillable { get; set; }
    }

}
