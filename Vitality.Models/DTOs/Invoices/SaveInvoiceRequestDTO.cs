using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitality.Models.DTOs.Invoices
{
    public class SaveInvoiceRequestDTO
    {
        public long InvoiceId { get; set; }
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
        public long? PatientId { get; set; }
        public long? FacilityId { get; set; }
        public long? ProviderBillTotal { get; set; }
        public long? PharmacyBillToltal { get; set; }
        public long? CardId { get; set; }
    }
}
