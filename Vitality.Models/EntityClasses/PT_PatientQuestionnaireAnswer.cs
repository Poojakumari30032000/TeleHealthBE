using System;

namespace Vitality.Models.EntityClasses
{
    /// <summary>One answered question of a submitted PT_PatientQuestionnaire.</summary>
    public partial class PT_PatientQuestionnaireAnswer
    {
        public long PatientQuestionnaireAnswerId { get; set; }
        public long PatientQuestionnaireId { get; set; }
        public string? FieldKey { get; set; }
        public string? Question { get; set; }
        public string? Answer { get; set; }
        public string? OtherText { get; set; }
        public string? Type { get; set; }
        public string? ConsentHtml { get; set; }
        public int? DisplayOrder { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }

        public virtual PT_PatientQuestionnaire PatientQuestionnaire { get; set; } = null!;
    }
}
