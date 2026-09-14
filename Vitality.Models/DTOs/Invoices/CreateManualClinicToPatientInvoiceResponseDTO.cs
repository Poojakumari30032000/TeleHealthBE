using System;

namespace Vitality.Models.DTOs.Invoices
{
    public class CreateManualClinicToPatientInvoiceResponseDTO
    {
        public int InvoiceId { get; set; }
        public string? InvoiceNumber { get; set; }
        public DateTime? InvoiceDate { get; set; }
        public DateTime? DueDate { get; set; }
        public decimal? AmountDue { get; set; }
        public string? Status { get; set; }
        public bool Success { get; set; }
        public string? Message { get; set; }
    }
}
