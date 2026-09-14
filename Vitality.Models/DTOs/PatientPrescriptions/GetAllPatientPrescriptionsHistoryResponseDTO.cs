using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.PatientPrescriptions
{
    public class GetAllPatientPrescriptionsHistoryResponseDTO
    {
        public long? PrescriptionId { get; set; }
        public string? PrescriptionInstruction { get; set; }
        public string? Name { get; set; }
        public string? PharmacyName { get; set; }
        public DateTime? WrittenDate { get; set; }
        public string? VisitStatus { get; set; }
        public string? ProductVarient { get; set; }
        public string? DrugName { get; set; }
        public DateTime? OrderDate { get; set; }
        public string? OrderStatus { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? LastEditedDate { get; set; }
    }
}
