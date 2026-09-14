using AutoMapper;
using DudeMeds.Models.Repos.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Vitality.Models.DTOs.Coupons;
using Vitality.Models.EntityClasses;
using Vitality.Models.Enums;
using Vitality.Models.Repos.Interfaces;
using Vitality.Models.CommonMethods;

namespace Vitality.Models.Repos
{
    public class CouponRepo : BaseRepo, ICouponRepo
    {
        private readonly IMapper _mapper;

        public CouponRepo(IMapper mapper)
        {
            _mapper = mapper;
        }

        public async Task<CouponCodeResponseDTO> CreateCouponCode(CreateCouponCodeRequestDTO request)
        {
            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                var duplicateExists = await _db.SYS_CouponCodes
                    .AsNoTracking()
                    .AnyAsync(cc => cc.FacilityId == request.FacilityId && cc.CoupanCode == request.CouponCode);

                if (duplicateExists)
                    throw new Exception("Coupon code already exists for this facility.");

                if (request.DiscountType == CouponDiscountType.Percentage &&
                    (request.Discount < 0m || request.Discount > 100m))
                    throw new Exception("Percentage discount must be between 0 and 100.");

                var couponCode = new SYS_CouponCode
                {
                    FacilityId = request.FacilityId,
                    CoupanCode = request.CouponCode,
                    Discount = Math.Round(request.Discount, 2, MidpointRounding.AwayFromZero),
                    DiscountType = (byte)request.DiscountType,
                    ExpiryDate = request.ExpiryDate,
                    AppliesToRecurring = request.AppliesToRecurring,
                    IsActive = true,
                    CreatedBy = request.CreatedBy,
                    CreatedDate = DateTime.UtcNow
                };

                if (request.BundleIds != null && request.BundleIds.Any())
                {
                    foreach (var bundleId in request.BundleIds)
                    {
                        couponCode.PD_CouponCodeBundles.Add(new PD_CouponCodeBundle
                        {
                            BundleId = bundleId,
                            IsActive = true,
                            CreatedBy = request.CreatedBy,
                            CreatedDate = DateTime.UtcNow
                        });
                    }
                }

                _db.SYS_CouponCodes.Add(couponCode);

                await _db.SaveChangesAsync();

                await transaction.CommitAsync();
                return await GetCouponCodeById(couponCode.CoupanCodeId);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<CouponCodeResponseDTO> UpdateCouponCode(UpdateCouponCodeRequestDTO request)
        {
            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                var couponCode = await (from cc in _db.SYS_CouponCodes
                                        where cc.CoupanCodeId == request.CoupanCodeId
                                        select cc).FirstOrDefaultAsync();

                if (couponCode == null)
                    throw new Exception("Coupon code not found.");

                if (!string.IsNullOrEmpty(request.CouponCode))
                    couponCode.CoupanCode = request.CouponCode;

                if (request.Discount.HasValue)
                    couponCode.Discount = Math.Round(request.Discount.Value, 2, MidpointRounding.AwayFromZero);

                if (request.DiscountType.HasValue)
                    couponCode.DiscountType = (byte)request.DiscountType.Value;

                if (request.ExpiryDate.HasValue)
                    couponCode.ExpiryDate = request.ExpiryDate;

                if (request.AppliesToRecurring.HasValue)
                    couponCode.AppliesToRecurring = request.AppliesToRecurring.Value;

                var currentType = (CouponDiscountType)couponCode.DiscountType;
                if (currentType == CouponDiscountType.Percentage &&
                    (couponCode.Discount < 0m || couponCode.Discount > 100m))
                    throw new Exception("Percentage discount must be between 0 and 100.");

                if (request.BundleIds != null)
                {
                    var existingBundles = from ccb in _db.PD_CouponCodeBundles
                                          where ccb.CoupanCodeId == request.CoupanCodeId
                                          select ccb;

                    _db.PD_CouponCodeBundles.RemoveRange(await existingBundles.ToListAsync());

                    var couponBundles = request.BundleIds.Select(bundleId => new PD_CouponCodeBundle
                    {
                        CoupanCodeId = couponCode.CoupanCodeId,
                        BundleId = bundleId,
                        IsActive = true,
                        CreatedBy = request.ModifiedBy ?? couponCode.CreatedBy ?? 0,
                        CreatedDate = DateTime.UtcNow
                    }).ToList();

                    _db.PD_CouponCodeBundles.AddRange(couponBundles);
                }

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                return await GetCouponCodeById(couponCode.CoupanCodeId);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<bool> DeleteCouponCode(long couponCodeId)
        {
            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                var couponCode = await (from cc in _db.SYS_CouponCodes
                                        where cc.CoupanCodeId == couponCodeId
                                        select cc).FirstOrDefaultAsync();

                if (couponCode == null)
                    throw new Exception("Coupon code not found.");

                var couponBundles = from ccb in _db.PD_CouponCodeBundles
                                    where ccb.CoupanCodeId == couponCodeId
                                    select ccb;

                _db.PD_CouponCodeBundles.RemoveRange(await couponBundles.ToListAsync());
                _db.SYS_CouponCodes.Remove(couponCode);

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<CouponCodeResponseDTO> GetCouponCodeById(long id)
        {
            var couponQuery = from cc in _db.SYS_CouponCodes.AsNoTracking()
                              join f in _db.SYS_Facilities.AsNoTracking() on cc.FacilityId equals f.FacilityId into facilityJoin
                              from facility in facilityJoin.DefaultIfEmpty()
                              where cc.CoupanCodeId == id
                              select new CouponCodeResponseDTO
                              {
                                  CoupanCodeId = cc.CoupanCodeId,
                                  FacilityId = cc.FacilityId,
                                  CouponCode = cc.CoupanCode,
                                  Discount = cc.Discount,
                                  DiscountType = (CouponDiscountType)cc.DiscountType,
                                  IsActive = cc.IsActive,
                                  AppliesToRecurring = cc.AppliesToRecurring ?? true,
                                  CreatedBy = cc.CreatedBy,
                                  CreatedDate = cc.CreatedDate,
                                  ExpiryDate = cc.ExpiryDate,
                                  FacilityName = facility.TitleShort
                              };

            var coupon = await couponQuery.FirstOrDefaultAsync();
            if (coupon == null)
                throw new Exception("Coupon code not found.");

            coupon.BundleIds = await _db.PD_CouponCodeBundles
                .AsNoTracking()
                .Where(ccb => ccb.CoupanCodeId == id && ccb.IsActive == true)
                .Select(ccb => ccb.BundleId)
                .ToListAsync();

            coupon.Bundles = await (from ccb in _db.PD_CouponCodeBundles.AsNoTracking()
                                    join b in _db.PD_Bundles.AsNoTracking() on ccb.BundleId equals b.BundleId
                                    where ccb.CoupanCodeId == id && ccb.IsActive == true
                                    select new BundleInfoDTO
                                    {
                                        BundleId = b.BundleId,
                                        Name = b.Name,
                                        OriginalPrice = b.Price,
                                        ComparePrice = b.ComparePrice,
                                        RegularImageURL = b.RegularImageURL
                                    }).ToListAsync();

            return coupon;
        }

        public async Task<(List<GetCouponsByFacilityItemDTO> Coupons, int TotalCount)> GetCouponsByFacility(GetCouponsByFacilityRequestDTO request)
        {
            await DeactivateExpiredCouponsAsync();

            var pageSize = (request.PageSize > 0) ? request.PageSize : 25;
            var pageNumber = (request.PageNumber > 0) ? request.PageNumber : 1;

            var baseQuery = from cc in _db.SYS_CouponCodes
                            join f in _db.SYS_Facilities on cc.FacilityId equals f.FacilityId into facilityJoin
                            from facility in facilityJoin.DefaultIfEmpty()
                            where cc.FacilityId == request.FacilityId
                            select new
                            {
                                Coupon = cc,
                                FacilityName = facility.TitleLong
                            };

            var todayUtc = DateTime.UtcNow.Date;
            var status = ResolveGetByFacilityStatusFilter(request);
            if (status == GetByFacilityStatus.Expired)
            {
                baseQuery = baseQuery.Where(x =>
                    x.Coupon.ExpiryDate.HasValue && x.Coupon.ExpiryDate.Value.Date < todayUtc);
            }
            else if (status == GetByFacilityStatus.Active)
            {
                baseQuery = baseQuery.Where(x =>
                    x.Coupon.IsActive == true
                    && (!x.Coupon.ExpiryDate.HasValue || x.Coupon.ExpiryDate.Value.Date >= todayUtc));
            }
            else if (status == GetByFacilityStatus.Inactive)
            {
                baseQuery = baseQuery.Where(x =>
                    (x.Coupon.ExpiryDate == null || x.Coupon.ExpiryDate.Value.Date >= todayUtc)
                    && x.Coupon.IsActive == false);
            }

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                var searchTerm = request.SearchTerm.Trim().ToLower();
                baseQuery = baseQuery.Where(x =>
                    x.Coupon.CoupanCode.ToLower().Contains(searchTerm) ||
                    (x.FacilityName != null && x.FacilityName.ToLower().Contains(searchTerm))
                );
            }

            var totalCount = await baseQuery.CountAsync();

            var sortKey = request.SortBy?.ToLower();
            var sortOrderDesc = request.SortOrder?.ToLower() == "desc";

            var sortedQuery = sortKey switch
            {
                "couponcode" => sortOrderDesc
                    ? baseQuery.OrderByDescending(x => x.Coupon.CoupanCode)
                    : baseQuery.OrderBy(x => x.Coupon.CoupanCode),

                "discount" => sortOrderDesc
                    ? baseQuery.OrderByDescending(x => x.Coupon.Discount)
                    : baseQuery.OrderBy(x => x.Coupon.Discount),

                "discountpercentage" => sortOrderDesc
                    ? baseQuery.OrderByDescending(x => x.Coupon.Discount)
                    : baseQuery.OrderBy(x => x.Coupon.Discount),

                "createddate" => sortOrderDesc
                    ? baseQuery.OrderByDescending(x => x.Coupon.CreatedDate)
                    : baseQuery.OrderBy(x => x.Coupon.CreatedDate),

                "isactive" => sortOrderDesc
                    ? baseQuery.OrderByDescending(x => x.Coupon.IsActive)
                    : baseQuery.OrderBy(x => x.Coupon.IsActive),

                _ => sortOrderDesc
                    ? baseQuery.OrderByDescending(x => x.Coupon.CreatedDate)
                    : baseQuery.OrderBy(x => x.Coupon.CreatedDate)
            };

            var paginatedData = await sortedQuery
                .Skip(pageSize * (pageNumber - 1))
                .Take(pageSize)
                .ToListAsync();

            var couponIds = paginatedData.Select(x => x.Coupon.CoupanCodeId).ToList();

            var bundleAssignments = await (from ccb in _db.PD_CouponCodeBundles
                                           join b in _db.PD_Bundles on ccb.BundleId equals b.BundleId
                                           where couponIds.Contains(ccb.CoupanCodeId) && ccb.IsActive == true
                                           select new
                                           {
                                               CoupanCodeId = ccb.CoupanCodeId,
                                               Bundle = new BundleInfoDTO
                                               {
                                                   BundleId = b.BundleId,
                                                   Name = b.Name,
                                                   OriginalPrice = b.Price,
                                                   ComparePrice = b.ComparePrice,
                                                   RegularImageURL = b.RegularImageURL
                                               }
                                           }).ToListAsync();

            var bundleIdsByCoupon = await (from ccb in _db.PD_CouponCodeBundles
                                           where couponIds.Contains(ccb.CoupanCodeId) && ccb.IsActive == true
                                           group ccb by ccb.CoupanCodeId into g
                                           select new
                                           {
                                               CoupanCodeId = g.Key,
                                               BundleIds = g.Select(x => x.BundleId).ToList()
                                           }).ToListAsync();

            var response = paginatedData.Select(item =>
            {
                var couponBundles = bundleAssignments
                    .Where(ba => ba.CoupanCodeId == item.Coupon.CoupanCodeId)
                    .Select(ba => ba.Bundle)
                    .ToList();

                var couponBundleIds = bundleIdsByCoupon
                    .FirstOrDefault(b => b.CoupanCodeId == item.Coupon.CoupanCodeId)?.BundleIds ?? new List<long>();

                var isExpired = IsCouponPastExpiryForList(item.Coupon.ExpiryDate);
                return new GetCouponsByFacilityItemDTO
                {
                    CoupanCodeId = item.Coupon.CoupanCodeId,
                    FacilityId = item.Coupon.FacilityId,
                    CouponCode = item.Coupon.CoupanCode,
                    Discount = item.Coupon.Discount,
                    DiscountType = (CouponDiscountType)item.Coupon.DiscountType,
                    IsActive = GetCouponListIsActiveString(item.Coupon),
                    IsExpired = isExpired,
                    AppliesToRecurring = item.Coupon.AppliesToRecurring ?? true,
                    CreatedBy = item.Coupon.CreatedBy,
                    CreatedDate = CommonMethods.CommonMethods.UtcToClientLocal(item.Coupon.CreatedDate, request.ClientTimezoneOffsetMinutes),
                    ExpiryDate = item.Coupon.ExpiryDate.HasValue
                        ? CommonMethods.CommonMethods.UtcToClientLocal(item.Coupon.ExpiryDate, request.ClientTimezoneOffsetMinutes)
                        : (DateTime?)null,
                    FacilityName = item.FacilityName,
                    BundleIds = couponBundleIds,
                    Bundles = couponBundles
                };
            }).ToList();

            return (response, totalCount);
        }

        private enum GetByFacilityStatus
        {
            All,
            Active,
            Inactive,
            Expired
        }

        private static GetByFacilityStatus ResolveGetByFacilityStatusFilter(GetCouponsByFacilityRequestDTO request)
        {
            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                var s = request.Status.Trim().ToLowerInvariant();
                if (s is "expired" or "e")
                    return GetByFacilityStatus.Expired;
                if (s is "true" or "active" or "1" or "t")
                    return GetByFacilityStatus.Active;
                if (s is "false" or "inactive" or "0" or "f")
                    return GetByFacilityStatus.Inactive;
            }

            if (request.IsActive.HasValue)
            {
                return request.IsActive.Value
                    ? GetByFacilityStatus.Active
                    : GetByFacilityStatus.Inactive;
            }

            return GetByFacilityStatus.All;
        }

        private static bool IsCouponPastExpiryForList(DateTime? expiryDate) =>
            expiryDate.HasValue && expiryDate.Value.Date < DateTime.UtcNow.Date;

        private static string GetCouponListIsActiveString(SYS_CouponCode c)
        {
            if (IsCouponPastExpiryForList(c.ExpiryDate))
                return "expired";
            if (c.IsActive == true)
                return "true";
            return "false";
        }

        public async Task<ApplyCouponResponseDTO> ApplyCouponToBundle(ApplyCouponRequestDTO request)
        {

            var validationResult = await ValidateCoupon(request.CouponCode, request.BundleId);

            var response = new ApplyCouponResponseDTO
            {
                IsValid = validationResult.IsValid,
                Message = validationResult.Message
            };

            if (!validationResult.IsValid || validationResult.Coupon == null)
                return response;

            var bundle = await (from b in _db.PD_Bundles
                                where b.BundleId == request.BundleId && b.IsActive == true
                                select b).FirstOrDefaultAsync();

            if (bundle == null)
            {
                response.IsValid = false;
                response.Message = "Bundle not found.";
                return response;
            }

            var originalPrice = bundle.Price ?? 0m;
            var coupon = validationResult.Coupon;
            var couponType = (CouponDiscountType)coupon.DiscountType;

            decimal discountAmount;
            if (couponType == CouponDiscountType.Percentage)
            {
                var pct = coupon.Discount < 0m ? 0m : (coupon.Discount > 100m ? 100m : coupon.Discount);
                discountAmount = Math.Round(originalPrice * (pct / 100m), 2, MidpointRounding.AwayFromZero);
            }
            else
            {
                var amt = coupon.Discount < 0m ? 0m : coupon.Discount;
                discountAmount = Math.Round(amt, 2, MidpointRounding.AwayFromZero);
            }

            if (discountAmount > originalPrice) discountAmount = originalPrice;

            var discountedPrice = Math.Round(originalPrice - discountAmount, 2, MidpointRounding.AwayFromZero);

            response.BundleId = bundle.BundleId;
            response.BundleName = bundle.Name;
            response.OriginalPrice = originalPrice;
            response.DiscountedPrice = discountedPrice;
            response.DiscountAmount = discountAmount;
            response.Discount = coupon.Discount;
            response.DiscountType = (CouponDiscountType)coupon.DiscountType;

            return response;
        }

        public async Task<CouponValidationResultDTO> ValidateCoupon(string couponCode, long bundleId)
        {
            var result = new CouponValidationResultDTO();

            var coupon = await (from cc in _db.SYS_CouponCodes
                                where cc.CoupanCode == couponCode && cc.IsActive == true
                                select cc).FirstOrDefaultAsync();

            if (coupon == null)
            {
                result.IsValid = false;
                result.Message = "Invalid or inactive coupon code.";
                return result;
            }

            if (coupon.ExpiryDate.HasValue && coupon.ExpiryDate.Value.Date < DateTime.UtcNow.Date)
            {
                result.IsValid = false;
                result.Message = "Coupon has expired.";
                return result;
            }

            var couponBundle = await (from ccb in _db.PD_CouponCodeBundles
                                      where ccb.CoupanCodeId == coupon.CoupanCodeId
                                         && ccb.BundleId == bundleId
                                         && ccb.IsActive == true
                                      select ccb).FirstOrDefaultAsync();

            if (couponBundle == null)
            {
                result.IsValid = false;
                result.Message = "This coupon code is not valid for the selected bundle.";
                return result;
            }

            result.IsValid = true;
            result.Message = "Coupon applied successfully.";
            result.Coupon = coupon;

            return result;
        }

        public async Task<List<CouponCodeResponseDTO>> GetAllCouponsWithBundles()
        {
            var couponsWithFacility = await (from cc in _db.SYS_CouponCodes
                                             join f in _db.SYS_Facilities on cc.FacilityId equals f.FacilityId into facilityJoin
                                             from facility in facilityJoin.DefaultIfEmpty()
                                             select new
                                             {
                                                 Coupon = cc,
                                                 FacilityName = facility.TitleLong
                                             }).ToListAsync();

            var response = new List<CouponCodeResponseDTO>();

            foreach (var item in couponsWithFacility)
            {
                var dto = new CouponCodeResponseDTO
                {
                    CoupanCodeId = item.Coupon.CoupanCodeId,
                    FacilityId = item.Coupon.FacilityId,
                    CouponCode = item.Coupon.CoupanCode,
                    Discount = item.Coupon.Discount,
                    DiscountType = (CouponDiscountType)item.Coupon.DiscountType,
                    IsActive = item.Coupon.IsActive,
                    IsExpired = item.Coupon.ExpiryDate.HasValue && item.Coupon.ExpiryDate.Value < DateTime.UtcNow,
                    AppliesToRecurring = item.Coupon.AppliesToRecurring ?? true,
                    CreatedBy = item.Coupon.CreatedBy,
                    CreatedDate = CommonMethods.CommonMethods.ToLocalTime(item.Coupon.CreatedDate),
                    ExpiryDate = item.Coupon.ExpiryDate,
                    FacilityName = item.FacilityName
                };

                var bundleIds = await (from ccb in _db.PD_CouponCodeBundles
                                       where ccb.CoupanCodeId == item.Coupon.CoupanCodeId && ccb.IsActive == true
                                       select ccb.BundleId).ToListAsync();

                var bundles = await (from ccb in _db.PD_CouponCodeBundles
                                     join b in _db.PD_Bundles on ccb.BundleId equals b.BundleId
                                     where ccb.CoupanCodeId == item.Coupon.CoupanCodeId && ccb.IsActive == true
                                     select new BundleInfoDTO
                                     {
                                         BundleId = b.BundleId,
                                         Name = b.Name,
                                         OriginalPrice = b.Price,
                                         ComparePrice = b.ComparePrice,
                                         RegularImageURL = b.RegularImageURL
                                     }).ToListAsync();

                dto.BundleIds = bundleIds;
                dto.Bundles = bundles;

                response.Add(dto);
            }

            return response;
        }

        public async Task<bool> BundleHasActiveCoupons(long bundleId)
        {
            var hasCoupons = await (from ccb in _db.PD_CouponCodeBundles
                                    join cc in _db.SYS_CouponCodes on ccb.CoupanCodeId equals cc.CoupanCodeId
                                    where ccb.BundleId == bundleId
                                       && ccb.IsActive == true
                                       && cc.IsActive == true
                                    select ccb.CouponCodeBundleId).AnyAsync();

            return hasCoupons;
        }

        public async Task<List<CouponCodeResponseDTO>> GetActiveCouponsForBundle(long bundleId)
        {
            await DeactivateExpiredCouponsAsync();

            var couponIds = await (from ccb in _db.PD_CouponCodeBundles
                                   where ccb.BundleId == bundleId && ccb.IsActive == true
                                   select ccb.CoupanCodeId).ToListAsync();

            if (!couponIds.Any())
                return new List<CouponCodeResponseDTO>();

            var couponsWithFacility = await (from cc in _db.SYS_CouponCodes
                                             join f in _db.SYS_Facilities on cc.FacilityId equals f.FacilityId into facilityJoin
                                             from facility in facilityJoin.DefaultIfEmpty()
                                             where couponIds.Contains(cc.CoupanCodeId) && cc.IsActive == true
                                             select new
                                             {
                                                 Coupon = cc,
                                                 FacilityName = facility.TitleLong
                                             }).ToListAsync();

            var response = new List<CouponCodeResponseDTO>();

            foreach (var item in couponsWithFacility)
            {
                var dto = new CouponCodeResponseDTO
                {
                    CoupanCodeId = item.Coupon.CoupanCodeId,
                    FacilityId = item.Coupon.FacilityId,
                    CouponCode = item.Coupon.CoupanCode,
                    Discount = item.Coupon.Discount,
                    DiscountType = (CouponDiscountType)item.Coupon.DiscountType,
                    IsActive = item.Coupon.IsActive,
                    IsExpired = item.Coupon.ExpiryDate.HasValue && item.Coupon.ExpiryDate.Value < DateTime.UtcNow,
                    AppliesToRecurring = item.Coupon.AppliesToRecurring ?? true,
                    CreatedBy = item.Coupon.CreatedBy,
                    CreatedDate = CommonMethods.CommonMethods.ToLocalTime(item.Coupon.CreatedDate),
                    ExpiryDate = item.Coupon.ExpiryDate,
                    FacilityName = item.FacilityName
                };

                var bundleIds = await (from ccb in _db.PD_CouponCodeBundles
                                       where ccb.CoupanCodeId == item.Coupon.CoupanCodeId && ccb.IsActive == true
                                       select ccb.BundleId).ToListAsync();

                dto.BundleIds = bundleIds;

                response.Add(dto);
            }

            return response;
        }

        public async Task<CouponCodeResponseDTO> SetCouponActiveStatus(long couponCodeId, bool isActive)
        {
            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var coupon = await (from cc in _db.SYS_CouponCodes
                                    where cc.CoupanCodeId == couponCodeId
                                    select cc).FirstOrDefaultAsync();

                if (coupon == null)
                    throw new Exception("Coupon code not found.");

                coupon.IsActive = isActive;
                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                return await GetCouponCodeById(coupon.CoupanCodeId);
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        public async Task<CouponCodeResponseDTO> ToggleCouponActiveStatus(long couponCodeId)
        {
            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var coupon = await (from cc in _db.SYS_CouponCodes
                                    where cc.CoupanCodeId == couponCodeId
                                    select cc).FirstOrDefaultAsync();

                if (coupon == null)
                    throw new Exception("Coupon code not found.");

                coupon.IsActive = !coupon.IsActive;
                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                return await GetCouponCodeById(coupon.CoupanCodeId);
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        public async Task<ValidateCouponResponseDTO> ValidateCouponAsync(ValidateCouponRequestDTO request, CancellationToken ct = default)
        {
            await DeactivateExpiredCouponsAsync(ct);

            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.FacilityId <= 0) return Invalid("Invalid facility.");
            if (request.BundleId <= 0) return Invalid("Invalid bundle.");
            if (request.BundlePrice < 0) return Invalid("Invalid bundle price.");
            if (string.IsNullOrWhiteSpace(request.CouponCode)) return Invalid("Invalid coupon.");

            var code = request.CouponCode.Trim();
            var codeLower = code.ToLower();

            var bundleExists = await _db.PD_Bundles
                .AsNoTracking()
                .AnyAsync(b => b.BundleId == request.BundleId && b.IsActive == true, ct);

            if (!bundleExists) return Invalid("Bundle not found or inactive.");

            var coupon = await _db.SYS_CouponCodes
                .AsNoTracking()
                .Where(c =>
                    (c.FacilityId == request.FacilityId || c.FacilityId == null) &&
                    c.CoupanCode.ToLower() == codeLower)
                .FirstOrDefaultAsync(ct);

            if (coupon == null) return Invalid("Invalid coupon.");
            if (!(coupon.IsActive ?? false)) return Invalid("Coupon is inactive.");
            if (coupon.ExpiryDate.HasValue && coupon.ExpiryDate.Value.Date < DateTime.UtcNow.Date)
                return Invalid("Coupon has expired.");

            var isAssigned = await _db.PD_CouponCodeBundles
                .AsNoTracking()
                .AnyAsync(cb =>
                    cb.CoupanCodeId == coupon.CoupanCodeId &&
                    cb.BundleId == request.BundleId &&
                    cb.IsActive == true, ct);

            if (!isAssigned) return Invalid("Coupon is not applicable to this bundle.");

            if (request.PatientId.HasValue && request.PatientId.Value > 0)
            {
                var alreadyUsed = await _db.PT_CouponUsages
                    .AsNoTracking()
                    .AnyAsync(cu =>
                        cu.PatientId == request.PatientId.Value &&
                        cu.CouponCode != null &&
                        cu.CouponCode.ToLower() == codeLower &&
                        (cu.IsActive == true || cu.IsActive == null), ct);

                if (alreadyUsed)
                {
                    return Invalid("This coupon has already been used by you. Each coupon can only be used once per patient.");
                }
            }
            else
            {

                if (!string.IsNullOrWhiteSpace(request.CouponCode))
                {

                }
            }

            var original = request.BundlePrice;
            var discounted = GetDiscountedPriceAfterCoupon(original, coupon.DiscountType, coupon.Discount);
            var discountAmount = Math.Round(original - discounted, 2, MidpointRounding.AwayFromZero);
            if (discountAmount < 0m) discountAmount = 0m;

            return new ValidateCouponResponseDTO
            {
                IsValid = true,
                Message = "Coupon applied.",
                CouponCodeId = coupon.CoupanCodeId,
                AppliesToRecurring = coupon.AppliesToRecurring ?? true,
                CouponCode = coupon.CoupanCode,
                Discount = coupon.Discount,
                DiscountType = (CouponDiscountType?)coupon.DiscountType,
                DiscountAmount = discountAmount,
                OriginalPrice = original,
                DiscountedPrice = discounted
            };

            static ValidateCouponResponseDTO Invalid(string msg) => new ValidateCouponResponseDTO
            {
                IsValid = false,
                Message = msg
            };
        }

        public async Task<(decimal ChargeAmount, bool CouponWasApplied)> ComputeRecurringChargeAmountAsync(
            long facilityId,
            long bundleId,
            decimal basePrice,
            long? recurringCouponCodeId,
            CancellationToken ct = default)
        {
            await DeactivateExpiredCouponsAsync(ct);

            var price = Math.Max(0m, Math.Round(basePrice, 2, MidpointRounding.AwayFromZero));
            if (!recurringCouponCodeId.HasValue)
                return (price, false);

            var coupon = await _db.SYS_CouponCodes
                .FirstOrDefaultAsync(c => c.CoupanCodeId == recurringCouponCodeId.Value, ct);

            if (coupon == null)
                return (price, false);
            if (coupon.ExpiryDate.HasValue && coupon.ExpiryDate.Value.Date < DateTime.UtcNow.Date)
                return (price, false);
            if (coupon.IsActive != true)
                return (price, false);

            if (coupon.AppliesToRecurring == false)
                return (price, false);

            if (!(coupon.FacilityId == facilityId || coupon.FacilityId == null))
                return (price, false);
            var assigned = await _db.PD_CouponCodeBundles
                .AsNoTracking()
                .AnyAsync(cb =>
                    cb.CoupanCodeId == coupon.CoupanCodeId &&
                    cb.BundleId == bundleId &&
                    cb.IsActive == true, ct);

            if (!assigned)
                return (price, false);

            var discounted = GetDiscountedPriceAfterCoupon(price, coupon.DiscountType, coupon.Discount);
            return (discounted, true);
        }

        private static decimal GetDiscountedPriceAfterCoupon(decimal originalPrice, byte discountType, decimal couponDiscountValue)
        {
            originalPrice = Math.Max(0m, Math.Round(originalPrice, 2, MidpointRounding.AwayFromZero));

            var ctype = (CouponDiscountType)discountType;
            decimal discountAmount;
            if (ctype == CouponDiscountType.Percentage)
            {
                var pct = couponDiscountValue < 0m ? 0m : (couponDiscountValue > 100m ? 100m : couponDiscountValue);
                discountAmount = Math.Round(originalPrice * (pct / 100m), 2, MidpointRounding.AwayFromZero);
            }
            else
            {
                var amt = couponDiscountValue < 0m ? 0m : couponDiscountValue;
                discountAmount = Math.Round(amt, 2, MidpointRounding.AwayFromZero);
            }

            if (discountAmount > originalPrice)
                discountAmount = originalPrice;

            var discounted = Math.Round(originalPrice - discountAmount, 2, MidpointRounding.AwayFromZero);
            if (discounted < 0m)
                return 0m;
            return discounted;
        }

        private async Task DeactivateExpiredCouponsAsync(CancellationToken ct = default)
        {
            var todayUtc = DateTime.UtcNow.Date;
            var expiredActiveCoupons = await _db.SYS_CouponCodes
                .Where(c => c.IsActive == true &&
                            c.ExpiryDate.HasValue &&
                            c.ExpiryDate.Value.Date < todayUtc)
                .ToListAsync(ct);

            if (expiredActiveCoupons.Count == 0)
                return;

            foreach (var coupon in expiredActiveCoupons)
                coupon.IsActive = false;

            await _db.SaveChangesAsync(ct);
        }
    }
}
