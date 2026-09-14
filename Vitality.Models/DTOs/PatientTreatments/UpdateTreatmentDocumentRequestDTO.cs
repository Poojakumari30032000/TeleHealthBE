using System.ComponentModel.DataAnnotations;

namespace Vitality.Models.DTOs.PatientTreatments
{
    public class UpdateTreatmentDocumentRequestDTO
    {
        [Required]
        public long PatientTreatmentDocumentId { get; set; }

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
