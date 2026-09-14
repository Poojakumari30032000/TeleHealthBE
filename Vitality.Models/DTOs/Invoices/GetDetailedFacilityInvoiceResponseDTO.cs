using System;
using System.Collections.Generic;
using DudeMeds.Models.DTOs.PatientOrders;

namespace Vitality.Models.DTOs.Invoices
{

    public class GetDetailedFacilityInvoiceResponseDTO
    {
        public long InvoiceId { get; set; }
        public string? InvoiceNumber { get; set; }
        public long? FacilityId { get; set; }
        public string? FacilityName { get; set; }
        public string? FacilityPhone { get; set; }
        public string? FacilityEmail { get; set; }
        public string? FacilityAddress { get; set; }
        public decimal? TotalAmount { get; set; }
        public decimal? ProviderBillTotal { get; set; }
        public decimal? PharmacyBillTotal { get; set; }
        public decimal? WholesaleTotal { get; set; }
        public decimal? RetailTotal { get; set; }

        public decimal? PlatformFee { get; set; }
        public bool IsLocked { get; set; }
        public string? Status { get; set; }
        public DateTime? InvoiceDate { get; set; }
        public DateTime? DueDate { get; set; }
        public string? InvoiceType { get; set; }

        public List<ProviderInvoiceDetailDTO> ProviderInvoiceDetails { get; set; } = new();

        public List<MedicationInvoiceDetailDTO> MedicationInvoiceDetails { get; set; } = new();

        public List<FacilityInvoiceOrderDTO> OrderDetails { get; set; } = new();

        public InvoiceSummaryDTO Summary { get; set; } = new();
    }

    public class ProviderInvoiceDetailDTO
    {
        public long? ProviderId { get; set; }
        public string? ProviderName { get; set; }
        public string? ProviderEmail { get; set; }
        public int AppointmentCount { get; set; }
        public decimal? TotalAppointmentAmount { get; set; }
        public List<AppointmentDetailDTO> Appointments { get; set; } = new();
    }

    public class AppointmentDetailDTO
    {
        public long AppointmentId { get; set; }
        public long? PatientId { get; set; }
        public string? PatientName { get; set; }
        public DateTime? AppointmentDate { get; set; }
        public string? AppointmentStatus { get; set; }
        public decimal? AppointmentAmount { get; set; }
        public string? ProductName { get; set; }
        public string? TreatmentName { get; set; }
    }

    public class MedicationInvoiceDetailDTO
    {
        public long? ProductId { get; set; }
        public string? ProductName { get; set; }
        public string? ProductType { get; set; }
        public int Quantity { get; set; }
        public decimal? UnitPrice { get; set; }
        public decimal? TotalPrice { get; set; }
        public long? BundleId { get; set; }
        public string? BundleName { get; set; }
        public List<OrderDetailDTO> Orders { get; set; } = new();
    }

    public class OrderDetailDTO
    {
        public long OrderId { get; set; }
        public long? PatientId { get; set; }
        public string? PatientName { get; set; }
        public DateTime? OrderDate { get; set; }
        public decimal? OrderAmount { get; set; }
        public string? OrderStatus { get; set; }
    }

    public class FacilityInvoiceOrderDTO
    {
        public long OrderId { get; set; }
        public long? PatientId { get; set; }
        public string? PatientName { get; set; }
        public DateTime? OrderDate { get; set; }
        public string? OrderStatus { get; set; }
        public string? CouponCode { get; set; }
        public decimal? RetailAmount { get; set; }
        public decimal? Discount { get; set; }
        public decimal? PayableAmount { get; set; }
        public decimal? WholesaleAmount { get; set; }
        public decimal? SuppliesWholesaleAmount { get; set; }
        public string? TreatmentName { get; set; }
        public OrderProviderDTO? Provider { get; set; }
        public IList<OrderDrugItemDTO> Drugs { get; set; } = new List<OrderDrugItemDTO>();
        public IList<OrderMedicineDTO> Medicines { get; set; } = new List<OrderMedicineDTO>();
    }

    public class InvoiceSummaryDTO
    {
        public int TotalAppointments { get; set; }
        public int TotalOrders { get; set; }
        public int TotalProviders { get; set; }
        public int TotalProducts { get; set; }
        public decimal? SubTotal { get; set; }
        public decimal? Tax { get; set; }
        public decimal? Total { get; set; }
    }
}
