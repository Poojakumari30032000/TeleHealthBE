using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.PatientPrescriptions
{
    public class GetPatientPrescriptionDetailsResponseDTO
    {
        public string? PatientName { get; set; }
        public string? Medication {  get; set; }
        public DateTime? IssuedOn { get; set; }
        public DateTime? DateRXWritten { get; set; }
        public int? Quantity { get; set; }
        public int? Refills { get; set; }
        public DateTime? ExpireOn { get; set; }
        public bool? IsDAW { get; set; }
        public string? PrescriptionInstruction { get; set; }
        public string? PrescribedBy { get; set; }
        public string? NPI { get; set; }
        public string? Address { get; set; }
        public string? Phone { get; set; }
    }
}
