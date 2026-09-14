using System;

namespace Vitality.Models.DTOs.Invoices
{

    public class RefundPaymentRequestDTO
    {
        public long PaymentId { get; set; }
        public decimal RefundAmount { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}
