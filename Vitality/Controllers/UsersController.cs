using AutoMapper;
using DudeMeds.Models.DTOs.Common;
using DudeMeds.Models.DTOs.Users;
using DudeMeds.Models.Repos.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Vitality.Filters;
using Vitality.Helper;
using Vitality.Models.DTOs.Users;
using Vitality.Models.Enums;
using Vitality.Models.DTOs.Facilities;
using Vitality.Services.Email;
using Vitality.Services.Sms;
using Vitality.Models.Repos.Services;
using Vitality.Models.Security;

namespace DudeMeds.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [AuthorizeRoles(UserRole.SuperAdmin, UserRole.GlobalAdmin, UserRole.ClinicAdmin, UserRole.Patient , UserRole.Provider)]
    [Vitality.Filters.Audit]
    public class UsersController : ControllerBase
    {
        public IConfiguration _configuration;
        private readonly IMapper _mapper;
        private readonly ISmsSender _smsSender;
        private readonly IUsersRepo _IUserRepo;
        private readonly IMailSender _mailSender;
        private readonly INotificationService _notificationService;
        private readonly IFacilitiesRepo _facilitiesRepo;

        public UsersController(
           IConfiguration config,
           IMapper IMapper,
           IUsersRepo IUsersRepo,
           IMailSender mailSender,
           ISmsSender smsSender,
           INotificationService notificationService,
           IFacilitiesRepo facilitiesRepo)
        {
            _configuration = config;
            _mapper = IMapper;
            _IUserRepo = IUsersRepo;
            _mailSender = mailSender;
            _smsSender = smsSender;
            _notificationService = notificationService;
            _facilitiesRepo = facilitiesRepo;
        }

        [HttpGet]
        [Route("getAllUsers")]
        [RequiresPermission(Permissions.User.View)]
        public ApiResponse<List<GetAllUsersResponseDTO>> GetAllUsers([FromQuery] GetAllUsersRequestDTO request)
        {
            ApiResponse<List<GetAllUsersResponseDTO>> response = new ApiResponse<List<GetAllUsersResponseDTO>>();
            List<GetAllUsersResponseDTO> result = new List<GetAllUsersResponseDTO>();

            var roleIdClaim = User.FindFirst("RoleId");
            if (roleIdClaim != null && int.TryParse(roleIdClaim.Value, out var requestingRoleId))
            {
                request.RequestingRoleId = requestingRoleId;
            }

            result = _IUserRepo.GetAllUsers(request, out int totalUserCount);
            int totalPages = (int)Math.Ceiling((double)totalUserCount / request.PageSize);
            response.Data = result;
            response.TotalEntityCount = totalUserCount;
            response.TotalPages = totalPages;
            return response;
        }

        [HttpGet]
        [Route("getUserById")]
        public ApiResponse<GetUserByIdResponseDTO> GetUserById([FromQuery] GetByIdRequestDTO request)
        {
            ApiResponse<GetUserByIdResponseDTO> response = new ApiResponse<GetUserByIdResponseDTO>();
            GetUserByIdResponseDTO result = new GetUserByIdResponseDTO();
            result = _IUserRepo.GetUserById(request.Id);
            response.Data = result;
            return response;
        }

        [HttpPost]
        [Route("saveUser")]
        [RequiresPermission(Permissions.User.Add, Permissions.User.Edit)]
        public async Task<ApiResponse<long>> SaveUser([FromBody] SaveUserRequestDTO request)
        {
            ApiResponse<long> response = new ApiResponse<long>();
            var OrganizationId = Convert.ToInt64(User.FindFirst("OrganizationId").Value);
            var UserId = Convert.ToInt64(User.FindFirst("UserId").Value);
            bool isNewUser = request.UserId == 0;
            long SavedUserId = _IUserRepo.SaveUser(request, OrganizationId, UserId);
            if (SavedUserId > 0)
            {
                response.Message = "User Has Created Successfully.";
                response.Data = SavedUserId;

                var loginUrl = "https://www.telehealthus.com";
                var displayName = $"{request.FirstName} {request.LastName}".Trim();

                var model = new MailTemplateModel
                {
                    ToEmail = request.Email,
                    ToName = displayName,
                    Subject = "Your TelehealthUS account is ready",
                    PreviewText = "Access your new TelehealthUS account with the details inside.",
                    Greeting = string.IsNullOrWhiteSpace(displayName) ? "Welcome!" : $"Welcome, {displayName}!",
                    BodyParagraphs = new List<string>
                    {
                        "Thanks for partnering with TelehealthUS. Your account has been created and you can sign in right away.",
                        $"<strong>Username:</strong> {request.Email}<br /><strong>Temporary password:</strong> AdminUser@123",
                        "For your security, please update your password after your first login."
                    },
                    ButtonText = "Open TelehealthUS",
                    ButtonUrl = loginUrl,
                    FooterNote = "Need help getting started? Reply to this email and our onboarding team will reach out."
                };

                await _mailSender.SendAsync(model, HttpContext.RequestAborted);

                if (isNewUser)
                {

                    if (request.RoleId == 2)
                    {
                        await _notificationService.SendGlobalAdminSignUpAsync(
                            userId: SavedUserId,
                            ct: HttpContext.RequestAborted
                        );
                    }
                    else if (request.RoleId == 3)
                    {
                        await _notificationService.SendClinicAdminSignUpAsync(
                            userId: SavedUserId,
                            facilityId: request.FacilityId,
                            ct: HttpContext.RequestAborted
                        );
                    }
                    else if (request.RoleId == 4)
                    {
                        await _notificationService.SendDoctorSignUpAsync(
                            userId: SavedUserId,
                            ct: HttpContext.RequestAborted
                        );
                    }

                    await _notificationService.SendUserManagementChangeAsync(
                        action: "Add",
                        userName: $"{request.FirstName} {request.LastName}",
                        facilityId: request.FacilityId,
                        changedUserRoleId: request.RoleId,
                        ct: HttpContext.RequestAborted
                    );
                }
                else
                {

                    await _notificationService.SendUserManagementChangeAsync(
                        action: "Update",
                        userName: $"{request.FirstName} {request.LastName}",
                        facilityId: request.FacilityId,
                        changedUserRoleId: request.RoleId,
                        ct: HttpContext.RequestAborted
                    );
                }

            }
            else if(SavedUserId == 0)
            {
                response.Message = "User Has Updated Successfully.";
                response.Data = SavedUserId;
            }
            else
            {
                response.Message = "Somethong Went Wrong. Please Try Again Later!";
                response.Data = SavedUserId;
            }
            return response;
        }

        [HttpPost]
        [Route("deleteUser")]
        [RequiresPermission(Permissions.User.Delete)]
        public async Task<ApiResponse<bool>> DeleteUser([FromBody] GetByIdRequestDTO request)
        {
            ApiResponse<bool> response = new ApiResponse<bool>();

            var user = _IUserRepo.GetUserById(request.Id);
            bool res = _IUserRepo.DeleteUser(request.Id);

            if (res && user != null)
            {
                await _notificationService.SendUserManagementChangeAsync(
                    action: "Delete",
                    userName: $"{user.FirstName} {user.LastName}",
                    facilityId: user.FacilityId,
                    changedUserRoleId: user.RoleId,
                    ct: HttpContext.RequestAborted
                );
            }

            response.Data = res;
            return response;
        }

        [HttpPost]
        [Route("activateUser")]
        [RequiresPermission(Permissions.User.Edit)]
        public ApiResponse<bool> ActivateUser([FromBody] ActivateUserRequestDTO request)
        {
            ApiResponse<bool> response = new ApiResponse<bool>();
            bool var = _IUserRepo.ActivateUser(request);
            response.Data = var;
            return response;
        }

        [HttpPost]
        [Route("UpdateUserPassword")]
        public ApiResponse<bool> UpdateUserPassword([FromBody] UpdateUserPasswordRequestDTO request)
        {
            ApiResponse<bool> response = new ApiResponse<bool>();
            bool res = _IUserRepo.UpdateUserPassword(request);
            response.Data = res;
            return response;
        }

        [HttpPost]
        [Route("assignUserToFacility")]
        [RequiresPermission(Permissions.User.Edit)]
        public ApiResponse<bool> AssignUserToFacility([FromBody] List<AssignUserToFacilityRequestDTO> requestList)
        {
            ApiResponse<bool> response = new ApiResponse<bool>();
            var OrganizationId = Convert.ToInt64(User.FindFirst("OrganizationId").Value);
            var UserId = Convert.ToInt64(User.FindFirst("UserId").Value);
            string res = _IUserRepo.AssignUserToFacility(requestList,UserId,OrganizationId);
            if(res == "User Successfully Assigned To Facility.")
            {
                response.Message = res;
                response.Data = true;
            }
            else
            {
                response.Message = res;
                response.Data = false;
            }
            return response;
        }

        [HttpGet]
        [Route("checkEmailAlreadyExist")]
        public ApiResponse<bool> CheckEmailAlreadyExist([FromQuery] CheckEmailExistRequestDTO request)
        {
            ApiResponse<bool> response = new ApiResponse<bool>();
            var result = _IUserRepo.CheckEmailExist(request);
            response.Data = result;
            return response;
        }

        [HttpPost]
        [Route("UpdateUserProfile")]
        public ApiResponse<bool> UpdateUserProfile([FromBody] UpdateUserProfileRequestDto request)
        {
            ApiResponse<bool> response = new ApiResponse<bool>();
            bool res = _IUserRepo.UpdateUserProfile(request);
            response.Data = res;
            return response;
        }

        [HttpPost]
        [Route("UpdateUserProfileUrl")]
        public async Task<ApiResponse<bool>> UpdateUserProfileUrl([FromBody] UpdateUserProfileUrlRequestDTO request)
        {
            ApiResponse<bool> response = new ApiResponse<bool>();

            if (request == null || request.UserId <= 0)
            {
                response.Message = "Invalid request. UserId is required.";
                response.Data = false;
                return response;
            }

            bool res = await _IUserRepo.UpdateUserProfileUrlAsync(request);
            response.Data = res;

            if (res)
            {
                response.Message = "Profile picture updated successfully.";
            }
            else
            {
                response.Message = "Failed to update profile picture. User not found.";
            }

            return response;
        }

        [AllowAnonymous]
        [HttpGet("ActiveUserExists")]
        public async Task<IActionResult> ActiveUserExists(
        [FromQuery] string email,
        CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(email))
                return BadRequest("Email is required.");

            bool exists = await _IUserRepo.ActiveUserExistsAsync(email, ct);

            return Ok(new
            {
                email = email.Trim(),
                activeUserExists = exists
            });
        }
        [HttpPost("send")]
        public async Task<IActionResult> Send([FromBody] SendSmsRequest request, CancellationToken ct)
        {
            await _smsSender.SendAsync(request.PhoneNumber, request.Message, ct);
            return Ok();
        }

        [AuthorizeRoles(UserRole.SuperAdmin, UserRole.GlobalAdmin)]
        [HttpPost]
        [Route("approveClinic")]
        [RequiresPermission(Permissions.Facility.Edit)]
        public async Task<ApiResponse<bool>> ApproveClinic([FromBody] ApproveClinicRequestDTO request)
        {
            var response = new ApiResponse<bool> { Data = false };
            if (request == null || request.FacilityId <= 0)
            {
                response.Message = "Valid FacilityId is required.";
                return response;
            }

            var approverUserId = Convert.ToInt64(User.FindFirst("UserId")?.Value ?? "0");
            var result = await _facilitiesRepo.ApproveExternalClinicAsync(
                request.FacilityId,
                approverUserId,
                request.CanViewChannels,
                request.IsBillable,
                request.PaymentModeId,
                HttpContext.RequestAborted);
            response.Message = result.Message;
            response.Data = result.FacilityId.HasValue;

            return response;
        }
    }

    public class SendSmsRequest
    {
        public string PhoneNumber { get; set; }
        public string Message { get; set; }
    }
}
