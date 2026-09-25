using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    /// <summary>
    /// One questionnaire assigned to one patient (TEL-57).
    /// Table created by Sql/Create_PT_PatientQuestionnaire.sql.
    /// </summary>
    public partial class PT_PatientQuestionnaire
    {
        public PT_PatientQuestionnaire()
        {
            PT_PatientQuestionnaireAnswers = new HashSet<PT_PatientQuestionnaireAnswer>();
        }

        public long PatientQuestionnaireId { get; set; }
        public long PatientId { get; set; }
        public long QuestionnaireId { get; set; }
        public long? FacilityId { get; set; }
        public long? PatientTreatmentId { get; set; }
        public string Status { get; set; } = PatientQuestionnaireStatus.Assigned;
        public long? AssignedBy { get; set; }
        public DateTime AssignedDate { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime? StartedDate { get; set; }
        public DateTime? SubmittedDate { get; set; }
        public string? DraftJson { get; set; }
        public DateTime? DraftSavedDate { get; set; }
        public string? QuestionnaireJsonSnapshot { get; set; }
        public string? Guid { get; set; }
        public bool? IsActive { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }

        public virtual PT_Patient Patient { get; set; } = null!;
        public virtual SYS_Questionnaire Questionnaire { get; set; } = null!;
        public virtual ICollection<PT_PatientQuestionnaireAnswer> PT_PatientQuestionnaireAnswers { get; set; }
    }

    /// <summary>The values allowed by CK_PT_PatientQuestionnaire_Status.</summary>
    public static class PatientQuestionnaireStatus
    {
        public const string Assigned = "Assigned";
        public const string InProgress = "InProgress";
        public const string Submitted = "Submitted";
        public const string Cancelled = "Cancelled";
        public const string Expired = "Expired";
    }
}
