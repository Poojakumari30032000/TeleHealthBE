using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.PatientAppointments
{
    public class SavePatientAppointmentRequestDTO
    {
        public long PatientAppointmentId { get; set; }
        public long? ProductId  { get; set; }
        public long? PatientTreatmentId  { get; set; }
        public long? UserId  { get; set; }
        public long? FacilityId { get; set; }
        public long? ProviderScheduledSlotId { get; set; }
        public string? Title { get; set; }
        public long? ProviderId { get; set; }
        public long? PatientId { get; set; }
        public DateTime? StartDate { get; set; }
        public string? StartTime { get; set; }
        public string? EndTime { get; set; }
        public int? Duration { get; set; }
        public bool? IsRecurrence { get; set; }
        public List<long>? ResurrenceProviderScheduledSlotId { get; set; }
    }
    public class SaveFollowUpAppointmentRequestDTO
    {
        public long? ProviderId { get; set; }
        public long? PatientTreatmentId { get; set; }
        public long? PatientId { get; set; }
        public long? ProviderScheduledSlotId { get; set; }
        public long? ProductId { get; set; }

        public long? UserId { get; set; }
    }

    public class UpdateAppointmentForFollowUpRequestDTO
    {

        public long PatientAppointmentSlotId { get; set; }

        public long ProviderScheduledSlotId { get; set; }

        public long? UserId { get; set; }
    }

}
