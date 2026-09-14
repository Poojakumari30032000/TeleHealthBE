using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.Products
{
    public class GetBundleByIdRequestDTO
    {
        public long? ProductId { get; set; }
        public long? DrugId { get; set; }
    }
}
