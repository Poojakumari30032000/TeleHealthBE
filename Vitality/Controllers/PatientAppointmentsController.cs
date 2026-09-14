using AutoMapper;
using DudeMeds.Models.DTOs.Common;
using DudeMeds.Models.DTOs.PatientAppointments;
using DudeMeds.Models.DTOs.ProviderSchedules;
using DudeMeds.Models.Repos.Interfaces;
using DudeMeds.Models.Repos.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Vitality.Filters;
using Vitality.Helper;
using Vitality.Models.DTOs.PatientAppointments;
using Vitality.Models.Enums;
using Vitality.Models.Repos.Services;
using Vitality.Models.Security;

namespace DudeMeds.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PatientAppointmentsController : ControllerBase
    {
        public IConfiguration _configuration;
        private readonly IMapper _mapper;

        private readonly IPatientAppointmentsRepo _IPatientAppointmentsRepo;
        private readonly INotificationService _notificationService;

        public PatientAppointmentsController(
           IConfiguration config,
           IMapper IMapper,
           IPatientAppointmentsRepo IPatientAppointmentsRepo,
           INotificationService notificationService)
        {
            _configuration = config;
            _mapper = IMapper;
            _IPatientAppointmentsRepo = IPatientAppointmentsRepo;
            _notificationService = notificationService;
        }

        [HttpGet]
        [Route("getAllPatientAppointmentsByMonth")]
        [RequiresPermission(Permissions.Appointment.View, Permissions.Calendar.View)]
        public ApiResponse<List<GetAllPatientAppointmentsByMonthResponseDTO>> GetAllPatientAppointmentsByMonth([FromQuery] GetAllPatientAppointmentsByMonthRequestDTO request)
        {
            ApiResponse<List<GetAllPatientAppointmentsByMonthResponseDTO>> response = new ApiResponse<List<GetAllPatientAppointmentsByMonthResponseDTO>>();
            List<GetAllPatientAppointmentsByMonthResponseDTO> res = _IPatientAppointmentsRepo.GetAllPatientAppointmentsByMonth(request);
            response.Data = res;
            return response;
        }

        [HttpGet]
        [Route("getAllPatientAppointmentsByDays")]
        [RequiresPermission(Permissions.Appointment.View, Permissions.Calendar.View)]
        public ApiResponse<List<GetAllPatientAppointmentsByDaysResponseDTO>> GetAllPatientAppointmentsByDays([FromQuery] GetAllPatientAppointmentsByDaysRequestDTO request)
        {
            ApiResponse<List<GetAllPatientAppointmentsByDaysResponseDTO>> response = new ApiResponse<List<GetAllPatientAppointmentsByDaysResponseDTO>>();
            List<GetAllPatientAppointmentsByDaysResponseDTO> res = _IPatientAppointmentsRepo.GetAllPatientAppointmentsByDays(request);
            response.Data = res;
            return response;
        }

        [HttpGet]
        [Route("getAllPatientAppointments")]
        [RequiresPermission(Permissions.Appointment.View, Permissions.Calendar.View)]
        public ApiResponse<List<GetAllPatientAppointmentsResponseDTO>> GetAllPatientAppointments([FromQuery] GetAllPatientAppointmentsRequestDTO request)
        {
            ApiResponse<List<GetAllPatientAppointmentsResponseDTO>> response = new ApiResponse<List<GetAllPatientAppointmentsResponseDTO>>();
            try
            {
                List<GetAllPatientAppointmentsResponseDTO> result = new List<GetAllPatientAppointmentsResponseDTO>();
                result = _IPatientAppointmentsRepo.GetAllPatientAppointments(request, out int totalPatientAppointmentCount);
                int totalPages = (int)Math.Ceiling((double)totalPatientAppointmentCount / request.PageSize);
                response.Data = result;
                response.TotalEntityCount = totalPatientAppointmentCount;
                response.TotalPages = totalPages;
                response.Data = result;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpGet]
        [Route("getPatientAppointmentById")]
        [RequiresPermission(Permissions.Appointment.View)]
        public ApiResponse<GetPatientAppointmentByIdResponseDTO> GetPatientAppointmentById([FromQuery] GetByIdRequestDTO request)
        {
            ApiResponse<GetPatientAppointmentByIdResponseDTO> response = new ApiResponse<GetPatientAppointmentByIdResponseDTO>();
            try
            {
                GetPatientAppointmentByIdResponseDTO result = new GetPatientAppointmentByIdResponseDTO();
                result = _IPatientAppointmentsRepo.GetPatientAppointmentById(request.Id);
                response.Data = result;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpPost]
        [Route("savePatientAppointment")]
        public async Task<ApiResponse<SavePatientAppointmentResponseDTO>> SavePatientAppointment([FromBody] SavePatientAppointmentRequestDTO request)
        {
            ApiResponse<SavePatientAppointmentResponseDTO> response = new ApiResponse<SavePatientAppointmentResponseDTO>();
            try
            {
                var UserId = Convert.ToInt64(User.FindFirst("UserId").Value);
                long appointmentId = await _IPatientAppointmentsRepo.SavePatientAppointmentAsync(request, UserId, 0, request.PatientId, 0, 0);

                if (appointmentId > 0)
                {

                    var appointment = _IPatientAppointmentsRepo.GetPatientAppointmentById(appointmentId);
                    response.Data = new SavePatientAppointmentResponseDTO
                    {
                        PatientAppointmentSlotId = appointmentId,

                        Success = true,
                        Message = "Appointment created successfully."
                    };
                }
                else
                {
                    response.Data = new SavePatientAppointmentResponseDTO
                    {
                        Success = false,
                        Message = "Failed to create appointment."
                    };
                }
            }
            catch (Exception ex)
            {
                response.Data = new SavePatientAppointmentResponseDTO
                {
                    Success = false,
                    Message = ex.Message
                };
            }
            return response;
        }

        [HttpPost]
        [Route("deletePatientAppointment")]
        public async Task<ApiResponse<bool>> DeletePatientAppointment([FromQuery] GetByIdRequestDTO request)
        {
            var response = new ApiResponse<bool>();
            try
            {
                var idClaim = User.FindFirst("UserId") ?? User.FindFirst("UserIdId");
                if (idClaim == null) throw new Exception("UserId claim missing.");
                var userId = Convert.ToInt64(idClaim.Value);

                var appointmentInfo = _IPatientAppointmentsRepo.GetPatientAppointmentInfo(request.Id, request.ClientTimezoneOffsetMinutes);

                long? patientId = null;
                var patientIdClaim = User.FindFirst("PatientId");
                if (patientIdClaim != null && long.TryParse(patientIdClaim.Value, out long parsedPatientId))
                {
                    patientId = parsedPatientId;
                }
                bool isPatientCancelling = appointmentInfo?.PatientId == patientId;

                response.Data = await _IPatientAppointmentsRepo.DeletePatientAppointmentAsync(request.Id, userId);

                if (response.Data)
                {

                    await _notificationService.SendAppointmentCancellationAsync(
                        appointmentId: request.Id,
                        isPatient: true,
                        ct: HttpContext.RequestAborted
                    );

                    await _notificationService.SendAppointmentCancellationAsync(
                        appointmentId: request.Id,
                        isPatient: false,
                        ct: HttpContext.RequestAborted
                    );
                }
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpGet]
        [Route("getPatientAppointmentInfo")]
        [RequiresPermission(Permissions.Appointment.View)]
        public ApiResponse<GetPatientAppointmentInfoResponseDTO> GetPatientAppointmentInfo([FromQuery] GetByIdRequestDTO request)
        {
            ApiResponse<GetPatientAppointmentInfoResponseDTO> response = new ApiResponse<GetPatientAppointmentInfoResponseDTO>();
            try
            {
                GetPatientAppointmentInfoResponseDTO result = new GetPatientAppointmentInfoResponseDTO();
                result = _IPatientAppointmentsRepo.GetPatientAppointmentInfo(request.Id, request.ClientTimezoneOffsetMinutes);
                response.Data = result;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpGet]
        [Route("getAllPatientAppointmentPrescriptions")]
        [RequiresPermission(Permissions.Prescription.View, Permissions.Appointment.View)]
        public ApiResponse<List<GetAllPatientAppointmentPrescriptionsResponseDTO>> GetAllPatientAppointmentPrescriptions([FromQuery] GetByIdRequestDTO request)
        {
            ApiResponse<List<GetAllPatientAppointmentPrescriptionsResponseDTO>> response = new ApiResponse<List<GetAllPatientAppointmentPrescriptionsResponseDTO>>();
            List<GetAllPatientAppointmentPrescriptionsResponseDTO> res = _IPatientAppointmentsRepo.GetAllPatientAppointmentPrescriptions(request.Id);
            response.Data = res;
            return response;
        }

        [HttpGet]
        [Route("getAllPatientAppointmentInTakeForm")]
        [RequiresPermission(Permissions.Appointment.View)]
        public ApiResponse<List<GetAllPatientAppointmentInTakeFormResponseDTO>> GetAllPatientAppointmentInTakeForm([FromQuery] GetByIdRequestDTO request)
        {
            ApiResponse<List<GetAllPatientAppointmentInTakeFormResponseDTO>> response = new ApiResponse<List<GetAllPatientAppointmentInTakeFormResponseDTO>>();
            List<GetAllPatientAppointmentInTakeFormResponseDTO> res = _IPatientAppointmentsRepo.GetAllPatientAppointmentInTakeForm(request.Id);
            response.Data = res;
            return response;
        }

        [HttpGet]
        [Route("getAllPatientAppointmentInTakeFormAttahments")]
        [RequiresPermission(Permissions.Appointment.View)]
        public ApiResponse<List<GetAllPatientAppointmentInTakeFormAttahmentsResponseDTO>> GetAllPatientAppointmentInTakeFormAttahments([FromQuery] GetByIdRequestDTO request)
        {
            ApiResponse<List<GetAllPatientAppointmentInTakeFormAttahmentsResponseDTO>> response = new ApiResponse<List<GetAllPatientAppointmentInTakeFormAttahmentsResponseDTO>>();
            List<GetAllPatientAppointmentInTakeFormAttahmentsResponseDTO> res = _IPatientAppointmentsRepo.GetAllPatientAppointmentInTakeFormAttahments(request.Id);
            response.Data = res;
            return response;
        }

        [HttpGet]
        [Route("getPatientAppointmentVideoCallId")]
        [RequiresPermission(Permissions.Appointment.View)]
        public ApiResponse<GetPatientAppointmentVideoCallIdRespnseDTO> GetPatientAppointmentVideoCallId([FromQuery] GetPatientAppointmentVideoCallIdRequestDTO request)
        {
            ApiResponse<GetPatientAppointmentVideoCallIdRespnseDTO> response = new ApiResponse<GetPatientAppointmentVideoCallIdRespnseDTO>();
            try
            {

                var userId = Convert.ToInt64(User.FindFirst("UserId").Value);
                var roleId = Convert.ToInt64(User.FindFirst("RoleId").Value);

                request.UserId = userId;
                request.RoleId = roleId;

                GetPatientAppointmentVideoCallIdRespnseDTO result = new GetPatientAppointmentVideoCallIdRespnseDTO();
                result = _IPatientAppointmentsRepo.GetPatientAppointmentVideoCallId(request);
                if (result == null)
                {
                    response.Message = "Unable to access this page.";
                }
                response.Data = result;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpGet]
        [Route("getPatientAppointmentAlert")]
        [RequiresPermission(Permissions.Appointment.View)]
        public ApiResponse<GetPatientAppointmentAlertResponseDTO> GetPatientAppointmentAlert([FromQuery] GetByIdRequestDTO request)
        {
            ApiResponse<GetPatientAppointmentAlertResponseDTO> response = new ApiResponse<GetPatientAppointmentAlertResponseDTO>();
            try
            {
                GetPatientAppointmentAlertResponseDTO result = new GetPatientAppointmentAlertResponseDTO();
                result = _IPatientAppointmentsRepo.GetPatientAppointmentAlert(request.Id);
                response.Data = result;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }
        [AuthorizeRoles(UserRole.Patient , UserRole.Provider)]
        [HttpGet]
        [Route("getPatientAppointmentZoomUrl")]
        [RequiresPermission(Permissions.Appointment.View)]
        public async Task<ApiResponse<GetPatientAppointmentZoomUrlResponseDTO>> GetPatientAppointmentZoomUrl([FromQuery] GetByIdRequestDTO request)
        {
            ApiResponse<GetPatientAppointmentZoomUrlResponseDTO> response = new ApiResponse<GetPatientAppointmentZoomUrlResponseDTO>();
            try
            {
                if (request.Id <= 0)
                {
                    response.Message = "Invalid appointment ID.";
                    return response;
                }

                GetPatientAppointmentZoomUrlResponseDTO result = await _IPatientAppointmentsRepo.GetPatientAppointmentZoomUrlAsync(request.Id, HttpContext.RequestAborted);
                response.Data = result;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }
        [HttpPost]
        [Route("recallPatientAppointment")]
        [RequiresPermission(Permissions.Appointment.Edit)]
        public async Task<ApiResponse<bool>> RecallPatientAppointment([FromBody] SaveFollowUpAppointmentRequestDTO request)
        {
            var response = new ApiResponse<bool> { Data = false };

            try
            {
                if (request == null)
                {
                    response.Message = "Invalid payload.";
                    return response;
                }
                if (request.ProviderId == null || request.ProviderId <= 0)
                {
                    response.Message = "ProviderId is required.";
                    return response;
                }
                if (request.PatientId == null || request.PatientId <= 0)
                {
                    response.Message = "PatientId is required.";
                    return response;
                }
                if (request.PatientTreatmentId == null || request.PatientTreatmentId <= 0)
                {
                    response.Message = "PatientTreatmentId is required.";
                    return response;
                }
                if (request.ProviderScheduledSlotId == null || request.ProviderScheduledSlotId <= 0)
                {
                    response.Message = "ProviderScheduledSlotId is required.";
                    return response;
                }

                bool ok = await _IPatientAppointmentsRepo.SaveFollowUpAppointmentAsync(request);

                if (ok)
                {
                    response.Data = true;
                    response.Message = "Follow-up appointment created successfully.";
                    return response;
                }
                else
                {
                    response.Data = false;
                    response.Message = "Unable to create follow-up appointment. The slot may already be booked or invalid data provided.";
                    return response;
                }

            }
            catch (Exception ex)
            {
                response.Data = false;
                response.Message = $"Unexpected error: {ex.Message}";
                return response;
            }
        }

        [HttpPost]
        [Route("updateAppointmentForFollowUp")]
        [RequiresPermission(Permissions.Appointment.Edit)]
        public async Task<ApiResponse<bool>> UpdateAppointmentForFollowUp([FromBody] UpdateAppointmentForFollowUpRequestDTO request)
        {
            var response = new ApiResponse<bool> { Data = false };

            try
            {
                if (request == null)
                {
                    response.Message = "Invalid payload.";
                    return response;
                }

                if (request.PatientAppointmentSlotId <= 0)
                {
                    response.Message = "PatientAppointmentSlotId is required.";
                    return response;
                }

                if (request.ProviderScheduledSlotId <= 0)
                {
                    response.Message = "ProviderScheduledSlotId is required for the followup appointment date/time.";
                    return response;
                }

                if (!request.UserId.HasValue || request.UserId <= 0)
                {
                    try
                    {
                        var userIdClaim = User.FindFirst("UserId");
                        if (userIdClaim != null && long.TryParse(userIdClaim.Value, out long userId))
                        {
                            request.UserId = userId;
                        }
                    }
                    catch
                    {

                    }
                }

                bool success = await _IPatientAppointmentsRepo.UpdateAppointmentForFollowUpAsync(request);

                if (!success)
                {
                    response.Data = false;
                    response.Message = "Unable to update appointment for followup. Appointment may not exist, new slot may already be booked, or invalid data provided.";
                    return response;
                }

                try
                {

                    await _notificationService.SendAppointmentConfirmationAsync(
                        appointmentId: request.PatientAppointmentSlotId,
                        isPatient: true,
                        ct: HttpContext.RequestAborted
                    );

                    await _notificationService.SendAppointmentConfirmationAsync(
                        appointmentId: request.PatientAppointmentSlotId,
                        isPatient: false,
                        ct: HttpContext.RequestAborted
                    );
                }
                catch (Exception notifEx)
                {

                    System.Diagnostics.Debug.WriteLine($"Failed to send appointment update notifications: {notifEx.Message}");
                }

                response.Data = true;
                response.Message = "Appointment updated successfully for followup.";
                return response;
            }
            catch (Exception ex)
            {
                response.Data = false;
                response.Message = $"Unexpected error: {ex.Message}";
                return response;
            }
        }

    }
}
