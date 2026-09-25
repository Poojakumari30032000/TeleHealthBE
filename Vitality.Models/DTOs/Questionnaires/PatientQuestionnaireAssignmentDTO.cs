using System;
using System.Collections.Generic;

namespace DudeMeds.Models.DTOs.Questionnaires
{
    // TEL-57 - questionnaires assigned to a patient and completed on demand.
    // Wherever a DTO carries PatientId or the caller's identity, the controller
    // sets it from claims; it is never bound from the request.

    /// <summary>Staff request to give a questionnaire to a patient.</summary>
    public class AssignPatientQuestionnaireRequestDTO
    {
        public long PatientId { get; set; }
        public long QuestionnaireId { get; set; }
        public long? PatientTreatmentId { get; set; }
        public DateTime? DueDate { get; set; }
    }

    /// <summary>
    /// Who is calling, taken from claims. The repo uses it to decide which
    /// patients and assignments the caller may reach.
    /// </summary>
    public class PatientQuestionnaireCallerDTO
    {
        public long UserId { get; set; }

        /// <summary>A <see cref="Vitality.Models.Enums.UserRole"/> value.</summary>
        public long? RoleId { get; set; }
        public long? OrganizationId { get; set; }

        /// <summary>Set only when the caller is a patient.</summary>
        public long? PatientId { get; set; }
    }

    /// <summary>A questionnaire staff can pick from when assigning.</summary>
    public class AssignableQuestionnaireDTO
    {
        public long QuestionnaireId { get; set; }
        public string? QuestionnaireName { get; set; }
        public string? QuestionaireType { get; set; }
        public string? Status { get; set; }

        /// <summary>True when the patient already has this one open, so it cannot be assigned again.</summary>
        public bool HasOpenAssignment { get; set; }
    }

    /// <summary>One assignment in a list, for the patient or for staff.</summary>
    public class PatientQuestionnaireAssignmentDTO
    {
        public long PatientQuestionnaireId { get; set; }
        public long PatientId { get; set; }
        public long QuestionnaireId { get; set; }
        public string? QuestionnaireName { get; set; }
        public long? PatientTreatmentId { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime AssignedDate { get; set; }
        public string? AssignedByName { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime? StartedDate { get; set; }
        public DateTime? SubmittedDate { get; set; }
        public DateTime? DraftSavedDate { get; set; }
        public int AnswerCount { get; set; }
    }

    /// <summary>
    /// What the patient needs to open an assignment: the questionnaire exactly as
    /// it stood when assigned, and any saved partial progress.
    /// </summary>
    public class PatientQuestionnaireFormDTO
    {
        public long PatientQuestionnaireId { get; set; }
        public long QuestionnaireId { get; set; }
        public string? QuestionnaireName { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? QuestionnaireJson { get; set; }
        public string? DraftJson { get; set; }
        public DateTime? DraftSavedDate { get; set; }
    }

    public class SavePatientQuestionnaireDraftRequestDTO
    {
        public long PatientQuestionnaireId { get; set; }
        public string? DraftJson { get; set; }
    }

    public class SubmitPatientQuestionnaireRequestDTO
    {
        public long PatientQuestionnaireId { get; set; }
        public List<PatientQuestionnaireAnswerInputDTO> Answers { get; set; } = new();
    }

    public class PatientQuestionnaireAnswerInputDTO
    {
        public string? FieldKey { get; set; }
        public string? Question { get; set; }
        public string? Answer { get; set; }
        public string? OtherText { get; set; }
        public string? Type { get; set; }
        public string? ConsentHtml { get; set; }
    }

    public class PatientQuestionnaireAnswerDTO
    {
        public long PatientQuestionnaireAnswerId { get; set; }
        public string? FieldKey { get; set; }
        public string? Question { get; set; }
        public string? Answer { get; set; }
        public string? OtherText { get; set; }
        public string? Type { get; set; }
        public string? ConsentHtml { get; set; }
        public int? DisplayOrder { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    /// <summary>A submitted assignment with its answers, read only.</summary>
    public class PatientQuestionnaireSubmissionDTO
    {
        public PatientQuestionnaireAssignmentDTO Assignment { get; set; } = new();
        public List<PatientQuestionnaireAnswerDTO> Answers { get; set; } = new();
    }

    /// <summary>
    /// Outcome of a write. Message is shown to the user; Id is the assignment
    /// affected, when there is one.
    /// </summary>
    public class PatientQuestionnaireResultDTO
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public long? Id { get; set; }

        public static PatientQuestionnaireResultDTO Ok(string message, long? id = null)
            => new() { Success = true, Message = message, Id = id };

        public static PatientQuestionnaireResultDTO Fail(string message)
            => new() { Success = false, Message = message };
    }
}
