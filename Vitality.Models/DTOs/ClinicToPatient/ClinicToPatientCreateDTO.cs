using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitality.Models.DTOs.ClinicToPatient
{
    public class ClinicToPatientCreateDTO
    {
        public long? DrugId { get; set; }
        public long? GAtoClinicId { get; set; }
        public long? FacilityId { get; set; }
        public decimal? ClinicSuggestedRetailPrice { get; set; }
        public bool? IsActive { get; set; }
    }
}
