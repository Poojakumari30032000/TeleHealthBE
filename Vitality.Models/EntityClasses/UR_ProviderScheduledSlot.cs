using System;

namespace Vitality.Models.EntityClasses
{
    public partial class UR_ProviderScheduledSlot
    {
        public long ProviderScheduledSlotId { get; set; }

        public DateTime? StartTimeUtc { get; set; }
        public DateTime? EndTimeUtc { get; set; }
        public int? DurationMinutes { get; set; }
        public string? SourceType { get; set; }
        public long? SourceOverrideId { get; set; }

        public long? ProviderId { get; set; }
        public long? FacilityId { get; set; }

        public string? Status { get; set; }

        public long? ProviderScheduleId { get; set; }
        public string? Title { get; set; }
        public DateTime? SlotDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public int? Duration { get; set; }

        public bool? IsActive { get; set; }
        public DateTime? CreatedDate { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public long? ModifiedBy { get; set; }
        public long? OrganizationId { get; set; }

        public virtual UR_ProviderDateOverride? SourceOverride { get; set; }
    }
}
