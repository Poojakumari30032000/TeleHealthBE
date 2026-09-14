using AutoMapper;
using DudeMeds.Models.DTOs.Common;
using DudeMeds.Models.DTOs.Conditions;
using DudeMeds.Models.Repos.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Vitality.Helper;

namespace DudeMeds.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductConditionsController : ControllerBase
    {
        public IConfiguration _configuration;
        private readonly IMapper _mapper;

        private readonly IProductConditionsRepo _IProductConditionsRepo;

        public ProductConditionsController(
            IConfiguration config,
            IMapper IMapper,
            IProductConditionsRepo IProductConditionsRepo)
        {
            _configuration = config;
            _mapper = IMapper;
            _IProductConditionsRepo = IProductConditionsRepo;
        }

        [HttpGet]
        [Route("getAllConditions")]
        public ApiResponse<List<GetAllConditionsResponseDTO>> GetAllConditions([FromQuery] GetAllConditionsRequestDTO request)
        {
            ApiResponse<List<GetAllConditionsResponseDTO>> response = new ApiResponse<List<GetAllConditionsResponseDTO>>();
            try
            {
                List<GetAllConditionsResponseDTO> result = new List<GetAllConditionsResponseDTO>();
                result = _IProductConditionsRepo.GetAllConditions(request, out int totalConditionCount);
                int totalPages = (int)Math.Ceiling((double)totalConditionCount / request.PageSize);
                response.Data = result;
                response.TotalEntityCount = totalConditionCount;
                response.TotalPages = totalPages;
                response.Data = result;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpGet]
        [Route("getConditionById")]
        public ApiResponse<GetConditionByIdResponseDTO> GetConditionById([FromQuery] GetByIdRequestDTO request)
        {
            ApiResponse<GetConditionByIdResponseDTO> response = new ApiResponse<GetConditionByIdResponseDTO>();
            try
            {
                GetConditionByIdResponseDTO result = new GetConditionByIdResponseDTO();
                result = _IProductConditionsRepo.GetConditionById(request.Id);
                response.Data = result;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpPost]
        [Route("saveCondition")]
        public ApiResponse<bool> SaveCondition([FromBody] SaveConditionRequestDTO request)
        {
            ApiResponse<bool> response = new ApiResponse<bool>();
            try
            {
                var OrganizationId = Convert.ToInt64(User.FindFirst("OrganizationId").Value);
                var UserId = Convert.ToInt64(User.FindFirst("UserId").Value);
                string res = _IProductConditionsRepo.SaveCondition(request, UserId, OrganizationId);
                if (res == "Condition Created Successfully" || res == "Condition Updated Successfully")
                {
                    response.Message = res;
                    response.Data = true;
                }
                else
                {
                    response.Message = res;
                    response.Data = false;
                }
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpPost]
        [Route("deleteCondition")]
        public ApiResponse<bool> DeleteCondition([FromBody] GetByIdRequestDTO request)
        {
            ApiResponse<bool> response = new ApiResponse<bool>();
            bool res = _IProductConditionsRepo.DeleteCondition(request.Id);
            response.Data = res;
            return response;
        }
    }
}
