using Vitality.Models.Repos.Services;
using AutoMapper;
using DudeMeds.Models.DTOs.Accounts;
using DudeMeds.Models.Repos.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Vitality.Helper;
using Vitality.Models.DTOs.Accounts;
using Vitality.Models.Enums;
using Vitality.Models.Repos.Services;
using Vitality.Services.Auth;
using Vitality.Services.Email;

namespace DudeMeds.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountsController : ControllerBase
    {
        public IConfiguration _configuration;
        private readonly IMapper _mapper;

        private readonly IAccountsRepo _IAccountsRepo;
        private readonly IMailSender _mailSender;
        private readonly IJwtTokenService _jwtTokenService;
        private readonly INotificationService _notificationService;
        private readonly ILogger<AccountsController> _logger;

        public AccountsController(
            IConfiguration config,
            IMapper IMapper,
            IAccountsRepo IAccountsRepo,
            IMailSender mailSender,
            IJwtTokenService jwtTokenService,
            INotificationService notificationService,
            ILogger<AccountsController> logger)
        {
            _configuration = config;
            _mapper = IMapper;
            _IAccountsRepo = IAccountsRepo;
            _mailSender = mailSender;
            _jwtTokenService = jwtTokenService;
            _notificationService = notificationService;
            _logger = logger;
        }

        [HttpPost]
        [Route("login")]

        [AllowAnonymous]
        public ApiResponse<LoginResponseDTO> Login([FromBody] LoginRequestDTO request)
        {
            ApiResponse<LoginResponseDTO> response = new ApiResponse<LoginResponseDTO>();

            try
            {
                var result = _IAccountsRepo.Login(request);

                if (!string.IsNullOrWhiteSpace(result.ErrorCode))
                {

                    response.Message = result.ErrorCode switch
                    {
                        "EMAIL_REQUIRED" => "Email is required.",
                        "PASSWORD_REQUIRED" => "Password is required.",
                        "INVALID_CREDENTIALS" => "Invalid email or password.",
                        "ACCOUNT_DISABLED" => "Your account has been disabled. Please contact your administrator.",
                        "FACILITY_DISABLED" => "Your facility has been disabled. Please contact your administrator.",
                        "USER_NOT_FOUND" => "User account not found. Please contact your administrator.",
                        "PATIENT_NOT_FOUND" => "Patient account not found. Please contact your administrator.",
                        "USER_CREATION_FAILED" => "Failed to create user account. Please contact your administrator.",
                        "INVALID_USER_ID" => "User account configuration error. Please contact your administrator.",
                        _ => result.ErrorMessage ?? "Login failed. Please try again."
                    };
                    response.Status = 0;
                    response.Data = result;
                    return response;
                }

                if ((result.UserId != null && result.UserId.HasValue) ||
                    (result.PatientId != null && result.PatientId.HasValue))
                {
                    var resolvedRoleName = ResolveRoleName(result.RoleId ?? 0, result.RoleName);
                    result.RoleName = resolvedRoleName;

                    if (result.UserId == null || result.UserId == 0)
                    {
                        response.Status = 0;
                        response.Message = "User account configuration error. Please contact your administrator.";
                        response.Data = result;
                        return response;
                    }

                    var token = _jwtTokenService.CreateToken(new TokenSubject
                    {
                        UserId = result.UserId.Value,
                        LoginId = result.LoginId ?? 0,
                        OrganizationId = result.OrganizationId,
                        RoleId = result.RoleId,
                        RoleName = resolvedRoleName,
                        RoleTitle = result.RoleTitle,
                        PatientId = result.PatientId
                    });

                    result.Token = token;
                    response.Data = result;
                    response.Status = 1;
                    response.Message = "Login successful";
                    return response;
                }

                if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
                {
                    response.Message = result.ErrorMessage;
                }
                else
                {

                    response.Message = "Invalid email or password.";
                }

                response.Status = 0;
                response.Data = result;
                return response;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during login for email: {Email}", request?.Email);
                response.Status = 0;
                response.Message = "An error occurred during login. Please try again later.";
                return response;
            }
        }

        [HttpPost]
        [AllowAnonymous]
        [Route("changePassword")]
        public ApiResponse<ChangePasswordResponseDTO> ChangePassword([FromBody] ChangePasswordRequestDTO request)
        {
            ApiResponse<ChangePasswordResponseDTO> response = new ApiResponse<ChangePasswordResponseDTO>();
            try
            {
                ChangePasswordResponseDTO res = new ChangePasswordResponseDTO();
                res = _IAccountsRepo.ChangePassword(request);
                response.Data = res;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpPost]
        [AllowAnonymous]
        [Route("forgotPassword")]
        public async Task<ApiResponse<bool>> ForgotPassword([FromBody] ForgotPasswordRequestDTO request)
        {
            ApiResponse<bool> response = new ApiResponse<bool>();
            var AuthCode = _IAccountsRepo.GenerateForgotPasswordCode(request.Email);
            if (!String.IsNullOrEmpty(AuthCode))
            {
                string baseUrl = "https://www.telehealthus.com";

                var resetLink = $"{baseUrl}/reset-password?code={AuthCode}";

                var model = new MailTemplateModel
                {
                    ToEmail = request.Email,
                    ToName = request.Email,
                    Subject = "Reset your TelehealthUS password",
                    PreviewText = "Use the secure link to set a new password.",
                    Greeting = "Hi there,",
                    BodyParagraphs = new List<string>
                    {
                        "We received a request to reset the password for your TelehealthUS account.",
                        "For your security, this link will expire shortly. Click the button below to choose a new password.",
                        $"If the button doesn't work, copy and paste this URL into your browser:<br /><a href='{resetLink}'>{resetLink}</a>"
                    },
                    ButtonText = "Set Password",
                    ButtonUrl = resetLink,
                    FooterNote = "Didn't request this change? Please ignore this email or contact support if you have concerns."
                };

                try
                {
                    await _mailSender.SendAsync(model, HttpContext.RequestAborted);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error sending password reset email to {Email}", request.Email);
                    response.Message = "Failed to send password reset email. Please try again later.";
                    response.Data = false;
                    response.Status = 0;
                    return response;
                }

                try
                {
                    await _notificationService.SendPasswordResetSmsAsync(
                        email: request.Email,
                        resetCode: AuthCode,
                        ct: HttpContext.RequestAborted
                    );
                }
                catch (Exception smsEx)
                {

                    _logger?.LogWarning(smsEx, "Failed to send password reset SMS to {Email}, but email was sent successfully", request.Email);
                }

                response.Data = true;
                response.Message = "Password reset link has been sent to your email.";
                return response;
            }
            response.Message = "Invalid Email";
            response.Data = false;
            response.Status = 0;
            return response;
        }

        private static string ResolveRoleName(long roleId, string? existingName)
        {
            if (Enum.IsDefined(typeof(UserRole), (int)roleId))
            {
                return EnumHelper.GetDescription((UserRole)roleId);
            }

            return string.IsNullOrWhiteSpace(existingName) ? "User" : existingName;
        }
    }
}
