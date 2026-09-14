using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class Sys_InvoicePayment
    {
        public long InvoicePaymentId { get; set; }
        public int InvoiceId { get; set; }
        public decimal PaymentAmount { get; set; }
        public string PaymentMethod { get; set; } = null!;
        public string? TransactionId { get; set; }
        public string? SquarePaymentId { get; set; }
        public string PaymentStatus { get; set; } = null!;
        public long? PaidByUserId { get; set; }
        public long? PaidByPatientId { get; set; }
        public long? PaidByFacilityId { get; set; }
        public long? CardId { get; set; }
        public string? PaymentNotes { get; set; }
        public DateTime PaymentDate { get; set; }
        public bool? IsActive { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public string? RefundTransactionId { get; set; }
        public decimal? RefundAmount { get; set; }
        public DateTime? RefundDate { get; set; }
        public string? RefundReason { get; set; }

        public virtual SYS_UserCard? Card { get; set; }
        public virtual Sys_Invoice Invoice { get; set; } = null!;
        public virtual SYS_Facility? PaidByFacility { get; set; }
        public virtual PT_Patient? PaidByPatient { get; set; }
        public virtual SYS_UserDetail? PaidByUser { get; set; }
    }
}
