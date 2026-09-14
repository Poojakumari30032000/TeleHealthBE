using System;

namespace Vitality.Models.EntityClasses
{
    public partial class PT_PatientDocument
    {
        public long PatientDocumentId { get; set; }
        public long PatientId { get; set; }
        public string DocumentName { get; set; } = null!;
        public string? Description { get; set; }
        public string DocumentUrl { get; set; } = null!;
        public bool? IsActive { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public string? Guid { get; set; }

        public virtual PT_Patient Patient { get; set; } = null!;
    }
}
