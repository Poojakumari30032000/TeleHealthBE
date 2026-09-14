using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.Pharmacies
{
    public class UpdatePharmacyStatusRequestDTO
    {
        public long? PharmacyId { get; set; }
        public string? Status { get; set; }
    }
}
