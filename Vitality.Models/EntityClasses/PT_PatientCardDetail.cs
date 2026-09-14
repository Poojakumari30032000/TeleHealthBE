using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class PT_PatientCardDetail
    {
        public long PatientCardId { get; set; }
        public long? PatientId { get; set; }
        public long? PatientPaymentId { get; set; }
        public string? CardNumber { get; set; }
        public DateTime? ExpirationDate { get; set; }
        public string? CVC { get; set; }
        public bool? IsActive { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
    }
}
