using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Vitality.Models.DTOs.Invoices
{
    public class CreateManualClinicToPatientInvoiceRequestDTO
    {
        [Required]
        public long FacilityId { get; set; }

        [Required]
        public long PatientId { get; set; }

        [Required]
        public decimal AmountDue { get; set; }

        public DateTime? InvoiceDate { get; set; }

        [Required]
        public List<InvoiceLineItemDTO> Items { get; set; } = new List<InvoiceLineItemDTO>();
    }

    public class InvoiceLineItemDTO
    {
        public long? DrugId { get; set; }
        public long? ProductId { get; set; }

        [Required]
        public string ProductLineItemName { get; set; } = string.Empty;

        [Required]
        public int Qty { get; set; }

        [Required]
        public decimal UnitPrice { get; set; }

        [Required]
        public decimal LineTotal { get; set; }
    }
}
