using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class PD_DigitalProduct
    {
        public long DigitalProductId { get; set; }
        public long? ProductId { get; set; }
        public string? ProductType { get; set; }
        public string? Name { get; set; }
        public string? Status { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public long? OrganizationId { get; set; }
        public long? FacilityId { get; set; }
        public string? Guid { get; set; }
        public bool? IsActive { get; set; }
    }
}
