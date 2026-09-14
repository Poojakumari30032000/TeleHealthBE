using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.PatientPayments
{
    public class GetAllPatientPaymentsRequestDTO
    {
        public int PageSize { get; set; }
        public int PageNumber { get; set; }
        public string? FacilityGuid { get; set; }
        public string? Title { get; set; }
        public long? PatientId { get; set; }
    }
}
