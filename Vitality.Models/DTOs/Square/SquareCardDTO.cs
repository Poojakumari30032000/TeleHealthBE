using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitality.Models.DTOs.Square
{
    public class SquareCardDTO
    {
        public long CardId { get; set; }
        public bool? IsActive { get; set; }
        public string? SquareCardId { get; set; }
        public string? SquareClientId { get; set; }
        public string? ExpirationYear { get; set; }
        public string? CardHolderName { get; set; }
        public string? ExpirationMonth { get; set; }
        public string? Last4 { get; set; }
        public string? CardBrand { get; set; }
        public string? Currency { get; set; }
        public long? UserId { get; set; }
        public bool? IsDefault { get; set; }

    }
}
