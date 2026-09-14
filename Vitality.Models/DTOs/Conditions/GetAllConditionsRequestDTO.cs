using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.Conditions
{
    public class GetAllConditionsRequestDTO
    {
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public long? CategoryId { get; set; }
        public long? OrganizationId { get; set; }
    }
}
