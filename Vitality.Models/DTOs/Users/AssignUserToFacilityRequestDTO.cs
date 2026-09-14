using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.Users
{
    public class AssignUserToFacilityRequestDTO
    {
        public long UserId { get; set; }
        public long? FacilityId { get; set; }
        public bool IsAssign { get; set; }
    }
}
