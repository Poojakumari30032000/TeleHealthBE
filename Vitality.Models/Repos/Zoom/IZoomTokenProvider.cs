using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitality.Models.Repos.Zoom
{
    public interface IZoomTokenProvider
    {
        Task<string> GetAccessTokenAsync(CancellationToken ct = default);
    }
}
