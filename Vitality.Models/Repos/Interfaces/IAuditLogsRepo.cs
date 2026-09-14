using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Vitality.Models.DTOs.AuditLogs;

namespace Vitality.Models.Repos.Interfaces
{
    public interface IAuditLogsRepo
    {

        Task<List<GetAuditLogsResponseDTO>> GetFacilityAuditLogsAsync(GetFacilityAuditLogsRequestDTO request);

        Task<List<GetAuditLogsResponseDTO>> GetUserAuditLogsAsync(GetUserAuditLogsRequestDTO request);

        Task<List<GetAuditLogsResponseDTO>> GetEntityAuditLogsAsync(GetEntityAuditLogsRequestDTO request);

        Task<(List<GetAuditLogsResponseDTO> logs, int totalCount)> GetAllAuditLogsAsync(GetAllAuditLogsRequestDTO request);

        Task<GetAuditLogStatisticsResponseDTO> GetFacilityAuditStatisticsAsync(long facilityId, DateTime? startDate = null, DateTime? endDate = null);

        Task<List<GetAuditLogsResponseDTO>> GetModuleAuditLogsAsync(GetModuleAuditLogsRequestDTO request);

        Task<(List<GetAuditLogsResponseDTO> logs, int totalCount)> GetPatientAuditLogsAsync(GetPatientAuditLogsRequestDTO request);
    }
}
