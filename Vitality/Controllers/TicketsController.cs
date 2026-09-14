using AutoMapper;
using DudeMeds.Models.DTOs.Common;
using DudeMeds.Models.DTOs.Tickets;
using DudeMeds.Models.Repos.Interfaces;
using DudeMeds.Models.Repos.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Vitality.Helper;
using Vitality.Models.DTOs.Chats;
using Vitality.Models.Enums;
using Vitality.Models.Repos.Services;
using Vitality.Filters;
using Vitality.Models.Security;

namespace DudeMeds.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TicketsController : ControllerBase
    {
        public IConfiguration _configuration;
        private readonly IMapper _mapper;
        private readonly ITicketsRepo _ITicketsRepo;
        private readonly ChatsRepo _chatsRepo;
        private readonly IUsersRepo _userRepo;
        private readonly INotificationService _notificationService;

        public TicketsController(
            IConfiguration config,
            IMapper IMapper,
            ITicketsRepo ITicketsRepo,
            ChatsRepo chatsRepo,
            IUsersRepo userRepo,
            INotificationService notificationService
            )
        {
            _configuration = config;
            _mapper = IMapper;
            _ITicketsRepo = ITicketsRepo;
            _chatsRepo = chatsRepo;
            _userRepo = userRepo;
            _notificationService = notificationService;

        }

        [HttpGet]
        [Route("getAllTickets")]
        [RequiresPermission(Permissions.SupportTicket.View)]
        public ApiResponse<List<GetAllTicketsResponseDTO>> GetAllTickets([FromQuery] GetAllTicketsRequestDTO request)
        {
            ApiResponse<List<GetAllTicketsResponseDTO>> response = new ApiResponse<List<GetAllTicketsResponseDTO>>();
            try
            {
                var OrganizationId = Convert.ToInt64(User.FindFirst("OrganizationId").Value);
                var currentUserId = User.FindFirst("UserId")?.Value != null ? Convert.ToInt64(User.FindFirst("UserId").Value) : (long?)null;
                var currentUserRoleId = User.FindFirst("RoleId")?.Value != null ? Convert.ToInt32(User.FindFirst("RoleId").Value) : (int?)null;
                var result = _ITicketsRepo.GetAllTickets(request, OrganizationId, currentUserId, currentUserRoleId, out int totalTicketCount);
                response.Data = result;
                var pageSize = request.PageSize > 0 ? request.PageSize : 25;
                response.TotalEntityCount = totalTicketCount;
                response.TotalPages = pageSize > 0 ? (int)Math.Ceiling((double)totalTicketCount / pageSize) : 0;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpGet]
        [Route("getTicketById")]
        [RequiresPermission(Permissions.SupportTicket.View)]
        public ApiResponse<GetTicketByIdResponseDTO> GetTicketById([FromQuery] GetByIdRequestDTO request)
        {
            ApiResponse<GetTicketByIdResponseDTO> response = new ApiResponse<GetTicketByIdResponseDTO>();
            try
            {
                var result = _ITicketsRepo.GetTicketById(request.Id);
                response.Data = result;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpGet]
        [Route("getTicketDetailsById")]
        [RequiresPermission(Permissions.SupportTicket.View)]
        public ApiResponse<GetTicketByIdResponseDTO> GetTicketDetailsById([FromQuery] GetByIdRequestDTO request)
        {
            return GetTicketById(request);
        }

        [HttpPost]
        [Route("saveTicket")]
        [RequiresPermission(Permissions.SupportTicket.Add, Permissions.SupportTicket.Edit)]
        public async Task<ApiResponse<long>> SaveTicket([FromBody] SaveTicketRequestDTO request)
        {
            ApiResponse<long> response = new ApiResponse<long>();
            try
            {
                var UserId = Convert.ToInt64(User.FindFirst("UserId").Value);
                var OrganizationId = Convert.ToInt64(User.FindFirst("OrganizationId").Value);
                long ticketId = _ITicketsRepo.SaveTicket(request, UserId, OrganizationId);
                response.Data = ticketId;

                if (ticketId > 0)
                {
                    var priorityStr = request.Priority.HasValue && Enum.IsDefined(typeof(TicketPriority), request.Priority.Value)
                        ? ((TicketPriority)request.Priority.Value).ToString()
                        : "Medium";
                    var techSupportUsers = _userRepo.GetTechSupportUsersWithEmail();
                    foreach (var (_, email) in techSupportUsers)
                    {
                        if (!string.IsNullOrWhiteSpace(email))
                        {
                            await _notificationService.SendTicketAssignedToTechSupportAsync(
                                ticketId,
                                email,
                                request.Subject ?? "Support ticket",
                                priorityStr,
                                HttpContext.RequestAborted);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpPost]
        [Route("deleteTicket")]
        [RequiresPermission(Permissions.SupportTicket.Delete)]
        public ApiResponse<bool> DeleteTicket([FromBody] GetByIdRequestDTO request)
        {
            ApiResponse<bool> response = new ApiResponse<bool>();
            try
            {
                bool res = _ITicketsRepo.DeleteTicket(request.Id);
                response.Data = res;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpPost]
        [Route("updateTicketStatus")]
        [RequiresPermission(Permissions.SupportTicket.Edit)]
        public async Task<ApiResponse<bool>> UpdateTicketStatus([FromBody] UpdateTicketStatusRequestDTO request)
        {
            ApiResponse<bool> response = new ApiResponse<bool>();
            try
            {
                var UserId = Convert.ToInt64(User.FindFirst("UserId").Value);
                var roleId = User.FindFirst("RoleId")?.Value != null ? Convert.ToInt32(User.FindFirst("RoleId").Value) : 0;
                bool res = _ITicketsRepo.UpdateTicketStatus(request, UserId);
                response.Data = res;

                if (res && roleId == (int)UserRole.GlobalAdmin)
                {
                    var ticket = _ITicketsRepo.GetTicketById(request.Id);
                    if (ticket != null)
                    {
                        var statusText = ((TicketStatus)request.Status).ToString();
                        var techSupportUsers = _userRepo.GetTechSupportUsersWithEmail();
                        foreach (var (_, email) in techSupportUsers)
                        {
                            if (!string.IsNullOrWhiteSpace(email))
                            {
                                await _notificationService.SendTicketUpdateToTechSupportAsync(
                                    request.Id,
                                    email,
                                    "Status updated",
                                    statusText,
                                    ticket.Title,
                                    HttpContext.RequestAborted);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpPost]
        [Route("deleteTicketComment")]
        [RequiresPermission(Permissions.SupportTicket.Delete, Permissions.SupportTicket.Edit)]
        public ApiResponse<bool> DeleteTicketComment([FromBody] GetByIdRequestDTO request)
        {
            var UserId = Convert.ToInt64(User.FindFirst("UserId").Value);
            ApiResponse<bool> response = new ApiResponse<bool>();
            try
            {
                bool res = _ITicketsRepo.DeleteTicketComment(request.Id, UserId);
                if (res)
                {
                    response.Data = true;
                    response.Message = "Ticket comment deleted successfully.";
                }
                else
                {
                    response.Data = false;
                    response.Message = "Only the comment creator can delete their comment";
                }

            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpPost]
        [Route("addTicketComment")]
        [RequiresPermission(Permissions.SupportTicket.Edit, Permissions.SupportTicket.Add)]
        public async Task<ApiResponse<bool>> AddTicketComment([FromBody] AddTicketCommentRequestDTO request)
        {
            ApiResponse<bool> response = new ApiResponse<bool>();
            try
            {
                var UserId = Convert.ToInt64(User.FindFirst("UserId").Value);
                var roleId = User.FindFirst("RoleId")?.Value != null ? Convert.ToInt32(User.FindFirst("RoleId").Value) : 0;
                bool res = _ITicketsRepo.AddTicketComment(request.TicketId, request.Comment ?? "", UserId);
                response.Data = res;
                if (!res) return response;

                var ticket = _ITicketsRepo.GetTicketById(request.TicketId);
                var ticketSubject = ticket?.Title ?? "Support ticket";
                var commentPreview = request.Comment ?? "";

                if (roleId == (int)UserRole.GlobalAdmin)
                {
                    var techSupportUsers = _userRepo.GetTechSupportUsersWithEmail();
                    foreach (var (_, email) in techSupportUsers)
                    {
                        if (!string.IsNullOrWhiteSpace(email))
                        {
                            await _notificationService.SendTicketUpdateToTechSupportAsync(
                                request.TicketId,
                                email,
                                "New comment",
                                commentPreview.Length > 200 ? commentPreview.Substring(0, 200) + "..." : commentPreview,
                                ticketSubject,
                                HttpContext.RequestAborted);
                        }
                    }
                }
                else if (roleId == (int)UserRole.TechSupport)
                {
                    var currentUser = _userRepo.GetUserById(UserId);
                    var userName = currentUser != null ? $"{currentUser.FirstName} {currentUser.LastName}".Trim() : "Tech Support";
                    if (string.IsNullOrWhiteSpace(userName)) userName = "Tech Support";

                    var createdByUserId = ticket?.CreatedByUserId;
                    if (createdByUserId.HasValue && createdByUserId.Value > 0)
                    {
                        var ticketCreator = _userRepo.GetUserById(createdByUserId.Value);
                        if (ticketCreator != null && !string.IsNullOrWhiteSpace(ticketCreator.Email))
                        {
                            await _notificationService.SendTicketCommentByTechSupportToGlobalAdminAsync(
                                request.TicketId,
                                ticketCreator.Email,
                                userName,
                                ticketSubject,
                                commentPreview,
                                HttpContext.RequestAborted);
                        }
                        _ITicketsRepo.CreateTicketCommentNotificationForTicketCreator(request.TicketId, createdByUserId.Value, userName, ticketSubject, commentPreview);
                    }
                }
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpPost]
        [Route("createTicketDocument")]
        [RequiresPermission(Permissions.SupportTicket.Add, Permissions.SupportTicket.Edit)]
        public ApiResponse<long> CreateTicketDocument([FromBody] CreateTicketDocumentRequestDTO request)
        {
            ApiResponse<long> response = new ApiResponse<long>();
            try
            {
                long fileId = _ITicketsRepo.CreateTicketDocument(request);
                response.Data = fileId;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

    }
}
