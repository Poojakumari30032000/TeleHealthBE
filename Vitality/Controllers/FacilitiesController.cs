using AutoMapper;
using DudeMeds.Models.DTOs.Common;
using DudeMeds.Models.DTOs.Facilities;
using DudeMeds.Models.Repos.Interfaces;
using DudeMeds.Models.Repos.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vitality.Filters;
using Vitality.Helper;
using Vitality.Models.DTOs.Facilities;
using Vitality.Models.Enums;
using Vitality.Models.Repos.Services;
using Vitality.Services.Email;
using Vitality.Models.Security;

namespace DudeMeds.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Vitality.Filters.Audit]
    public class FacilitiesController : ControllerBase
    {
        public IConfiguration _configuration;
        private readonly IMapper _mapper;

        private readonly IFacilitiesRepo _IFacilitiesRepo;
        private readonly INotificationService _notificationService;
        private readonly IMailSender _mailSender;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<FacilitiesController> _logger;

        public FacilitiesController(
            IConfiguration config,
            IMapper IMapper,
            IFacilitiesRepo IFacilitiesRepo,
            INotificationService notificationService,
            IMailSender mailSender,
            IServiceScopeFactory scopeFactory,
            ILogger<FacilitiesController> logger)
        {
            _configuration = config;
            _mapper = IMapper;
            _IFacilitiesRepo = IFacilitiesRepo;
            _notificationService = notificationService;
            _mailSender = mailSender;
            _scopeFactory = scopeFactory;
            _logger = logger;

        }

        [HttpGet]
        [Route("getAllFacilities")]
        [RequiresPermission(Permissions.Facility.View, Permissions.ClinicInfo.View)]
        public ApiResponse<List<GetAllFacilitiesResponseDTO>> GetAllFacilities([FromQuery] GetAllFacilitiesRequestDTO request)
        {
            ApiResponse<List<GetAllFacilitiesResponseDTO>> response = new ApiResponse<List<GetAllFacilitiesResponseDTO>>();
            try
            {
                List<GetAllFacilitiesResponseDTO> result = new List<GetAllFacilitiesResponseDTO>();
                result = _IFacilitiesRepo.GetAllFacilities(request, out int totalFacilityCount);
                int totalPages = (int)Math.Ceiling((double)totalFacilityCount / request.PageSize);
                response.Data = result;
                response.TotalEntityCount = totalFacilityCount;
                response.TotalPages = totalPages;
                response.Data = result;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [AuthorizeRoles(UserRole.SuperAdmin, UserRole.GlobalAdmin)]
        [HttpGet]
        [Route("getPendingExternalClinics")]
        [RequiresPermission(Permissions.Facility.View)]
        public async Task<ApiResponse<List<GetAllFacilitiesResponseDTO>>> GetPendingExternalClinics(
            [FromQuery] int pageSize = 25,
            [FromQuery] int pageNumber = 1)
        {
            var response = new ApiResponse<List<GetAllFacilitiesResponseDTO>>();
            try
            {
                var ps = pageSize > 0 ? pageSize : 25;
                var pn = pageNumber > 0 ? pageNumber : 1;
                var (items, total) = await _IFacilitiesRepo.GetPendingExternalClinicsAsync(ps, pn, HttpContext.RequestAborted);
                var totalPages = (int)Math.Ceiling((double)total / ps);
                response.Data = items;
                response.TotalEntityCount = total;
                response.TotalPages = totalPages;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpGet]
        [Route("getFacilityById")]
        [RequiresPermission(Permissions.Facility.View, Permissions.ClinicInfo.View)]
        public ApiResponse<GetFacilityByIdResponseDTO> GetFacilityById([FromQuery] GetByIdRequestDTO request)
        {
            ApiResponse<GetFacilityByIdResponseDTO> response = new ApiResponse<GetFacilityByIdResponseDTO>();
            try
            {
                GetFacilityByIdResponseDTO result = new GetFacilityByIdResponseDTO();
                result = _IFacilitiesRepo.GetFacilityById(request.Id);
                response.Data = result;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpGet]
        [Route("getFacilityPaymentMode")]
        [AllowAnonymous]
        public ApiResponse<GetFacilityPaymentModeResponseDTO> GetFacilityPaymentMode([FromQuery] GetByIdRequestDTO request)
        {
            ApiResponse<GetFacilityPaymentModeResponseDTO> response = new ApiResponse<GetFacilityPaymentModeResponseDTO>();
            try
            {
                var result = _IFacilitiesRepo.GetFacilityPaymentMode(request.Id);
                response.Data = result;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpPost]
        [Route("saveFacility")]
        public async Task<ApiResponse<bool>> SaveFacility([FromBody] SaveFacilityRequestDTO request)
        {
            ApiResponse<bool> response = new ApiResponse<bool>();
            try
            {
                var OrganizationId = Convert.ToInt64(User.FindFirst("OrganizationId").Value);
                var UserId = Convert.ToInt64(User.FindFirst("UserId").Value);
                bool isNewFacility = request.FacilityId == 0;
                var saveResult = await _IFacilitiesRepo.SaveFacilityAsync(request, UserId, OrganizationId);
                var res = saveResult.Message;
                if(res == "Facility Created Successfully" || res == "Facility Updated Successfully")
                {
                    response.Message = res;
                    response.Data = true;

                    if (isNewFacility && res == "Facility Created Successfully")
                    {

                        var facilities = _IFacilitiesRepo.GetAllFacilities(new GetAllFacilitiesRequestDTO { PageSize = 1000, PageNumber = 1 }, out _);
                        var facility = facilities?.FirstOrDefault(f =>
                            (!string.IsNullOrWhiteSpace(request.Email) && f.Email == request.Email) ||
                            (!string.IsNullOrWhiteSpace(request.TitleLong) && f.TitleLong == request.TitleLong));

                        if (facility != null && facility.FacilityId > 0)
                        {
                            await _notificationService.SendClinicSignUpAsync(
                                facilityId: facility.FacilityId,
                                ct: HttpContext.RequestAborted
                            );

                            await _notificationService.SendClinicManagementChangeAsync(
                                action: "Add",
                                facilityId: facility.FacilityId,
                                facilityName: facility.TitleLong ?? string.Empty,
                                ct: HttpContext.RequestAborted
                            );
                        }
                    }
                    else if (!isNewFacility && res == "Facility Updated Successfully")
                    {

                        var facility = _IFacilitiesRepo.GetFacilityById(request.FacilityId);
                        if (facility != null)
                        {
                            await _notificationService.SendClinicManagementChangeAsync(
                                action: "Update",
                                facilityId: request.FacilityId,
                                facilityName: facility.TitleLong ?? string.Empty,
                                ct: HttpContext.RequestAborted
                            );
                        }
                    }
                }
                else
                {
                    response.Message = res;
                    response.Data = false;
                }
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpPost]
        [Route("deleteFacility")]
        [RequiresPermission(Permissions.Facility.Delete)]
        public async Task<ApiResponse<bool>> DeleteFacility([FromBody] GetByIdRequestDTO request)
        {
            ApiResponse<bool> response = new ApiResponse<bool>();
            try
            {

                var facility = _IFacilitiesRepo.GetFacilityById(request.Id);
                bool res = _IFacilitiesRepo.DeleteFacility(request.Id);

                if (res && facility != null)
                {
                    await _notificationService.SendClinicManagementChangeAsync(
                        action: "Remove",
                        facilityId: request.Id,
                        facilityName: facility.TitleLong ?? string.Empty,
                        ct: HttpContext.RequestAborted
                    );
                }

                response.Data = res;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpPost]
        [Route("updateFacilityStatus")]
        [RequiresPermission(Permissions.Facility.Edit)]
        public async Task<ApiResponse<bool>> UpdateFacilityStatus([FromBody] UpdateFacilityStatusRequestDTO request)
        {
            ApiResponse<bool> response = new ApiResponse<bool>();
            try
            {
                bool res = await _IFacilitiesRepo.UpdateFacilityStatusAsync(request, HttpContext.RequestAborted);
                response.Data = res;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [AuthorizeRoles(UserRole.SuperAdmin, UserRole.GlobalAdmin)]
        [HttpPost]
        [Route("updateFacilityBillingByFacilityID")]
        [RequiresPermission(Permissions.Facility.Edit)]
        public async Task<ApiResponse<bool>> UpdateFacilityBillingByFacilityID([FromBody] UpdateFacilityBillingRequestDTO request)
        {
            var response = new ApiResponse<bool> { Data = false };
            try
            {
                if (request == null || request.FacilityId <= 0)
                {
                    response.Message = "Valid FacilityId is required.";
                    return response;
                }

                var userId = Convert.ToInt64(User.FindFirst("UserId")?.Value ?? "0");
                bool res = await _IFacilitiesRepo.UpdateFacilityBillingByFacilityIDAsync(request, userId, HttpContext.RequestAborted);
                response.Data = res;
                response.Message = res
                    ? (request.IsBillable ? "Facility marked as billable." : "Facility marked as not billable.")
                    : "Facility not found.";
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpGet]
        [Route("bulkImportTemplate")]
        [RequiresPermission(Permissions.Facility.Add)]
        public IActionResult DownloadBulkImportTemplate()
        {
            var bytes = _IFacilitiesRepo.GenerateFacilityBulkImportTemplate();
            const string fileName = "Facility_Bulk_Import_Template.xlsx";
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }

        [HttpPost]
        [Route("bulkImportFacilitiesExcel")]
        [RequiresPermission(Permissions.Facility.Add)]
        public async Task<ApiResponse<BulkImportFacilitiesExcelResponseDto>> BulkImportFacilitiesExcel(
            [FromForm] BulkUploadFacilitiesRequestDTO request)
        {
            var resp = new ApiResponse<BulkImportFacilitiesExcelResponseDto>();

            try
            {
                if (request?.File == null || request.File.Length == 0)
                {
                    resp.Status = 0;
                    resp.Message = "Excel file is required (.xlsx).";
                    return resp;
                }

                var ext = Path.GetExtension(request.File.FileName);
                if (!string.Equals(ext, ".xlsx", StringComparison.OrdinalIgnoreCase))
                {
                    resp.Status = 0;
                    resp.Message = "Only .xlsx files are supported.";
                    return resp;
                }

                var organizationId = Convert.ToInt64(User.FindFirst("OrganizationId").Value);
                var userId = Convert.ToInt64(User.FindFirst("UserId").Value);

                await using var stream = request.File.OpenReadStream();
                var result = await _IFacilitiesRepo.ImportFacilitiesFromExcelAsync(
                    stream,
                    userId,
                    organizationId,
                    HttpContext.RequestAborted);

                QueueBulkImportFollowUps(result);

                resp.Data = result;
                resp.Status = 1;

                var failedFacility = result.FacilitiesFailed.Count;
                var failedAdmin = result.AdminsFailed.Count;
                var parseErr = result.ParseAndValidationErrors.Count;

                if (result.ImportSucceeded)
                {
                    resp.Message = failedFacility + failedAdmin + parseErr > 0
                        ? $"Import finished with partial success: {result.FacilitiesCreated} clinic(s), {result.AdminsCreated} administrator(s) created. Review failed rows in the summary."
                        : $"Import completed: {result.FacilitiesCreated} clinic(s), {result.AdminsCreated} administrator(s) created.";
                }
                else if (parseErr > 0 && result.FacilityRowsRead == 0)
                {
                    resp.Status = 0;
                    resp.Message = "The workbook could not be imported. Fix the issues listed below and try again.";
                }
                else
                {
                    resp.Message = "No clinics were created. Review the error list for details.";
                    resp.Status = result.ProcessingCompleted ? 1 : 0;
                }
            }
            catch (Exception ex)
            {
                resp.Status = 0;
                resp.Message = ex.Message;
            }

            return resp;
        }

        private void QueueBulkImportFollowUps(BulkImportFacilitiesExcelResponseDto? result)
        {
            if (result == null)
            {
                return;
            }

            var facilities = result.FacilitiesSucceeded?.ToList() ?? new List<FacilityImportSuccessDto>();
            var admins = result.AdminsSucceeded?.ToList() ?? new List<AdminImportSuccessDto>();
            if (facilities.Count == 0 && admins.Count == 0)
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                    var mailSender = scope.ServiceProvider.GetRequiredService<IMailSender>();
                    await SendBulkImportFollowUpsCoreAsync(
                        notificationService,
                        mailSender,
                        facilities,
                        admins,
                        CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Bulk import follow-up notifications/emails failed after import.");
                }
            });
        }

        private static async Task SendBulkImportFollowUpsCoreAsync(
            INotificationService notificationService,
            IMailSender mailSender,
            List<FacilityImportSuccessDto> facilities,
            List<AdminImportSuccessDto> admins,
            CancellationToken ct)
        {
            var distinctFacilities = facilities
                .Where(f => f.FacilityId > 0)
                .GroupBy(f => f.FacilityId)
                .Select(g => g.First())
                .ToList();

            var facilityTasks = distinctFacilities.SelectMany(f => new[]
            {
                SafeRunAsync(ct, () => notificationService.SendClinicSignUpAsync(f.FacilityId, ct)),
                SafeRunAsync(
                    ct,
                    () => notificationService.SendClinicManagementChangeAsync(
                        "Add",
                        f.FacilityId,
                        f.TitleLong ?? string.Empty,
                        ct)),
            });

            await Task.WhenAll(facilityTasks);

            const int maxAdminParallelism = 8;
            using var gate = new SemaphoreSlim(maxAdminParallelism);
            var adminTasks = admins.Select(async a =>
            {
                await gate.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    await SafeRunAsync(ct, () => notificationService.SendClinicAdminSignUpAsync(a.UserId, a.FacilityId, ct))
                        .ConfigureAwait(false);
                    await SafeRunAsync(
                            ct,
                            () => notificationService.SendUserManagementChangeAsync(
                                "Add",
                                a.Email,
                                a.FacilityId,
                                3,
                                ct))
                        .ConfigureAwait(false);
                    await SafeRunAsync(ct, () => mailSender.SendAsync(BuildBulkImportAdminWelcomeEmail(a.Email), ct))
                        .ConfigureAwait(false);
                }
                finally
                {
                    gate.Release();
                }
            });

            await Task.WhenAll(adminTasks).ConfigureAwait(false);
        }

        private static MailTemplateModel BuildBulkImportAdminWelcomeEmail(string email)
        {
            const string loginUrl = "https://www.telehealthus.com";
            return new MailTemplateModel
            {
                ToEmail = email,
                ToName = email,
                Subject = "Your TelehealthUS account is ready",
                PreviewText = "Access your new TelehealthUS account with the details inside.",
                Greeting = "Welcome!",
                BodyParagraphs = new List<string>
                {
                    "Thanks for partnering with TelehealthUS. Your clinic administrator account has been created and you can sign in right away.",
                    $"<strong>Username:</strong> {email}<br /><strong>Temporary password:</strong> AdminUser@123",
                    "For your security, please update your password after your first login."
                },
                ButtonText = "Open TelehealthUS",
                ButtonUrl = loginUrl,
                FooterNote = "Need help getting started? Reply to this email and our onboarding team will reach out."
            };
        }

        private static async Task SafeRunAsync(CancellationToken ct, Func<Task> action)
        {
            try
            {
                await action().ConfigureAwait(false);
            }
            catch
            {

            }
        }

        [HttpPost]
        [Route("bulkUploadCsv")]
        [RequiresPermission(Permissions.Facility.Add)]
        public async Task<ApiResponse<BulkUploadFacilitiesResponseDTO>> BulkUploadCsv([FromForm] BulkUploadFacilitiesRequestDTO request)
        {
            var resp = new ApiResponse<BulkUploadFacilitiesResponseDTO>();

            try
            {
                if (request?.File == null || request.File.Length == 0)
                {
                    resp.Message = "CSV file is required.";
                    return resp;
                }

                long orgId = 1, userId = 2;
                var orgClaim = User.FindFirst("OrganizationId")?.Value;
                var userClaim = User.FindFirst("UserId")?.Value;
                if (long.TryParse(orgClaim, out var oid)) orgId = oid;
                if (long.TryParse(userClaim, out var uid)) userId = uid;

                using var stream = request.File.OpenReadStream();
                var result = await _IFacilitiesRepo.ImportFacilitiesFromCsvAsync(stream, userId, orgId);

                resp.Data = new BulkUploadFacilitiesResponseDTO
                {
                    Inserted = result.Inserted,
                    Errors = result.Errors
                };

                resp.Message = $"Inserted: {result.Inserted}, Existing Skipped: {result.SkippedExisting}, " +
                               $"Duplicates In File: {result.SkippedDuplicateInFile}, Invalid Rows: {result.InvalidRows}";
            }
            catch (Exception ex)
            {
                resp.Message = ex.Message;
            }

            return resp;
        }

        [HttpPost("assign")]
        public async Task<ApiResponse<bool>> Assign([FromBody] AssignFacilityCategoriesRequestDTO request)
        {
            var resp = new ApiResponse<bool>();
            try
            {
                var userId = Convert.ToInt64(User.FindFirst("UserId").Value);
                resp.Data = await _IFacilitiesRepo.AssignCategoriesAsync(request.FacilityId, request.CategoryIds, userId);
                resp.Message = "Categories assigned.";
            }
            catch (Exception ex)
            {
                resp.Message = ex.Message;
            }
            return resp;
        }

        [HttpPost("unassign")]
        public async Task<ApiResponse<bool>> Unassign([FromBody] UnassignFacilityCategoriesRequestDTO request)
        {
            var resp = new ApiResponse<bool>();
            try
            {
                var userId = Convert.ToInt64(User.FindFirst("UserId").Value);
                resp.Data = await _IFacilitiesRepo.UnassignCategoriesAsync(request.FacilityId, request.CategoryIds, userId);
                resp.Message = "Categories unassigned.";
            }
            catch (Exception ex)
            {
                resp.Message = ex.Message;
            }
            return resp;
        }

        [HttpGet("getassigned")]
        public async Task<ApiResponse<List<GetAssignedCategoryResponseDTO>>> GetAssigned([FromQuery] long facilityId)
        {
            var resp = new ApiResponse<List<GetAssignedCategoryResponseDTO>>();
            try
            {
                resp.Data = await _IFacilitiesRepo.GetAssignedCategoriesAsync(facilityId);
            }
            catch (Exception ex)
            {
                resp.Message = ex.Message;
            }
            return resp;
        }

    }
}
