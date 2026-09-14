using DudeMeds.Models.DTOs.Pharmacies;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.Repos.Interfaces
{
    public interface IPharmaciesRepo
    {
        public List<GetAllPharmaciesResponseDTO> GetAllPharmacies(GetAllPharmaciesRequestDTO request, out int totalPharmacyCount);
        public GetPharmacyByIdResponseDTO GetPharmacyById(long PharmacyId);
        public string SavePharmacy(SavePharmacyRequestDTO request, long UserId, long OrganizationId);
        public bool DeletePharmacy(long PharmacyId);
        public bool UpdatePharmacyStatus(UpdatePharmacyStatusRequestDTO request);
    }
}
