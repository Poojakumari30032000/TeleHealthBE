using System.ComponentModel.DataAnnotations;

namespace Vitality.Models.DTOs.Patients
{
    public class CreatePatientDocumentRequestDTO
    {
        [Required]
        public long PatientId { get; set; }

        [Required]
        [StringLength(500)]
        public string DocumentName { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Description { get; set; }

        [Required]
        [StringLength(2000)]
        public string DocumentUrl { get; set; } = string.Empty;
    }
}
