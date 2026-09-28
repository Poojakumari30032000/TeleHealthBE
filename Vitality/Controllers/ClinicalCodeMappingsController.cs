using DudeMeds.Models.DTOs.ClinicalCodes;
using DudeMeds.Models.Repos.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Vitality.Filters;
using Vitality.Helper;
using Vitality.Models.EntityClasses;
using Vitality.Models.Enums;
using Vitality.Models.Security;

namespace DudeMeds.Controllers
{
    /// <summary>
    /// TEL-20 - which ICD-10 / CPT codes belong to a Category, a Service or a
    /// Package, so that selecting a service on an encounter carries the right
    /// billing codes with it. The codes themselves are TEL-19 reference data and
    /// searching them is TEL-21.
    /// <para>
    /// <see cref="AuditActionFilter"/> is applied to the whole controller: every
    /// call here is an administrative change to billing data, and criterion 2 of
    /// the ticket asks for each one to reach the audit log. The filter records
    /// the request; <c>ClinicalCodeMappingsRepo</c> additionally records the
    /// before and after code sets against the entity.
    /// </para>
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [ServiceFilter(typeof(AuditActionFilter))]
    public class ClinicalCodeMappingsController : ControllerBase
    {
        private readonly IClinicalCodeMappingsRepo _mappingsRepo;

        public ClinicalCodeMappingsController(IClinicalCodeMappingsRepo mappingsRepo)
        {
            _mappingsRepo = mappingsRepo;
        }

        long UserId() => long.TryParse(User?.FindFirst("UserId")?.Value, out var id) ? id : 0;

        static ApiResponse<T> Failed<T>(ApiResponse<T> response, string message)
        {
            response.Status = 0;
            response.Success = false;
            response.Message = message;
            return response;
        }

        /// <summary>
        /// The codes mapped to one Category, Service or Package, each resolved
        /// against the code release in force on <c>onDate</c> (today by default).
        /// A code that is no longer in force still comes back, with
        /// <c>isInForce</c> false, so the caller can show it rather than lose it.
        /// </summary>
        [HttpGet("getMappings")]
        [AuthorizeRoles(UserRole.SuperAdmin, UserRole.GlobalAdmin, UserRole.ClinicAdmin, UserRole.Provider)]
        [RequiresPermission(Permissions.Product.View, Permissions.ProductCategory.View)]
        public ApiResponse<List<ClinicalCodeMappingDTO>> GetMappings([FromQuery] GetClinicalCodeMappingsRequestDTO request)
        {
            var response = new ApiResponse<List<ClinicalCodeMappingDTO>>();
            try
            {
                response.Data = _mappingsRepo.GetMappings(request);
            }
            catch (Exception ex) { return Failed(response, ex.Message); }
            return response;
        }

        /// <summary>
        /// Every mapping flagged for review - the queue left behind when a new
        /// code release terminates a code that was already mapped.
        /// </summary>
        [HttpGet("getMappingsNeedingReview")]
        [AuthorizeRoles(UserRole.SuperAdmin, UserRole.GlobalAdmin, UserRole.ClinicAdmin)]
        [RequiresPermission(Permissions.Product.Edit, Permissions.ProductCategory.Edit)]
        public ApiResponse<List<ClinicalCodeMappingDTO>> GetMappingsNeedingReview([FromQuery] DateTime? onDate)
        {
            var response = new ApiResponse<List<ClinicalCodeMappingDTO>>();
            try
            {
                response.Data = _mappingsRepo.GetMappingsNeedingReview(onDate ?? DateTime.UtcNow);
            }
            catch (Exception ex) { return Failed(response, ex.Message); }
            return response;
        }

        /// <summary>
        /// Replaces the complete set of codes on one target. Send the list the
        /// administrator is looking at: codes left out are removed.
        /// <para>
        /// A code that is unknown, malformed, or not in force on the date refuses
        /// the whole request and nothing is written, so a target is never left
        /// half coded.
        /// </para>
        /// </summary>
        [HttpPost("saveMappings")]
        [AuthorizeRoles(UserRole.SuperAdmin, UserRole.GlobalAdmin, UserRole.ClinicAdmin)]
        [RequiresPermission(Permissions.Product.Edit, Permissions.ProductCategory.Edit)]
        public ApiResponse<SaveClinicalCodeMappingsResultDTO> SaveMappings([FromBody] SaveClinicalCodeMappingsRequestDTO request)
        {
            var response = new ApiResponse<SaveClinicalCodeMappingsResultDTO>();
            try
            {
                if (request is null) return Failed(response, "No mapping request was supplied.");

                var result = _mappingsRepo.SaveMappings(request, UserId());

                response.Data = result;
                if (!result.Success) return Failed(response, result.Message);

                response.Message = result.Message;
                response.Success = true;
            }
            catch (Exception ex) { return Failed(response, ex.Message); }
            return response;
        }

        /// <summary>Removes one code from its target. The mapping is deactivated, not deleted.</summary>
        [HttpPost("deleteMapping")]
        [AuthorizeRoles(UserRole.SuperAdmin, UserRole.GlobalAdmin, UserRole.ClinicAdmin)]
        [RequiresPermission(Permissions.Product.Delete, Permissions.ProductCategory.Delete)]
        public ApiResponse<SaveClinicalCodeMappingsResultDTO> DeleteMapping([FromBody] DeleteClinicalCodeMappingRequestDTO request)
        {
            var response = new ApiResponse<SaveClinicalCodeMappingsResultDTO>();
            try
            {
                if (request is null || request.ClinicalCodeMappingId <= 0)
                    return Failed(response, "A mapping id is required.");

                var result = _mappingsRepo.DeleteMapping(request.ClinicalCodeMappingId, UserId());

                response.Data = result;
                if (!result.Success) return Failed(response, result.Message);

                response.Message = result.Message;
                response.Success = true;
            }
            catch (Exception ex) { return Failed(response, ex.Message); }
            return response;
        }

        /// <summary>
        /// Whether a reference code is in use, and by which targets. Anything that
        /// would remove or retire a code has to ask this first and refuse while
        /// the answer is yes - the database enforces the same rule with a
        /// NO ACTION foreign key, but a 400 here is a better error than a
        /// constraint violation.
        /// </summary>
        [HttpGet("getCodeUsage")]
        [AuthorizeRoles(UserRole.SuperAdmin, UserRole.GlobalAdmin, UserRole.ClinicAdmin)]
        [RequiresPermission(Permissions.Product.View, Permissions.ProductCategory.View)]
        public ApiResponse<ClinicalCodeUsageDTO> GetCodeUsage([FromQuery] string? codeSystem, [FromQuery] string? code)
        {
            var response = new ApiResponse<ClinicalCodeUsageDTO>();
            try
            {
                if (string.IsNullOrWhiteSpace(code))
                    return Failed(response, "A code is required.");

                response.Data = _mappingsRepo.GetCodeUsage(codeSystem ?? ClinicalCodeSystem.Icd10Cm, code);
            }
            catch (Exception ex) { return Failed(response, ex.Message); }
            return response;
        }

        /// <summary>
        /// Re-evaluates every mapping against the releases in force on the date.
        /// A code-set import runs this automatically; this endpoint is here for
        /// the case where an administrator wants the queue rebuilt on demand.
        /// </summary>
        [HttpPost("refreshReviewFlags")]
        [AuthorizeRoles(UserRole.SuperAdmin, UserRole.GlobalAdmin)]
        [RequiresPermission(Permissions.Product.Edit, Permissions.ProductCategory.Edit)]
        public ApiResponse<RefreshClinicalCodeReviewFlagsResultDTO> RefreshReviewFlags([FromQuery] DateTime? onDate)
        {
            var response = new ApiResponse<RefreshClinicalCodeReviewFlagsResultDTO>();
            try
            {
                var result = _mappingsRepo.RefreshReviewFlags(onDate ?? DateTime.UtcNow, UserId());

                response.Data = result;
                response.Message = $"{result.Flagged} flagged, {result.Cleared} cleared.";
                response.Success = true;
            }
            catch (Exception ex) { return Failed(response, ex.Message); }
            return response;
        }
    }
}
