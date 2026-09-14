using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class PT_PatientTreatment
    {
        public PT_PatientTreatment()
        {
            PT_PatientTreatmentDocuments = new HashSet<PT_PatientTreatmentDocument>();
        }

        public long PatientTreatmentId { get; set; }
        public string? FacilityGuid { get; set; }
        public long? PatientId { get; set; }
        public long? ProductId { get; set; }
        public string? TreatmentStatus { get; set; }
        public long? ProviderScheduledSlotId { get; set; }
        public string? Status { get; set; }
        public bool? IsActive { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
        public string? Guid { get; set; }
        public long? OrganizationId { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public int? UserId { get; set; }
        public int? FacilityId { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public bool? IsRecurring { get; set; }
        public DateTime? NextRecurringPaymentDate { get; set; }
        public decimal? OriginalPaymentAmount { get; set; }

        public long? RecurringCouponCodeId { get; set; }

        public decimal? RecurringAmountAfterCoupon { get; set; }
        public int? RecurringDurationMonths { get; set; }
        public DateTime? RecurringStartDate { get; set; }

        public DateTime? RecurringPausedDate { get; set; }

        public int? RetryCount { get; set; }

        public string? LastFailureMessage { get; set; }

        public DateTime? LastFailureAt { get; set; }
        public bool? Refill { get; set; }

        public virtual ICollection<PT_PatientTreatmentDocument> PT_PatientTreatmentDocuments { get; set; }
    }
}
