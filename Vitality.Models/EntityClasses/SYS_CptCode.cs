using System;

namespace Vitality.Models.EntityClasses
{
    /// <summary>
    /// One CPT procedure code within one release (TEL-19). CPT is AMA-licensed;
    /// the table stays empty until a license is confirmed.
    /// </summary>
    public partial class SYS_CptCode
    {
        public long CptCodeId { get; set; }
        public long CodeSetVersionId { get; set; }
        public string Code { get; set; } = null!;
        public string? ShortDescription { get; set; }
        public string LongDescription { get; set; } = null!;
        public DateTime EffectiveDate { get; set; }
        public DateTime? TerminationDate { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }

        public virtual SYS_CodeSetVersion CodeSetVersion { get; set; } = null!;
    }
}
