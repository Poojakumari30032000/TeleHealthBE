using AutoMapper;
using DudeMeds.Models.DTOs.Tickets;
using DudeMeds.Models.Repos.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vitality.Models.CommonMethods;
using Vitality.Models.EntityClasses;
using Vitality.Models.Enums;
using Vitality.Models.Repos;
using Vitality.Models.Repos.Services.Audit;

namespace DudeMeds.Models.Repos.Services
{
    public class TicketsRepo : BaseRepo, ITicketsRepo
    {
        private readonly IMapper _mapper;
        private readonly IAuditService _auditService;
        public TicketsRepo(IMapper mapper, IAuditService auditService)
        {
            _mapper = mapper;
            _auditService = auditService;
        }

        private static int PriorityToInt(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return (int)TicketPriority.Medium;
            return Enum.TryParse<TicketPriority>(s, true, out var e) ? (int)e : (int)TicketPriority.Medium;
        }
        private static string PriorityToString(int? value)
        {
            if (!value.HasValue) return nameof(TicketPriority.Medium);
            return Enum.IsDefined(typeof(TicketPriority), value.Value) ? ((TicketPriority)value.Value).ToString() : nameof(TicketPriority.Medium);
        }
        private static int StatusToInt(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return (int)TicketStatus.Pending;
            return Enum.TryParse<TicketStatus>(s, true, out var e) ? (int)e : (int)TicketStatus.Pending;
        }
        private static string StatusToString(int? value)
        {
            if (!value.HasValue) return nameof(TicketStatus.Pending);
            return Enum.IsDefined(typeof(TicketStatus), value.Value) ? ((TicketStatus)value.Value).ToString() : nameof(TicketStatus.Pending);
        }

        public List<GetAllTicketsResponseDTO> GetAllTickets(GetAllTicketsRequestDTO request, long OrganizationId, long? currentUserId, int? currentUserRoleId, out int totalTicketCount)
        {
            var query = _db.SYS_Tickets.Where(x => x.IsActive == true && x.OrganizationId == OrganizationId).AsQueryable();

            if (request.AssignedToUserId.HasValue)
                query = query.Where(x => x.AssignedToUserId == request.AssignedToUserId.Value);

            if (request.TicketId.HasValue && request.TicketId.Value > 0)
                query = query.Where(x => x.TicketId == request.TicketId.Value);
            if (!string.IsNullOrWhiteSpace(request.Subject))
                query = query.Where(x => x.Subject != null && x.Subject.Contains(request.Subject));
            if (request.Status.HasValue)
                query = query.Where(x => x.Status == StatusToString(request.Status));
            if (request.Priority.HasValue)
                query = query.Where(x => x.Priority == PriorityToString(request.Priority));

            totalTicketCount = query.Count();

            var pageNumber = request.PageNumber > 0 ? request.PageNumber : 1;
            var pageSize = request.PageSize > 0 ? request.PageSize : 25;
            var list = query.OrderByDescending(x => x.CreatedDate)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var finalresponse = new List<GetAllTicketsResponseDTO>(list.Count);
            foreach (var item in list)
            {
                var assignedToName = item.AssignedToUserId.HasValue
                    ? _db.SYS_UserDetails.Where(x => x.UserId == item.AssignedToUserId).Select(x => x.FirstName + " " + x.LastName).FirstOrDefault()
                    : null;
                finalresponse.Add(new GetAllTicketsResponseDTO
                {
                    Id = item.TicketId,
                    TicketId = item.TicketId,
                    Title = item.Subject,
                    Priority = PriorityToInt(item.Priority),
                    Status = StatusToInt(item.Status),
                    CreatedBy = _db.SYS_UserDetails.Where(x => x.UserId == item.CreatedBy).Select(x => x.FirstName + " " + x.LastName).FirstOrDefault(),
                    CreatedDate = CommonMethods.ToLocalTime(item.CreatedDate),
                    FacilityId = item.FacilityId,
                    AssignedToUserId = item.AssignedToUserId,
                    AssignedToUserName = assignedToName,
                    Description = item.Description,
                    Type = item.Type,
                    CommentCount = _db.TK_TicketComments.Count(x => x.TicketId == item.TicketId && x.Comment != null && x.IsActive == true),
                    IsActive = item.IsActive,
                    ModifiedBy = _db.SYS_UserDetails.Where(x => x.UserId == item.ModifiedBy).Select(x => x.FirstName + " " + x.LastName).FirstOrDefault(),
                    ModifiedDate = CommonMethods.ToLocalTime(item.ModifiedDate),
                });
            }

            return finalresponse;
        }

        public GetTicketByIdResponseDTO GetTicketById(long TicketId)
        {
            GetTicketByIdResponseDTO response = new GetTicketByIdResponseDTO();
            SYS_Ticket Ticket = _db.SYS_Tickets.Where(x => x.TicketId == TicketId).FirstOrDefault();
            if (Ticket == null) return response;
            response.Id = Ticket.TicketId;
            response.TicketId = Ticket.TicketId;
            response.Title = Ticket.Subject;
            response.Description = Ticket.Description;
            response.Priority = PriorityToInt(Ticket.Priority);
            response.Status = StatusToInt(Ticket.Status);
            response.CreatedBy = _db.SYS_UserDetails.Where(x => x.UserId == Ticket.CreatedBy).Select(x => x.FirstName + " " + x.LastName).FirstOrDefault();
            response.CreatedByUserId = Ticket.CreatedBy;
            response.CreatedDate = CommonMethods.ToLocalTime(Ticket.CreatedDate);
            response.AssignedToUserName = Ticket.AssignedToUserId.HasValue
                ? _db.SYS_UserDetails.Where(x => x.UserId == Ticket.AssignedToUserId).Select(x => x.FirstName + " " + x.LastName).FirstOrDefault()
                : null;
            response.AssignedToUserId = Ticket.AssignedToUserId;
            response.Type = Ticket.Type;
            var ticketFiles = _db.TK_TicketFiles.Where(tf => tf.TicketId == TicketId).ToList();
            response.Documents = ticketFiles.Select(x => new SaveTicketFilesResponseDTO
            {
                TicketFileId = x.TicketFileId,
                TicketFileURL = x.TicketFileURL,
                TicketFileName = x.TicketFileName,
                Description = x.Description,
                CreatedDate = GetTimeElapsed(x.CreatedDate),
            }).ToList();

            var ticketComments = _db.TK_TicketComments.Where(tf => tf.TicketId == TicketId && (tf.IsActive == null || tf.IsActive == true)).ToList();
            response.Comments = ticketComments.Select(x => new SaveTicketCommentResponseDTO
            {
                TicketCommentId = x.TicketCommentId,
                Comment = x.Comment,
                CreatedBy = _db.SYS_UserDetails.Where(y => y.UserId == x.CreatedBy).Select(u => u.FirstName + " " + u.LastName).FirstOrDefault(),
                CreatedDate = GetTimeElapsed(x.CreatedDate),
            }).ToList();

            return response;
        }

        public string GetTimeElapsed(DateTime createdDate)
        {
            var timeSpan = DateTime.Now - createdDate;

            if (timeSpan.TotalDays >= 1)
            {
                return $"{(int)timeSpan.TotalDays} day(s) ago";
            }
            if (timeSpan.TotalHours >= 1)
            {
                return $"{(int)timeSpan.TotalHours} hour(s) ago";
            }
            if (timeSpan.TotalMinutes >= 1)
            {
                return $"{(int)timeSpan.TotalMinutes} minute(s) ago";
            }
            return "Just now";
        }

        public long SaveTicket(SaveTicketRequestDTO request, long UserId, long OrganizationId)
        {
            try
            {
                SYS_Ticket Ticket = new SYS_Ticket();
                if (request.TicketId == 0)
                {
                    Ticket = _mapper.Map<SYS_Ticket>(request);
                    Ticket.CreatedBy = UserId;
                    Ticket.CreatedDate = DateTime.UtcNow;
                    Ticket.IsActive = true;
                    Ticket.OrganizationId = OrganizationId;
                    Ticket.AssignedToUserId = request.AssignedToUserId;
                    Ticket.Priority = PriorityToString(request.Priority);
                    Ticket.Status = StatusToString(request.Status ?? (int)TicketStatus.Pending);
                    Ticket.ContactEmail = request.ContactEmail;
                    _db.SYS_Tickets.Add(Ticket);
                    _db.SaveChanges();

                    _auditService.LogEntityChange(
                        action: "Create",
                        entityType: "SYS_Ticket",
                        entityId: Ticket.TicketId,
                        newValues: new { Ticket.Subject, Ticket.Status, Ticket.Priority, Ticket.AssignedToUserId },
                        userId: UserId,
                        description: $"Ticket '{Ticket.Subject}' created - Status: {Ticket.Status}, Priority: {Ticket.Priority}",
                        module: "Ticket"
                    );

                    var techSupportUserIds = _db.SYS_UserDetails
                        .AsNoTracking()
                        .Where(u => u.IsActive == true && u.Login != null && u.Login.RoleId == (int)UserRole.TechSupport)
                        .Select(u => u.UserId)
                        .ToList();
                    foreach (var tsUserId in techSupportUserIds)
                    {
                        _db.SYS_Notifications.Add(new SYS_Notification
                        {
                            UserId = tsUserId,
                            NotificationType = "Ticket",
                            IsRead = false,
                            Description = $"New ticket assigned: {(Ticket.Subject ?? "Support ticket")} (Priority: {Ticket.Priority})",
                            CreatedDate = DateTime.UtcNow
                        });
                    }
                    if (techSupportUserIds.Count > 0)
                        _db.SaveChanges();
                    return Ticket.TicketId;
                }
                else
                {
                    Ticket = _db.SYS_Tickets.Where(x => x.TicketId == request.TicketId).FirstOrDefault();
                    if (Ticket == null) return -1;
                    _mapper.Map(request, Ticket);
                    Ticket.AssignedToUserId = request.AssignedToUserId;
                    if (request.Priority.HasValue) Ticket.Priority = PriorityToString(request.Priority);
                    if (request.Status.HasValue) Ticket.Status = StatusToString(request.Status);
                    Ticket.ContactEmail = request.ContactEmail ?? Ticket.ContactEmail;
                    Ticket.ModifiedBy = UserId;
                    Ticket.ModifiedDate = DateTime.UtcNow;
                    _db.SaveChanges();
                    return 0;
                }
            }
            catch
            {
                return -1;
            }
        }

        public bool DeleteTicket(long TicketId)
        {
            try
            {
                SYS_Ticket Ticket = _db.SYS_Tickets.Where(x => x.TicketId == TicketId).FirstOrDefault();
                if (Ticket != null)
                {
                    var oldTicket = _db.SYS_Tickets.AsNoTracking().FirstOrDefault(x => x.TicketId == TicketId);
                    Ticket.IsActive = false;
                    _db.SaveChanges();

                    _auditService.LogEntityChange(
                        action: "Delete",
                        entityType: "SYS_Ticket",
                        entityId: TicketId,
                        oldValues: oldTicket,
                        description: $"Ticket '{Ticket.Subject}' (ID: {TicketId}) deleted",
                        module: "Ticket"
                    );
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool UpdateTicketStatus(UpdateTicketStatusRequestDTO request, long UserId)
        {
            SYS_Ticket Ticket = _db.SYS_Tickets.Where(x => x.TicketId == request.Id).FirstOrDefault();
            if (Ticket != null)
            {
                var oldStatus = Ticket.Status;
                Ticket.Status = StatusToString(request.Status);
                Ticket.ModifiedBy = UserId;
                Ticket.ModifiedDate = DateTime.UtcNow;
                _db.SaveChanges();

                _auditService.LogEntityChange(
                    action: "Update",
                    entityType: "SYS_Ticket",
                    entityId: request.Id,
                    oldValues: new { Status = oldStatus },
                    newValues: new { Status = Ticket.Status },
                    userId: UserId,
                    description: $"Ticket '{Ticket.Subject}' (ID: {request.Id}) status changed from '{oldStatus}' to '{Ticket.Status}'",
                    module: "Ticket"
                );
            }
            return true;
        }

        public long CreateTicketDocument(CreateTicketDocumentRequestDTO request)
        {
            try
            {
                var ticket = _db.SYS_Tickets.FirstOrDefault(x => x.TicketId == request.TicketId && (x.IsActive == null || x.IsActive == true));
                if (ticket == null) return 0;
                var file = new TK_TicketFile
                {
                    TicketFileId = 0,
                    TicketId = request.TicketId,
                    Description = request.Description,
                    TicketFileName = request.DocumentName,
                    TicketFileURL = request.DocumentUrl,
                    CreatedDate = DateTime.UtcNow
                };
                _db.TK_TicketFiles.Add(file);
                _db.SaveChanges();
                return file.TicketFileId;
            }
            catch
            {
                return 0;
            }
        }

        public bool DeleteTicketComment(long TicketCommentId, long userId)
        {
            try
            {
                TK_TicketComment TicketComment = _db.TK_TicketComments.Where(x => x.TicketCommentId == TicketCommentId).FirstOrDefault();
                if (TicketComment == null) return false;
                if(TicketComment.CreatedBy != userId) return false;
                TicketComment.IsActive = false;
                _db.SaveChanges();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool AddTicketComment(long ticketId, string comment, long userId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(comment)) return false;
                var ticket = _db.SYS_Tickets.FirstOrDefault(x => x.TicketId == ticketId && (x.IsActive == null || x.IsActive == true));
                if (ticket == null) return false;
                var ticketComment = new TK_TicketComment
                {
                    TicketCommentId = 0,
                    TicketId = ticketId,
                    Comment = comment.Trim(),
                    CreatedBy = userId,
                    CreatedDate = DateTime.UtcNow,
                    IsActive = true
                };
                _db.TK_TicketComments.Add(ticketComment);
                _db.SaveChanges();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void CreateTicketCommentNotificationForTicketCreator(long ticketId, long createdByUserId, string commentAuthorUserName, string ticketSubject, string commentPreview)
        {
            var preview = !string.IsNullOrEmpty(commentPreview) && commentPreview.Length > 100
                ? commentPreview.Substring(0, 100) + "..."
                : (commentPreview ?? "");
            var description = $"{commentAuthorUserName} added a comment on ticket #{ticketId} ({ticketSubject}): {preview}";
            _db.SYS_Notifications.Add(new SYS_Notification
            {
                UserId = createdByUserId,
                NotificationType = "Ticket",
                IsRead = false,
                Description = description,
                CreatedDate = DateTime.UtcNow
            });
            _db.SaveChanges();
        }

    }
}
