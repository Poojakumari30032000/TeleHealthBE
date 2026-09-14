using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class PT_PatientTreatmentInTakeForm
    {
        public long PatientTreatmentInTakeFormId { get; set; }
        public long? PatientTreatmentId { get; set; }
        public string? Question { get; set; }
        public string? Answer { get; set; }
        public string? ConsentHtml { get; set; }
        public string? OtherText { get; set; }
        public string? Type { get; set; }
        public DateTime? CreatedDate { get; set; }
        public long? CreatedBy { get; set; }
    }
}
