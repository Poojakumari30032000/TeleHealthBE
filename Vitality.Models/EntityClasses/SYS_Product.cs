using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class SYS_Product
    {
        public long ProductId { get; set; }
        public string? ProductType { get; set; }
        public string? ProductName { get; set; }
        public string? Status { get; set; }
        public bool? IsActive { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
        public string? Guid { get; set; }
        public long? OrganizationId { get; set; }
    }
}
