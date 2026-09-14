using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class PH_PharmacyNote
    {
        public long PharmacyNoteId { get; set; }
        public long? PatientOrderId { get; set; }
        public long? PharmacyId { get; set; }
        public string? PharmacyNote { get; set; }
        public DateTime? CreatedDate { get; set; }
        public long? CreatedBy { get; set; }
    }
}
