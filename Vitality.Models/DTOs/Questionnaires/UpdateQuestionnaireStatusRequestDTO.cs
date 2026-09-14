using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.Questionnaires
{
    public class UpdateQuestionnaireStatusRequestDTO
    {
        public long? QuestionnaireId { get; set; }
        public string? Review { get; set; }
        public string? Status { get; set; }
    }
    public class UpdateQuestionnaireJsonRequestDTO
    {
        public long? QuestionnaireId { get; set; }
        public string? Json { get; set; }
        public long? FacilityId { get; set; }
    }

    public class UpdateFacilityQuestionnaireJsonRequestDTO
    {
        public long QuestionnaireId { get; set; }
        public long FacilityId { get; set; }
        public string Json { get; set; } = "";
    }

    public class GetQuestionnaireJsonRequestDTO
    {
        public long QuestionnaireId { get; set; }
        public long? FacilityId { get; set; }
    }
}
