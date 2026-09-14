using System;
using System.Collections.Generic;

namespace Vitality.Models.DTOs.Invoices
{
    public class FacilityInvoiceListItemDTO
    {
        public long InvoiceId { get; set; }
        public string? InvoiceNumber { get; set; }
        public long? FacilityId { get; set; }
        public string? FacilityName { get; set; }
        public decimal? Amount { get; set; }
        public long? ProviderBillTotal { get; set; }
        public long? PharmacyBillTotal { get; set; }
        public string? Status { get; set; }
        public string? InvoiceType { get; set; }
        public DateTime? InvoiceDate { get; set; }
        public DateTime? DueDate { get; set; }
        public bool? IsMonthlyInvoiceGenerated { get; set; }
    }

    public class PagedFacilityInvoicesResponseDTO
    {
        public List<FacilityInvoiceListItemDTO> Invoices { get; set; } = new List<FacilityInvoiceListItemDTO>();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }
}
