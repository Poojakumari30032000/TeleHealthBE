using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.Products
{
    public class SaveLabTestRequestDTO
    {
        public long LabTestId { get; set; }
        public string? Name { get; set; }
        public long? TemplateId { get; set; }
        public long? FacilityId { get; set; }
    }
}
