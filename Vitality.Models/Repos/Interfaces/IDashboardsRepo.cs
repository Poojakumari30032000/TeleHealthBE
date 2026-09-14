using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vitality.Models.DTOs.DashBooards;

namespace Vitality.Models.Repos.Interfaces
{
    public interface IDashboardsRepo
    {
        public Task<List<GetAllDashboardTilesResponseDTO>> GetAllDashboardTilesAsync(GetAllDashboardTilesRequestDTO request);
        public Task<(string[] Months, decimal[] RevenueData, decimal[] SubscriptionData)> GetRevenueAndNewSubscriptionsGrowthAsync();
        public Task<List<object>> GetPaymentStatusPieChartAsync(DateTime? startDate, DateTime? endDate);
        public Task<List<object>> GetClinicStatusPieChartAsync();
        public Task<List<object>> GetTreatmentTypePieChartAsync();
        public Task<(List<object> Series, List<string> Days)> GetEarningsAsync(long? facilityId, DateTime? startDate, DateTime? endDate);
        public Task<(List<object> Series, List<string> Months)> GetAppointmentsAsync(long? facilityId, DateTime? startDate, DateTime? endDate);

        public Task<List<object>> GetPendingPrescriptionsAsync(long? facilityId);

        public Task<(List<string> Months, List<decimal> RevenueData)> GetMonthlyRevenueByMonthsAsync(DateTime? startDate, DateTime? endDate);
        public Task<List<object>> GetMonthlyTreatmentCategoryPieChartAsync(DateTime? startDate, DateTime? endDate);
        public Task<int> GetActiveProvidersCountAsync();

        public Task<List<object>> GetMedicationHistoryAsync(long userId);
        public Task<List<object>> GetPaymentHistoryAsync(long userId);

        public Task<List<GetAllDashboardTilesResponseDTO>> GetProviderSummaryAsync(long userId, long roleId);
        public Task<List<object>> GetDailyAppointmentsAsync(long userId, DateTime date, int? clientTimezoneOffsetMinutes);
        public Task<int> GetTotalPatientsAsync(long userId, DateTime? startDate, DateTime? endDate);
        public Task<(List<string> Months, List<int> PatientCounts)> GetPatientCountsAsync(long userId, DateTime? startDate, DateTime? endDate);
        public Task<List<object>> GetTreatmentDistributionAsync(long userId);
    }
}
