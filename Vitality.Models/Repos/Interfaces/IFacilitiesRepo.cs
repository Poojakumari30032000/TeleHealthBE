using DudeMeds.Models.DTOs.Facilities;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vitality.Models.DTOs.Facilities;

namespace DudeMeds.Models.Repos.Interfaces
{
    public interface IFacilitiesRepo
    {
        public List<GetAllFacilitiesResponseDTO> GetAllFacilities(GetAllFacilitiesRequestDTO request, out int totalFacilityCount);
        public GetFacilityByIdResponseDTO GetFacilityById(long FailityId);
        GetFacilityPaymentModeResponseDTO? GetFacilityPaymentMode(long facilityId);
        public Task<SaveFacilityResult> SaveFacilityAsync(SaveFacilityRequestDTO request, long UserId, long OrganizationId);
        Task<SaveFacilityResult> ExternalClinicSignupAsync(ClinicSignupRequestDTO request, long organizationId, CancellationToken ct = default);
        Task<SaveFacilityResult> ApproveExternalClinicAsync(long facilityId, long approvedByUserId, bool? canViewChannels, bool? isBillable, int paymentModeId, CancellationToken ct = default);
        Task<(List<GetAllFacilitiesResponseDTO> Items, int TotalCount)> GetPendingExternalClinicsAsync(int pageSize, int pageNumber, CancellationToken ct = default);
        public bool DeleteFacility(long FacilityId);
        public Task<bool> UpdateFacilityStatusAsync(UpdateFacilityStatusRequestDTO request, CancellationToken ct = default);
        Task<bool> UpdateFacilityBillingByFacilityIDAsync(UpdateFacilityBillingRequestDTO request, long modifiedByUserId, CancellationToken ct = default);
        Task<BulkImportFacilitiesResultDTO> ImportFacilitiesFromCsvAsync(
          Stream csvStream,
          long userId = 2,
          long organizationId = 1);

        byte[] GenerateFacilityBulkImportTemplate();

        Task<BulkImportFacilitiesExcelResponseDto> ImportFacilitiesFromExcelAsync(
            Stream excelStream,
            long userId,
            long organizationId,
            System.Threading.CancellationToken ct = default);

        Task<bool> AssignCategoriesAsync(long facilityId, IEnumerable<long> categoryIds, long userId, CancellationToken ct = default);
        Task<bool> UnassignCategoriesAsync(long facilityId, IEnumerable<long> categoryIds, long userId, CancellationToken ct = default);
        Task<List<GetAssignedCategoryResponseDTO>> GetAssignedCategoriesAsync(long facilityId, CancellationToken ct = default);
    }
}
