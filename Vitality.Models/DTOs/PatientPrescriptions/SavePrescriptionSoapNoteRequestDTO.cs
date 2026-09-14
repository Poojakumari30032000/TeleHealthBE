using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitality.Models.DTOs.PatientPrescriptions
{
    public class SavePrescriptionSoapNoteRequestDTO
    {
        public long SoapNoteId { get; set; }
        public long PatientPrescriptionId { get; set; }
        public long? PatientAppointmentSlotId { get; set; }

        public string? Subjective { get; set; }
        public string? Objective { get; set; }
        public string? Assessment { get; set; }
        public string? Plan { get; set; }
        public string? AllergiesJson { get; set; }

        public string? SignaturePath { get; set; }
        public string? Signature { get; set; }
        public string? SignedBy { get; set; }
        public DateTime? SignedAt { get; set; }
        public string? Status { get; set; }
    }

    public class AppointmentInfoDTO
    {
        public long? PatientAppointmentSlotId { get; set; }
        public string? Title { get; set; }
        public long? PatientId { get; set; }
        public string? PatientName { get; set; }
        public long? ProviderId { get; set; }
        public string? ProviderName { get; set; }
        public long? FacilityId { get; set; }
        public string? FacilityName { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }
        public int? Duration { get; set; }
        public string? Status { get; set; }
    }

    public class PrescriptionSoapNoteDTO
    {
        public long SoapNoteId { get; set; }
        public long PatientPrescriptionId { get; set; }
        public string? Subjective { get; set; }
        public string? Objective { get; set; }
        public string? Assessment { get; set; }
        public string? Plan { get; set; }
        public string? AllergiesJson { get; set; }
        public string? SignaturePath { get; set; }
        public string? Signature { get; set; }
        public string? SignedBy { get; set; }
        public DateTime? SignedAt { get; set; }
        public string? Status { get; set; }
        public DateTime? CreatedDate { get; set; }
    }

    public class GetSoapWithAppointmentResponseDTO
    {
        public long PatientPrescriptionId { get; set; }
        public AppointmentInfoDTO? Appointment { get; set; }
        public PrescriptionSoapNoteDTO? SoapNote { get; set; }
    }
}
