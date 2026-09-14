using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitality.Models.DTOs.PatientOrders
{
    public class GetPatientOrderPharmacyNotesResponseDTO
    {
        public long? PharmacyId { get; set; }
        public string? PharmacyName { get; set; }
        public string? OrderStatus { get; set; }
        public string? Timeunshipped { get; set; }
        public List<OrderPharmacyNotesDTO>? PharmacyNotes { get; set; }
    }
    public class OrderPharmacyNotesDTO
    {
        public long? PharmacyNoteId { get; set; }
        public string? PharmacyNote { get; set; }
        public DateTime? CreatedDate { get; set; }
    }
}
