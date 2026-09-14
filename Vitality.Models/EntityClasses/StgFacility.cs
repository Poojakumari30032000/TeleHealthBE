using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class StgFacility
    {
        public string? FacilityName { get; set; }
        public string? Address { get; set; }
        public int? CityId { get; set; }
        public int? StateId { get; set; }
        public string? ZipCode { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? BillingAddressType { get; set; }
        public string? BillingAddress { get; set; }
        public int? BillingCityId { get; set; }
        public int? BillingStateId { get; set; }
        public string? BillingZipCode { get; set; }
    }
}
