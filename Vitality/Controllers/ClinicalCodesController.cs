using DudeMeds.Models.DTOs.ClinicalCodes;
using DudeMeds.Models.DTOs.Common;
using DudeMeds.Models.Repos.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Vitality.Filters;
using Vitality.Helper;
using Vitality.Models.EntityClasses;
using Vitality.Models.Enums;
using Vitality.Models.Helpers;
using Vitality.Models.Security;

namespace DudeMeds.Controllers
{
    /// <summary>
    /// TEL-19 - ICD-10-CM and CPT reference data: loading a published release and
    /// reading it back (TEL-19), and searching it (TEL-21).
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class ClinicalCodesController : ControllerBase
    {
        // A full ICD-10-CM order file is about 15 MB; this leaves headroom.
        private const long MaxCodeSetFileBytes = 64L * 1024 * 1024;

        private readonly IClinicalCodesRepo _clinicalCodesRepo;
        private readonly ISoapNoteCodesRepo _soapNoteCodesRepo;
        private readonly IConfiguration _configuration;

        public ClinicalCodesController(IClinicalCodesRepo clinicalCodesRepo, ISoapNoteCodesRepo soapNoteCodesRepo, IConfiguration configuration)
        {
            _clinicalCodesRepo = clinicalCodesRepo;
            _soapNoteCodesRepo = soapNoteCodesRepo;
            _configuration = configuration;
        }

        long UserId() => long.TryParse(User?.FindFirst("UserId")?.Value, out var id) ? id : 0;
        long? ClaimLong(string key) => long.TryParse(User?.FindFirst(key)?.Value, out var v) ? v : null;

        /// <summary>The caller, built only from the signed token.</summary>
        ClinicalCodeCallerDTO Caller() => new ClinicalCodeCallerDTO
        {
            UserId = UserId(),
            RoleId = ClaimLong("RoleId"),
            OrganizationId = ClaimLong("OrganizationId")
        };

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

        /// <summary>
        /// TEL-21 - type-ahead search for coding an encounter. Accepts a code, part
        /// of a code with or without the dot ('E11.6', 'e116'), or words from the
        /// description ('type 2 diab'), and returns ranked, paged matches from the
        /// release in force on OnDate (default today). CPT is supported but returns
        /// nothing until a licensed CPT release is loaded (TEL-19).
        /// </summary>
        [HttpGet("searchCodes")]
        [AuthorizeRoles(UserRole.SuperAdmin, UserRole.GlobalAdmin, UserRole.ClinicAdmin, UserRole.Provider)]
        [RequiresPermission(Permissions.PatientTreatment.Edit, Permissions.Treatment.Update)]
        public ApiResponse<SearchClinicalCodesResultDTO> SearchCodes([FromQuery] SearchClinicalCodesRequestDTO request)
        {
            var response = new ApiResponse<SearchClinicalCodesResultDTO>();
            try
            {
                var system = request?.CodeSystem?.Trim();
                if (!string.IsNullOrEmpty(system)
                    && !string.Equals(system, ClinicalCodeSystem.Icd10Cm, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(system, ClinicalCodeSystem.Cpt, StringComparison.OrdinalIgnoreCase))
                    return Failed(response, $"Code system must be '{ClinicalCodeSystem.Icd10Cm}' or '{ClinicalCodeSystem.Cpt}'.");

                if (!ClinicalCodeSearch.Parse(request?.Query, ClinicalCodeSystem.Icd10Cm).IsSearchable)
                    return Failed(response, $"Enter at least {ClinicalCodeSearch.MinQueryLength} characters to search.");

                response.Data = _clinicalCodesRepo.SearchCodes(request!);
                response.Success = true;
            }
            catch (Exception ex) { return Failed(response, ex.Message); }
            return response;
        }

        // ================================================================
        // TEL-22 - codes on a treatment SOAP note, the "encounter" TEL-22
        // codes against. There are no treatment SOAP note endpoints in this
        // repository to mirror, so access follows the permissions the SOAP
        // note screen and TEL-21 search already use, plus the TEL-57
        // patient-reach check in the repository.
        // ================================================================

        /// <summary>The codes on one treatment SOAP note, in order, with the date they are coded against.</summary>
        [HttpGet("getSoapNoteCodes")]
        [AuthorizeRoles(UserRole.SuperAdmin, UserRole.GlobalAdmin, UserRole.ClinicAdmin, UserRole.Provider)]
        [RequiresPermission(Permissions.PatientTreatment.View, Permissions.Treatment.View)]
        public ApiResponse<SoapNoteCodesDTO> GetSoapNoteCodes([FromQuery] GetByIdRequestDTO request)
        {
            var response = new ApiResponse<SoapNoteCodesDTO>();
            try
            {
                var result = _soapNoteCodesRepo.GetSoapNoteCodes(request?.Id ?? 0, Caller());
                if (!result.Success) return Failed(response, result.Message);
                response.Data = result.Data!;
                response.Success = true;
            }
            catch (Exception ex) { return Failed(response, ex.Message); }
            return response;
        }

        /// <summary>
        /// Replaces every code on a treatment SOAP note. Each code must be active and
        /// in force on the note's date in the release it was picked from, and not an
        /// ICD-10-CM header code. All-or-nothing; the rejected codes come back in Errors.
        /// Provider only, as editing the note itself is on the SOAP note screen.
        /// </summary>
        [HttpPost("saveSoapNoteCodes")]
        [AuthorizeRoles(UserRole.Provider)]
        [RequiresPermission(Permissions.PatientTreatment.Edit, Permissions.Treatment.Update)]
        public ApiResponse<SoapNoteCodesResultDTO> SaveSoapNoteCodes([FromBody] SaveSoapNoteCodesRequestDTO request)
        {
            var response = new ApiResponse<SoapNoteCodesResultDTO>();
            try
            {
                if (request is null) return Failed(response, "A request body is required.");

                var result = _soapNoteCodesRepo.SaveSoapNoteCodes(request, Caller());
                response.Data = result;
                if (!result.Success) return Failed(response, result.Message);
                response.Success = true;
                response.Message = result.Message;
            }
            catch (Exception ex) { return Failed(response, ex.Message); }
            return response;
        }
    }
}
