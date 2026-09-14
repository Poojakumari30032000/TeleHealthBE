using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Vitality.Models.DTOs.Coupons;

namespace DudeMeds.Models.Repos.Interfaces
{
    public interface ICouponRepo
    {
        Task<CouponCodeResponseDTO> CreateCouponCode(CreateCouponCodeRequestDTO request);
        Task<CouponCodeResponseDTO> UpdateCouponCode(UpdateCouponCodeRequestDTO request);
        Task<bool> DeleteCouponCode(long couponCodeId);
        Task<CouponCodeResponseDTO> GetCouponCodeById(long id);
        Task<(List<GetCouponsByFacilityItemDTO> Coupons, int TotalCount)> GetCouponsByFacility(GetCouponsByFacilityRequestDTO request);
        Task<ApplyCouponResponseDTO> ApplyCouponToBundle(ApplyCouponRequestDTO request);
        Task<CouponValidationResultDTO> ValidateCoupon(string couponCode, long bundleId);

        Task<List<CouponCodeResponseDTO>> GetAllCouponsWithBundles();
        Task<bool> BundleHasActiveCoupons(long bundleId);
        Task<List<CouponCodeResponseDTO>> GetActiveCouponsForBundle(long bundleId);

        Task<CouponCodeResponseDTO> SetCouponActiveStatus(long couponCodeId, bool isActive);
        Task<CouponCodeResponseDTO> ToggleCouponActiveStatus(long couponCodeId);
        Task<ValidateCouponResponseDTO> ValidateCouponAsync(ValidateCouponRequestDTO request, CancellationToken ct = default);

        Task<(decimal ChargeAmount, bool CouponWasApplied)> ComputeRecurringChargeAmountAsync(long facilityId, long bundleId, decimal basePrice, long? recurringCouponCodeId, CancellationToken ct = default);
    }
}
