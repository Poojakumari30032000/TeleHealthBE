using System;

namespace Vitality.Models.EntityClasses
{
    public partial class PT_PatientProfileNote
    {
        public long PatientProfileNoteId { get; set; }
        public long PatientId { get; set; }
        public string? NoteText { get; set; }
        public bool? IsActive { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }

        public virtual PT_Patient Patient { get; set; } = null!;
    }
}
