using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.PatientAppointments
{
    public class GetPatientAppointmentZoomUrlResponseDTO
    {
        public long PatientAppointmentSlotId { get; set; }
        public string? ZoomJoinUrl { get; set; }
        public string? ZoomPassword { get; set; }
        public string? ZoomMeetingId { get; set; }
        public string? ZoomUUID { get; set; }
        public string? ZoomStatus { get; set; }
        public string? ZoomHostEmail { get; set; }
        public DateTime? ZoomCreatedAt { get; set; }
        public bool HasZoomMeeting { get; set; }
        public string? Message { get; set; }
    }
}
