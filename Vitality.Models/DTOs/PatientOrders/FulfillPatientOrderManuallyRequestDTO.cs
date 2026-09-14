using System;

namespace DudeMeds.Models.DTOs.PatientOrders
{
    public class FulfillPatientOrderManuallyRequestDTO
    {
        public long PatientOrderId { get; set; }
        public string? OrderNumber { get; set; }
        public string OrderStatus { get; set; } = string.Empty;
        public string? TrackingNumber { get; set; }
        public string? TrackingUrl { get; set; }
        public string? ShippingProvider { get; set; }
        public DateTime? DateShipped { get; set; }
        public string? Notes { get; set; }
    }

    public class FulfillPatientOrderManuallyResultDTO
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public ManualOrderTrackingInfoDTO? ManualTracking { get; set; }
    }
}
