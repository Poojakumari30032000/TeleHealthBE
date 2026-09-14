using System;
using System.Collections.Generic;

namespace Vitality.Models.DTOs.Invoices
{

    public class GetPaymentDetailResponseDTO
    {
        public long PaymentId { get; set; }
        public long InvoiceId { get; set; }
        public string? InvoiceNumber { get; set; }
        public decimal PaymentAmount { get; set; }
        public string? PaymentMethod { get; set; }
        public string? TransactionId { get; set; }
        public string? SquarePaymentId { get; set; }
        public string? PaymentStatus { get; set; }
        public DateTime PaymentDate { get; set; }
        public string? PaidByName { get; set; }
        public string? PaidByEmail { get; set; }
        public long? PaidByUserId { get; set; }
        public long? PaidByPatientId { get; set; }
        public string? PatientName { get; set; }
        public long? PaidByFacilityId { get; set; }
        public string? FacilityName { get; set; }
        public string? CardLast4 { get; set; }
        public string? CardBrand { get; set; }
        public string? CardHolderName { get; set; }
        public string? PaymentNotes { get; set; }
        public bool IsRefunded { get; set; }
        public decimal? RefundAmount { get; set; }
        public DateTime? RefundDate { get; set; }
        public string? RefundReason { get; set; }
        public string? RefundTransactionId { get; set; }
        public long? CardId { get; internal set; }
    }

    public class GetInvoicePaymentSummaryResponseDTO
    {
        public long InvoiceId { get; set; }
        public string? InvoiceNumber { get; set; }
        public decimal? InvoiceAmount { get; set; }
        public decimal TotalPaidAmount { get; set; }
        public decimal RemainingAmount { get; set; }
        public string? InvoiceStatus { get; set; }
        public string? InvoiceType { get; set; }
        public DateTime? InvoiceDate { get; set; }
        public DateTime? DueDate { get; set; }
        public bool IsFullyPaid { get; set; }
        public bool IsPartiallyPaid { get; set; }
        public bool IsOverpaid { get; set; }
        public int PaymentCount { get; set; }
        public List<GetPaymentDetailResponseDTO> Payments { get; set; } = new();
    }

    public class GetPaymentsRequestDTO
    {
        public long? InvoiceId { get; set; }
        public long? FacilityId { get; set; }
        public long? PatientId { get; set; }
        public long? UserId { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public int? ClientTimezoneOffsetMinutes { get; set; }
        public string? PaymentStatus { get; set; }
        public string? InvoiceType { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    public class GetPaymentsResponseDTO
    {
        public List<GetPaymentDetailResponseDTO> Payments { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    public class GetFacilityPaymentSummaryResponseDTO
    {
        public long FacilityId { get; set; }
        public string? FacilityName { get; set; }
        public decimal TotalInvoiceAmount { get; set; }
        public decimal TotalPaidAmount { get; set; }
        public decimal TotalPendingAmount { get; set; }
        public int TotalInvoices { get; set; }
        public int PaidInvoices { get; set; }
        public int PendingInvoices { get; set; }
        public List<GetInvoicePaymentSummaryResponseDTO> Invoices { get; set; } = new();
    }

    public class GetAdminPaymentDashboardResponseDTO
    {
        public decimal TotalRevenue { get; set; }
        public decimal TotalPendingPayments { get; set; }
        public decimal TotalRefundedAmount { get; set; }
        public int TotalPayments { get; set; }
        public int SuccessfulPayments { get; set; }
        public int FailedPayments { get; set; }
        public int PendingPayments { get; set; }
        public List<GetPaymentDetailResponseDTO> RecentPayments { get; set; } = new();
        public List<GetFacilityPaymentSummaryResponseDTO> FacilityPayments { get; set; } = new();
    }
}
