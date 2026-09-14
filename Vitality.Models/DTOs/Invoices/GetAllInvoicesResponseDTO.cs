using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitality.Models.DTOs.Invoices
{
    public class GetInvoicesByFacilityRequestDTO
    {
        public int FacilityId { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? Status { get; set; }
        public long? InvoiceId { get; set; }
        public string? PatientName { get; set; }
    }

    public class GetInvoicesByPatientRequestDTO
    {
        public long PatientId { get; set; }
        public string? Status { get; set; }
        public long? InvoiceId { get; set; }
        public string? PatientName { get; set; }
    }

    public class GetAllInvoicesResponseDTO
    {
        public long InvoiceId { get; set; }
        public string? InvoiceNumber { get; set; }
        public string? CustomerName { get; set; }
        public string? Amount { get; set; }
        public string? Status { get; set; }
        public string? InvoiceType { get; set; }
        public bool? IsActive { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
        public long? SubscriptionId { get; set; }
        public long? CardId { get; set; }
    }
}
