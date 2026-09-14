using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitality.Models.DTOs.Chats
{
    public class GetAllUsersforChatResponseDTO
    {
        public long? UserId { get; set; }
        public long? PatientId { get; set; }
        public string? UserName { get; set; }
        public string? UserType { get; set; }
        public string? FacilityName { get; set; }
    }

    public sealed class UserLite
    {
        public long? UserId { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public int? RoleId { get; set; }
    }
}
