using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class PT_PatientAppointmentSlot
    {
        public long PatientAppointmentSlotId { get; set; }
        public long? ProductId { get; set; }
        public long? ProviderScheduledSlotId { get; set; }
        public long? PatientTreatmentId { get; set; }
        public long? FacilityId { get; set; }
        public string? Title { get; set; }
        public long? ProviderId { get; set; }
        public long? PatientId { get; set; }
        public DateTime? StartDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public int? Duration { get; set; }
        public string? Status { get; set; }
        public string? VonageSessionId { get; set; }
        public string? VonageTokenId { get; set; }
        public bool? IsActive { get; set; }
        public DateTime? CreatedDate { get; set; }
        public long? CreatedBy { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public long? OrganizationId { get; set; }
        public string? Guid { get; set; }
        public string? ZoomMeetingId { get; set; }
        public string? ZoomUUID { get; set; }
        public string? ZoomJoinUrl { get; set; }
        public string? ZoomStartUrl { get; set; }
        public string? ZoomPassword { get; set; }
        public string? ZoomHostEmail { get; set; }
        public DateTime? ZoomCreatedAt { get; set; }
        public string? ZoomStatus { get; set; }
    }
}
