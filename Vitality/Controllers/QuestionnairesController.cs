using AutoMapper;
using DudeMeds.Models.DTOs.Common;
using DudeMeds.Models.DTOs.Facilities;
using DudeMeds.Models.DTOs.Patients;
using DudeMeds.Models.DTOs.Questionnaires;
using DudeMeds.Models.Repos.Interfaces;
using DudeMeds.Models.Repos.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Vitality.Helper;
using Vitality.Filters;
using Vitality.Models.Enums;
using Vitality.Models.Security;

namespace DudeMeds.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class QuestionnairesController : ControllerBase
    {
        public IConfiguration _configuration;
        private readonly IMapper _mapper;

        private readonly IQuestionnairesRepo _IQuestionnairesRepo;
        private const long ROLE_GLOBAL_ADMIN = 2;
        private const long ROLE_CLINIC_ADMIN = 3;

        public QuestionnairesController(
            IConfiguration config,
            IMapper IMapper,
            IQuestionnairesRepo IQuestionnairesRepo)
        {
            _configuration = config;
            _mapper = IMapper;
            _IQuestionnairesRepo = IQuestionnairesRepo;

        }
        long? UserId() => TryGet<long>("UserId");
        long? OrgId() => TryGet<long>("OrganizationId");
        long? RoleId() => TryGet<long>("RoleId");
        long? FacilityIdClaim() => TryGet<long>("FacilityId");
        long? PatientIdClaim() => TryGet<long>("PatientId");

        T? TryGet<T>(string key)
        {
            var claim = User?.FindFirst(key)?.Value;
            if (string.IsNullOrWhiteSpace(claim)) return default;
            try { return (T)Convert.ChangeType(claim, typeof(T)); }
            catch { return default; }
        }

        [HttpGet("getAllQuestionnaires")]
        [RequiresPermission(Permissions.Questionnaire.View)]
        public ApiResponse<List<GetAllQuestionnairesResponseDTO>> GetAllQuestionnaires([FromQuery] GetAllQuestionnairesRequestDTO request)
        {
            var resp = new ApiResponse<List<GetAllQuestionnairesResponseDTO>>();
            try
            {

                if ((RoleId() == ROLE_GLOBAL_ADMIN) && request.OrganizationId is null)
                    request.OrganizationId = OrgId();

                var data = _IQuestionnairesRepo.GetAllQuestionnaires(request, out int total);
                resp.Data = data;
                resp.TotalEntityCount = total;
                resp.TotalPages = (int)Math.Ceiling((double)total / (request.PageSize > 0 ? request.PageSize : 10));
            }
            catch (Exception ex) { resp.Message = ex.Message; }
            return resp;
        }

        [HttpGet("getQuestionnaireById")]
        [RequiresPermission(Permissions.Questionnaire.View)]
        public ApiResponse<GetQuestionnaireByIdResponseDTO> GetQuestionnaireById([FromQuery] GetByIdRequestDTO request)
        {
            var resp = new ApiResponse<GetQuestionnaireByIdResponseDTO>();
            try
            {
                resp.Data = _IQuestionnairesRepo.GetQuestionnaireById(request.Id, request.FacilityId);
            }
            catch (Exception ex)
            {
                resp.Message = ex.Message;
            }
            return resp;
        }

        [HttpPost("saveQuestionnaire")]
        [RequiresPermission(Permissions.Questionnaire.Add, Permissions.Questionnaire.Edit)]
        public ApiResponse<bool> SaveQuestionnaire([FromBody] SaveQuestionnaireRequestDTO request)
        {
            var resp = new ApiResponse<bool>();
            try
            {
                if (RoleId() != ROLE_GLOBAL_ADMIN)
                {
                    resp.Message = "Only Global Admin can create or update base questionnaires.";
                    resp.Data = false;
                    return resp;
                }

                var uid = UserId() ?? 0;
                var org = OrgId() ?? 0;
                var msg = _IQuestionnairesRepo.SaveQuestionnaire(request, uid, org);

                resp.Message = msg;
                resp.Data = (msg == "Questionnaire Created Successfully" || msg == "Questionnaire Updated Successfully");
            }
            catch (Exception ex) { resp.Message = ex.Message; }
            return resp;
        }

        [HttpPost("deleteQuestionnaire")]
        [RequiresPermission(Permissions.Questionnaire.Delete)]
        public ApiResponse<bool> DeleteQuestionnaire([FromBody] GetByIdRequestDTO request)
        {
            var resp = new ApiResponse<bool>();
            try
            {
                if (RoleId() != ROLE_GLOBAL_ADMIN)
                {
                    resp.Message = "Only Global Admin can delete questionnaires.";
                    resp.Data = false;
                    return resp;
                }

                resp.Data = _IQuestionnairesRepo.DeleteQuestionnaire(request.Id);
            }
            catch (Exception ex) { resp.Message = ex.Message; }
            return resp;
        }

        [HttpPost("updateQuestionnaireStatus")]
        [RequiresPermission(Permissions.Questionnaire.Edit, Permissions.Questionnaire.ListEdit)]
        public ApiResponse<bool> UpdateQuestionnaireStatus([FromBody] UpdateQuestionnaireStatusRequestDTO request)
        {
            var resp = new ApiResponse<bool>();
            if (RoleId() != ROLE_GLOBAL_ADMIN)
            {
                resp.Message = "Only Global Admin can change questionnaire status.";
                resp.Data = false;
                return resp;
            }
            resp.Data = _IQuestionnairesRepo.UpdateQuestionnaireStatus(request);
            return resp;
        }

        [HttpPost("UpdateQuestionnaireJson")]
        [RequiresPermission(Permissions.Questionnaire.Edit)]
        public ApiResponse<bool> UpdateQuestionnaireJson([FromBody] UpdateQuestionnaireJsonRequestDTO request)
        {
            var resp = new ApiResponse<bool>();
            try
            {
                var role = RoleId();
                var uid = UserId() ?? 0;

                if (role == ROLE_GLOBAL_ADMIN)
                {
                    resp.Data = _IQuestionnairesRepo.UpdateQuestionnaireJson(request);
                    return resp;
                }

                if (role == ROLE_CLINIC_ADMIN)
                {
                    var fid = request.FacilityId;
                    if (fid is null || fid <= 0)
                    {
                        resp.Message = "Missing FacilityId for clinic admin.";
                        resp.Data = false;
                        return resp;
                    }

                    var facReq = new UpdateFacilityQuestionnaireJsonRequestDTO
                    {
                        QuestionnaireId = request.QuestionnaireId ?? 0,
                        FacilityId = fid.Value,
                        Json = request.Json
                    };

                    resp.Data = _IQuestionnairesRepo.UpsertFacilityQuestionnaireJson(facReq, uid);
                    if (!resp.Data) resp.Message = "Not allowed or questionnaire not visible to this facility.";
                    return resp;
                }

                resp.Message = "Unauthorized role.";
                resp.Data = false;
            }
            catch (Exception ex) { resp.Message = ex.Message; }
            return resp;
        }

        [HttpPost]
        [Route("duplicateQuestionnaire")]
        [RequiresPermission(Permissions.Questionnaire.Add)]
        public ApiResponse<long> DuplicateQuestionnaire([FromBody] DuplicateQuestionnaireRequestDTO request)
        {
            ApiResponse<long> response = new ApiResponse<long>();
            try
            {
                var UserId = Convert.ToInt64(User.FindFirst("UserId").Value);
                var OrganizationId = Convert.ToInt64(User.FindFirst("OrganizationId").Value);
                long res = _IQuestionnairesRepo.DuplicateQuestionnaire(request, UserId, OrganizationId);
                if (res == -1)
                {
                    response.Message = "Something went wrong. Please try agmin later.";
                    response.Data = res;
                }
                else if(res == 0)
                {
                    response.Message = "Questionnaire Not Found.";
                    response.Data = res;
                }
                else
                {
                    response.Message = "Questionnaire Duplicate Created Successfully";
                    response.Data = res;
                }
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpGet]
        [Route("getQuestionnaireJsonById")]
        public ApiResponse<GetQuestionnaireJsonByIdResponseDTO> GetQuestionnaireJsonById([FromQuery] GetQuestionnaireJsonByIdRequestDTO request)
        {
            ApiResponse<GetQuestionnaireJsonByIdResponseDTO> response = new ApiResponse<GetQuestionnaireJsonByIdResponseDTO>();
            try
            {
                GetQuestionnaireJsonByIdResponseDTO result = new GetQuestionnaireJsonByIdResponseDTO();
                result = _IQuestionnairesRepo.GetQuestionnaireJsonById(request);
                response.Data = result;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }
        [HttpGet("getQuestionnaireJson")]
        public ApiResponse<GetQuestionnaireJsonByIdResponseDTO> GetQuestionnaireJson([FromQuery] GetQuestionnaireJsonRequestDTO request)
        {
            var resp = new ApiResponse<GetQuestionnaireJsonByIdResponseDTO>();
            try
            {
                resp.Data = _IQuestionnairesRepo.GetQuestionnaireJson(request);
            }
            catch (Exception ex) { resp.Message = ex.Message; }
            return resp;
        }

        /// <summary>
        /// The questionnaires the calling patient has completed, most recent first.
        /// The patient is taken from the PatientId claim and is never accepted from
        /// the caller, so this endpoint cannot be pointed at another patient.
        /// </summary>
        [HttpGet("getPatientQuestionnaires")]
        [AuthorizeRoles(UserRole.Patient)]
        [RequiresPermission(Permissions.PatientPortal.View)]
        public ApiResponse<List<GetPatientQuestionnaireSummaryDTO>> GetPatientQuestionnaires()
        {
            var response = new ApiResponse<List<GetPatientQuestionnaireSummaryDTO>>();
            try
            {
                var patientId = PatientIdClaim();
                if (patientId is null || patientId <= 0)
                {
                    response.Message = "No patient is associated with this account.";
                    return response;
                }

                response.Data = _IQuestionnairesRepo.GetPatientQuestionnaires(
                    new GetPatientQuestionnairesRequestDTO { PatientId = patientId.Value });
            }
            catch (Exception ex) { response.Message = ex.Message; }
            return response;
        }

        /// <summary>
        /// The answers of one of the calling patient's own submissions, read only.
        /// The repository re-checks ownership against the PatientId claim, so a
        /// changed PatientTreatmentId returns nothing rather than another
        /// patient's answers.
        /// </summary>
        [HttpGet("getPatientQuestionnaireResponses")]
        [AuthorizeRoles(UserRole.Patient)]
        [RequiresPermission(Permissions.PatientPortal.View)]
        public ApiResponse<List<GetPatientQuestionnaireResponseItemDTO>> GetPatientQuestionnaireResponses([FromQuery] GetByIdRequestDTO request)
        {
            var response = new ApiResponse<List<GetPatientQuestionnaireResponseItemDTO>>();
            try
            {
                var patientId = PatientIdClaim();
                if (patientId is null || patientId <= 0)
                {
                    response.Message = "No patient is associated with this account.";
                    return response;
                }

                var result = _IQuestionnairesRepo.GetPatientQuestionnaireResponses(
                    new GetPatientQuestionnaireResponsesRequestDTO
                    {
                        PatientTreatmentId = request?.Id ?? 0,
                        PatientId = patientId.Value
                    });

                if (result is null)
                {
                    response.Message = "Questionnaire not found.";
                    return response;
                }

                response.Data = result;
            }
            catch (Exception ex) { response.Message = ex.Message; }
            return response;
        }

        // ================================================================
        // TEL-57 - questionnaires assigned to a patient.
        // The caller is always built from claims. A patient id, where one is
        // taken from the request, only names the patient being acted on - the
        // repository checks the caller may reach that patient.
        // ================================================================

        PatientQuestionnaireCallerDTO Caller() => new PatientQuestionnaireCallerDTO
        {
            UserId = UserId() ?? 0,
            RoleId = RoleId(),
            OrganizationId = OrgId(),
            PatientId = RoleId() == (long)UserRole.Patient ? PatientIdClaim() : null
        };

        static ApiResponse<T> Failed<T>(ApiResponse<T> response, string message)
        {
            response.Status = 0;
            response.Success = false;
            response.Message = message;
            return response;
        }

        static ApiResponse<long?> FromResult(PatientQuestionnaireResultDTO result)
        {
            var response = new ApiResponse<long?>();
            if (!result.Success) return Failed(response, result.Message);
            response.Success = true;
            response.Message = result.Message;
            response.Data = result.Id;
            return response;
        }

        /// <summary>Questionnaires staff can give to this patient.</summary>
        [HttpGet("getAssignableQuestionnaires")]
        [AuthorizeRoles(UserRole.SuperAdmin, UserRole.GlobalAdmin, UserRole.ClinicAdmin, UserRole.Provider)]
        [RequiresPermission(Permissions.Patient.Edit)]
        public ApiResponse<List<AssignableQuestionnaireDTO>> GetAssignableQuestionnaires([FromQuery] GetByIdRequestDTO request)
        {
            var response = new ApiResponse<List<AssignableQuestionnaireDTO>>();
            try
            {
                response.Data = _IQuestionnairesRepo.GetAssignableQuestionnaires(request?.Id ?? 0, Caller());
            }
            catch (Exception ex) { return Failed(response, ex.Message); }
            return response;
        }

        [HttpPost("assignPatientQuestionnaire")]
        [AuthorizeRoles(UserRole.SuperAdmin, UserRole.GlobalAdmin, UserRole.ClinicAdmin, UserRole.Provider)]
        [RequiresPermission(Permissions.Patient.Edit)]
        public ApiResponse<long?> AssignPatientQuestionnaire([FromBody] AssignPatientQuestionnaireRequestDTO request)
        {
            try
            {
                return FromResult(_IQuestionnairesRepo.AssignPatientQuestionnaire(request, Caller()));
            }
            catch (Exception ex) { return Failed(new ApiResponse<long?>(), ex.Message); }
        }

        [HttpPost("cancelPatientQuestionnaire")]
        [AuthorizeRoles(UserRole.SuperAdmin, UserRole.GlobalAdmin, UserRole.ClinicAdmin, UserRole.Provider)]
        [RequiresPermission(Permissions.Patient.Edit)]
        public ApiResponse<long?> CancelPatientQuestionnaire([FromBody] GetByIdRequestDTO request)
        {
            try
            {
                return FromResult(_IQuestionnairesRepo.CancelPatientQuestionnaire(request?.Id ?? 0, Caller()));
            }
            catch (Exception ex) { return Failed(new ApiResponse<long?>(), ex.Message); }
        }

        /// <summary>Every assignment of one patient, for the provider's patient view.</summary>
        [HttpGet("getPatientQuestionnaireAssignments")]
        [AuthorizeRoles(UserRole.SuperAdmin, UserRole.GlobalAdmin, UserRole.ClinicAdmin, UserRole.Provider)]
        [RequiresPermission(Permissions.Patient.View)]
        public ApiResponse<List<PatientQuestionnaireAssignmentDTO>> GetPatientQuestionnaireAssignments([FromQuery] GetByIdRequestDTO request)
        {
            var response = new ApiResponse<List<PatientQuestionnaireAssignmentDTO>>();
            try
            {
                response.Data = _IQuestionnairesRepo.GetPatientQuestionnaireAssignments(request?.Id ?? 0, Caller());
            }
            catch (Exception ex) { return Failed(response, ex.Message); }
            return response;
        }

        /// <summary>The calling patient's assigned questionnaires, with status.</summary>
        [HttpGet("getMyAssignedQuestionnaires")]
        [AuthorizeRoles(UserRole.Patient)]
        [RequiresPermission(Permissions.PatientPortal.View)]
        public ApiResponse<List<PatientQuestionnaireAssignmentDTO>> GetMyAssignedQuestionnaires()
        {
            var response = new ApiResponse<List<PatientQuestionnaireAssignmentDTO>>();
            try
            {
                var caller = Caller();
                if (caller.PatientId is null || caller.PatientId <= 0)
                    return Failed(response, "No patient is associated with this account.");

                response.Data = _IQuestionnairesRepo.GetMyAssignedQuestionnaires(caller);
            }
            catch (Exception ex) { return Failed(response, ex.Message); }
            return response;
        }

        /// <summary>The form and saved progress of one of the patient's open assignments.</summary>
        [HttpGet("getMyQuestionnaireForm")]
        [AuthorizeRoles(UserRole.Patient)]
        [RequiresPermission(Permissions.PatientPortal.View)]
        public ApiResponse<PatientQuestionnaireFormDTO> GetMyQuestionnaireForm([FromQuery] GetByIdRequestDTO request)
        {
            var response = new ApiResponse<PatientQuestionnaireFormDTO>();
            try
            {
                var result = _IQuestionnairesRepo.GetMyQuestionnaireForm(request?.Id ?? 0, Caller());
                if (result is null)
                    return Failed(response, "This questionnaire is not available. It may already have been submitted.");

                response.Data = result;
            }
            catch (Exception ex) { return Failed(response, ex.Message); }
            return response;
        }

        [HttpPost("saveMyQuestionnaireDraft")]
        [AuthorizeRoles(UserRole.Patient)]
        [RequiresPermission(Permissions.PatientPortal.View)]
        public ApiResponse<long?> SaveMyQuestionnaireDraft([FromBody] SavePatientQuestionnaireDraftRequestDTO request)
        {
            try
            {
                return FromResult(_IQuestionnairesRepo.SaveMyQuestionnaireDraft(request, Caller()));
            }
            catch (Exception ex) { return Failed(new ApiResponse<long?>(), ex.Message); }
        }

        [HttpPost("submitMyQuestionnaire")]
        [AuthorizeRoles(UserRole.Patient)]
        [RequiresPermission(Permissions.PatientPortal.View)]
        public ApiResponse<long?> SubmitMyQuestionnaire([FromBody] SubmitPatientQuestionnaireRequestDTO request)
        {
            try
            {
                return FromResult(_IQuestionnairesRepo.SubmitMyQuestionnaire(request, Caller()));
            }
            catch (Exception ex) { return Failed(new ApiResponse<long?>(), ex.Message); }
        }

        /// <summary>
        /// A submitted assignment and its answers, read only. Open to the patient
        /// for their own, and to staff for patients they can reach.
        /// </summary>
        [HttpGet("getPatientQuestionnaireSubmission")]
        [AuthorizeRoles(UserRole.SuperAdmin, UserRole.GlobalAdmin, UserRole.ClinicAdmin, UserRole.Provider, UserRole.Patient)]
        [RequiresPermission(Permissions.Patient.View, Permissions.PatientPortal.View)]
        public ApiResponse<PatientQuestionnaireSubmissionDTO> GetPatientQuestionnaireSubmission([FromQuery] GetByIdRequestDTO request)
        {
            var response = new ApiResponse<PatientQuestionnaireSubmissionDTO>();
            try
            {
                var result = _IQuestionnairesRepo.GetPatientQuestionnaireSubmission(request?.Id ?? 0, Caller());
                if (result is null)
                    return Failed(response, "Questionnaire not found.");

                response.Data = result;
            }
            catch (Exception ex) { return Failed(response, ex.Message); }
            return response;
        }
    }
}
