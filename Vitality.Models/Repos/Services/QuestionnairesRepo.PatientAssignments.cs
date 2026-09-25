using DudeMeds.Models.DTOs.Questionnaires;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using Vitality.Models.EntityClasses;
using Vitality.Models.Enums;

namespace DudeMeds.Models.Repos.Services
{
    /// <summary>
    /// TEL-57 - questionnaires assigned to a patient and completed on demand,
    /// backed by PT_PatientQuestionnaire and PT_PatientQuestionnaireAnswer.
    ///
    /// Every method takes the caller as a <see cref="PatientQuestionnaireCallerDTO"/>
    /// built from claims, and scopes what it returns to that caller. A patient
    /// reaches only their own assignments; staff reach only patients in their
    /// organisation (Global Admin) or in a facility they are assigned to
    /// (Clinic Admin, Provider).
    /// </summary>
    public partial class QuestionnairesRepo
    {
        // A signature captured in a consent field can travel inside the draft,
        // so the ceiling is generous. It exists to stop an unbounded payload.
        private const int MaxDraftJsonLength = 2_000_000;
        private const int MaxAnswerCount = 1000;

        private static bool IsOpen(string status) =>
            status == PatientQuestionnaireStatus.Assigned || status == PatientQuestionnaireStatus.InProgress;

        // ---------------------------------------------------------------- staff

        /// <summary>Active questionnaires a staff member may give to this patient.</summary>
        public List<AssignableQuestionnaireDTO> GetAssignableQuestionnaires(long patientId, PatientQuestionnaireCallerDTO caller)
        {
            if (!CanStaffReachPatient(patientId, caller))
                return new List<AssignableQuestionnaireDTO>();

            var patientOrgId = _db.PT_Patients.AsNoTracking()
                .Where(p => p.PatientId == patientId)
                .Select(p => p.OrganizationId)
                .FirstOrDefault();

            return _db.SYS_Questionnaires.AsNoTracking()
                .Where(q => q.IsActive == true
                         && q.QuestionnaireJson != null
                         && (patientOrgId == null || q.OrgzanizationId == null || q.OrgzanizationId == patientOrgId))
                .OrderBy(q => q.QuestionnaireName)
                .Select(q => new AssignableQuestionnaireDTO
                {
                    QuestionnaireId = q.QuestionnaireId,
                    QuestionnaireName = q.QuestionnaireName,
                    QuestionaireType = q.QuestionaireType,
                    Status = q.Status,
                    HasOpenAssignment = _db.PT_PatientQuestionnaires.Any(a =>
                        a.PatientId == patientId
                        && a.QuestionnaireId == q.QuestionnaireId
                        && a.IsActive == true
                        && (a.Status == PatientQuestionnaireStatus.Assigned || a.Status == PatientQuestionnaireStatus.InProgress))
                })
                .ToList();
        }

        public PatientQuestionnaireResultDTO AssignPatientQuestionnaire(AssignPatientQuestionnaireRequestDTO request, PatientQuestionnaireCallerDTO caller)
        {
            if (request is null || request.PatientId <= 0 || request.QuestionnaireId <= 0)
                return PatientQuestionnaireResultDTO.Fail("A patient and a questionnaire are required.");

            if (!CanStaffReachPatient(request.PatientId, caller))
                return PatientQuestionnaireResultDTO.Fail("Patient not found.");

            var patient = _db.PT_Patients.AsNoTracking()
                .Where(p => p.PatientId == request.PatientId)
                .Select(p => new { p.FacilityId, p.OrganizationId })
                .First();

            var questionnaire = _db.SYS_Questionnaires.AsNoTracking()
                .FirstOrDefault(q => q.QuestionnaireId == request.QuestionnaireId && q.IsActive == true);

            if (questionnaire is null)
                return PatientQuestionnaireResultDTO.Fail("Questionnaire not found.");

            if (patient.OrganizationId != null && questionnaire.OrgzanizationId != null
                && patient.OrganizationId != questionnaire.OrgzanizationId)
                return PatientQuestionnaireResultDTO.Fail("Questionnaire not found.");

            if (request.PatientTreatmentId is long treatmentId && treatmentId > 0)
            {
                var treatmentBelongs = _db.PT_PatientTreatments.AsNoTracking()
                    .Any(t => t.PatientTreatmentId == treatmentId && t.PatientId == request.PatientId);
                if (!treatmentBelongs)
                    return PatientQuestionnaireResultDTO.Fail("That treatment does not belong to this patient.");
            }

            if (request.DueDate is DateTime due && due.Date < DateTime.UtcNow.Date)
                return PatientQuestionnaireResultDTO.Fail("The due date cannot be in the past.");

            var alreadyOpen = _db.PT_PatientQuestionnaires.AsNoTracking().Any(a =>
                a.PatientId == request.PatientId
                && a.QuestionnaireId == request.QuestionnaireId
                && a.IsActive == true
                && (a.Status == PatientQuestionnaireStatus.Assigned || a.Status == PatientQuestionnaireStatus.InProgress));

            if (alreadyOpen)
                return PatientQuestionnaireResultDTO.Fail("This questionnaire is already assigned to the patient and not yet submitted.");

            // Freeze the definition the patient will see, including this facility's
            // wording, so a later edit cannot change the form mid-completion.
            var snapshot = GetQuestionnaireJson(new GetQuestionnaireJsonRequestDTO
            {
                QuestionnaireId = questionnaire.QuestionnaireId,
                FacilityId = patient.FacilityId
            }).QuestionnaireJson;

            if (string.IsNullOrWhiteSpace(snapshot))
                return PatientQuestionnaireResultDTO.Fail("This questionnaire has no questions yet.");

            var now = DateTime.UtcNow;
            var row = new PT_PatientQuestionnaire
            {
                PatientId = request.PatientId,
                QuestionnaireId = questionnaire.QuestionnaireId,
                FacilityId = patient.FacilityId,
                PatientTreatmentId = request.PatientTreatmentId > 0 ? request.PatientTreatmentId : null,
                Status = PatientQuestionnaireStatus.Assigned,
                AssignedBy = caller.UserId,
                AssignedDate = now,
                DueDate = request.DueDate,
                QuestionnaireJsonSnapshot = snapshot,
                Guid = Guid.NewGuid().ToString(),
                IsActive = true,
                CreatedBy = caller.UserId,
                CreatedDate = now
            };

            _db.PT_PatientQuestionnaires.Add(row);
            try
            {
                _db.SaveChanges();
            }
            catch (DbUpdateException)
            {
                // UX_PT_PatientQuestionnaire_OpenAssignment - a concurrent assign won.
                _db.Entry(row).State = EntityState.Detached;
                return PatientQuestionnaireResultDTO.Fail("This questionnaire is already assigned to the patient and not yet submitted.");
            }

            _auditService.LogEntityChange(
                action: "Create",
                entityType: "PT_PatientQuestionnaire",
                entityId: row.PatientQuestionnaireId,
                userId: caller.UserId,
                patientId: row.PatientId,
                facilityId: row.FacilityId,
                description: $"Questionnaire '{questionnaire.QuestionnaireName}' assigned to patient",
                module: "Questionnaire");

            return PatientQuestionnaireResultDTO.Ok("Questionnaire assigned.", row.PatientQuestionnaireId);
        }

        public PatientQuestionnaireResultDTO CancelPatientQuestionnaire(long patientQuestionnaireId, PatientQuestionnaireCallerDTO caller)
        {
            var row = _db.PT_PatientQuestionnaires
                .FirstOrDefault(a => a.PatientQuestionnaireId == patientQuestionnaireId && a.IsActive == true);

            if (row is null || !CanStaffReachPatient(row.PatientId, caller))
                return PatientQuestionnaireResultDTO.Fail("Assignment not found.");

            if (!IsOpen(row.Status))
                return PatientQuestionnaireResultDTO.Fail($"A {row.Status.ToLowerInvariant()} questionnaire cannot be cancelled.");

            row.Status = PatientQuestionnaireStatus.Cancelled;
            row.ModifiedBy = caller.UserId;
            row.ModifiedDate = DateTime.UtcNow;
            _db.SaveChanges();

            _auditService.LogEntityChange(
                action: "Update",
                entityType: "PT_PatientQuestionnaire",
                entityId: row.PatientQuestionnaireId,
                userId: caller.UserId,
                patientId: row.PatientId,
                facilityId: row.FacilityId,
                description: "Questionnaire assignment cancelled",
                module: "Questionnaire");

            return PatientQuestionnaireResultDTO.Ok("Assignment cancelled.", row.PatientQuestionnaireId);
        }

        /// <summary>All assignments of one patient, for the provider's patient view.</summary>
        public List<PatientQuestionnaireAssignmentDTO> GetPatientQuestionnaireAssignments(long patientId, PatientQuestionnaireCallerDTO caller)
        {
            if (!CanStaffReachPatient(patientId, caller))
                return new List<PatientQuestionnaireAssignmentDTO>();

            return ProjectAssignments(_db.PT_PatientQuestionnaires.AsNoTracking()
                    .Where(a => a.PatientId == patientId && a.IsActive == true))
                .OrderByDescending(a => a.AssignedDate)
                .ToList();
        }

        // -------------------------------------------------------------- patient

        /// <summary>The calling patient's assignments, cancelled ones left out.</summary>
        public List<PatientQuestionnaireAssignmentDTO> GetMyAssignedQuestionnaires(PatientQuestionnaireCallerDTO caller)
        {
            if (caller.PatientId is not long patientId || patientId <= 0)
                return new List<PatientQuestionnaireAssignmentDTO>();

            return ProjectAssignments(_db.PT_PatientQuestionnaires.AsNoTracking()
                    .Where(a => a.PatientId == patientId
                             && a.IsActive == true
                             && a.Status != PatientQuestionnaireStatus.Cancelled))
                .OrderByDescending(a => a.AssignedDate)
                .ToList();
        }

        /// <summary>
        /// The form and saved progress of one of the calling patient's open
        /// assignments. Null when it is not theirs or is no longer open.
        /// </summary>
        public PatientQuestionnaireFormDTO? GetMyQuestionnaireForm(long patientQuestionnaireId, PatientQuestionnaireCallerDTO caller)
        {
            if (caller.PatientId is not long patientId || patientId <= 0)
                return null;

            return (
                from a in _db.PT_PatientQuestionnaires.AsNoTracking()
                join q in _db.SYS_Questionnaires.AsNoTracking() on a.QuestionnaireId equals q.QuestionnaireId
                where a.PatientQuestionnaireId == patientQuestionnaireId
                   && a.PatientId == patientId
                   && a.IsActive == true
                   && (a.Status == PatientQuestionnaireStatus.Assigned || a.Status == PatientQuestionnaireStatus.InProgress)
                select new PatientQuestionnaireFormDTO
                {
                    PatientQuestionnaireId = a.PatientQuestionnaireId,
                    QuestionnaireId = a.QuestionnaireId,
                    QuestionnaireName = q.QuestionnaireName,
                    Status = a.Status,
                    // Older rows may predate the snapshot; fall back to the live form.
                    QuestionnaireJson = a.QuestionnaireJsonSnapshot ?? q.QuestionnaireJson,
                    DraftJson = a.DraftJson,
                    DraftSavedDate = a.DraftSavedDate
                }).FirstOrDefault();
        }

        public PatientQuestionnaireResultDTO SaveMyQuestionnaireDraft(SavePatientQuestionnaireDraftRequestDTO request, PatientQuestionnaireCallerDTO caller)
        {
            if (caller.PatientId is not long patientId || patientId <= 0)
                return PatientQuestionnaireResultDTO.Fail("No patient is associated with this account.");

            if (request is null || request.PatientQuestionnaireId <= 0)
                return PatientQuestionnaireResultDTO.Fail("Assignment not found.");

            if (request.DraftJson is { Length: > MaxDraftJsonLength })
                return PatientQuestionnaireResultDTO.Fail("Your progress is too large to save.");

            var row = _db.PT_PatientQuestionnaires.FirstOrDefault(a =>
                a.PatientQuestionnaireId == request.PatientQuestionnaireId
                && a.PatientId == patientId
                && a.IsActive == true);

            if (row is null)
                return PatientQuestionnaireResultDTO.Fail("Assignment not found.");

            if (!IsOpen(row.Status))
                return PatientQuestionnaireResultDTO.Fail("This questionnaire has already been submitted or closed.");

            var now = DateTime.UtcNow;
            row.DraftJson = request.DraftJson;
            row.DraftSavedDate = now;
            row.StartedDate ??= now;
            row.Status = PatientQuestionnaireStatus.InProgress;
            row.ModifiedBy = caller.UserId;
            row.ModifiedDate = now;
            _db.SaveChanges();

            return PatientQuestionnaireResultDTO.Ok("Progress saved.", row.PatientQuestionnaireId);
        }

        /// <summary>
        /// Stores the answers and closes the assignment. The status change is a
        /// conditional update inside the transaction, so a double submit (two tabs,
        /// a retried request) records the answers once and fails the second time.
        /// </summary>
        public PatientQuestionnaireResultDTO SubmitMyQuestionnaire(SubmitPatientQuestionnaireRequestDTO request, PatientQuestionnaireCallerDTO caller)
        {
            if (caller.PatientId is not long patientId || patientId <= 0)
                return PatientQuestionnaireResultDTO.Fail("No patient is associated with this account.");

            if (request is null || request.PatientQuestionnaireId <= 0)
                return PatientQuestionnaireResultDTO.Fail("Assignment not found.");

            var answers = (request.Answers ?? new List<PatientQuestionnaireAnswerInputDTO>())
                .Where(a => a != null && !string.IsNullOrWhiteSpace(a.Question))
                .ToList();

            if (answers.Count == 0)
                return PatientQuestionnaireResultDTO.Fail("There are no answers to submit.");

            if (answers.Count > MaxAnswerCount)
                return PatientQuestionnaireResultDTO.Fail("Too many answers in one submission.");

            var assignment = _db.PT_PatientQuestionnaires.AsNoTracking()
                .Where(a => a.PatientQuestionnaireId == request.PatientQuestionnaireId
                         && a.PatientId == patientId
                         && a.IsActive == true)
                .Select(a => new { a.PatientQuestionnaireId, a.Status, a.FacilityId, a.QuestionnaireId })
                .FirstOrDefault();

            if (assignment is null)
                return PatientQuestionnaireResultDTO.Fail("Assignment not found.");

            if (!IsOpen(assignment.Status))
                return PatientQuestionnaireResultDTO.Fail("This questionnaire has already been submitted or closed.");

            var now = DateTime.UtcNow;

            using var tx = _db.Database.BeginTransaction();

            var closed = _db.PT_PatientQuestionnaires
                .Where(a => a.PatientQuestionnaireId == assignment.PatientQuestionnaireId
                         && (a.Status == PatientQuestionnaireStatus.Assigned || a.Status == PatientQuestionnaireStatus.InProgress))
                .ExecuteUpdate(s => s
                    .SetProperty(a => a.Status, PatientQuestionnaireStatus.Submitted)
                    .SetProperty(a => a.SubmittedDate, now)
                    .SetProperty(a => a.StartedDate, a => a.StartedDate ?? now)
                    .SetProperty(a => a.DraftJson, (string?)null)
                    .SetProperty(a => a.DraftSavedDate, (DateTime?)null)
                    .SetProperty(a => a.ModifiedBy, caller.UserId)
                    .SetProperty(a => a.ModifiedDate, now));

            if (closed == 0)
            {
                tx.Rollback();
                return PatientQuestionnaireResultDTO.Fail("This questionnaire has already been submitted or closed.");
            }

            var order = 0;
            foreach (var a in answers)
            {
                _db.PT_PatientQuestionnaireAnswers.Add(new PT_PatientQuestionnaireAnswer
                {
                    PatientQuestionnaireId = assignment.PatientQuestionnaireId,
                    FieldKey = Truncate(a.FieldKey, 128),
                    Question = a.Question,
                    Answer = a.Answer,
                    OtherText = string.IsNullOrWhiteSpace(a.OtherText) ? null : a.OtherText,
                    Type = Truncate(a.Type, 50),
                    ConsentHtml = a.ConsentHtml,
                    DisplayOrder = ++order,
                    CreatedBy = caller.UserId,
                    CreatedDate = now
                });
            }

            _db.SaveChanges();
            tx.Commit();

            _auditService.LogEntityChange(
                action: "Update",
                entityType: "PT_PatientQuestionnaire",
                entityId: assignment.PatientQuestionnaireId,
                userId: caller.UserId,
                patientId: patientId,
                facilityId: assignment.FacilityId,
                description: $"Questionnaire submitted with {answers.Count} answer(s)",
                module: "Questionnaire");

            return PatientQuestionnaireResultDTO.Ok("Questionnaire submitted.", assignment.PatientQuestionnaireId);
        }

        // --------------------------------------------------------------- shared

        /// <summary>
        /// A submitted assignment with its answers, read only. Serves the patient
        /// (own assignments only) and staff (patients they can reach). Null when
        /// the caller may not see it, so a changed id reveals nothing.
        /// </summary>
        public PatientQuestionnaireSubmissionDTO? GetPatientQuestionnaireSubmission(long patientQuestionnaireId, PatientQuestionnaireCallerDTO caller)
        {
            var owner = _db.PT_PatientQuestionnaires.AsNoTracking()
                .Where(a => a.PatientQuestionnaireId == patientQuestionnaireId
                         && a.IsActive == true
                         && a.Status == PatientQuestionnaireStatus.Submitted)
                .Select(a => (long?)a.PatientId)
                .FirstOrDefault();

            if (owner is not long patientId)
                return null;

            var allowed = caller.RoleId == (long)UserRole.Patient
                ? caller.PatientId == patientId
                : CanStaffReachPatient(patientId, caller);

            if (!allowed)
                return null;

            var assignment = ProjectAssignments(_db.PT_PatientQuestionnaires.AsNoTracking()
                    .Where(a => a.PatientQuestionnaireId == patientQuestionnaireId))
                .First();

            var answers = _db.PT_PatientQuestionnaireAnswers.AsNoTracking()
                .Where(x => x.PatientQuestionnaireId == patientQuestionnaireId)
                .OrderBy(x => x.DisplayOrder)
                .ThenBy(x => x.PatientQuestionnaireAnswerId)
                .Select(x => new PatientQuestionnaireAnswerDTO
                {
                    PatientQuestionnaireAnswerId = x.PatientQuestionnaireAnswerId,
                    FieldKey = x.FieldKey,
                    Question = x.Question,
                    Answer = x.Answer,
                    OtherText = x.OtherText,
                    Type = x.Type,
                    ConsentHtml = x.ConsentHtml,
                    DisplayOrder = x.DisplayOrder,
                    CreatedDate = x.CreatedDate
                })
                .ToList();

            return new PatientQuestionnaireSubmissionDTO { Assignment = assignment, Answers = answers };
        }

        // -------------------------------------------------------------- helpers

        private IQueryable<PatientQuestionnaireAssignmentDTO> ProjectAssignments(IQueryable<PT_PatientQuestionnaire> source)
        {
            return
                from a in source
                join q in _db.SYS_Questionnaires on a.QuestionnaireId equals q.QuestionnaireId
                select new PatientQuestionnaireAssignmentDTO
                {
                    PatientQuestionnaireId = a.PatientQuestionnaireId,
                    PatientId = a.PatientId,
                    QuestionnaireId = a.QuestionnaireId,
                    QuestionnaireName = q.QuestionnaireName,
                    PatientTreatmentId = a.PatientTreatmentId,
                    Status = a.Status,
                    AssignedDate = a.AssignedDate,
                    AssignedByName = _db.SYS_UserDetails
                        .Where(u => u.UserId == a.AssignedBy)
                        .Select(u => (u.FirstName ?? "") + " " + (u.LastName ?? ""))
                        .FirstOrDefault(),
                    DueDate = a.DueDate,
                    StartedDate = a.StartedDate,
                    SubmittedDate = a.SubmittedDate,
                    DraftSavedDate = a.DraftSavedDate,
                    AnswerCount = _db.PT_PatientQuestionnaireAnswers
                        .Count(x => x.PatientQuestionnaireId == a.PatientQuestionnaireId)
                };
        }

        /// <summary>
        /// Whether a staff caller may act on this patient. Correlated Any rather
        /// than a list Contains - see the OPENJSON note in GetPatientQuestionnaires.
        /// </summary>
        private bool CanStaffReachPatient(long patientId, PatientQuestionnaireCallerDTO caller)
        {
            if (caller is null || patientId <= 0)
                return false;

            var patient = _db.PT_Patients.AsNoTracking()
                .Where(p => p.PatientId == patientId && p.IsActive != false)
                .Select(p => new { p.FacilityId, p.OrganizationId })
                .FirstOrDefault();

            if (patient is null)
                return false;

            switch ((UserRole)(caller.RoleId ?? 0))
            {
                case UserRole.SuperAdmin:
                    return true;

                case UserRole.GlobalAdmin:
                    return patient.OrganizationId == null || patient.OrganizationId == caller.OrganizationId;

                case UserRole.ClinicAdmin:
                case UserRole.Provider:
                    return patient.FacilityId != null
                        && _db.FC_UsersInFacilities.AsNoTracking().Any(u =>
                            u.UserId == caller.UserId
                            && u.FacilityId == patient.FacilityId
                            && u.IsAssign == true);

                default:
                    return false;
            }
        }

        private static string? Truncate(string? value, int max) =>
            value is null || value.Length <= max ? value : value.Substring(0, max);
    }
}
