using System;

namespace Vitality.Models.DTOs.Patients
{

    public class PatientProfileNoteDTO
    {
        public long PatientProfileNoteId { get; set; }
        public long PatientId { get; set; }
        public string? NoteText { get; set; }
        public long? CreatedBy { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime? CreatedDate { get; set; }
        public long? ModifiedBy { get; set; }
        public string? ModifiedByName { get; set; }
        public DateTime? ModifiedDate { get; set; }
    }

    public class GetPatientProfileNotesRequestDTO
    {
        public long PatientId { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int? ClientTimezoneOffsetMinutes { get; set; }
    }

    public class SavePatientProfileNoteRequestDTO
    {

        public long PatientProfileNoteId { get; set; }
        public long PatientId { get; set; }
        public string? NoteText { get; set; }
    }
}
