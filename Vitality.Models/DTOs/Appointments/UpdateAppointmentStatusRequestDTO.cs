using System;

namespace Vitality.Models.DTOs.Appointments
{
    public class UpdateAppointmentStatusRequestDTO
    {
        public long AppointmentId { get; set; }
        public string Status { get; set; } = default!;
    }
}
