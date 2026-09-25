using System;

namespace Vitality.Models.EntityClasses
{
    /// <summary>One ICD-10-CM diagnosis code within one release (TEL-19).</summary>
    public partial class SYS_Icd10Code
    {
        public long Icd10CodeId { get; set; }
        public long CodeSetVersionId { get; set; }
        public string Code { get; set; } = null!;
        public string DisplayCode { get; set; } = null!;
        public string? ShortDescription { get; set; }
        public string LongDescription { get; set; } = null!;
        public bool IsBillable { get; set; }
        public DateTime EffectiveDate { get; set; }
        public DateTime? TerminationDate { get; set; }
        public bool IsActive { get; set; }
        public int? SortOrder { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }

        public virtual SYS_CodeSetVersion CodeSetVersion { get; set; } = null!;
    }
}
