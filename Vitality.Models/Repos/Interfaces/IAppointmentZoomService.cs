using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitality.Models.Repos.Interfaces
{
    public interface IAppointmentZoomService
    {
        Task<bool> EnsureZoomMeetingForAppointmentAsync(long appointmentSlotId, CancellationToken ct = default);
        Task<bool> UpdateZoomMeetingForAppointmentAsync(long appointmentSlotId, CancellationToken ct = default);
        Task<bool> RehostZoomMeetingForAppointmentAsync(long appointmentSlotId, long newProviderUserId, CancellationToken ct = default);
        Task<bool> CancelZoomMeetingForAppointmentAsync(long appointmentSlotId, CancellationToken ct = default);
    }
}
