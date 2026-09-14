using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.Accounts
{
    public class ChangePasswordResponseDTO
    {
        public string? Message { get; set; }
        public bool? IsUpdated { get; set; }
    }
}
