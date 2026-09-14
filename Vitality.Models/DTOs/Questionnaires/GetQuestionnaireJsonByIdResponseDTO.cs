using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.Questionnaires
{
    public class GetQuestionnaireJsonByIdResponseDTO
    {
        public long QuestionnaireId { get; set; }
        public string? QuestionnaireName { get; set; }
        public string? QuestionnaireJson { get; set; }
        public string? QuestionaireType { get; set; }
    }
}
