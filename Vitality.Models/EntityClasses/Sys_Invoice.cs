using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class Sys_Invoice
    {
        public Sys_Invoice()
        {
            Sys_InvoiceLineItems = new HashSet<Sys_InvoiceLineItem>();
            Sys_InvoicePayments = new HashSet<Sys_InvoicePayment>();
        }

        public int InvoiceId { get; set; }
        public string? InvoiceNumber { get; set; }
        public string? CustomerName { get; set; }
        public decimal? Amount { get; set; }
        public string? Status { get; set; }
        public string? InvoiceType { get; set; }
        public bool? IsActive { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
        public long? SubscriptionId { get; set; }
        public long? UserId { get; set; }
        public long? FacilityId { get; set; }
        public long? ProviderBillTotal { get; set; }
        public long? PharmacyBillToltal { get; set; }
        public bool? IsMonthlyInvoiceGenerated { get; set; }
        public long? CardId { get; set; }
        public long? PatientId { get; set; }
        public string? PdfS3Url { get; set; }
        public DateTime? InvoiceDate { get; set; }

        public virtual PT_Patient? Patient { get; set; }
        public virtual SYS_Subscription? Subscription { get; set; }
        public virtual SYS_UserDetail? User { get; set; }
        public virtual ICollection<Sys_InvoiceLineItem> Sys_InvoiceLineItems { get; set; }
        public virtual ICollection<Sys_InvoicePayment> Sys_InvoicePayments { get; set; }
    }
}
