using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Vitality.Models.DTOs.Invoices
{

    public class CreateManualGAToClinicInvoiceRequestDTO
    {
        [Required]
        public long FacilityId { get; set; }

        [Required]
        public decimal AmountDue { get; set; }

        public DateTime? InvoiceDate { get; set; }

        [Required]
        public List<InvoiceLineItemDTO> Items { get; set; } = new List<InvoiceLineItemDTO>();
    }
}
