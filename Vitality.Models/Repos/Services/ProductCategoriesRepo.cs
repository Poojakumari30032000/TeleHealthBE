using AutoMapper;
using DudeMeds.Models.DTOs.Categories;
using DudeMeds.Models.Repos.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using Vitality.Models.DTOs.ProductCategories;
using Vitality.Models.EntityClasses;
using Vitality.Models.Repos;
using Vitality.Models.Repos.Services.Audit;

namespace DudeMeds.Models.Repos.Services
{
    public class ProductCategoriesRepo : BaseRepo, IProductCategoriesRepo
    {
        private readonly IMapper _mapper;
        private readonly IAuditService _auditService;

        public ProductCategoriesRepo(IMapper mapper, IAuditService auditService)
        {
            _mapper = mapper;
            _auditService = auditService;
        }

        public List<GetAllCategoriesResponseDTO> GetAllCategories(GetAllCategoriesRequestDTO request, out int totalCategoryCount)
        {
            List<GetAllCategoriesResponseDTO> response = new List<GetAllCategoriesResponseDTO>();
            List<PD_Category> list = new List<PD_Category>();

            if (request.FacilityId.HasValue && request.FacilityId.Value > 0)
            {
                list = (from c in _db.PD_Categories
                        join fc in _db.PD_FacilityCategories on c.CategoryId equals fc.CategoryId
                        where c.IsActive == true
                           && fc.FacilityId == request.FacilityId.Value
                           && fc.IsActive == true
                        select c).ToList();
            }
            else
            {
                list = _db.PD_Categories.Where(x => x.IsActive == true).ToList();
            }

            totalCategoryCount = list.Count;
            list = list.OrderBy(x => x.CategoryName).Skip((request.PageNumber - 1) * request.PageSize).Take(request.PageSize).ToList();
            response = _mapper.Map<List<GetAllCategoriesResponseDTO>>(list);
            return response;
        }

        public GetCategoryByIdResponseDTO GetCategoryById(long CategoryId)
        {
            GetCategoryByIdResponseDTO response = new GetCategoryByIdResponseDTO();
            PD_Category category = _db.PD_Categories.Where(x => x.CategoryId == CategoryId).FirstOrDefault();
            response = _mapper.Map<GetCategoryByIdResponseDTO>(category);
            return response;

        }
        public string SaveCategory(SaveCategoryRequestDTO request, long UserId)
        {

            try
            {
                PD_Category category = new PD_Category();
                if (request.CategoryId == 0)
                {
                    category = _mapper.Map<PD_Category>(request);
                    category.CreatedBy = UserId;
                    category.CreatedDate = DateTime.UtcNow;
                    category.IsActive = true;
                    _db.PD_Categories.Add(category);
                    _db.SaveChanges();

                    _auditService.LogEntityChange(
                        action: "Create",
                        entityType: "PD_Category",
                        entityId: category.CategoryId,
                        newValues: category,
                        userId: UserId,
                        description: $"Category '{category.CategoryName}' created - Description: {category.CategoryDescription}, Image: {category.ImageURL}",
                        module: "Category"
                    );

                    return "Category Created Successfully";

                }
                else
                {

                    var oldCategory = _db.PD_Categories.AsNoTracking()
                        .Where(x => x.CategoryId == request.CategoryId)
                        .FirstOrDefault();

                    category = _db.PD_Categories.Where(x => x.CategoryId == request.CategoryId).FirstOrDefault();
                    _mapper.Map(request, category);
                    _db.SaveChanges();

                    _auditService.LogEntityChange(
                        action: "Update",
                        entityType: "PD_Category",
                        entityId: category.CategoryId,
                        oldValues: oldCategory,
                        newValues: category,
                        userId: UserId,
                        description: $"Category '{category.CategoryName}' updated - Description: {category.CategoryDescription}, Image: {category.ImageURL}",
                        module: "Category"
                    );

                    return "Category Updated Successfully";

                }
            }
            catch (Exception ex)
            {
                return "Something went wrong. Please try again later.";
            }
        }

        public bool DeleteCategory(long CategoryId)
        {
            PD_Category category = _db.PD_Categories.Where(x => x.CategoryId == CategoryId).FirstOrDefault();
            if (category != null)
            {
                category.IsActive = false;
                _db.SaveChanges();

                _auditService.LogEntityChange(
                    action: "Delete",
                    entityType: "PD_Category",
                    entityId: category.CategoryId,
                    oldValues: category,
                    description: $"Category '{category.CategoryName}' deleted",
                    module: "Category"
                );

                return true;
            }
            else
            {
                return false;
            }
        }

        public async Task<List<GetAllCategoriesWithBundlesResponseDTO>> GetCategoriesWithBundlesAsync(
    long facilityId,
    CancellationToken ct = default)
        {

            var categoriesWithBundles = await (
                from c in _db.PD_Categories.AsNoTracking()
                join fc in _db.PD_FacilityCategories.AsNoTracking()
                    on c.CategoryId equals fc.CategoryId
                where c.IsActive == true
                   && fc.FacilityId == facilityId && fc.IsActive == true
                   && _db.PD_Bundles.Any(b => b.IsActive == true && b.Status == "Active" && b.CategoryId == c.CategoryId
                       && (b.FacilityId == facilityId || _db.PD_FacilityBundlePrices.AsNoTracking().Any(fbp => fbp.FacilityId == facilityId && fbp.BundleId == b.BundleId)))
                select new GetAllCategoriesWithBundlesResponseDTO
                {
                    CategoryId = c.CategoryId,
                    CategoryName = c.CategoryName,
                    CategoryDescription = c.CategoryDescription,
                    ImageURL = c.ImageURL,
                    Bundles = _db.PD_Bundles
                        .AsNoTracking()
                        .Where(b => b.IsActive == true && b.Status == "Active" && b.CategoryId == c.CategoryId
                            && (b.FacilityId == facilityId || _db.PD_FacilityBundlePrices.AsNoTracking().Any(fbp => fbp.FacilityId == facilityId && fbp.BundleId == b.BundleId)))
                        .Select(b => new BundleDTO
                        {
                            BundleId = b.BundleId,
                            Name = b.Name,
                            Description = b.Description,
                            Price = (
                                _db.PD_FacilityBundlePrices.AsNoTracking()
                                   .Where(fp => fp.FacilityId == facilityId && fp.BundleId == b.BundleId)
                                   .Select(fp => (decimal?)fp.ClinicPrice)
                                   .FirstOrDefault()
                                ?? (b.Price ?? 0m)
                            ),
                            IsRecurring = _db.PD_FacilityBundlePrices.AsNoTracking()
                                   .Where(fp => fp.FacilityId == facilityId && fp.BundleId == b.BundleId)
                                   .Select(fp => (bool?)fp.IsRecurring)
                                   .FirstOrDefault(),
                            Duration = b.visits
                        })
                        .OrderBy(b => b.Name)
                        .ToList()
                }
            ).ToListAsync(ct);

            return categoriesWithBundles;
        }

        public List<GetAllCategoriesWithBundlesResponseDTO> GetCategoriesWithBundles()
        {
            var categoriesWithBundles = _db.PD_Categories
                .Where(x => x.IsActive == true)
                .Select(category => new GetAllCategoriesWithBundlesResponseDTO
                {
                    CategoryId = category.CategoryId,
                    CategoryName = category.CategoryName,
                    CategoryDescription = category.CategoryDescription,
                    Bundles = _db.PD_Bundles
                        .Where(bundle => bundle.CategoryId == category.CategoryId && bundle.IsActive == true)
                        .Select(bundle => new BundleDTO
                        {
                            BundleId = bundle.BundleId,
                            Name = bundle.Name,
                            Price = bundle.Price ?? 0,
                        }).ToList()
                })
                .Where(x => x.Bundles.Any())
                .ToList();

            return categoriesWithBundles;
        }

        public async Task<List<BundleDTO>> GetBundlesByCategoryAndFacilityAsync(
     long categoryId,
     long facilityId,
     CancellationToken ct = default)
        {
            if (categoryId <= 0) throw new ArgumentException("categoryId must be > 0.");
            if (facilityId <= 0) throw new ArgumentException("facilityId must be > 0.");

            var query =
                from b in _db.PD_Bundles.AsNoTracking()
                where b.IsActive == true && b.Status == "Active" && b.CategoryId == categoryId && (b.FacilityId == facilityId || _db.PD_FacilityBundlePrices.AsNoTracking().Any(fbp => fbp.FacilityId == facilityId && fbp.BundleId == b.BundleId))
                join fp0 in _db.PD_FacilityBundlePrices
                        .AsNoTracking()
                        .Where(x => x.FacilityId == facilityId)
                    on b.BundleId equals fp0.BundleId into fpJoin
                from fp in fpJoin.DefaultIfEmpty()
                orderby b.Name
                select new BundleDTO
                {
                    BundleId = b.BundleId,
                    Name = b.Name,
                    Description = b.Description,
                    Price = (decimal?)fp.ClinicPrice ?? (b.Price ?? 0m),
                    IsRecurring = (bool?)fp.IsRecurring,
                    Duration = b.visits
                };

            return await query.ToListAsync(ct);
        }

        public async Task<BundleDTO?> GetFacilityBundleDetailAsync(long facilityId, long bundleId, CancellationToken ct = default)
        {
            if (facilityId <= 0) throw new ArgumentException("facilityId must be > 0.");
            if (bundleId <= 0) throw new ArgumentException("bundleId must be > 0.");

            var query =
                from b in _db.PD_Bundles.AsNoTracking()
                where b.BundleId == bundleId && b.IsActive == true && b.Status == "Active"
                  && (b.FacilityId == facilityId || _db.PD_FacilityBundlePrices.AsNoTracking().Any(fbp => fbp.FacilityId == facilityId && fbp.BundleId == b.BundleId))
                join fp0 in _db.PD_FacilityBundlePrices
                        .AsNoTracking()
                        .Where(x => x.FacilityId == facilityId)
                    on b.BundleId equals fp0.BundleId into fpJoin
                from fp in fpJoin.DefaultIfEmpty()
                select new BundleDTO
                {
                    BundleId = b.BundleId,
                    Name = b.Name ?? "",
                    Description = b.Description,
                    BrandName = null,
                    Price = (decimal?)fp.ClinicPrice ?? (b.Price ?? 0m),
                    Dosage = null,
                    QuantityUnit = null,
                    IsRecurring = (bool?)fp.IsRecurring,
                    Duration = b.visits
                };

            return await query.FirstOrDefaultAsync(ct);
        }
    }
}
