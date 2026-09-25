using DudeMeds.Models.DTOs.ClinicalCodes;
using DudeMeds.Models.Repos.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Vitality.Filters;
using Vitality.Helper;
using Vitality.Models.EntityClasses;
using Vitality.Models.Enums;

namespace DudeMeds.Controllers
{
    /// <summary>
    /// TEL-19 - ICD-10-CM and CPT reference data: loading a published release and
    /// reading it back. Search belongs to TEL-21.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class ClinicalCodesController : ControllerBase
    {
        // A full ICD-10-CM order file is about 15 MB; this leaves headroom.
        private const long MaxCodeSetFileBytes = 64L * 1024 * 1024;

        private readonly IClinicalCodesRepo _clinicalCodesRepo;
        private readonly IConfiguration _configuration;

        public ClinicalCodesController(IClinicalCodesRepo clinicalCodesRepo, IConfiguration configuration)
        {
            _clinicalCodesRepo = clinicalCodesRepo;
            _configuration = configuration;
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
        /// Loads one release from its published file. For ICD-10-CM this is the
        /// CMS order file (icd10cm_order_YYYY.txt). Re-uploading the same
        /// release updates it in place; uploading the next year's file closes
        /// the previous release the day before the new one takes effect.
        /// </summary>
        [HttpPost("importCodeSet")]
        [AuthorizeRoles(UserRole.SuperAdmin, UserRole.GlobalAdmin)]
        [RequestSizeLimit(MaxCodeSetFileBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxCodeSetFileBytes)]
        public ApiResponse<ImportCodeSetResultDTO> ImportCodeSet(IFormFile file, [FromForm] ImportCodeSetRequestDTO request)
        {
            var response = new ApiResponse<ImportCodeSetResultDTO>();
            try
            {
                if (file is null || file.Length == 0)
                    return Failed(response, "No file was selected for upload.");

                var isCpt = string.Equals(request?.CodeSystem?.Trim(), ClinicalCodeSystem.Cpt, StringComparison.OrdinalIgnoreCase);
                if (isCpt && !_configuration.GetValue<bool>("ClinicalCodes:CptLicensed"))
                    return Failed(response, "CPT codes cannot be loaded until a CPT license is confirmed (ClinicalCodes:CptLicensed).");

                using var stream = file.OpenReadStream();
                var result = _clinicalCodesRepo.ImportCodeSet(request!, file.FileName, stream, UserId());

                response.Data = result;
                if (!result.Success) return Failed(response, result.Message);

                response.Success = true;
                response.Message = result.Message;
            }
            catch (Exception ex) { return Failed(response, ex.Message); }
            return response;
        }

        [HttpGet("getCodeSetVersions")]
        [AuthorizeRoles(UserRole.SuperAdmin, UserRole.GlobalAdmin)]
        public ApiResponse<List<CodeSetVersionDTO>> GetCodeSetVersions([FromQuery] string? codeSystem)
        {
            var response = new ApiResponse<List<CodeSetVersionDTO>>();
            try
            {
                response.Data = _clinicalCodesRepo.GetCodeSetVersions(codeSystem);
            }
            catch (Exception ex) { return Failed(response, ex.Message); }
            return response;
        }

        /// <summary>
        /// One ICD-10-CM code as it stood on a date, so a code recorded on an old
        /// encounter resolves against the release in force at the time.
        /// </summary>
        [HttpGet("getIcd10Code")]
        [AuthorizeRoles(UserRole.SuperAdmin, UserRole.GlobalAdmin, UserRole.ClinicAdmin, UserRole.Provider)]
        public ApiResponse<Icd10CodeDTO> GetIcd10Code([FromQuery] GetIcd10CodeRequestDTO request)
        {
            var response = new ApiResponse<Icd10CodeDTO>();
            try
            {
                var onDate = request?.OnDate?.Date ?? DateTime.UtcNow.Date;
                var result = _clinicalCodesRepo.GetIcd10Code(request?.Code ?? string.Empty, onDate);
                if (result is null)
                    return Failed(response, $"No ICD-10-CM code '{request?.Code}' was in force on {onDate:yyyy-MM-dd}.");

                response.Data = result;
            }
            catch (Exception ex) { return Failed(response, ex.Message); }
            return response;
        }
    }
}
