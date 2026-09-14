using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.Patients
{
    public class GetAllPatientsRequestDTO
    {
       public long? ProviderId { get; set; }
       public long? FacilityId { get; set; }
       public string? Title { get; set; }
       public DateTime? StartDate { get; set; }
       public DateTime? EndDate { get; set; }
       public string? Status { get; set; }
       public string? FulfillmentStatus { get; set; }
       public string? VisitStatus { get; set; }

       public string? RefillStatus { get; set; }
       public string? Refills {  get; set; }
       public bool? IsArchieved { get; set; }
       public bool? TestMode { get; set; }
       public bool? UnreadChats { get; set; }
       public bool? IsWaitList { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 100;
    }
}
