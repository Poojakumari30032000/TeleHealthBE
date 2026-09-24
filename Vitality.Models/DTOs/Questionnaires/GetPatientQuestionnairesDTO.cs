using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.Questionnaires
{
    /// <summary>
    /// Request for the questionnaires a patient has completed.
    /// PatientId is never bound from the query string - the controller sets it
    /// from the PatientId claim so a patient cannot read another patient's forms.
    /// </summary>
    public class GetPatientQuestionnairesRequestDTO
    {
        public long PatientId { get; set; }
    }

    /// <summary>
    /// One completed questionnaire in a patient's history.
    /// A patient completes a questionnaire as the intake form of a treatment,
    /// so a submission is identified by its PatientTreatmentId.
    /// </summary>
    public class GetPatientQuestionnaireSummaryDTO
    {
        public long PatientTreatmentId { get; set; }
        public long? ProductId { get; set; }
        public string? ProductName { get; set; }
        public string? QuestionnaireName { get; set; }
        public DateTime? SubmittedDate { get; set; }
        public int AnswerCount { get; set; }
    }

    /// <summary>
    /// Request for the answers of one submission.
    /// PatientId is set from the claim, not the caller, and the repo requires the
    /// treatment to belong to that patient before any answer is returned.
    /// </summary>
    public class GetPatientQuestionnaireResponsesRequestDTO
    {
        public long PatientTreatmentId { get; set; }
        public long PatientId { get; set; }
    }

    /// <summary>One answered question within a submission.</summary>
    public class GetPatientQuestionnaireResponseItemDTO
    {
        public long PatientTreatmentInTakeFormId { get; set; }
        public string? Question { get; set; }
        public string? Answer { get; set; }
        public string? OtherText { get; set; }
        public string? Type { get; set; }
        public string? ConsentHtml { get; set; }
        public DateTime? CreatedDate { get; set; }
    }
}
