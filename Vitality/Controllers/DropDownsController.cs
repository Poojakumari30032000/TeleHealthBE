using AutoMapper;
using DudeMeds.Models.DTOs.Common;
using DudeMeds.Models.DTOs.DropDown;
using DudeMeds.Models.DTOs.Products;
using DudeMeds.Models.Repos.Interfaces;
using DudeMeds.Models.Repos.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Vitality.Filters;
using Vitality.Helper;
using Vitality.Models.DTOs.DropDown;
using Vitality.Models.Security;

namespace DudeMeds.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DropDownsController : ControllerBase
    {
        public IConfiguration _configuration;
        private readonly IMapper _mapper;

        private readonly IDropDownsRepo _IDropDownsRepo;

        public DropDownsController(
          IConfiguration config,
          IMapper IMapper,
          IDropDownsRepo IDropDownsRepo)
        {
            _configuration = config;
            _mapper = IMapper;
            _IDropDownsRepo = IDropDownsRepo;
        }
        [AllowAnonymous]
        [HttpGet]
        [Route("getAllCountry")]
        public ApiResponse<List<GetAllCountryResponseDTO>> GetAllCountry()
        {
            ApiResponse<List<GetAllCountryResponseDTO>> response = new ApiResponse<List<GetAllCountryResponseDTO>>();
            try
            {
                List<GetAllCountryResponseDTO> result = new List<GetAllCountryResponseDTO>();
                result = _IDropDownsRepo.GetAllCountry();
                response.Data = result;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }
        [AllowAnonymous]
        [HttpGet]
        [Route("getAllUSCities")]
        public async Task<ApiResponse<List<DropdownResponseDTO>>> GetAllUSCities()
        {

            ApiResponse<List<DropdownResponseDTO>> response = new ApiResponse<List<DropdownResponseDTO>>();
            List<DropdownResponseDTO> result = _IDropDownsRepo.GetUSCities();
            response.Data = result;

            return response;
        }
        [AllowAnonymous]
        [HttpGet]
        [Route("getUSStatesByCityId")]
        public ApiResponse<DropdownResponseDTO> GetUSStatesByCityId([FromQuery] GetByIdRequestDTO request)
        {
            ApiResponse<DropdownResponseDTO> response = new ApiResponse<DropdownResponseDTO>();
            DropdownResponseDTO result = _IDropDownsRepo.GetUSState(Convert.ToInt32(request.Id));
            response.Data = result;
            return response;
        }
        [AllowAnonymous]
        [HttpGet]
        [Route("GetAllStateOfUSA")]
        public ApiResponse<List<DropdownResponseDTO>> GetAllUSARegion()
        {
            ApiResponse<List<DropdownResponseDTO>> response = new ApiResponse<List<DropdownResponseDTO>>();
            try
            {
                List<DropdownResponseDTO> result = new List<DropdownResponseDTO>();
                result = _IDropDownsRepo.GetAllUSARegion();
                response.Data = result;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpGet]
        [Route("getAllRoles")]
        public ApiResponse<List<DropdownResponseDTO>> GetAllRoles()
        {
            ApiResponse<List<DropdownResponseDTO>> response = new ApiResponse<List<DropdownResponseDTO>>();
            response.Data = _IDropDownsRepo.GetAllRoles();
            return response;
        }

        [HttpGet]
        [Route("getAllRoleTitles")]
        public ApiResponse<List<DropdownResponseDTO>> GetAllRoleTitles()
        {
            var response = new ApiResponse<List<DropdownResponseDTO>>();
            response.Data = _IDropDownsRepo.GetAllRoleTitles();
            return response;
        }

        [HttpPost]
        [Route("saveRoleTitle")]
        public ApiResponse<int> SaveRoleTitle([FromBody] SaveRoleTitleRequestDTO request)
        {
            var response = new ApiResponse<int>();
            var id = _IDropDownsRepo.SaveRoleTitle(request);
            response.Data = id;
            response.Message = id switch
            {
                > 0 => "Role title saved successfully.",
                -2 => "Role title already exists.",
                _ => "Unable to save role title."
            };
            return response;
        }

        [HttpPost]
        [Route("deleteRoleTitle")]
        public ApiResponse<bool> DeleteRoleTitle([FromBody] GetByIdRequestDTO request)
        {
            var response = new ApiResponse<bool>();
            var deleted = _IDropDownsRepo.DeleteRoleTitle((int)request.Id);
            response.Data = deleted;
            response.Message = deleted ? "Role title deleted successfully." : "Role title cannot be deleted.";
            return response;
        }
        [AllowAnonymous]
        [HttpGet]
        [Route("GetCitiesByStateId")]
        public ApiResponse<List<DropdownResponseDTO>> GetCitiesByStateId([FromQuery] GetByIdRequestDTO request)
        {
            ApiResponse<List<DropdownResponseDTO>> response = new ApiResponse<List<DropdownResponseDTO>>();
            try
            {
                if (request.Id <= 0)
                {
                    response.Message = "Invalid state ID.";
                    return response;
                }

                List<DropdownResponseDTO> result = _IDropDownsRepo.GetCitiesByStateId(Convert.ToInt32(request.Id));
                response.Data = result;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpGet]
        [Route("getAllProviders")]
        public ApiResponse<List<GetAllProviderResponseDTO>> GetAllProviders([FromQuery] GetAllProviderRequestDTO request)
        {
            ApiResponse<List<GetAllProviderResponseDTO>> response = new ApiResponse<List<GetAllProviderResponseDTO>>();
            List<GetAllProviderResponseDTO> result = _IDropDownsRepo.GetAllDoctors(request);
            response.Data = result;
            return response;
        }

        [HttpGet]
        [Route("getAllUnAssignedProviders")]
        public ApiResponse<List<GetAllProviderResponseDTO>> GetAllUnAssignedProviders([FromQuery] GetAllUnAssignedProvidersRequestDTO request)
        {
            ApiResponse<List<GetAllProviderResponseDTO>> response = new ApiResponse<List<GetAllProviderResponseDTO>>();
            List<GetAllProviderResponseDTO> result = _IDropDownsRepo.GetAllUnAssignedProviders(request);
            response.Data = result;
            return response;
        }

        [HttpGet]
        [Route("getProviderScheduledSlots")]
        public ApiResponse<List<GetProviderScheduledSlotsResponseDTO>> GetProviderScheduledSlots([FromQuery] GetProviderScheduledSlotsRequestDTO request)
        {
            ApiResponse<List<GetProviderScheduledSlotsResponseDTO>> response = new ApiResponse<List<GetProviderScheduledSlotsResponseDTO>>();
            try
            {
                List<GetProviderScheduledSlotsResponseDTO> result = new List<GetProviderScheduledSlotsResponseDTO>();
                result = _IDropDownsRepo.GetProviderScheduledSlots(request);
                response.Data = result;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpGet]
        [Route("getAllFacilities")]
        public ApiResponse<List<GetAllFacilitiesDropDownResponseDTO>> GetAllFacilities([FromQuery] GetByIdRequestDTO request)
        {
            ApiResponse<List<GetAllFacilitiesDropDownResponseDTO>> response = new ApiResponse<List<GetAllFacilitiesDropDownResponseDTO>>();
            List<GetAllFacilitiesDropDownResponseDTO> result = _IDropDownsRepo.GetAllFacilities(request.Id);
            response.Data = result;
            return response;
        }

        [HttpGet]
        [Route("GetAllFacilitiesbyProviderId")]
        public ApiResponse<List<GetAllFacilitiesDropDownResponseDTO>> GetAllFacilitiesbyProviderId([FromQuery] GetByIdRequestDTO request)
        {
            ApiResponse<List<GetAllFacilitiesDropDownResponseDTO>> response = new ApiResponse<List<GetAllFacilitiesDropDownResponseDTO>>();
            List<GetAllFacilitiesDropDownResponseDTO> result = _IDropDownsRepo.GetAllFacilitiesByProviderId(request.Id);
            response.Data = result;
            return response;
        }

        [HttpGet]
        [Route("getActiveFacilitiesByCategoryId")]
        public ApiResponse<List<GetAllFacilitiesDropDownResponseDTO>> GetActiveFacilitiesByCategoryId([FromQuery] long categoryId)
        {
            ApiResponse<List<GetAllFacilitiesDropDownResponseDTO>> response = new ApiResponse<List<GetAllFacilitiesDropDownResponseDTO>>();
            List<GetAllFacilitiesDropDownResponseDTO> result = _IDropDownsRepo.GetActiveFacilitiesByCategoryId(categoryId);
            response.Data = result;
            return response;
        }

        [HttpGet]
        [Route("getAllCategories")]
        public ApiResponse<List<GetAllCategoriesDropDownResponseDTO>> GetAllCategories([FromQuery] long? FacilityId = null)
        {
            ApiResponse<List<GetAllCategoriesDropDownResponseDTO>> response = new ApiResponse<List<GetAllCategoriesDropDownResponseDTO>>();
            List<GetAllCategoriesDropDownResponseDTO> result = _IDropDownsRepo.GetAllCategories(FacilityId);
            response.Data = result;
            return response;
        }

        [HttpGet]
        [Route("getAllConditions")]
        public ApiResponse<List<GetAllConditionsDropDownResponseDTO>> GetAllConditions([FromQuery] GetByIdRequestDTO request)
        {
            ApiResponse<List<GetAllConditionsDropDownResponseDTO>> response = new ApiResponse<List<GetAllConditionsDropDownResponseDTO>>();
            List<GetAllConditionsDropDownResponseDTO> result = _IDropDownsRepo.GetAllConditions(request.Id);
            response.Data = result;
            return response;
        }

        [HttpGet]
        [Route("getAllProducts")]
        public ApiResponse<List<GetAllProductsDropDownResponseDTO>> GetAllProducts([FromQuery] GetAllProductsDropDownRequestDTO request)
        {
            ApiResponse<List<GetAllProductsDropDownResponseDTO>> response = new ApiResponse<List<GetAllProductsDropDownResponseDTO>>();
            List<GetAllProductsDropDownResponseDTO> result = _IDropDownsRepo.GetAllProducts(request);
            response.Data = result;
            return response;
        }
        [HttpGet]
        [Route("GetAllDrugsWithData")]
        public ApiResponse<List<GetAllDrugsDropDownResponseDTO>> GetAllDrugsWithData([FromQuery] GetAllProductsDropDownRequestDTO request)
        {
            ApiResponse<List<GetAllDrugsDropDownResponseDTO>> response = new ApiResponse<List<GetAllDrugsDropDownResponseDTO>>();
            List<GetAllDrugsDropDownResponseDTO> result = _IDropDownsRepo.GetAllDrugs(request);
            response.Data = result;
            return response;
        }

        [HttpGet]
        [Route("GetAllCustomDrugsWithData")]
        public ApiResponse<List<GetAllDrugsDropDownResponseDTO>> GetAllCustomDrugsWithData([FromQuery] GetAllProductsDropDownRequestDTO request)
        {
            ApiResponse<List<GetAllDrugsDropDownResponseDTO>> response = new ApiResponse<List<GetAllDrugsDropDownResponseDTO>>();

            List<GetAllDrugsDropDownResponseDTO> result = _IDropDownsRepo.GetAllDrugs(request);
            response.Data = result;
            return response;
        }
        [HttpGet]
        [Route("GetAllDrugsSupplies")]
        public ApiResponse<List<GetAllDrugsDropDownResponseDTO>> GetAllDrugsSupplies([FromQuery] GetAllProductsDropDownRequestDTO request)
        {
            ApiResponse<List<GetAllDrugsDropDownResponseDTO>> response = new ApiResponse<List<GetAllDrugsDropDownResponseDTO>>();
            List<GetAllDrugsDropDownResponseDTO> result = _IDropDownsRepo.GetAllDrugsSupplies(request);
            response.Data = result;
            return response;
        }

        [HttpGet]
        [Route("GetAllCustomDrugsSupplies")]
        public ApiResponse<List<GetAllDrugsDropDownResponseDTO>> GetAllCustomDrugsSupplies([FromQuery] GetAllProductsDropDownRequestDTO request)
        {
            ApiResponse<List<GetAllDrugsDropDownResponseDTO>> response = new ApiResponse<List<GetAllDrugsDropDownResponseDTO>>();
            List<GetAllDrugsDropDownResponseDTO> result = _IDropDownsRepo.GetAllCustomDrugsSupplies(request);
            response.Data = result;
            return response;
        }

        [HttpGet]
        [Route("GetAllDrugsAndSupplies")]
        public async Task<ApiResponse<List<GetAllDrugsDropDownResponseDTO>>> GetAllDrugsAndSupplies([FromQuery] GetAllProductsDropDownRequestDTO request, CancellationToken ct)
        {
            ApiResponse<List<GetAllDrugsDropDownResponseDTO>> response = new ApiResponse<List<GetAllDrugsDropDownResponseDTO>>();
            List<GetAllDrugsDropDownResponseDTO> result = await _IDropDownsRepo.GetAllDrugsAndSuppliesAsync(request, ct);
            response.Data = result;
            return response;
        }

        [HttpGet]
        [Route("GetAllCustomDrugsAndSupplies")]
        public async Task<ApiResponse<List<GetAllDrugsDropDownResponseDTO>>> GetAllCustomDrugsAndSupplies([FromQuery] GetAllProductsDropDownRequestDTO request, CancellationToken ct)
        {
            ApiResponse<List<GetAllDrugsDropDownResponseDTO>> response = new ApiResponse<List<GetAllDrugsDropDownResponseDTO>>();
            List<GetAllDrugsDropDownResponseDTO> result = await _IDropDownsRepo.GetAllCustomDrugsAndSuppliesAsync(request, ct);
            response.Data = result;
            return response;
        }

        [HttpGet]
        [Route("getAllQuestionnaireProducts")]
        public ApiResponse<List<GetAllProductsDropDownResponseDTO>> GetAllQuestionnaireProducts([FromQuery] GetAllQuestionnaireProductsDropDownRequestDTO request)
        {
            ApiResponse<List<GetAllProductsDropDownResponseDTO>> response = new ApiResponse<List<GetAllProductsDropDownResponseDTO>>();
            List<GetAllProductsDropDownResponseDTO> result = _IDropDownsRepo.GetAllQuestionnaireProducts(request);
            response.Data = result;
            return response;
        }

        [HttpGet]
        [Route("getAllInTakeFormProducts")]
        public ApiResponse<List<GetAllInTakeFormProductsDropDownResponseDTO>> GetAllInTakeFormProducts([FromQuery] GetAllInTakeFormProductsDropDownRequestDTO request)
        {
            ApiResponse<List<GetAllInTakeFormProductsDropDownResponseDTO>> response = new ApiResponse<List<GetAllInTakeFormProductsDropDownResponseDTO>>();
            List<GetAllInTakeFormProductsDropDownResponseDTO> result = _IDropDownsRepo.GetAllInTakeFormProducts(request);
            response.Data = result;
            return response;
        }

        [HttpGet]
        [Route("getAllPharmacies")]
        public ApiResponse<List<GetAllPharmaciesDropDownResponseDTO>> GetAllPharmacies()
        {
            ApiResponse<List<GetAllPharmaciesDropDownResponseDTO>> response = new ApiResponse<List<GetAllPharmaciesDropDownResponseDTO>>();
            List<GetAllPharmaciesDropDownResponseDTO> result = _IDropDownsRepo.GetAllPharmacies();
            response.Data = result;
            return response;
        }

        [HttpGet]
        [Route("getAllProviderGroups")]
        public ApiResponse<List<GetAllProviderGroupsDropDownResponseDTO>> GetAllProviderGroups()
        {
            ApiResponse<List<GetAllProviderGroupsDropDownResponseDTO>> response = new ApiResponse<List<GetAllProviderGroupsDropDownResponseDTO>>();
            List<GetAllProviderGroupsDropDownResponseDTO> result = _IDropDownsRepo.GetAllProviderGroups();
            response.Data = result;
            return response;
        }

        [HttpGet]
        [Route("getAllPatients")]
        [RequiresPermission(Permissions.Patient.View)]
        public async Task<ApiResponse<List<GetAllPatientsDropDownResponseDTO>>> GetAllPatients([FromQuery] GetAllPatientsDropDownRequestDTO request)
        {
            ApiResponse<List<GetAllPatientsDropDownResponseDTO>> response = new ApiResponse<List<GetAllPatientsDropDownResponseDTO>>();
            List<GetAllPatientsDropDownResponseDTO> result = await _IDropDownsRepo.GetAllPatientsAsync(request.FacilityId,request.ProviderId);
            response.Data = result;
            return response;
        }

        [HttpGet]
        [Route("getAllPatientTreatments")]
        public ApiResponse<List<GetAllPatientTreatmentsDropDownResponseDTO>> GetAllPatientTreatments([FromQuery] GetAllPatientTreatmentsDropDownRequestDTO request)
        {
            ApiResponse<List<GetAllPatientTreatmentsDropDownResponseDTO>> response = new ApiResponse<List<GetAllPatientTreatmentsDropDownResponseDTO>>();
            List<GetAllPatientTreatmentsDropDownResponseDTO> result = _IDropDownsRepo.GetAllPatientTreatments(request.PatientId);
            response.Data = result;
            return response;
        }

        [HttpGet]
        [Route("getAllPatientOrders")]
        public ApiResponse<List<GetAllPatientOrdersDropDownResponseDTO>> GetAllPatientOrders([FromQuery] GetAllPatientOrdersDropDownRequestDTO request)
        {
            ApiResponse<List<GetAllPatientOrdersDropDownResponseDTO>> response = new ApiResponse<List<GetAllPatientOrdersDropDownResponseDTO>>();
            List<GetAllPatientOrdersDropDownResponseDTO> result = _IDropDownsRepo.GetAllPatientOrders(request.PatientTreatmentId);
            response.Data = result;
            return response;
        }

        [HttpGet]
        [Route("getAllSubscriptions")]
        public ApiResponse<List<GetAllSubscriptionsDropDownResponseDTO>> GetAllSubscriptions()
        {
            ApiResponse<List<GetAllSubscriptionsDropDownResponseDTO>> response = new ApiResponse<List<GetAllSubscriptionsDropDownResponseDTO>>();
            List<GetAllSubscriptionsDropDownResponseDTO> result = _IDropDownsRepo.GetAllSubscriptions();
            response.Data = result;
            return response;
        }

        [HttpGet]
        [Route("GetAllBundlesNew")]
        public async Task<ApiResponse<List<GetBundleByIdResponse2DTO>>> GetAllBundlesNew([FromQuery] long facilityId, CancellationToken ct)
        {
            var response = new ApiResponse<List<GetBundleByIdResponse2DTO>>();
            try
            {
                response.Data = await _IDropDownsRepo.GetAllBundlesAsync(facilityId, ct);
                if (response.Data == null || response.Data.Count == 0)
                    response.Message = "No bundles found.";
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpGet]
        [Route("getProviderScheduledSlotsByProvider")]
        public ApiResponse<List<GetProviderScheduledSlotsResponseDTO>> GetProviderScheduledSlotsByProvider(
    [FromQuery] GetProviderScheduledSlotsByProviderRequestDTO request)
        {
            var response = new ApiResponse<List<GetProviderScheduledSlotsResponseDTO>>();

            try
            {
                var result = _IDropDownsRepo.GetProviderScheduledSlotsByProvider(request);
                response.Data = result;
                response.Success = true;
                response.Message = "OK";
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = ex.Message;
                response.Data = new List<GetProviderScheduledSlotsResponseDTO>();
            }

            return response;
        }

        [HttpGet]
        [Route("getAllCatalogsDropDown")]
        public ApiResponse<List<CatalogResponseDTO>> GetAllCatalogs([FromQuery] GetAllCatalogsRequestDTO request)
        {
            var response = new ApiResponse<List<CatalogResponseDTO>>();
            try
            {
                response.Data = _IDropDownsRepo.GetAllCatalogs(request);
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

    }
}
