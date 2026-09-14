using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class SYS_UserCard
    {
        public SYS_UserCard()
        {
            Sys_InvoicePayments = new HashSet<Sys_InvoicePayment>();
        }

        public long CardId { get; set; }
        public bool? IsActive { get; set; }
        public string? SquareCardId { get; set; }
        public string? ExpirationYear { get; set; }
        public string? ExpirationMonth { get; set; }
        public string? Last4 { get; set; }
        public string? CardBrand { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? SquareClientId { get; set; }
        public long? UserId { get; set; }
        public bool IsDefault { get; set; }
        public string? CardHolderName { get; set; }
        public string? Currency { get; set; }

        public string? StripePaymentMethodId { get; set; }

        public string? StripeCustomerId { get; set; }

        public string? StripeAccountId { get; set; }

        public virtual SYS_UserDetail? User { get; set; }
        public virtual ICollection<Sys_InvoicePayment> Sys_InvoicePayments { get; set; }
    }
}
