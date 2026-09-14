using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.Users
{
    public class GetAllUsersRequestDTO
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 100;
        public long? OrganizationId { get; set; }
        public long? FacilityId { get; set; }
        public string? Title { get; set; }
        public int? RoleId { get; set; }
        public string? Status { get; set; } = null;
        public long? CategoryId { get; set; }
        public int? RequestingRoleId { get; set; }
    }
}
