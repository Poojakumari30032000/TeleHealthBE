using AutoMapper;
using DudeMeds.Models.DTOs.Categories;
using DudeMeds.Models.DTOs.Common;
using DudeMeds.Models.Repos.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Vitality.Helper;
using Vitality.Models.DTOs.ProductCategories;
using Vitality.Filters;
using Vitality.Models.Security;

namespace DudeMeds.Controllers
{
    [Route("api/[controller]")]
    [ApiController]

    public class ProductCategoriesController : ControllerBase
    {
        public IConfiguration _configuration;
        private readonly IMapper _mapper;

        private readonly IProductCategoriesRepo _ICategoriesRepo;

        public ProductCategoriesController(
            IConfiguration config,
            IMapper IMapper,
            IProductCategoriesRepo ICategoriesRepo)
        {
            _configuration = config;
            _mapper = IMapper;
            _ICategoriesRepo = ICategoriesRepo;

        }

        [HttpGet]
        [Route("getAllCategories")]
        [RequiresPermission(Permissions.ProductCategory.View)]
        public ApiResponse<List<GetAllCategoriesResponseDTO>> GetAllCategories([FromQuery] GetAllCategoriesRequestDTO request)
        {
            ApiResponse<List<GetAllCategoriesResponseDTO>> response = new ApiResponse<List<GetAllCategoriesResponseDTO>>();
            try
            {
                List<GetAllCategoriesResponseDTO> result = new List<GetAllCategoriesResponseDTO>();
                result = _ICategoriesRepo.GetAllCategories(request, out int totalCategoryCount);
                int totalPages = (int)Math.Ceiling((double)totalCategoryCount / request.PageSize);
                response.TotalEntityCount = totalCategoryCount;
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
        [Route("getCategoryById")]
        [RequiresPermission(Permissions.ProductCategory.View)]
        public ApiResponse<GetCategoryByIdResponseDTO> GetCategoryById([FromQuery] GetByIdRequestDTO request)
        {
            ApiResponse<GetCategoryByIdResponseDTO> response = new ApiResponse<GetCategoryByIdResponseDTO>();
            try
            {
                GetCategoryByIdResponseDTO result = new GetCategoryByIdResponseDTO();
                result = _ICategoriesRepo.GetCategoryById(request.Id);
                response.Data = result;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpPost]
        [Route("saveCategory")]
        [RequiresPermission(Permissions.ProductCategory.Add, Permissions.ProductCategory.Edit)]
        public ApiResponse<bool> SaveCategory([FromBody] SaveCategoryRequestDTO request)
        {
            ApiResponse<bool> response = new ApiResponse<bool>();
            try
            {
                var UserId = Convert.ToInt64(User.FindFirst("UserId").Value);
                string res = _ICategoriesRepo.SaveCategory(request, UserId);
                if (res == "Category Created Successfully" || res == "Category Updated Successfully")
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
        [Route("deleteCategory")]
        [RequiresPermission(Permissions.ProductCategory.Delete)]
        public ApiResponse<bool> DeleteCategory([FromBody] GetByIdRequestDTO request)
        {
            ApiResponse<bool> response = new ApiResponse<bool>();
            bool res = _ICategoriesRepo.DeleteCategory(request.Id);
            response.Data = res;
            return response;
        }

        [HttpGet]
        [Route("getCategoriesWithDrugs")]
        public ApiResponse<List<GetAllCategoriesWithBundlesResponseDTO>> GetCategoriesWithDrugs()
        {
            ApiResponse<List<GetAllCategoriesWithBundlesResponseDTO>> response = new ApiResponse<List<GetAllCategoriesWithBundlesResponseDTO>>();
            try
            {
                var result = _ICategoriesRepo.GetCategoriesWithBundles();
                response.Data = result;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }
    }
}
