using AutoMapper;
using DudeMeds.Models.Repos.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Vitality.Helper;
using Vitality.Models.DTOs.DashBooards;
using Vitality.Models.Repos.Interfaces;
using Vitality.Filters;
using Vitality.Models.Security;

namespace Vitality.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [RequiresPermission(Permissions.Dashboard.Admin, Permissions.Dashboard.Clinic, Permissions.Dashboard.Provider, Permissions.Dashboard.Customer, Permissions.Dashboard.Patient)]
    public class DashboardsController : ControllerBase
    {
        public IConfiguration _configuration;
        private readonly IMapper _mapper;
        private readonly IDashboardsRepo _IDashboardsRepo;

        public DashboardsController(
            IConfiguration config,
            IMapper IMapper,
            IDashboardsRepo IDashboardsRepo)
        {
            _configuration = config;
            _mapper = IMapper;
            _IDashboardsRepo = IDashboardsRepo;
        }

        [HttpGet]
        [Route("getDashBoardTiles")]
        public async Task<ApiResponse<List<GetAllDashboardTilesResponseDTO>>> GetGlobalAdminTilesAsync([FromQuery] GetAllDashboardTilesRequestDTO request)
        {
            ApiResponse<List<GetAllDashboardTilesResponseDTO>> response = new ApiResponse<List<GetAllDashboardTilesResponseDTO>>();
            try
            {
                List<GetAllDashboardTilesResponseDTO> result = new List<GetAllDashboardTilesResponseDTO>();
                result = await _IDashboardsRepo.GetAllDashboardTilesAsync(request);
                response.Data = result;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpGet]
        [Route("getRevenueAndNewSubscriptionsGrowth")]
        public async Task<ApiResponse<object>> GetRevenueAndNewSubscriptionsGrowthAsync()
        {
            var response = new ApiResponse<object>();
            var (months, revenueData, subscriptionData) = await _IDashboardsRepo.GetRevenueAndNewSubscriptionsGrowthAsync();

            var series = new[]
                    {
                new { name = "Revenue", data = revenueData.Select(d => (object)d).ToArray() },
                new { name = "New Subscriptions", data = subscriptionData.Select(d => (object)d).ToArray() }
            };

            response.Data = new { Series = series, Months = months };
            return response;
        }

        [HttpGet]
        [Route("getPaymentStatusPieChart")]
        public async Task<ApiResponse<object>> GetPaymentStatusPieChartAsync([FromQuery] DateTime? StartDate, [FromQuery] DateTime? EndDate)
        {
            var response = new ApiResponse<object>();
            try
            {
                var data = await _IDashboardsRepo.GetPaymentStatusPieChartAsync(StartDate, EndDate);
                response.Data = new { data };
                return response;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
                return response;
            }
        }

        [HttpGet]
        [Route("getClinicStatusPieChart")]
        public async Task<ApiResponse<object>> GetClinicStatusPieChartAsync()
        {
            var response = new ApiResponse<object>();
            try
            {
                var data = await _IDashboardsRepo.GetClinicStatusPieChartAsync();
                response.Data = new { data };
                return response;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
                return response;
            }
        }

        [HttpGet]
        [Route("getTreatmentTypePieChart")]
        public async Task<ApiResponse<object>> GetTreatmentTypePieChartAsync()
        {
            var response = new ApiResponse<object>();
            try
            {
                var data = await _IDashboardsRepo.GetTreatmentTypePieChartAsync();
                response.Data = new { data };
                return response;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
                return response;
            }
        }

        [HttpGet]
        [Route("getEarnings")]
        public async Task<ApiResponse<object>> GetEarningsAsync([FromQuery] GetAllDashboardTilesRequestDTO request, [FromQuery] DateTime? StartDate, [FromQuery] DateTime? EndDate)
        {
            var response = new ApiResponse<object>();
            try
            {
                var (series, days) = await _IDashboardsRepo.GetEarningsAsync(request.FacilityId, StartDate, EndDate);
                response.Data = new
                {
                    series = series,
                    days = days
                };
                response.TotalEntityCount = 0;
                response.TotalPages = 0;
                return response;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
                return response;
            }
        }

        [HttpGet]
        [Route("getAppointments")]
        public async Task<ApiResponse<object>> GetAppointmentsAsync([FromQuery] GetAllDashboardTilesRequestDTO request, [FromQuery] DateTime? StartDate, [FromQuery] DateTime? EndDate)
        {
            var response = new ApiResponse<object>();
            try
            {
                var (series, months) = await _IDashboardsRepo.GetAppointmentsAsync(request.FacilityId, StartDate, EndDate);
                response.Data = new
                {
                    series = series,
                    months = months
                };
                response.TotalEntityCount = 0;
                response.TotalPages = 0;
                return response;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
                return response;
            }
        }

        [HttpGet]
        [Route("getPendingPrescriptions")]
        public async Task<ApiResponse<object>> GetPendingPrescriptionsAsync([FromQuery] GetAllDashboardTilesRequestDTO request)
        {
            var response = new ApiResponse<object>();
            try
            {
                var data = await _IDashboardsRepo.GetPendingPrescriptionsAsync(request.FacilityId);
                response.Data = data;
                return response;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
                return response;
            }
        }

        [HttpGet]
        [Route("getMonthlyRevenueByMonths")]
        public async Task<ApiResponse<object>> GetMonthlyRevenueByMonthsAsync([FromQuery] DateTime? StartDate, [FromQuery] DateTime? EndDate)
        {
            var response = new ApiResponse<object>();
            try
            {
                var (months, revenueData) = await _IDashboardsRepo.GetMonthlyRevenueByMonthsAsync(StartDate, EndDate);
                response.Data = new
                {
                    months = months,
                    series = new[]
                    {
                        new { name = "Revenue", data = revenueData.Select(d => (object)d).ToArray() }
                    }
                };
                return response;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
                return response;
            }
        }

        [HttpGet]
        [Route("getMonthlyTreatmentCategoryPieChart")]
        public async Task<ApiResponse<object>> GetMonthlyTreatmentCategoryPieChartAsync([FromQuery] DateTime? StartDate, [FromQuery] DateTime? EndDate)
        {
            var response = new ApiResponse<object>();
            try
            {
                var data = await _IDashboardsRepo.GetMonthlyTreatmentCategoryPieChartAsync(StartDate, EndDate);
                response.Data = new { data };
                return response;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
                return response;
            }
        }

        [HttpGet]
        [Route("getActiveProvidersCount")]
        public async Task<ApiResponse<object>> GetActiveProvidersCountAsync()
        {
            var response = new ApiResponse<object>();
            try
            {
                var count = await _IDashboardsRepo.GetActiveProvidersCountAsync();
                response.Data = new { count = count };
                return response;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
                return response;
            }
        }

        [HttpGet]
        [Route("getMedicationHistory")]
        public async Task<ApiResponse<object>> GetMedicationHistoryAsync([FromQuery] GetAllDashboardTilesRequestDTO request)
        {
            var response = new ApiResponse<object>();
            try
            {
                if (!request.UserId.HasValue)
                {
                    response.Message = "UserId is required";
                    response.Status = 0;
                    return response;
                }
                var data = await _IDashboardsRepo.GetMedicationHistoryAsync(request.UserId.Value);
                response.Data = data;
                return response;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
                return response;
            }
        }

        [HttpGet]
        [Route("getPaymentHistory")]
        public async Task<ApiResponse<object>> GetPaymentHistoryAsync([FromQuery] GetAllDashboardTilesRequestDTO request)
        {
            var response = new ApiResponse<object>();
            try
            {
                if (!request.UserId.HasValue)
                {
                    response.Message = "UserId is required";
                    response.Status = 0;
                    return response;
                }
                var data = await _IDashboardsRepo.GetPaymentHistoryAsync(request.UserId.Value);
                response.Data = data;
                return response;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
                return response;
            }
        }

        [HttpGet]
        [Route("getProviderSummary")]
        public async Task<ApiResponse<List<GetAllDashboardTilesResponseDTO>>> GetProviderSummaryAsync([FromQuery] GetAllDashboardTilesRequestDTO request)
        {
            var response = new ApiResponse<List<GetAllDashboardTilesResponseDTO>>();
            try
            {
                if (!request.UserId.HasValue || !request.RoleId.HasValue)
                {
                    response.Message = "UserId and RoleId are required";
                    response.Status = 0;
                    return response;
                }
                var data = await _IDashboardsRepo.GetProviderSummaryAsync(request.UserId.Value, request.RoleId.Value);
                response.Data = data;
                return response;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
                return response;
            }
        }

        [HttpGet]
        [Route("getDailyAppointments")]
        public async Task<ApiResponse<object>> GetDailyAppointmentsAsync([FromQuery] GetAllDashboardTilesRequestDTO request, [FromQuery] DateTime? Date)
        {
            var response = new ApiResponse<object>();
            try
            {
                if (!request.UserId.HasValue)
                {
                    response.Message = "UserId is required";
                    response.Status = 0;
                    return response;
                }
                var date = Date ?? DateTime.UtcNow.Date;
                var data = await _IDashboardsRepo.GetDailyAppointmentsAsync(
                    request.UserId.Value,
                    date,
                    request.ClientTimezoneOffsetMinutes);
                response.Data = data;
                return response;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
                return response;
            }
        }

        [HttpGet]
        [Route("getTotalPatients")]
        public async Task<ApiResponse<object>> GetTotalPatientsAsync([FromQuery] GetAllDashboardTilesRequestDTO request, [FromQuery] DateTime? StartDate, [FromQuery] DateTime? EndDate)
        {
            var response = new ApiResponse<object>();
            try
            {
                if (!request.UserId.HasValue)
                {
                    response.Message = "UserId is required";
                    response.Status = 0;
                    return response;
                }
                var count = await _IDashboardsRepo.GetTotalPatientsAsync(request.UserId.Value, StartDate, EndDate);
                response.Data = new { count = count };
                return response;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
                return response;
            }
        }

        [HttpGet]
        [Route("getPatientCounts")]
        public async Task<ApiResponse<object>> GetPatientCountsAsync([FromQuery] GetAllDashboardTilesRequestDTO request, [FromQuery] DateTime? StartDate, [FromQuery] DateTime? EndDate)
        {
            var response = new ApiResponse<object>();
            try
            {
                if (!request.UserId.HasValue)
                {
                    response.Message = "UserId is required";
                    response.Status = 0;
                    return response;
                }
                var (months, patientCounts) = await _IDashboardsRepo.GetPatientCountsAsync(request.UserId.Value, StartDate, EndDate);
                response.Data = new
                {
                    months = months,
                    visits = patientCounts
                };
                return response;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
                return response;
            }
        }

        [HttpGet]
        [Route("getTreatmentDistribution")]
        public async Task<ApiResponse<object>> GetTreatmentDistributionAsync([FromQuery] GetAllDashboardTilesRequestDTO request)
        {
            var response = new ApiResponse<object>();
            try
            {
                if (!request.UserId.HasValue)
                {
                    response.Message = "UserId is required";
                    response.Status = 0;
                    return response;
                }
                var data = await _IDashboardsRepo.GetTreatmentDistributionAsync(request.UserId.Value);
                response.Data = new { data };
                return response;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
                return response;
            }
        }
    }
}
