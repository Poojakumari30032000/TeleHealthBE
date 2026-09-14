using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitality.Models.DTOs.Patients
{
    public class UpdateStatusRequestDTO
    {
        public long? Id { get; set; }
        public string? Type { get; set; }
        public string? Status { get; set; }
    }
}
