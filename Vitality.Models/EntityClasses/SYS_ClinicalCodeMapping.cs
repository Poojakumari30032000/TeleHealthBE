using System;

namespace Vitality.Models.EntityClasses
{
    /// <summary>
    /// One clinical code attached to one Category, Service or Package (TEL-20).
    /// Table created by Sql/Create_SYS_ClinicalCodeMapping.sql.
    /// <para>
    /// The durable identity of a mapping is (<see cref="TargetType"/>,
    /// <see cref="TargetId"/>, <see cref="CodeSystem"/>, <see cref="Code"/>) - the
    /// code string, not the reference row. Reference rows are per release
    /// (TEL-19), so a mapping keyed on <c>Icd10CodeId</c> alone would stop
    /// resolving the moment the next fiscal year was imported. The row id is
    /// kept alongside for provenance ("mapped against FY2026's E11.65") and to
    /// carry the foreign key that stops a referenced code being deleted.
    /// </para>
    /// </summary>
    public partial class SYS_ClinicalCodeMapping
    {
        public long ClinicalCodeMappingId { get; set; }

        /// <summary>'Category', 'Service' or 'Package' - see <see cref="ClinicalCodeTargetType"/>.</summary>
        public string TargetType { get; set; } = null!;

        /// <summary>PD_Category.CategoryId, SYS_Product.ProductId or PD_Bundle.BundleId.</summary>
        public long TargetId { get; set; }

        /// <summary>'ICD10CM' or 'CPT' - see <see cref="ClinicalCodeSystem"/>.</summary>
        public string CodeSystem { get; set; } = null!;

        /// <summary>Normalised, as published and without the dot: 'E1165'.</summary>
        public string Code { get; set; } = null!;

        /// <summary>The reference row this mapping was authored against. Exactly one of the two is set.</summary>
        public long? Icd10CodeId { get; set; }
        public long? CptCodeId { get; set; }

        /// <summary>
        /// Set when the mapped code is no longer in force. A mapping to an
        /// already terminated code is refused outright; this flag exists for the
        /// other case, where an import terminated a code that was mapped earlier.
        /// </summary>
        public bool NeedsReview { get; set; }
        public string? ReviewReason { get; set; }

        public bool IsActive { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }

        public virtual SYS_Icd10Code? Icd10Code { get; set; }
        public virtual SYS_CptCode? CptCode { get; set; }
    }

    /// <summary>The values allowed by CK_SYS_ClinicalCodeMapping_TargetType.</summary>
    public static class ClinicalCodeTargetType
    {
        /// <summary>PD_Category.</summary>
        public const string Category = "Category";

        /// <summary>SYS_Product - the service offering. See ProductsRepo.</summary>
        public const string Service = "Service";

        /// <summary>PD_Bundle - a package of services. See PD_DrugVarientsInBundle, InvoiceRepo.</summary>
        public const string Package = "Package";
    }
}
