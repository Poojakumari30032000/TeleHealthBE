using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class SYS_Questionnaire
    {
        public SYS_Questionnaire()
        {
            SYS_QuestionnaireFacilityJsons = new HashSet<SYS_QuestionnaireFacilityJson>();
        }

        public long QuestionnaireId { get; set; }
        public string? QuestionnaireName { get; set; }
        public string? QuestionnaireJson { get; set; }
        public string? QuestionaireType { get; set; }
        public string? Language { get; set; }
        public string? Status { get; set; }
        public string? Review { get; set; }
        public int? QuestionCount { get; set; }
        public bool? IsActive { get; set; }
        public DateTime? CreatedDate { get; set; }
        public long? CreatedBy { get; set; }
        public long? OrgzanizationId { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public string? Guid { get; set; }

        public virtual ICollection<SYS_QuestionnaireFacilityJson> SYS_QuestionnaireFacilityJsons { get; set; }
    }
}
