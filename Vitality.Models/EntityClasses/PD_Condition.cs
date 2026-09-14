using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class PD_Condition
    {
        public long ConditionId { get; set; }
        public long? CategoryId { get; set; }
        public string? ConditionName { get; set; }
        public string? ImageURL { get; set; }
        public bool? IsActive { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
        public long? OrganizationId { get; set; }
    }
}
