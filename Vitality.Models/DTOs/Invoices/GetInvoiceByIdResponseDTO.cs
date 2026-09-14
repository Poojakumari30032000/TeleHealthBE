using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitality.Models.DTOs.Invoices
{
    public class GetInvoiceByIdResponseDTO
    {
        public long InvoiceId { get; set; }
        public string? InvoiceNumber { get; set; }
        public string? CustomerName { get; set; }
        public decimal? Amount { get; set; }
        public string? Status { get; set; }
        public string? InvoiceType { get; set; }
        public bool? IsActive { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
        public long? SubscriptionId { get; set; }

        public string? ExpirationYear { get; set; }
        public string? ExpirationMonth { get; set; }
        public string? Last4 { get; set; }
        public string? CardBrand { get; set; }
        public string? CardHolderName { get; set; }
        public string? Currency { get; set; }
        public long? CardId { get; set; }
        public string? Email { get; set; }
        public DateTime? DueDate { get; set; }
        public string? FacilityPhone { get; set; }
        public string? FacilityAddress { get; set; }
        public string? PdfS3Url { get; set; }

        public List<GetInvoiceByIdLineItemDTO> LineItems { get; set; } = new();
    }

    public class GetInvoiceByIdLineItemDTO
    {
        public long InvoiceLineItemId { get; set; }
        public string ProductLineItemName { get; set; } = string.Empty;
        public int Qty { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
        public long? DrugId { get; set; }
        public long? ProductId { get; set; }
        public long? BundleId { get; set; }
        public string? CouponCode { get; set; }
        public decimal? OriginalPrice { get; set; }
        public decimal? DiscountedPrice { get; set; }
        public decimal? DiscountAmount { get; set; }
    }
}
