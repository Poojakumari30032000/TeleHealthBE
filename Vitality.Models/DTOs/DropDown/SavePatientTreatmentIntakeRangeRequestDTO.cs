using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitality.Models.DTOs.DropDown
{
    public class SavePatientTreatmentIntakeItemDTO
    {
        public string? Question { get; set; }
        public string? Answer { get; set; }
        public string? ConsentHtml { get; set; }
        public string? OtherText { get; set; }
        public string? Type { get; set; }
    }

    public class SavePatientTreatmentIntakeRangeRequestDTO
    {
        public long PatientTreatmentId { get; set; }
        public List<SavePatientTreatmentIntakeItemDTO> Items { get; set; } = new();
    }
}
