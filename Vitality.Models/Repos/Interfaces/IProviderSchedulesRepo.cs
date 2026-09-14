using DudeMeds.Models.DTOs.ProviderSchedules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.Repos.Interfaces
{
    public interface IProviderSchedulesRepo
    {
        public List<GetAllProviderSchedulesResponseDTO> GetAllProviderSchedules(GetAllProviderSchedulesRequestDTO request, out int totalProviderScheduleCount);
        public List<GetAllProviderScheduledSlotsResponseDTO> GetAllProviderScheduledSlots(GetAllProviderScheduledSlotsRequestDTO request, out int totalProviderScheduledSlotCount);
        public GetProviderScheduleByIdResponseDTO GetProviderSsheduleById(long ProviderScheduleId);
        public bool SaveProviderSlot(SaveProviderSlotRequestDTO request, long UserId, long OrganizationId);
        public bool RescheduleProviderScheduledSlot(SaveRescheduleProviderScheduledSlotRequestDTO request, long UserId);
        public GetProviderSchedulesStartDateByIdResponseDTO GetProviderSchedulesStartDateById(long ProviderId, int? clientTimezoneOffsetMinutes = null);
        public GetProviderScheduledSlotStartDateByIdResponseDTO GetProviderScheduledSlotStartDateById(long ProviderScheduleId, int? clientTimezoneOffsetMinutes = null);
        public List<GetProviderScheduledSlotTimesResponseDTO> GetProviderScheduledSlotTimes(GetProviderScheduledSlotTimesRequestDTO request);
        public bool DeleteProviderScheduleById(long ProviderScheduleId, long UserId);
        public bool DeleteProviderScheduledSlotById(long ProviderScheduledSlotId, long UserId);
        public List<GetAllProviderScheduledSlotsByMonthResponseDTO> GetAllProviderScheduledSlotsByMonth(GetAllProviderScheduledSlotsByMonthRequestDTO request);
        public List<GetAllProviderScheduledSlotsByDaysResponseDTO> GetAllProviderScheduledSlotsByDays(GetAllProviderScheduledSlotsByDaysRequestDTO request);
    }
}
