using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.Questionnaires
{
    public class GetQuestionnaireByIdResponseDTO
    {
        public string? Guid { get; set; }
        public long QuestionnaireId { get; set; }
        public string? QuestionnaireName { get; set; }
        public string? QuestionnaireJson { get; set; }
        public string? QuestionaireType { get; set; }
        public List<long>? ProductId { get; set; }
        public List<long>? CategoryId { get; set; }
        public string? Language { get; set; }
        public long? CreatedBy { get; set; }
        public string? CreatedByName { get; set; }
        public string? Status { get; set; }
    }
}
