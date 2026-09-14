using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.Conditions
{
    public class GetAllConditionsResponseDTO
    {
        public long ConditionId { get; set; }
        public long? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public string? ConditionName { get; set; }
        public string? ImageURL { get; set; }
    }
}
