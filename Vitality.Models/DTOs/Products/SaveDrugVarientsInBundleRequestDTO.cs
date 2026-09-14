using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.Products
{
    public class SaveDrugVarientsInBundleRequestDTO
    {
        public long? ProductId { get; set; }
        public long? BundleId { get; set; }
        public long? DrugVarientBundleId { get; set; }
        public decimal? Price { get; set; }
        public int? OrderCount {  get; set; }
        public int? visits { get; set; }
        public string? ItemDesignatorID { get; set; }
        public bool? Refrigerated { get; set; }

    }
}
