using DudeMeds.Models.DTOs.Tickets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.Repos.Interfaces
{
    public interface ITicketsRepo
    {
        public List<GetAllTicketsResponseDTO> GetAllTickets(GetAllTicketsRequestDTO request, long OrganizationId, long? currentUserId, int? currentUserRoleId, out int totalTicketCount);
        public GetTicketByIdResponseDTO GetTicketById(long TicketId);

        public long SaveTicket(SaveTicketRequestDTO request, long UserId, long OrganizationId);
        public bool DeleteTicket(long TicketId);
        public bool UpdateTicketStatus(UpdateTicketStatusRequestDTO request, long UserId);
        public bool DeleteTicketComment(long TicketCommentId, long userId);
        public bool AddTicketComment(long ticketId, string comment, long userId);

        public long CreateTicketDocument(CreateTicketDocumentRequestDTO request);

        public void CreateTicketCommentNotificationForTicketCreator(long ticketId, long createdByUserId, string commentAuthorUserName, string ticketSubject, string commentPreview);
    }
}
