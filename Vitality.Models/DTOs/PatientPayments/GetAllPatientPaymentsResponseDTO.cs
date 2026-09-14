using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.PatientPayments
{
    public class GetAllPatientPaymentsResponseDTO
    {
        public long PatientPaymentId { get; set; }
        public string? FacilityGuid { get; set; }
        public long? PatientId { get; set; }
        public string? PatientName { get; set; }
        public string? OrderNumber { get; set; }
        public string? PaymentStatus { get; set; }
        public DateTime? Date {  get; set; }
        public decimal? GrossPayment {  get; set; }
        public decimal? Fees { get; set; }
        public decimal? Refunds { get; set; }
        public decimal? NetPayment { get; set;}
    }
}
