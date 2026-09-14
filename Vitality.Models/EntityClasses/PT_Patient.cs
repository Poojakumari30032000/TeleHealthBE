using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class PT_Patient
    {
        public PT_Patient()
        {
            PT_CouponUsages = new HashSet<PT_CouponUsage>();
            Sys_EmpowerOrders = new HashSet<Sys_EmpowerOrder>();
            Sys_InvoicePayments = new HashSet<Sys_InvoicePayment>();
            Sys_Invoices = new HashSet<Sys_Invoice>();
        }

        public long PatientId { get; set; }
        public long? FacilityId { get; set; }
        public string? MRN { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? Gender { get; set; }
        public DateTime? DOB { get; set; }
        public string? Address { get; set; }
        public string? Street { get; set; }
        public int? CityId { get; set; }
        public int? StateId { get; set; }
        public string? Zipcode { get; set; }
        public string? Phone { get; set; }
        public string? Status { get; set; }
        public bool? IsArchieved { get; set; }
        public long? ArchivedBy { get; set; }
        public DateTime? ArchievedDate { get; set; }
        public bool? IsWaitList { get; set; }
        public string? WaitListReason { get; set; }
        public bool? IsActive { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public long? OrganizationId { get; set; }
        public string? Guid { get; set; }
        public long? LoginId { get; set; }

        public virtual ICollection<PT_CouponUsage> PT_CouponUsages { get; set; }
        public virtual ICollection<Sys_EmpowerOrder> Sys_EmpowerOrders { get; set; }
        public virtual ICollection<Sys_InvoicePayment> Sys_InvoicePayments { get; set; }
        public virtual ICollection<Sys_Invoice> Sys_Invoices { get; set; }
    }
}
