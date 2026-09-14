using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vitality.Models.EntityClasses;

namespace Vitality.Models.DTOs.FacilitySquareCred
{
    public class SaveSquareCredRequestDto
    {
        public long? FacilityId { get; set; }
        public string? ApplicationId { get; set; }
        public string? AccessToken { get; set; }
        public string? LocationId { get; set; }
        public bool? IsActive { get; set; }
    }
}
