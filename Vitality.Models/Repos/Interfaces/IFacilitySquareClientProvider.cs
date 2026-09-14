using Square;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitality.Models.Repos.Interfaces
{
    public interface IFacilitySquareClientProvider
    {
        Task<SquareClient> GetAsync(long facilityId, CancellationToken ct = default);
        void Invalidate(long facilityId);
    }
}
