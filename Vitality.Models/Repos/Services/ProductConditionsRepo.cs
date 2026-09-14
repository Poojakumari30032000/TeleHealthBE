using AutoMapper;
using DudeMeds.Models.DTOs.Conditions;
using DudeMeds.Models.Repos.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vitality.Models.EntityClasses;
using Vitality.Models.Repos;

namespace DudeMeds.Models.Repos.Services
{
    public class ProductConditionsRepo : BaseRepo , IProductConditionsRepo
    {
        private readonly IMapper _mapper;
        public ProductConditionsRepo(IMapper mapper)
        {
            _mapper = mapper;
        }
        public List<GetAllConditionsResponseDTO> GetAllConditions(GetAllConditionsRequestDTO request, out int totalConditionCount)
        {
            List<GetAllConditionsResponseDTO> response = new List<GetAllConditionsResponseDTO>();
            List<PD_Condition> list = new List<PD_Condition>();
            if (request.CategoryId != null)
            {
                list = _db.PD_Conditions.Where(x => x.OrganizationId == request.OrganizationId && x.CategoryId == request.CategoryId && x.IsActive == true).ToList();
            }
            else
            {
                list = _db.PD_Conditions.Where(x => x.OrganizationId == request.OrganizationId && x.IsActive == true).ToList();
            }
            totalConditionCount = list.Count;
            list = list.OrderBy(x => x.ConditionName).Skip((request.PageNumber - 1) * request.PageSize).Take(request.PageSize).ToList();
            response = _mapper.Map<List<GetAllConditionsResponseDTO>>(list);
            foreach (var item in response)
            {
                item.CategoryName = _db.PD_Categories.Where(x => x.CategoryId == item.CategoryId).Select(x => x.CategoryName).FirstOrDefault();
            }
            return response;
        }

        public GetConditionByIdResponseDTO GetConditionById(long ConditionId)
        {
            GetConditionByIdResponseDTO response = new GetConditionByIdResponseDTO();
            PD_Condition condition = _db.PD_Conditions.Where(x => x.ConditionId == ConditionId).FirstOrDefault();
            response = _mapper.Map<GetConditionByIdResponseDTO>(condition);
            return response;

        }
        public string SaveCondition(SaveConditionRequestDTO request, long UserId, long OrganizationId)
        {

            try
            {
                PD_Condition condition = new PD_Condition();

                if (request.ConditionId == 0)
                {
                    condition = _mapper.Map<PD_Condition>(request);

                    condition.CreatedBy = UserId;
                    condition.CreatedDate = DateTime.UtcNow;
                    condition.OrganizationId = OrganizationId;
                    condition.IsActive = true;
                    _db.PD_Conditions.Add(condition);
                    _db.SaveChanges();
                    return "Condition Created Successfully";

                }
                else
                {
                    condition = _db.PD_Conditions.Where(x => x.ConditionId == request.ConditionId).FirstOrDefault();
                    _mapper.Map(request, condition);
                    _db.SaveChanges();
                    return "Condition Updated Successfully";

                }
            }
            catch (Exception ex)
            {
                return "Something went wrong. Please try again later.";
            }
        }

        public bool DeleteCondition(long ConditionId)
        {
            PD_Condition condition = _db.PD_Conditions.Where(x => x.ConditionId == ConditionId).FirstOrDefault();
            if (condition != null)
            {
                condition.IsActive = false;
                _db.SaveChanges();
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
