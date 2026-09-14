using DudeMeds.Models.DTOs.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vitality.Models.DTOs.Notifications;

namespace Vitality.Models.Repos.Interfaces
{
    public interface INotificationsRepo
    {
        public Task<List<GetAllNotificationsResponseDTO>> GetAllNotificationsAsync(GetAllNotificationsRequestDTO request);
        public Task<bool> UpdateNotificationsAsync(List<GetByIdRequestDTO> requestList, long? userId, long? roleId, long? facilityId);
    }
}
