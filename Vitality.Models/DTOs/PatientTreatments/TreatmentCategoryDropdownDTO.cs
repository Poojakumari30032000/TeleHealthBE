using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitality.Models.DTOs.PatientTreatments
{
    public class TreatmentCategoryDropdownDTO
    {
        public long PatientTreatmentId { get; set; }
        public long? Category { get; set; }
        public string? CategoryName { get; set; }
        public bool IsFirstQuestionaire { get; set; }

    }
}
