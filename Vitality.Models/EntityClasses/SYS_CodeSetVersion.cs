using System;

namespace Vitality.Models.EntityClasses
{
    /// <summary>
    /// One published release of a clinical code system, e.g. ICD-10-CM FY2026 (TEL-19).
    /// Table created by Sql/Create_SYS_ClinicalCodeSets.sql.
    /// </summary>
    public partial class SYS_CodeSetVersion
    {
        public long CodeSetVersionId { get; set; }
        public string CodeSystem { get; set; } = null!;
        public string VersionLabel { get; set; } = null!;
        public DateTime EffectiveDate { get; set; }
        public DateTime? TerminationDate { get; set; }
        public string? SourceFileName { get; set; }
        public int CodeCount { get; set; }
        public long? ImportedBy { get; set; }
        public DateTime? ImportedDate { get; set; }
        public bool IsActive { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
    }

    /// <summary>The values allowed by CK_SYS_CodeSetVersion_CodeSystem.</summary>
    public static class ClinicalCodeSystem
    {
        public const string Icd10Cm = "ICD10CM";
        public const string Cpt = "CPT";
    }
}
