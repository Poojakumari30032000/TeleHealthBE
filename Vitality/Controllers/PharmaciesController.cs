using AutoMapper;
using DudeMeds.Models.DTOs.Common;
using DudeMeds.Models.DTOs.Pharmacies;
using DudeMeds.Models.Repos.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Vitality.Helper;
using Vitality.Filters;
using Vitality.Models.Security;

namespace DudeMeds.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PharmaciesController : ControllerBase
    {
        public IConfiguration _configuration;
        private readonly IMapper _mapper;

        private readonly IPharmaciesRepo _IPharmaciesRepo;

        public PharmaciesController(
            IConfiguration config,
            IMapper IMapper,
            IPharmaciesRepo IPharmaciesRepo)
        {
            _configuration = config;
            _mapper = IMapper;
            _IPharmaciesRepo = IPharmaciesRepo;

        }

        [HttpGet]
        [Route("getAllPharmacies")]
        [RequiresPermission(Permissions.Pharmacy.View)]
        public ApiResponse<List<GetAllPharmaciesResponseDTO>> GetAllPharmacies([FromQuery] GetAllPharmaciesRequestDTO request)
        {
            ApiResponse<List<GetAllPharmaciesResponseDTO>> response = new ApiResponse<List<GetAllPharmaciesResponseDTO>>();
            try
            {
                List<GetAllPharmaciesResponseDTO> result = new List<GetAllPharmaciesResponseDTO>();
                result = _IPharmaciesRepo.GetAllPharmacies(request, out int totalPharmacyCount);
                int totalPages = (int)Math.Ceiling((double)totalPharmacyCount / request.PageSize);
                response.Data = result;
                response.TotalEntityCount = totalPharmacyCount;
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
        [Route("getPharmacyById")]
        [RequiresPermission(Permissions.Pharmacy.View)]
        public ApiResponse<GetPharmacyByIdResponseDTO> GetPharmacyById([FromQuery] GetByIdRequestDTO request)
        {
            ApiResponse<GetPharmacyByIdResponseDTO> response = new ApiResponse<GetPharmacyByIdResponseDTO>();
            try
            {
                GetPharmacyByIdResponseDTO result = new GetPharmacyByIdResponseDTO();
                result = _IPharmaciesRepo.GetPharmacyById(request.Id);
                response.Data = result;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpPost]
        [Route("savePharmacy")]
        [RequiresPermission(Permissions.Pharmacy.Add, Permissions.Pharmacy.Edit)]
        public ApiResponse<bool> SavePharmacy([FromBody] SavePharmacyRequestDTO request)
        {
            ApiResponse<bool> response = new ApiResponse<bool>();
            try
            {
                var OrganizationId = Convert.ToInt64(User.FindFirst("OrganizationId").Value);
                var UserId = Convert.ToInt64(User.FindFirst("UserId").Value);
                string res = _IPharmaciesRepo.SavePharmacy(request, UserId, OrganizationId);
                if (res == "Pharmacy Created Successfully" || res == "Pharmacy Updated Successfully")
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
        [Route("deletePharmacy")]
        [RequiresPermission(Permissions.Pharmacy.Delete)]
        public ApiResponse<bool> DeletePharmacy([FromBody] GetByIdRequestDTO request)
        {
            ApiResponse<bool> response = new ApiResponse<bool>();
            bool res = _IPharmaciesRepo.DeletePharmacy(request.Id);
            response.Data = res;
            return response;
        }

        [HttpPost]
        [Route("updatePharmacyStatus")]
        [RequiresPermission(Permissions.Pharmacy.Edit)]
        public ApiResponse<bool> UpdatePharmacyStatus([FromBody] UpdatePharmacyStatusRequestDTO request)
        {
            ApiResponse<bool> response = new ApiResponse<bool>();
            bool res = _IPharmaciesRepo.UpdatePharmacyStatus(request);
            response.Data = res;
            return response;
        }
    }
}
