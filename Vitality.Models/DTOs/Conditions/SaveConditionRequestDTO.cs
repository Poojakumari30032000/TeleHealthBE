using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.Conditions
{
    public class SaveConditionRequestDTO
    {
        public long ConditionId { get; set; }
        public long? CategoryId { get; set; }
        public string? ConditionName { get; set; }
        public string? ImageURL { get; set; }
    }
}
