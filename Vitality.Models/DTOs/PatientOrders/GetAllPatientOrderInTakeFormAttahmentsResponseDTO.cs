using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.PatientOrders
{
    public class GetAllPatientOrderInTakeFormAttahmentsResponseDTO
    {
        public string? AttachmentURL { get; set; }
        public string? UploadedBy { get; set; }
        public DateTime? UploadedDate { get; set; }
    }
}
