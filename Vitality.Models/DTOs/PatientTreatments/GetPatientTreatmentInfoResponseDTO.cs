using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vitality.Models.DTOs.PatientTreatments;

namespace DudeMeds.Models.DTOs.PatientTreatments
{
    public class GetPatientTreatmentInfoResponseDTO
    {
        public long? ChannelId { get; set; }
        public string? ChannelName { get; set; }
        public string? PatientTreatmentGuid { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime? LastEditedDate { get; set; }
        public long? PatientId { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Address { get; set; }
        public string? Gender { get; set; }
        public DateTime? DOB { get; set; }
        public string? PharmacyName { get; set; }
        public string? MRN { get; set; }
        public long? PrescriptionId { get; set; }
        public string? TreatmentType { get; set; }
        public string? Questionnaire { get; set; }
        public DateTime? DateStarted { get; set; }
        public DateTime? DatePrescribed { get; set; }
        public long? LastOrderId { get; set; }
        public DateTime? LastOrderDate { get; set; }
        public decimal? TotalSpentToDate { get; set; }
        public decimal? AverageOrderValue { get; set; }
        public string? OrderStatus { get; set; }
        public string? MembershipPlan { get; set; }
        public decimal? MembershipPrice { get; set; }
        public string? VisitStatus { get; set; }
        public string? VisitTime { get; set; }
        public string? Waittime { get; set; }
        public DateTime? VisitDate { get; set; }
        public string? TreatmentStatus { get; set; }

        public DateTime? NextShippingDate { get; set; }
        public string? SubscriptionStatus { get; set; }
        public string? EmailMarketing { get; set; }
        public string? SMSMarketing { get; set; }
        public string? Coupon { get; set; }
        public AppliedTreatmentCouponDTO? AppliedCoupon { get; set; }
        public DateTime? CreatedAt { get; set; }

        public bool? IsRecurring { get; set; }
        public decimal? RecurringAmountAfterCoupon { get; set; }
        public DateTime? NextRecurringPaymentDate { get; set; }
        public decimal? OriginalPaymentAmount { get; set; }
        public int? RecurringDurationMonths { get; set; }
        public DateTime? RecurringStartDate { get; set; }
        public bool? Refill { get; set; }

        public IList<TreatmentAppointmentDTO> Appointments { get; set; } = new List<TreatmentAppointmentDTO>();
        public IList<PatientPrescriptionDTO> Prescriptions { get; set; } = new List<PatientPrescriptionDTO>();

        public ReadBundleDTO? Bundle { get; set; }
        public ReadCategoryDTO? Category { get; set; }
        public IList<OrderSummaryDTO> Orders { get; set; } = new List<OrderSummaryDTO>();

        public IList<TreatmentIntakeFormDTO> TreatmentIntakeForms { get; set; } = new List<TreatmentIntakeFormDTO>();

        public IList<TreatmentDocumentListItemDTO> Documents { get; set; } = new List<TreatmentDocumentListItemDTO>();

        public IList<TreatmentSoapNoteListItemDTO> SoapNotes { get; set; } = new List<TreatmentSoapNoteListItemDTO>();
    }

    public class TreatmentAppointmentDTO
    {
        public long PatientAppointmentSlotId { get; set; }
        public DateTime StartDate { get; set; }
        public string StartTime { get; set; } = default!;
        public string EndTime { get; set; } = default!;
        public string? Status { get; set; }
        public long ProviderId { get; set; }
        public string? ProviderName { get; set; }
        public long? ProviderScheduledSlotId { get; set; }
    }

    public class TreatmentIntakeFormDTO
    {

        public string? Question { get; set; }
        public string? Answer { get; set; }
        public string? ConsentHtml { get; set; }
        public string? OtherText { get; set; }
        public string? Type { get; set; }

    }

    public class TreatmentDocumentListItemDTO
    {
        public long PatientTreatmentDocumentId { get; set; }
        public string? DocumentName { get; set; }
        public string? Description { get; set; }
        public string? DocumentUrl { get; set; }
    }

    public class TreatmentSoapNoteListItemDTO
    {
        public long SoapNoteId { get; set; }
        public long PatientTreatmentId { get; set; }
        public string? SignedBy { get; set; }
        public DateTime? SignedAt { get; set; }
        public string? Status { get; set; }
        public DateTime? CreatedDate { get; set; }
        public string? CreatedByName { get; set; }
    }
    public class OrderSummaryDTO
    {
        public long PatientOrderId { get; set; }
        public DateTime? CreatedDate { get; set; }
        public string? OrderStatus { get; set; }
        public decimal? OrderTotal { get; set; }
        public decimal? OrderDiscount { get; set; }
        public decimal? OrderPayableAmount { get; set; }
        public string? CouponCode { get; set; }
        public string? FacilityGuid { get; set; }
        public long? ProviderScheduledSlotId { get; set; }

        public string? PatientName { get; set; }

    }

    public class AppliedTreatmentCouponDTO
    {
        public long? CouponCodeId { get; set; }
        public string? CouponCode { get; set; }
        public decimal? DiscountValue { get; set; }
        public string? DiscountType { get; set; }
        public bool AppliesToRecurring { get; set; } = true;
        public string CouponApplicationType { get; set; } = "RecurringAllowed";
        public bool IsActive { get; set; }
        public bool IsExpired { get; set; }
        public bool IsAssignedToBundle { get; set; }
        public bool IsValid { get; set; }
        public string? InvalidReason { get; set; }
        public DateTime? ExpiryDate { get; set; }
    }

    public class PatientPrescriptionDTO
    {
        public long PatientPrescriptionId { get; set; }
        public string? FacilityGuid { get; set; }
        public long? PatientId { get; set; }
        public string? ProductType { get; set; }
        public long? ProductId { get; set; }
        public long? PatientTreatmentId { get; set; }
        public long? PatientOrderId { get; set; }
        public string? OrderStatus { get; set; }
        public long? PatientAppointmentId { get; set; }
        public string? PrescriptionInstruction { get; set; }
        public string? PrescriptionStatus { get; set; }
        public bool? IsActive { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
        public string? Guid { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public string? PresImage { get; set; }
        public DateTime? PrescritionDate { get; set; }
        public DateTime? ExpiryDate { get; set; }

        public string? PatientName { get; set; }
        public string? ProviderName { get; set; }
    }

    public class ReadBundleDTO
    {
        public long BundleId { get; set; }
        public long? ProductId { get; set; }
        public long? ActiveDrugId { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public decimal? Price { get; set; }
        public decimal? ComparePrice { get; set; }
        public string? RegularImageURL { get; set; }
        public bool? IsActive { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public string? Status { get; set; }
        public long? CategoryId { get; set; }
        public int? Visits { get; set; }
    }

    public class ReadCategoryDTO
    {
        public long CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public string? CategoryDescription { get; set; }
        public string? ImageURL { get; set; }
        public bool? IsActive { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
    }
}
