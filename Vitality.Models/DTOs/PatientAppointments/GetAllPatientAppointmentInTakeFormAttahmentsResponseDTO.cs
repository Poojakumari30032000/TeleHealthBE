using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.PatientAppointments
{
    public class GetAllPatientAppointmentInTakeFormAttahmentsResponseDTO
    {
        public string? AttachmentURL { get; set; }
        public string? UploadedBy { get; set; }
        public DateTime? UploadedDate { get; set; }
    }
}
