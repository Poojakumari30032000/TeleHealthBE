using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vitality.Models.DTOs.Brands;

namespace Vitality.Models.Repos.Interfaces
{
    public interface IBrandsRepo
    {
        public GetBrandByIdResponseDTO GetBrandById(string? FacilityGuid);
        public bool SaveBrand(SaveBrandRequestDTO request, long UserId);
    }
}
