using DudeMeds.Models.DTOs.ClinicalCodes;
using DudeMeds.Models.Repos.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using Vitality.Models.EntityClasses;
using Vitality.Models.Enums;
using Vitality.Models.Helpers;
using Vitality.Models.Repos.Services.Audit;

namespace DudeMeds.Models.Repos.Services
{
    /// <summary>
    /// TEL-22 - ICD-10-CM / CPT codes attached to a treatment SOAP note, the
    /// "encounter" TEL-22 codes against.
    ///
    /// Takes the DI-registered <see cref="MainContext"/>, like ClinicalCodesRepo,
    /// rather than deriving from BaseRepo (docs/project-map.md, quirk 1).
    ///
    /// Every statement here touches one note's rows, or one code by its unique
    /// (release, code) key, so each stays far below the development server's
    /// query governor cost limit of 3000.
    /// </summary>
    public class SoapNoteCodesRepo : ISoapNoteCodesRepo
    {
        private const string NotFound = "SOAP note not found.";

        private readonly MainContext _db;
        private readonly IAuditService _auditService;

        public SoapNoteCodesRepo(MainContext db, IAuditService auditService)
        {
            _db = db;
            _auditService = auditService;
        }

        public SoapNoteCodesResultDTO GetSoapNoteCodes(long soapNoteId, ClinicalCodeCallerDTO caller)
        {
            var note = LoadReachableNote(soapNoteId, caller);
            if (note is null) return Fail(NotFound);

            return new SoapNoteCodesResultDTO
            {
                Success = true,
                Data = BuildCodes(note, caller)
            };
        }

        public SoapNoteCodesResultDTO SaveSoapNoteCodes(SaveSoapNoteCodesRequestDTO request, ClinicalCodeCallerDTO caller)
        {
            ArgumentNullException.ThrowIfNull(request);

            var note = LoadReachableNote(request.SoapNoteId, caller);
            if (note is null) return Fail(NotFound);

            if (!CanEdit(caller))
                return Fail("Only a provider can change the codes on a SOAP note.");

            var (requested, errors) = SoapNoteCodeRequestValidator.Normalize(request.Codes);

            // ---- every code must exist, be active, and be in force on the note's date
            // in the release it was picked from. One indexed lookup per code
            // (UX_SYS_Icd10Code_Version_Code), never a list Contains - EF Core 8
            // turns that into OPENJSON (docs/project-map.md, quirk 4).

            var day = note.CodingDate;
            var resolved = new List<(NormalizedSoapNoteCode Request, ResolvedCode Row)>();
            foreach (var r in requested)
            {
                var row = r.CodeSystem == ClinicalCodeSystem.Icd10Cm
                    ? ResolveIcd10(r.CodeSetVersionId, r.Code, day)
                    : ResolveCpt(r.CodeSetVersionId, r.Code, day);

                if (row is null)
                {
                    errors.Add($"{r.Code} is not an active {r.CodeSystem} code in force on {day:yyyy-MM-dd} in the release it was picked from.");
                    continue;
                }

                // A header (category) code such as E11 is not valid on a claim, and
                // TEL-20 will carry these codes to billing. Only billable codes are kept.
                if (!row.IsBillable)
                {
                    errors.Add($"{row.DisplayCode} is a header code, not a billable diagnosis. Pick a more specific code under it.");
                    continue;
                }

                resolved.Add((r, row));
            }

            if (errors.Count > 0)
            {
                var failed = Fail($"The codes were not saved: {errors.Count} problem(s). Nothing was changed.");
                failed.Errors = errors;
                return failed;
            }

            // ---- replace: keep matching rows (their CreatedDate survives), update
            // their order and release, insert the new ones, delete the rest.

            var now = DateTime.UtcNow;
            using var tx = _db.Database.BeginTransaction();

            var existing = _db.PT_PatientTreatmentSoapNoteCodes
                .Where(c => c.SoapNoteId == note.SoapNoteId)
                .ToList();

            var before = existing
                .OrderBy(c => c.DisplayOrder)
                .Select(c => $"{c.CodeSystem} {c.DisplayCode}")
                .ToList();

            var keep = new HashSet<long>();
            foreach (var (r, row) in resolved)
            {
                var match = existing.FirstOrDefault(c => c.CodeSystem == r.CodeSystem && c.Code == row.Code);
                if (match is null)
                {
                    match = new PT_PatientTreatmentSoapNoteCode
                    {
                        SoapNoteId = note.SoapNoteId,
                        CodeSystem = r.CodeSystem,
                        CreatedBy = caller.UserId,
                        CreatedDate = now
                    };
                    _db.PT_PatientTreatmentSoapNoteCodes.Add(match);
                }
                else
                {
                    keep.Add(match.SoapNoteCodeId);
                    if (IsUnchanged(match, row, r.DisplayOrder)) continue;
                    match.ModifiedBy = caller.UserId;
                    match.ModifiedDate = now;
                }

                match.CodeSetVersionId = row.CodeSetVersionId;
                match.Icd10CodeId = r.CodeSystem == ClinicalCodeSystem.Icd10Cm ? row.CodeId : null;
                match.CptCodeId = r.CodeSystem == ClinicalCodeSystem.Cpt ? row.CodeId : null;
                match.Code = row.Code;
                match.DisplayCode = row.DisplayCode;
                match.Description = row.Description;
                match.DisplayOrder = r.DisplayOrder;
            }

            var removed = existing.Where(c => !keep.Contains(c.SoapNoteCodeId)).ToList();
            _db.PT_PatientTreatmentSoapNoteCodes.RemoveRange(removed);

            _db.SaveChanges();
            tx.Commit();

            var after = resolved.Select(x => $"{x.Request.CodeSystem} {x.Row.DisplayCode}").ToList();

            // Logged against the note, so it appears in the patient's audit history
            // alongside the note itself (AuditLogsRepo reads PT_PatientTreatmentSoapNote).
            _auditService.LogEntityChange(
                action: "UpdateCodes",
                entityType: "PT_PatientTreatmentSoapNote",
                entityId: note.SoapNoteId,
                oldValues: new { Codes = before },
                newValues: new { Codes = after },
                userId: caller.UserId,
                patientId: note.PatientId,
                facilityId: note.FacilityId,
                description: $"SOAP note {note.SoapNoteId} codes set to: {(after.Count == 0 ? "none" : string.Join(", ", after))}",
                module: "ClinicalCodes");

            return new SoapNoteCodesResultDTO
            {
                Success = true,
                Message = after.Count == 0 ? "Codes removed from the SOAP note." : $"{after.Count} code(s) saved on the SOAP note.",
                Data = BuildCodes(note, caller)
            };
        }

        // ------------------------------------------------------------ access

        private sealed record ReachableNote(long SoapNoteId, long? PatientId, long? FacilityId, DateTime CodingDate);

        /// <summary>
        /// The note, if it exists, is active, and the caller may reach its patient.
        /// A note the caller may not reach is reported exactly like a missing one.
        /// </summary>
        private ReachableNote? LoadReachableNote(long soapNoteId, ClinicalCodeCallerDTO caller)
        {
            if (soapNoteId <= 0 || caller is null) return null;

            var note = (
                from n in _db.PT_PatientTreatmentSoapNotes.AsNoTracking()
                join t in _db.PT_PatientTreatments.AsNoTracking() on n.PatientTreatmentId equals t.PatientTreatmentId
                where n.SoapNoteId == soapNoteId && n.IsActive != false
                select new { n.SoapNoteId, PatientId = t.PatientId ?? n.PatientId, n.FacilityId, n.CreatedDate }
            ).FirstOrDefault();

            if (note?.PatientId is null || !CanStaffReachPatient(note.PatientId.Value, caller))
                return null;

            return new ReachableNote(note.SoapNoteId, note.PatientId, note.FacilityId, note.CreatedDate.Date);
        }

        /// <summary>
        /// Same rules as the TEL-57 check in QuestionnairesRepo.CanStaffReachPatient:
        /// Super Admin reaches everyone, Global Admin their organisation, Clinic Admin
        /// and Provider the facilities they are assigned to in FC_UsersInFacilities
        /// (there is no FacilityId claim).
        /// </summary>
        private bool CanStaffReachPatient(long patientId, ClinicalCodeCallerDTO caller)
        {
            var patient = _db.PT_Patients.AsNoTracking()
                .Where(p => p.PatientId == patientId && p.IsActive != false)
                .Select(p => new { p.FacilityId, p.OrganizationId })
                .FirstOrDefault();

            if (patient is null) return false;

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

        /// <summary>Mirrors the SOAP note screen, where only a provider may edit a note.</summary>
        private static bool CanEdit(ClinicalCodeCallerDTO caller) =>
            (UserRole)(caller.RoleId ?? 0) == UserRole.Provider;

        // ------------------------------------------------------------ codes

        private sealed record ResolvedCode(long CodeId, long CodeSetVersionId, string Code, string DisplayCode, string Description, bool IsBillable);

        private ResolvedCode? ResolveIcd10(long versionId, string code, DateTime day) => (
            from c in _db.SYS_Icd10Codes.AsNoTracking()
            join v in _db.SYS_CodeSetVersions.AsNoTracking() on c.CodeSetVersionId equals v.CodeSetVersionId
            where c.CodeSetVersionId == versionId
               && c.Code == code
               && c.IsActive
               && v.IsActive
               && v.CodeSystem == ClinicalCodeSystem.Icd10Cm
               && c.EffectiveDate <= day
               && (c.TerminationDate == null || c.TerminationDate >= day)
            select new ResolvedCode(c.Icd10CodeId, c.CodeSetVersionId, c.Code, c.DisplayCode, c.LongDescription, c.IsBillable)
        ).FirstOrDefault();

        private ResolvedCode? ResolveCpt(long versionId, string code, DateTime day) => (
            from c in _db.SYS_CptCodes.AsNoTracking()
            join v in _db.SYS_CodeSetVersions.AsNoTracking() on c.CodeSetVersionId equals v.CodeSetVersionId
            where c.CodeSetVersionId == versionId
               && c.Code == code
               && c.IsActive
               && v.IsActive
               && v.CodeSystem == ClinicalCodeSystem.Cpt
               && c.EffectiveDate <= day
               && (c.TerminationDate == null || c.TerminationDate >= day)
            select new ResolvedCode(c.CptCodeId, c.CodeSetVersionId, c.Code, c.Code, c.LongDescription, true)
        ).FirstOrDefault();

        private static bool IsUnchanged(PT_PatientTreatmentSoapNoteCode row, ResolvedCode code, int order) =>
            row.CodeSetVersionId == code.CodeSetVersionId
            && row.DisplayOrder == order
            && row.DisplayCode == code.DisplayCode
            && row.Description == code.Description
            && (row.Icd10CodeId ?? row.CptCodeId) == code.CodeId;

        private SoapNoteCodesDTO BuildCodes(ReachableNote note, ClinicalCodeCallerDTO caller)
        {
            var codes = (
                from c in _db.PT_PatientTreatmentSoapNoteCodes.AsNoTracking()
                join v in _db.SYS_CodeSetVersions.AsNoTracking() on c.CodeSetVersionId equals v.CodeSetVersionId
                where c.SoapNoteId == note.SoapNoteId
                orderby c.DisplayOrder
                select new SoapNoteCodeDTO
                {
                    SoapNoteCodeId = c.SoapNoteCodeId,
                    CodeSystem = c.CodeSystem,
                    CodeSetVersionId = c.CodeSetVersionId,
                    VersionLabel = v.VersionLabel,
                    CodeId = c.Icd10CodeId ?? c.CptCodeId ?? 0,
                    Code = c.Code,
                    DisplayCode = c.DisplayCode,
                    Description = c.Description,
                    // Only billable codes can be saved (see SaveSoapNoteCodes).
                    IsBillable = true,
                    DisplayOrder = c.DisplayOrder
                }).ToList();

            return new SoapNoteCodesDTO
            {
                SoapNoteId = note.SoapNoteId,
                CodingDate = note.CodingDate,
                CanEdit = CanEdit(caller),
                Codes = codes
            };
        }

        private static SoapNoteCodesResultDTO Fail(string message) =>
            new() { Success = false, Message = message };
    }
}
