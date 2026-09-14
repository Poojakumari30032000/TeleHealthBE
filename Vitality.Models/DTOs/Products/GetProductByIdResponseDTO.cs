using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.Products
{
    public class GetProductByIdResponseDTO
    {
        public long ProductId { get; set; }
        public long? ConditionId { get; set; }
        public long? CategoryId { get; set; }
        public string? ProductCode { get; set; }
        public string? ProductType { get; set; }
        public string? ProductGroup { get; set; }
        public string? Name { get; set; }
        public string? BrandName { get; set; }
        public string? ShortName { get; set; }
        public string? LabelerName { get; set; }
        public string? GenericName { get; set; }
        public string? DosageForm { get; set; }
        public decimal? ConsultOnlyPrice { get; set; }
        public long? TemplateId { get; set; }
        public string? Description { get; set; }
        public string? Guid { get; set; }
    }
}
