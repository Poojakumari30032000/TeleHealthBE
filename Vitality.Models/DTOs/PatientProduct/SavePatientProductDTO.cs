using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitality.Models.DTOs.PatientProduct
{
    public class SavePatientProductDTO
    {
        public long PatientProductId { get; set; }
        public long? UserId { get; set; }
        public long? PatientId { get; set; }
        public long? ProductId { get; set; }
        public long? FacilityId { get; set; }
    }
}
