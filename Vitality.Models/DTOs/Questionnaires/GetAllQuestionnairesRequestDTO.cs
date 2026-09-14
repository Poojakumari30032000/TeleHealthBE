using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.Questionnaires
{
    public class GetAllQuestionnairesRequestDTO
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 100;
        public long? OrganizationId { get; set; }
        public string? Title { get; set; } = null;
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? Status { get; set; }
        public long? FacilityId { get; set; }
    }
}
