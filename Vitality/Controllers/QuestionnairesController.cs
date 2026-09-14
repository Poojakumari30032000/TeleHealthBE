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
    }
}
