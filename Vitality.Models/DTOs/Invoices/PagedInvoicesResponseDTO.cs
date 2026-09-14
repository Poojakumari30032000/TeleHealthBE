using System.Collections.Generic;

namespace Vitality.Models.DTOs.Invoices
{
    public class PagedInvoicesResponseDTO
    {
        public List<GetAllInvoicesResponseDTO> Invoices { get; set; } = new List<GetAllInvoicesResponseDTO>();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }
}
