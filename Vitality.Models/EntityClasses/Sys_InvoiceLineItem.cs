using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class Sys_InvoiceLineItem
    {
        public long InvoiceLineItemId { get; set; }
        public int InvoiceId { get; set; }
        public long? DrugId { get; set; }
        public long? ProductId { get; set; }
        public string ProductLineItemName { get; set; } = null!;
        public int Qty { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
        public bool? IsActive { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public long? BundleId { get; set; }
        public string? CouponCode { get; set; }
        public decimal? OriginalPrice { get; set; }
        public decimal? DiscountedPrice { get; set; }
        public decimal? DiscountAmount { get; set; }

        public virtual Sys_Invoice Invoice { get; set; } = null!;
    }
}
