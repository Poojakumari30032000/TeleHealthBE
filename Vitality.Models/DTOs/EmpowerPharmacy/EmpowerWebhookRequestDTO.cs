using System;
using System.Collections.Generic;

namespace Vitality.Models.DTOs.EmpowerPharmacy
{

    public class EmpowerWebhookRequestDTO
    {
        public string? SalesForceClinicAccountId { get; set; }
        public int? LifeFile_Practice_ID__c { get; set; }
        public string? LFProviderId { get; set; }
        public string? PrescriberOrderNumber { get; set; }
        public int? EipOrderId { get; set; }
        public string? LfOrderId { get; set; }
        public string? LfPatientId { get; set; }
        public string? LfReferenceId { get; set; }
        public string? ClientOrderId { get; set; }
        public string? ClientPatientId { get; set; }
        public string? MessageId { get; set; }
        public int? CanonicalOrderId { get; set; }
        public string? SalesForceOrderId { get; set; }
        public string? Reference1 { get; set; }
        public string? Reference2 { get; set; }
        public string? Reference3 { get; set; }
        public string? Reference4 { get; set; }
        public string? Reference5 { get; set; }
        public string? OrderStatus { get; set; }
        public string? Error { get; set; }
        public string? PrescriptionPdfBase64 { get; set; }
        public DateTime? OrderStatusLastUpdatedTime { get; set; }
        public List<EmpowerWebhookOrderLineDTO>? OrderLines { get; set; }
        public List<EmpowerWebhookShipmentLineDTO>? ShipmentLines { get; set; }
    }

    public class EmpowerWebhookOrderLineDTO
    {
        public string? ItemDesignatorId { get; set; }
        public string? Name { get; set; }
        public string? DosageForm { get; set; }
        public string? Strength { get; set; }
        public string? Size { get; set; }
        public bool? Controlled { get; set; }
        public bool? Refrigerated { get; set; }
        public decimal? Quantity { get; set; }
        public decimal? Price { get; set; }
        public decimal? Total { get; set; }
    }

    public class EmpowerWebhookShipmentLineDTO
    {
        public string? ShipmentStatus { get; set; }
        public DateTime? ShipmentStatusLastUpdatedTime { get; set; }
        public string? ShipmentTrackingNumber { get; set; }
        public string? ShipmentTrackingUrl { get; set; }
        public string? ShipmentProvider { get; set; }
        public string? ShippingAttention { get; set; }
    }
}
