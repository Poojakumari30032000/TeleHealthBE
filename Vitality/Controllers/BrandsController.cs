using AutoMapper;
using DudeMeds.Models.DTOs.Common;
using DudeMeds.Models.Repos.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Vitality.Helper;
using Vitality.Models.DTOs.Brands;
using Vitality.Models.Repos.Interfaces;

namespace Vitality.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BrandsController : ControllerBase
    {
        public IConfiguration _configuration;
        private readonly IMapper _mapper;
        private readonly IBrandsRepo _IBrandsRepo;

        public BrandsController(
            IConfiguration config,
            IMapper IMapper,
            IBrandsRepo IBrandsRepo)
        {
            _configuration = config;
            _mapper = IMapper;
            _IBrandsRepo = IBrandsRepo;
        }

        [HttpGet]
        [Route("getBrandById")]
        [AllowAnonymous]
        public ApiResponse<GetBrandByIdResponseDTO> GetBrandById([FromQuery] string? FacilityGuid)
        {
            ApiResponse<GetBrandByIdResponseDTO> response = new ApiResponse<GetBrandByIdResponseDTO>();
            try
            {
                GetBrandByIdResponseDTO result = new GetBrandByIdResponseDTO();
                result = _IBrandsRepo.GetBrandById(FacilityGuid);
                response.Data = result;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpPost]
        [Route("saveBrand")]
        public ApiResponse<bool> SaveBrand([FromBody] SaveBrandRequestDTO request)
        {
            ApiResponse<bool> response = new ApiResponse<bool>();
            try
            {
                var UserId = Convert.ToInt64(User.FindFirst("UserId").Value);
                bool res = _IBrandsRepo.SaveBrand(request, UserId);
                response.Data = res;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }
    }
}
