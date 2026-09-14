using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class SYS_QuestionnaireFacilityJson
    {
        public long QuestionnaireFacilityJsonId { get; set; }
        public long QuestionnaireId { get; set; }
        public long FacilityId { get; set; }
        public string? QuestionnaireJson { get; set; }
        public bool? IsActive { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }

        public virtual SYS_Facility Facility { get; set; } = null!;
        public virtual SYS_Questionnaire Questionnaire { get; set; } = null!;
    }
}
