using DudeMeds.Models.DTOs.Conditions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.Repos.Interfaces
{
    public interface IProductConditionsRepo
    {
        public List<GetAllConditionsResponseDTO> GetAllConditions(GetAllConditionsRequestDTO request, out int totalConditionCount);
        public GetConditionByIdResponseDTO GetConditionById(long ConditionId);
        public string SaveCondition(SaveConditionRequestDTO request, long UserId, long OrganizationId);
        public bool DeleteCondition(long ConditionId);
    }
}
