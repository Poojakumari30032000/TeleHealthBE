using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.PatientOrders
{
    public class GetAllPatientOrdersRequestDTO
    {
        public int PageSize { get; set; }
        public int PageNumber { get; set; }
        public string? FacilityGuid { get; set; }
        public string? Title { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string? PaymentStatus { get; set; }
        public string? VisitStatus { get; set; }
        public string? OrderStatus { get; set; }
        public long? ProductId { get; set; }
        public long? PatientId { get; set; }
        public long? FacilityId { get; set; }
        public long? ProviderId { get; set; }
    }
}
