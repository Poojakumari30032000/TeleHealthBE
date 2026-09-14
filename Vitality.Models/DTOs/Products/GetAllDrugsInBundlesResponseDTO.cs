using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.Products
{
    public class GetAllDrugsInBundlesResponseDTO
    {
        public long? DrugId { get; set; }
        public string? DrugName { get; set; }
    }
}
