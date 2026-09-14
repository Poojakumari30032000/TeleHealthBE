using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Vitality.Models.EntityClasses;
using Vitality.Models.Enums;

namespace Vitality.Models.DTOs.Coupons
{
    public class CreateCouponCodeRequestDTO
    {
        [Required]
        public long FacilityId { get; set; }

        [Required, StringLength(50)]
        public string CouponCode { get; set; }

        [Required]
        public decimal Discount { get; set; }

        [Required]
        public CouponDiscountType DiscountType { get; set; }

        public DateTime? ExpiryDate { get; set; }

        [Required]
        public long CreatedBy { get; set; }

        [Required]
        public List<long> BundleIds { get; set; } = new();

        public bool AppliesToRecurring { get; set; } = true;
    }

    public class UpdateCouponCodeRequestDTO
    {
        public long CoupanCodeId { get; set; }
        public string? CouponCode { get; set; }
        public decimal? Discount { get; set; }
        public CouponDiscountType? DiscountType { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public long? ModifiedBy { get; set; }
        public List<long>? BundleIds { get; set; }

        public bool? AppliesToRecurring { get; set; }
    }

    public class ApplyCouponRequestDTO
    {
        [Required]
        public string CouponCode { get; set; }

        [Required]
        public long BundleId { get; set; }
    }

    public class GetCouponsByFacilityRequestDTO
    {
        public long FacilityId { get; set; }
        public int? ClientTimezoneOffsetMinutes { get; set; }

        public string? Status { get; set; }

        public bool? IsActive { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 100;
        public string SortBy { get; set; } = "CreatedDate";
        public string SortOrder { get; set; } = "desc";
        public string? SearchTerm { get; set; }
    }

    public class GetCouponsByFacilityItemDTO
    {
        public long CoupanCodeId { get; set; }
        public long? FacilityId { get; set; }
        public string? CouponCode { get; set; }

        public decimal Discount { get; set; }
        public CouponDiscountType DiscountType { get; set; }

        public string IsActive { get; set; } = "false";
        public bool IsExpired { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public bool AppliesToRecurring { get; set; } = true;
        public List<long> BundleIds { get; set; } = new();
        public List<BundleInfoDTO> Bundles { get; set; } = new();
        public string? FacilityName { get; set; }
    }

    public class CouponCodeResponseDTO
    {
        public long CoupanCodeId { get; set; }
        public long? FacilityId { get; set; }
        public string? CouponCode { get; set; }

        public decimal Discount { get; set; }
        public CouponDiscountType DiscountType { get; set; }

        public bool? IsActive { get; set; }
        public bool IsExpired { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public bool AppliesToRecurring { get; set; } = true;
        public List<long> BundleIds { get; set; } = new();
        public List<BundleInfoDTO> Bundles { get; set; } = new();
        public string? FacilityName { get; set; }
    }

    public class BundleInfoDTO
    {
        public long BundleId { get; set; }
        public string? Name { get; set; }
        public decimal? OriginalPrice { get; set; }
        public decimal? ComparePrice { get; set; }
        public string? RegularImageURL { get; set; }
    }

    public class ApplyCouponResponseDTO
    {
        public long BundleId { get; set; }
        public string? BundleName { get; set; }
        public decimal OriginalPrice { get; set; }
        public decimal DiscountedPrice { get; set; }
        public decimal DiscountAmount { get; set; }

        public decimal Discount { get; set; }
        public CouponDiscountType DiscountType { get; set; }

        public bool IsValid { get; set; }
        public string? Message { get; set; }
    }

    public class CouponValidationResultDTO
    {
        public bool IsValid { get; set; }
        public string? Message { get; set; }
        public SYS_CouponCode? Coupon { get; set; }
    }

    public class ValidateCouponRequestDTO
    {
        public long FacilityId { get; set; }
        public string CouponCode { get; set; } = string.Empty;
        public long BundleId { get; set; }
        public decimal BundlePrice { get; set; }
        public long? PatientId { get; set; }
    }

    public class ValidateCouponResponseDTO
    {
        public bool IsValid { get; set; }
        public string Message { get; set; } = string.Empty;

        public long? CouponCodeId { get; set; }

        public bool AppliesToRecurring { get; set; } = true;

        public string? CouponCode { get; set; }

        public decimal? Discount { get; set; }
        public CouponDiscountType? DiscountType { get; set; }
        public decimal? DiscountAmount { get; set; }

        public decimal? OriginalPrice { get; set; }
        public decimal? DiscountedPrice { get; set; }
    }
}
