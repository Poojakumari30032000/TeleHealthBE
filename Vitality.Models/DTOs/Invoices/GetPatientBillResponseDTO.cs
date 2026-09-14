using System;
using System.Collections.Generic;

namespace Vitality.Models.DTOs.Invoices
{

    public class GetPatientBillResponseDTO
    {
        public long InvoiceId { get; set; }
        public string? InvoiceNumber { get; set; }
        public long? PatientId { get; set; }
        public string? PatientName { get; set; }
        public string? PatientEmail { get; set; }
        public string? PatientAddress { get; set; }
        public long? FacilityId { get; set; }
        public string? FacilityName { get; set; }
        public long? ProviderId { get; set; }
        public string? ProviderName { get; set; }
        public decimal? TotalAmount { get; set; }
        public decimal? DiscountAmount { get; set; }
        public decimal? PayableAmount { get; set; }
        public string? CouponCode { get; set; }
        public string? Status { get; set; }
        public DateTime? InvoiceDate { get; set; }
        public DateTime? DueDate { get; set; }
        public string? InvoiceType { get; set; }

        public List<PatientBillItemDTO> Items { get; set; } = new();

        public PaymentDetailDTO? Payment { get; set; }
    }

    public class PatientBillItemDTO
    {
        public long? OrderId { get; set; }
        public long? ProductId { get; set; }
        public string? ProductName { get; set; }
        public string? ProductType { get; set; }
        public int Quantity { get; set; }
        public decimal? UnitPrice { get; set; }
        public decimal? TotalPrice { get; set; }
        public string? CouponCode { get; set; }
        public decimal? Discount { get; set; }
        public DateTime? OrderDate { get; set; }
    }

    public class PaymentDetailDTO
    {
        public long? PaymentId { get; set; }
        public DateTime? PaymentDate { get; set; }
        public string? PaymentStatus { get; set; }
        public string? PaymentMethod { get; set; }
        public string? TransactionId { get; set; }
    }
}
