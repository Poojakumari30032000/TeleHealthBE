using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitality.Models.DTOs.Users
{
    public class UpdateUserProfileRequestDto
    {
        public long UserId { get; set; }
        public string? ProfileUrl { get; set; }
    }
}
