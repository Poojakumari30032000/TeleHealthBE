using System;

namespace Vitality.Models.DTOs.Invoices
{

    public class GetFacilityInvoiceSummaryRequestDTO
    {
        public long? FacilityId { get; set; }
        public string? InvoiceType { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public int? ClientTimezoneOffsetMinutes { get; set; }
        public bool IncludePaid { get; set; } = true;
        public bool IncludePending { get; set; } = true;
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
