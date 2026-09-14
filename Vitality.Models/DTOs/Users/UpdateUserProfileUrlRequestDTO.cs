using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.Users
{
    public class UpdateUserProfileUrlRequestDTO
    {
        public long UserId { get; set; }
        public string? ProfileUrl { get; set; }
    }
}
