using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.PatientOrders
{
    public class SavePharmacyNoteRequestDTO
    {
        public long PharmacyNoteId { get; set; }
        public long? PatientOrderId { get; set; }
        public string? PharmacyNote { get; set; }
    }
}
