using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.Questionnaires
{
    public class GetAllQuestionnairesResponseDTO
    {
        public string? Guid { get; set; }
        public long QuestionnaireId { get; set; }
        public string? QuestionnaireName { get; set; }
        public string? QuestionaireType { get; set; }
        public int ProductCount { get; set; }
        public string? Language { get; set; }
        public string? Status { get; set; }
        public string? Review { get; set; }
        public int? QuestionCount { get; set; }
        public DateTime? CreatedDate { get; set; }
        public long? CreatedBy { get; set; }
        public string? CreatedByName { get; set; }
    }
}
