using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.PatientPayments
{
    public class SavePatientPaymentResponseDTO
    {
        public long PatientPaymentId { get; set; }
        public string? FacilityGuid { get; set; }
        public long? PatientId { get; set; }
        public long? FacilityId { get; set; }
        public long? ProductId { get; set; }
        public long? ProviderId { get; set; }
        public long? PatientTreatmentId { get; set; }
        public decimal? TotalPrice { get; set; }
        public string? CouponCode { get; set; }
        public decimal? DiscountPrice { get; set; }
        public string? ShipmentAddress { get; set; }
        public string? ShipmentStreet { get; set; }
        public int? ShipmentCityId { get; set; }
        public int? ShipmentStateId { get; set; }
        public string? ShipmentZipCode { get; set; }
        public long? ProviderScheduledSlotId { get; set; }
        public long? PatientAppointmentId { get; set; }
    }
}
