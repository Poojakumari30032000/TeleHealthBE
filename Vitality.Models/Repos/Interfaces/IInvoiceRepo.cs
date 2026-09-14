using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vitality.Models.DTOs.Brands;
using Vitality.Models.DTOs.Invoices;
using Vitality.Models.EntityClasses;

namespace Vitality.Models.Repos.Interfaces
{
    public interface IInvoiceRepo
    {
        public GetInvoiceByIdResponseDTO GetInvoiceById(long? InvoiceId);
        public bool SaveInvoice(SaveInvoiceRequestDTO request, long UserId);

        List<GetAllInvoicesResponseDTO> GetAllInvoices();

        PagedInvoicesResponseDTO GetInvoicesByFacilityId(GetInvoicesByFacilityRequestDTO request);

        Task<bool> PayInvoice(long UserId, long InvoiceId);
        List<GetAllInvoicesResponseDTO> GetInvoicesByPatientId(GetInvoicesByPatientRequestDTO request);
        Sys_Invoice SaveInvoiceReturnInoice(SaveInvoiceRequestDTO request, long UserId);

        GetDetailedFacilityInvoiceResponseDTO GetDetailedFacilityInvoice(long invoiceId);
        GetPatientBillResponseDTO GetPatientBill(long invoiceId);
        GetPatientBillResponseDTO GetPatientInvoiceDetail(long invoiceId);
        Task<GetDetailedFacilityInvoiceResponseDTO> GenerateMonthlyFacilityInvoice(long facilityId, DateTime startDate, DateTime endDate);
        Task<bool> CreatePatientBillInvoice(long patientOrderId, long userId);
        PagedFacilityInvoicesResponseDTO GetFacilityInvoicesForGlobalAdmin(GetFacilityInvoiceSummaryRequestDTO request);

        Task<GetPaymentDetailResponseDTO> RecordPayment(long invoiceId, decimal amount, string transactionId, string? squarePaymentId, long? userId, long? patientId, long? facilityId, long? cardId, string paymentMethod, string? notes);
        Task<GetInvoicePaymentSummaryResponseDTO> GetInvoicePaymentSummary(long invoiceId);
        Task<GetPaymentsResponseDTO> GetPayments(GetPaymentsRequestDTO request);
        Task<GetFacilityPaymentSummaryResponseDTO> GetFacilityPaymentSummary(long facilityId, DateTime? startDate, DateTime? endDate);
        Task<GetAdminPaymentDashboardResponseDTO> GetAdminPaymentDashboard(DateTime? startDate, DateTime? endDate);
        Task<bool> RefundPayment(long paymentId, decimal refundAmount, string reason, long userId);
        Task<List<GetInvoicePaymentSummaryResponseDTO>> GetPatientPaymentHistory(long patientId);
        Task<bool> UpdateAppointmentStatus(long appointmentId, string status);

        Task<bool> AutoPayFacilityInvoice(long invoiceId);

        Task<CreateManualClinicToPatientInvoiceResponseDTO> CreateManualClinicToPatientInvoice(CreateManualClinicToPatientInvoiceRequestDTO request, long userId);

        Task<CreateManualClinicToPatientInvoiceResponseDTO> CreateManualGAToClinicInvoice(CreateManualGAToClinicInvoiceRequestDTO request, long userId);

        Task<bool> CreateInvoiceLineItemForBundle(int invoiceId, long bundleId, string? couponCode, decimal originalPrice, decimal discountedPrice, long userId);

        Task<bool> PayManualInvoice(long userId, long invoiceId);

        Task<bool> CancelInvoice(long invoiceId, string? cancellationReason = null);
    }
}
