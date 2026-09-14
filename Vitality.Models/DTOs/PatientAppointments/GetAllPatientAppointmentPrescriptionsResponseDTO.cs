using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.PatientAppointments
{
    public class GetAllPatientAppointmentPrescriptionsResponseDTO
    {
        public long? DrugId { get; set; }
        public string? DrugName { get; set; }
        public string? PharmacyName { get; set; }
        public string? Brand {  get; set; }
        public string? Dosage {  get; set; }
        public int? Quantity { get; set; }
        public int? RefillQuantity { get; set; }
        public int? DirectionQuantity { get; set; }
    }
}
