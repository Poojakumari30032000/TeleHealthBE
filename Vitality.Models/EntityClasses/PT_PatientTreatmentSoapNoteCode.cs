using System;

namespace Vitality.Models.EntityClasses
{
    /// <summary>
    /// One ICD-10-CM or CPT code on a treatment SOAP note (TEL-22).
    /// Table created by Sql/Create_PT_PatientTreatmentSoapNoteCode.sql.
    /// Exactly one of <see cref="Icd10CodeId"/> / <see cref="CptCodeId"/> is set,
    /// matching <see cref="CodeSystem"/>.
    /// </summary>
    public partial class PT_PatientTreatmentSoapNoteCode
    {
        public long SoapNoteCodeId { get; set; }
        public long SoapNoteId { get; set; }
        public string CodeSystem { get; set; } = null!;
        public long CodeSetVersionId { get; set; }
        public long? Icd10CodeId { get; set; }
        public long? CptCodeId { get; set; }
        public string Code { get; set; } = null!;
        public string DisplayCode { get; set; } = null!;
        public string Description { get; set; } = null!;
        public int DisplayOrder { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }

        public virtual PT_PatientTreatmentSoapNote SoapNote { get; set; } = null!;
        public virtual SYS_CodeSetVersion CodeSetVersion { get; set; } = null!;
    }
}
