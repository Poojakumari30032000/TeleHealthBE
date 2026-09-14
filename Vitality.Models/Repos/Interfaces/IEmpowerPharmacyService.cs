using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vitality.Models.DTOs.EmpowerPharmacy;

namespace Vitality.Models.Repos.Interfaces
{
    public interface IEmpowerPharmacyService
    {
        Task<EmpowerOrderResultDTO> CreateOrderForPrescriptionAsync(long patientPrescriptionId);

        Task<List<string>> GetShippingTypeNamesAsync();
    }
}
