using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.Facilities
{

    public class GetFacilityPaymentModeResponseDTO
    {
        public long FacilityId { get; set; }
        public int? PaymentModeId { get; set; }
        public string PaymentModeName { get; set; } = string.Empty;
    }
}
