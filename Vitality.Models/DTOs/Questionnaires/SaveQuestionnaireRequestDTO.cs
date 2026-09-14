using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.Questionnaires
{
    public class SaveQuestionnaireRequestDTO
    {
        public long QuestionnaireId { get; set; }
        public string? QuestionnaireName { get; set; }
        public string? QuestionnaireJson { get; set; }
        public string? QuestionaireType { get; set; }
        public List<long>? ProductId { get; set; }
        public List<long>? CategoryId { get; set; }
        public string? Status { get; set; }
        public string? Review { get; set; }
        public int? QuestionCount { get; set; }
    }
}
