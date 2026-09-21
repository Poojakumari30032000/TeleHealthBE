using AutoMapper;
using Dapper;
using DudeMeds.Models.DTOs.Categories;
using DudeMeds.Models.DTOs.Conditions;
using DudeMeds.Models.DTOs.Facilities;
using DudeMeds.Models.DTOs.Products;
using DudeMeds.Models.DTOs.Questionnaires;
using DudeMeds.Models.Repos.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using Vitality.Models.DTOs.Bundles;
using Vitality.Models.DTOs.ClinicToPatient;
using Vitality.Models.DTOs.Products;
using Vitality.Models.EntityClasses;
using Vitality.Models.Repos;
using Vitality.Models.Repos.Services;
using Vitality.Models.Repos.Services.Audit;

namespace DudeMeds.Models.Repos.Services
{
    public partial class ProductsRepo : BaseRepo, IProductsRepo
    {
        private const long EmpowerCatalogId = 1;
        private const long DefaultCustomCatalogId = 2;
        private readonly IMapper _mapper;
        private readonly IAuditService _auditService;
        private readonly INotificationService? _notificationService;

        public ProductsRepo(IMapper mapper, IAuditService auditService, INotificationService? notificationService = null)
        {
            _mapper = mapper;
            _auditService = auditService;

            _notificationService = notificationService;
        }

        private long ResolveCatalogIdForSave(SaveDrugRequestDTO request)
        {
            if (request.CatalogId.HasValue && request.CatalogId.Value > 0)
                return request.CatalogId.Value;

            return request.IsCustom == true ? DefaultCustomCatalogId : EmpowerCatalogId;
        }

        private IQueryable<PD_Drug> ApplyCatalogFilters(IQueryable<PD_Drug> query, long? catalogId)
        {
            if (catalogId.HasValue && catalogId.Value > 0)
                query = query.Where(d => d.CatalogId == catalogId.Value);
            return query;
        }

        private IQueryable<PD_Drug> ApplyCatalogFacilityVisibility(IQueryable<PD_Drug> query, long? facilityId)
        {
            if (!facilityId.HasValue || facilityId.Value <= 0)
                return query;

            var fid = facilityId.Value;
            return query.Where(d =>
                !_db.PC_CATALOGFACILITYASSIGNMENTs.Any(cfa => cfa.CatalogId == d.CatalogId && cfa.IsActive == true)
                || _db.PC_CATALOGFACILITYASSIGNMENTs.Any(cfa => cfa.CatalogId == d.CatalogId && cfa.FacilityId == fid && cfa.IsActive == true));
        }

        public GetProductDashboardTilesResponseDTO GetProductDashboardTiles(long Drug)
        {
            GetProductDashboardTilesResponseDTO response = new GetProductDashboardTilesResponseDTO();
            response.TotalOrders = 0;
            response.TotalPatients = 0;
            response.TotalRevenue = 0;

            return response;
        }

        public List<GetAllProductsResponseDTO> GetAllProducts(GetAllProductsRequestDTO request, out int totalProductCount)
        {
            int pageNumber = (request?.PageNumber > 0) ? request.PageNumber : 1;
            int pageSize = (request?.PageSize > 0) ? request.PageSize : 10;

            var query = _db.PD_Drugs.AsNoTracking().Where(d => d.IsActive == true);
            query = ApplyCatalogFilters(query, request?.CatalogId ?? EmpowerCatalogId);
            query = ApplyCatalogFacilityVisibility(query, request?.FacilityId);

            if (!string.IsNullOrWhiteSpace(request?.Title))
            {
                string like = $"%{request.Title.Trim()}%";
                query = query.Where(d =>
                    EF.Functions.Like(d.Name ?? "", like) ||
                    EF.Functions.Like(d.BrandName ?? "", like) ||
                    EF.Functions.Like(d.GenericName ?? "", like)
                );
            }

            if (!string.IsNullOrWhiteSpace(request?.Status))
            {
                string status = request.Status.Trim();
                query = query.Where(d => d.Status == status);
            }

            totalProductCount = query.Count();

            var projected = query.Select(d => new GetAllProductsResponseDTO
            {
                DrugId = d.DrugId,
                PharmacyId = d.PharmacyId,
                PharmacyName = _db.SYS_Pharmacies
                                      .Where(ph => ph.PharmacyId == d.PharmacyId)
                                      .Select(ph => ph.PharmacyName)
                                      .FirstOrDefault(),

                ProductId = d.ProductId,
                CategoryId = d.CategoryId,
                CatalogId = d.CatalogId,
                CatalogName = _db.PD_Catalogs
                    .AsNoTracking()
                    .Where(c => c.CatalogId == d.CatalogId && c.IsActive == true)
                    .Select(c => c.CatalogName)
                    .FirstOrDefault(),

                DrugType = d.Type,
                Name = d.Name,
                BrandName = d.BrandName,
                GenericName = d.GenericName,

                DosageForm = d.DosageForm,
                Strenght = d.Strenght,
                PackageSize = d.PackageSize,
                Quantity = d.Quantity,
                QuantityUnit = d.QuantityUnit,
                Refills = d.Refills,

                Price = d.Price,
                ComparePrice = d.ComparePrice,
                SuggestedRetail = d.SuggestedRetail,

                ControlSubstance = d.ControlSubstance,
                Refrigerated = d.Refrigerated,
                ItemDesignatorID = d.ItemDesignatorID,

                Status = d.Status,

            });

            var final = projected
                .OrderBy(x => x.Name)
                .ThenBy(x => x.DrugId)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return final;
        }

        public List<GetAllBundleResponseDTO> GetAllBundles(GetAllBundlesRequestDTO request, out int totalProductCount)
        {
            int pageNumber = (request?.PageNumber > 0) ? request.PageNumber : 1;
            int pageSize = (request?.PageSize > 0) ? request.PageSize : 10;

            IQueryable<PD_Bundle> query = _db.PD_Bundles.AsNoTracking().Where(d => d.IsActive == true);

            if (request?.FacilityId.HasValue == true && request.FacilityId.Value > 0)
            {
                long fid = request.FacilityId.Value;
                query = query.Where(d =>
                    d.FacilityId == fid
                    || _db.PD_FacilityBundlePrices.AsNoTracking().Any(fbp => fbp.FacilityId == fid && fbp.BundleId == d.BundleId));
            }

            if (!string.IsNullOrWhiteSpace(request?.Title))
            {
                string like = $"%{request.Title.Trim()}%";
                query = query.Where(d => EF.Functions.Like(d.Name ?? "", like));
            }

            if (!string.IsNullOrWhiteSpace(request?.Status))
            {
                string status = request.Status.Trim();
                query = query.Where(d => d.Status == status);
            }

            if (request?.CategoryId.HasValue == true && request.CategoryId.Value > 0)
            {
                query = query.Where(d => d.CategoryId == request.CategoryId.Value);
            }

            totalProductCount = query.Count();

            var projected = from b in query
                            join c in _db.PD_Categories.AsNoTracking() on b.CategoryId equals c.CategoryId into catGroup
                            from c in catGroup.DefaultIfEmpty()
                            join f in _db.SYS_Facilities.AsNoTracking() on b.FacilityId equals f.FacilityId into facGroup
                            from f in facGroup.DefaultIfEmpty()
                            select new GetAllBundleResponseDTO
                            {
                                Name = b.Name,
                                BundleId = b.BundleId,
                                CategoryId = b.CategoryId,
                                CategoryName = c != null ? c.CategoryName : null,
                                FacilityId = b.FacilityId,
                                FacilityName = f != null ? f.TitleLong : null,
                                AssignedFacilities = null,
                                Price = b.Price,
                                ComparePrice = b.ComparePrice,
                                Description = b.Description,
                                RegularImageUrl = b.RegularImageURL,
                                Visits = b.visits,
                                Status = b.Status,
                            };

            var final = projected
                .OrderBy(x => x.Name)
                .ThenBy(x => x.BundleId)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var bundleIds = final.Select(x => x.BundleId ?? 0).Where(id => id > 0).Distinct().ToList();
            if (bundleIds.Count > 0)
            {
                var joined = _db.PD_FacilityBundlePrices
                    .AsNoTracking()
                    .Where(fbp => bundleIds.Contains(fbp.BundleId))
                    .Join(_db.SYS_Facilities.AsNoTracking(), fbp => fbp.FacilityId, fac => fac.FacilityId, (fbp, fac) => new { fbp.BundleId, fbp.FacilityId, fac.TitleLong })
                    .ToList();
                var multiFacilityNames = joined
                    .GroupBy(x => x.BundleId)
                    .ToDictionary(g => g.Key, g => string.Join(", ", g.Select(x => x.TitleLong ?? "").Where(s => !string.IsNullOrEmpty(s)).OrderBy(s => s)));
                var facilityIdsByBundle = joined
                    .GroupBy(x => x.BundleId)
                    .ToDictionary(g => g.Key, g => g.Select(x => x.FacilityId).OrderBy(id => id).ToList());
                foreach (var dto in final)
                {
                    if (dto.FacilityId.HasValue && dto.FacilityId.Value > 0)
                    {
                        dto.AssignedFacilities = dto.FacilityName ?? "";
                        dto.FacilityIds = new List<long> { dto.FacilityId.Value };
                    }
                    else if (dto.BundleId.HasValue)
                    {
                        if (multiFacilityNames.TryGetValue(dto.BundleId.Value, out var names))
                            dto.AssignedFacilities = names;
                        else
                            dto.AssignedFacilities = "";
                        dto.FacilityIds = facilityIdsByBundle.TryGetValue(dto.BundleId.Value, out var ids) ? ids : new List<long>();
                    }
                    else
                    {
                        dto.AssignedFacilities = "";
                        dto.FacilityIds = new List<long>();
                    }
                }
            }

            return final;
        }

        public List<GetAllDrugsForGlobalAdminResponseDTO> GetAllDrugsForGlobalAdmin(GetAllProductsRequestDTO request, out int totalProductCount)
        {
            int pageNumber = (request?.PageNumber > 0) ? request.PageNumber : 1;
            int pageSize = (request?.PageSize > 0) ? request.PageSize : 10;

            var query = _db.PD_Drugs.AsNoTracking().Where(d => d.IsActive == true);
            query = ApplyCatalogFilters(query, request?.CatalogId ?? EmpowerCatalogId);
            query = ApplyCatalogFacilityVisibility(query, request?.FacilityId);

            if (!string.IsNullOrWhiteSpace(request?.Title))
            {
                string like = $"%{request.Title.Trim()}%";
                query = query.Where(d =>
                    EF.Functions.Like(d.Name ?? "", like) ||
                    EF.Functions.Like(d.BrandName ?? "", like) ||
                    EF.Functions.Like(d.GenericName ?? "", like)
                );
            }

            if (!string.IsNullOrWhiteSpace(request?.Status))
            {
                query = query.Where(d => d.Status == request.Status.Trim());
            }

            if (request?.ControlSubstance.HasValue == true)
            {
                query = query.Where(d =>
                    (d.ControlSubstance ?? d.Control_Substance) == request.ControlSubstance.Value);
            }
            if (request?.ControlSubstance.HasValue == true)
            {
                query = query.Where(d => d.ControlSubstance == request.ControlSubstance.Value);
            }

            totalProductCount = query.Count();

            var pageDrugs = query
                .OrderBy(d => d.Name)
                .ThenBy(d => d.DrugId)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(d => new
                {
                    d.DrugId,
                    d.ProductId,
                    d.PharmacyId,
                    d.CatalogId,
                    d.CategoryId,
                    d.Type,
                    d.Name,
                    d.BrandName,
                    d.GenericName,
                    d.DosageForm,
                    d.Strenght,
                    d.PackageSize,
                    d.Quantity,
                    d.QuantityUnit,
                    d.Refills,
                    d.Price,
                    d.ComparePrice,
                    d.ControlSubstance,
                    d.Refrigerated,
                    d.ItemDesignatorID,
                    d.Markup,
                    d.Status,
                    d.IsCustom
                })
                .ToList();

            if (pageDrugs.Count == 0)
                return new List<GetAllDrugsForGlobalAdminResponseDTO>();

            var drugIds = pageDrugs.Select(x => x.DrugId).ToList();
            var catalogIds = pageDrugs.Select(x => x.CatalogId).Distinct().ToList();
            var catalogNames = _db.PD_Catalogs.AsNoTracking()
                .Where(c => c.IsActive == true && catalogIds.Contains(c.CatalogId))
                .ToDictionary(c => c.CatalogId, c => c.CatalogName);

            var ptgAll = _db.PC_PHARMTOGLOBALs
                .AsNoTracking()
                .Where(p => drugIds.Contains(p.DrugId ?? 0) && p.IsActive == true)
                .ToList();

            var latestPtgByDrug = ptgAll
                .GroupBy(p => p.DrugId ?? 0)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.PharmToGlobalId).First());

            var ptgIds = latestPtgByDrug.Values.Select(p => p.PharmToGlobalId).ToList();

            var gacAll = ptgIds.Count > 0
                ? _db.PC_GATOCLINICs
                    .AsNoTracking()
                    .Where(g => ptgIds.Contains(g.PharmToGlobalId) && g.IsActive == true)
                    .ToList()
                : new List<PC_GATOCLINIC>();

            var latestGacByPtgId = gacAll
                .GroupBy(g => g.PharmToGlobalId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.GAtoClinicId).First());

            Dictionary<long, PC_CLINICTOPATIENT>? latestCtpByDrug = null;
            if (request.FacilityId.HasValue)
            {
                var ctpAll = _db.PC_CLINICTOPATIENTs
                    .AsNoTracking()
                    .Where(c => drugIds.Contains(c.DrugId) && c.FacilityId == request.FacilityId.Value && c.IsActive == true)
                    .ToList();

                latestCtpByDrug = ctpAll
                    .GroupBy(c => c.DrugId)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.ClinicToPatientId).First());
            }

            var result = new List<GetAllDrugsForGlobalAdminResponseDTO>(pageDrugs.Count);
            foreach (var d in pageDrugs)
            {
                latestPtgByDrug.TryGetValue(d.DrugId, out var ptg);
                PC_GATOCLINIC? gac = null;
                if (ptg != null)
                    latestGacByPtgId.TryGetValue(ptg.PharmToGlobalId, out gac);

                PC_CLINICTOPATIENT? ctp = null;
                if (latestCtpByDrug != null)
                    latestCtpByDrug.TryGetValue(d.DrugId, out ctp);

                result.Add(new GetAllDrugsForGlobalAdminResponseDTO
                {
                    DrugId = d.DrugId,
                    ProductId = d.ProductId,
                    PharmacyId = d.PharmacyId,
                    CatalogId = d.CatalogId,
                    CatalogName = catalogNames.TryGetValue(d.CatalogId, out var catalogName) ? catalogName : null,
                    CategoryId = d.CategoryId,
                    DrugType = d.Type,
                    Name = d.Name,
                    BrandName = d.BrandName,
                    GenericName = d.GenericName,
                    DosageForm = d.DosageForm,
                    Strenght = d.Strenght,
                    PackageSize = d.PackageSize,
                    Quantity = d.Quantity,
                    QuantityUnit = d.QuantityUnit,
                    Refills = d.Refills,
                    Price = d.Price,
                    ComparePrice = d.ComparePrice,
                    ControlSubstance = d.ControlSubstance,
                    Refrigerated = d.Refrigerated,
                    ItemDesignatorID = d.ItemDesignatorID,
                    Status = d.Status,
                    Markup = d.Markup,

                    PharmToGlobalId = ptg != null ? ptg.PharmToGlobalId : null,
                    PharmacyPrice = ptg?.PharmacyPrice,
                    MarkupPercent = ptg != null && string.Equals(ptg.MarkupType, "Amount", StringComparison.OrdinalIgnoreCase)
                        ? null
                        : ptg?.MarkupPercent,
                    WholesalePrice = ptg?.WholesalePrice,
                    MarkupType = ptg?.MarkupType,

                    GAtoClinicIdGlobal = gac != null ? gac.GAtoClinicId : null,
                    SuggestedRetail = gac != null ? gac.SuggestedRetailPrice : null,

                    ClinicToPatientId = ctp != null ? ctp.ClinicToPatientId : null,
                    GAtoClinicId = ctp?.GAtoClinicId,
                    CustomerSuggestedRetailPrice = ctp != null ? ctp.ClinicSuggestedRetailPrice : null
                    ,
                    IsCustom = d.IsCustom
                });
            }

            return result;
        }

        public List<GetAllDrugsForGlobalAdminResponseDTO> GetAllCustomDrugsForGlobalAdmin(GetAllProductsRequestDTO request, out int totalProductCount)
        {

            int pageNumber = (request?.PageNumber > 0) ? request.PageNumber : 1;
            int pageSize = (request?.PageSize > 0) ? request.PageSize : 10;

            var query = _db.PD_Drugs.AsNoTracking().Where(d => d.IsActive == true && d.IsCustom == true);
            query = ApplyCatalogFilters(query, request?.CatalogId);
            query = ApplyCatalogFacilityVisibility(query, request?.FacilityId);

            if (!string.IsNullOrWhiteSpace(request?.Title))
            {
                string like = $"%{request.Title.Trim()}%";
                query = query.Where(d =>
                    EF.Functions.Like(d.Name ?? "", like) ||
                    EF.Functions.Like(d.BrandName ?? "", like) ||
                    EF.Functions.Like(d.GenericName ?? "", like)
                );
            }

            if (!string.IsNullOrWhiteSpace(request?.Status))
            {
                query = query.Where(d => d.Status == request.Status.Trim());
            }
            if (request?.ControlSubstance.HasValue == true)
            {
                query = query.Where(d => d.ControlSubstance == request.ControlSubstance.Value);
            }

            totalProductCount = query.Count();

            var pageDrugs = query
                .OrderBy(d => d.Name)
                .ThenBy(d => d.DrugId)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(d => new
                {
                    d.DrugId,
                    d.ProductId,
                    d.PharmacyId,
                    d.CatalogId,
                    d.CategoryId,
                    d.Type,
                    d.Name,
                    d.BrandName,
                    d.GenericName,
                    d.DosageForm,
                    d.Strenght,
                    d.PackageSize,
                    d.Quantity,
                    d.QuantityUnit,
                    d.Refills,
                    d.Price,
                    d.ComparePrice,
                    d.ControlSubstance,
                    d.Refrigerated,
                    d.ItemDesignatorID,
                    d.Markup,
                    d.Status,
                    d.IsCustom
                })
                .ToList();

            if (pageDrugs.Count == 0)
                return new List<GetAllDrugsForGlobalAdminResponseDTO>();

            var drugIds = pageDrugs.Select(x => x.DrugId).ToList();
            var catalogIds = pageDrugs.Select(x => x.CatalogId).Distinct().ToList();
            var catalogNames = _db.PD_Catalogs.AsNoTracking()
                .Where(c => c.IsActive == true && catalogIds.Contains(c.CatalogId))
                .ToDictionary(c => c.CatalogId, c => c.CatalogName);

            var ptgAll = _db.PC_PHARMTOGLOBALs
                .AsNoTracking()
                .Where(p => drugIds.Contains(p.DrugId ?? 0) && p.IsActive == true)
                .ToList();

            var latestPtgByDrug = ptgAll
                .GroupBy(p => p.DrugId ?? 0)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.PharmToGlobalId).First());

            var ptgIds = latestPtgByDrug.Values.Select(p => p.PharmToGlobalId).ToList();

            var gacAll = ptgIds.Count > 0
                ? _db.PC_GATOCLINICs
                    .AsNoTracking()
                    .Where(g => ptgIds.Contains(g.PharmToGlobalId) && g.IsActive == true)
                    .ToList()
                : new List<PC_GATOCLINIC>();

            var latestGacByPtgId = gacAll
                .GroupBy(g => g.PharmToGlobalId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.GAtoClinicId).First());

            Dictionary<long, PC_CLINICTOPATIENT>? latestCtpByDrug = null;
            if (request.FacilityId.HasValue)
            {
                var ctpAll = _db.PC_CLINICTOPATIENTs
                    .AsNoTracking()
                    .Where(c => drugIds.Contains(c.DrugId) && c.FacilityId == request.FacilityId.Value && c.IsActive == true)
                    .ToList();

                latestCtpByDrug = ctpAll
                    .GroupBy(c => c.DrugId)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.ClinicToPatientId).First());
            }

            var result = new List<GetAllDrugsForGlobalAdminResponseDTO>(pageDrugs.Count);
            foreach (var d in pageDrugs)
            {
                latestPtgByDrug.TryGetValue(d.DrugId, out var ptg);
                PC_GATOCLINIC? gac = null;
                if (ptg != null)
                    latestGacByPtgId.TryGetValue(ptg.PharmToGlobalId, out gac);

                PC_CLINICTOPATIENT? ctp = null;
                if (latestCtpByDrug != null)
                    latestCtpByDrug.TryGetValue(d.DrugId, out ctp);

                result.Add(new GetAllDrugsForGlobalAdminResponseDTO
                {
                    DrugId = d.DrugId,
                    ProductId = d.ProductId,
                    PharmacyId = d.PharmacyId,
                    CatalogId = d.CatalogId,
                    CatalogName = catalogNames.TryGetValue(d.CatalogId, out var catalogName) ? catalogName : null,
                    CategoryId = d.CategoryId,
                    DrugType = d.Type,
                    Name = d.Name,
                    BrandName = d.BrandName,
                    GenericName = d.GenericName,
                    DosageForm = d.DosageForm,
                    Strenght = d.Strenght,
                    PackageSize = d.PackageSize,
                    Quantity = d.Quantity,
                    QuantityUnit = d.QuantityUnit,
                    Refills = d.Refills,
                    Price = d.Price,
                    ComparePrice = d.ComparePrice,
                    ControlSubstance = d.ControlSubstance,
                    Refrigerated = d.Refrigerated,
                    ItemDesignatorID = d.ItemDesignatorID,
                    Status = d.Status,
                    Markup = d.Markup,

                    PharmToGlobalId = ptg != null ? ptg.PharmToGlobalId : null,
                    PharmacyPrice = ptg?.PharmacyPrice,
                    MarkupPercent = ptg != null && string.Equals(ptg.MarkupType, "Amount", StringComparison.OrdinalIgnoreCase)
                        ? null
                        : ptg?.MarkupPercent,
                    WholesalePrice = ptg?.WholesalePrice,
                    MarkupType = ptg?.MarkupType,

                    GAtoClinicIdGlobal = gac != null ? gac.GAtoClinicId : null,
                    SuggestedRetail = gac != null ? gac.SuggestedRetailPrice : null,

                    ClinicToPatientId = ctp != null ? ctp.ClinicToPatientId : null,
                    GAtoClinicId = ctp?.GAtoClinicId,
                    CustomerSuggestedRetailPrice = ctp != null ? ctp.ClinicSuggestedRetailPrice : null
                    ,
                    IsCustom = d.IsCustom
                });
            }

            return result;
        }

        public GetProductInfoByNameResponseDTO GetProductInfoByName(GetProductInfoByNameRequestDTO request)
        {
            GetProductInfoByNameResponseDTO response = new GetProductInfoByNameResponseDTO();
            SYS_Facility facility = _db.SYS_Facilities.Where(x => x.Guid == request.FacilityGuid).FirstOrDefault();
            if (facility != null)
            {
                SYS_Product product = _db.SYS_Products.Where(x => x.ProductName == request.ProductName && x.IsActive == true && x.OrganizationId == facility.OrganizationId).FirstOrDefault();
                if (product != null)
                {
                    if (product.ProductType == "Drug")
                    {
                        PD_Drug drug = _db.PD_Drugs.Where(x => x.ProductId == product.ProductId && x.IsActive == true).FirstOrDefault();
                        if (drug != null)
                        {
                            response.ProductId = drug.ProductId;
                            response.ProductName = drug.Name;
                            response.CategoryId = drug.CategoryId;
                            response.CategoryName = _db.PD_Categories.Where(x => x.CategoryId == drug.CategoryId).Select(x => x.CategoryName).FirstOrDefault();
                            response.Price = drug.Price;
                            response.ShippingFrequency = drug.ShippingFrequency;
                            response.BillingFrequency = drug.BillingFrequency;
                            response.QuantityUnit = drug.QuantityUnit;
                            response.Dosage = drug.Dosage;
                            response.Refills = drug.Refills;
                            response.RegularImageURL = drug.RegularImageURL;
                            response.Dose = drug.Dose;
                            response.Strenght = drug.Strenght;
                            response.DrugType = drug.Type;
                            response.ProductType = "Drug";
                            response.Quantity = drug.Quantity;

                        }
                    }
                }
            }
            return response;
        }

        public bool DeleteProduct(DeleteProductRequestDTO request)
        {
            SYS_Product product = _db.SYS_Products.Where(x => x.ProductId == request.ProductId).FirstOrDefault();
            product.IsActive = false;
            if (product.ProductType == "Drug")
            {
                PD_Drug drug = _db.PD_Drugs.Where(x => x.ProductId == request.ProductId).FirstOrDefault();
                drug.IsActive = false;
                List<DG_DrugIngredient> drugIngredient = _db.DG_DrugIngredients.Where(x => x.DrugId == drug.DrugId).ToList();
                foreach (var ingredient in drugIngredient)
                {
                    ingredient.IsActive = false;
                }
            }
            else if (product.ProductType == "Bundle")
            {
               PD_Bundle bundle = _db.PD_Bundles.Where( x=> x.ProductId == request.ProductId).FirstOrDefault();
               bundle.IsActive = false;
                List<PD_DrugVarientsInBundle> drugVarientsInBundle = _db.PD_DrugVarientsInBundles.Where(x => x.BundleId == bundle.BundleId).ToList();
                foreach (var item in drugVarientsInBundle)
                {
                    item.IsActive = false;
                }
            }
            else
            {
                return false;
            }
            _db.SaveChanges();
            return true;
        }

        public bool UpdateProductStatus(UpdateProductStatusRequestDTO request)
        {
            SYS_Product product = _db.SYS_Products.Where(x => x.ProductId == request.ProductId).FirstOrDefault();
            product.Status = request.Status;
            if (product.ProductType == "Drug")
            {
                PD_Drug drug = _db.PD_Drugs.Where(x => x.ProductId == request.ProductId).FirstOrDefault();
                drug.Status = request.Status;

            }
            else if (product.ProductType == "Bundle")
            {
                PD_Bundle bundle = _db.PD_Bundles.Where(x => x.ProductId == request.ProductId).FirstOrDefault();
                bundle.Status = request.Status;

            }
            else
            {
                return false;
            }
            _db.SaveChanges();
            return true;
        }

        public List<GetAllProductsForShoppingResponseDTO> GetAllProductsForShopping(long OrganizationId, GetAllProductsForShoppingRequestDTO request)
        {
            List<GetAllProductsForShoppingResponseDTO> finalResponse = new List<GetAllProductsForShoppingResponseDTO>();

            List<SYS_Product> productList = _db.SYS_Products.Where(x => x.IsActive == true && x.OrganizationId == request.OrganizationId).ToList();
            foreach (var item in productList)
            {
                GetAllProductsForShoppingResponseDTO emptyResponse = new GetAllProductsForShoppingResponseDTO();
                if (item.ProductType == "Drug")
                {
                    PD_Drug drug = _db.PD_Drugs.Where(x => x.ProductId == item.ProductId && x.IsActive == true && (x.IsCustom == null || x.IsCustom == false)).FirstOrDefault();
                    if (drug != null)
                    {
                        emptyResponse.ProductId = item.ProductId;
                        emptyResponse.ProductName = item.ProductName;
                        emptyResponse.ProductType = item.ProductType;
                        emptyResponse.DrugId = drug.DrugId;
                        emptyResponse.ProductPrice = drug.Price;
                        emptyResponse.ComparePrice = drug.ComparePrice;
                        emptyResponse.Quantity = drug.Quantity;
                        emptyResponse.Dosage = drug.Dosage;
                        emptyResponse.Dose = drug.Dose;
                        emptyResponse.ProductURL = drug.RegularImageURL;
                        emptyResponse.GenericName = drug.GenericName;
                        emptyResponse.CategoryId = drug.CategoryId;
                    }
                    else
                    {
                        continue;
                    }
                }
                else if (item.ProductType == "Bundle")
                {
                    PD_Bundle bundle = _db.PD_Bundles.Where(x => x.ProductId == item.ProductId).FirstOrDefault();
                    if (bundle != null)
                    {
                        emptyResponse.ProductId = item.ProductId;
                        emptyResponse.ProductName = bundle.Name;
                        emptyResponse.ProductPrice = bundle.Price;
                        emptyResponse.ProductURL = bundle.RegularImageURL;
                        emptyResponse.BundleId = bundle.BundleId;
                    }
                    else { continue; }
                }
                else
                {
                    continue;
                }
                finalResponse.Add(emptyResponse);
            }
            if (!string.IsNullOrEmpty(request.Title))
            {
                finalResponse = finalResponse.Where(x => x.ProductName.Contains(request.Title) || x.GenericName.Contains(request.Title)).ToList();
            }
            if (request.CategoryId != null)
            {
                finalResponse = finalResponse.Where(x => x.CategoryId == request.CategoryId).ToList();
            }
            if (!string.IsNullOrEmpty(request.ProductType))
            {
                finalResponse = finalResponse.Where(x => x.ProductType == request.ProductType).ToList();
            }
            return finalResponse;
        }

        public GetDrugByIdResponseDTO GetDrugById(long ProductId)
        {
            GetDrugByIdResponseDTO response = new GetDrugByIdResponseDTO();
            PD_Drug drug = _db.PD_Drugs.Where(x => x.ProductId == ProductId).FirstOrDefault();
            response = _mapper.Map<GetDrugByIdResponseDTO>(drug);
            response.CreatedByName = _db.SYS_UserDetails.Where(x => x.UserId == drug.CreatedBy).Select(x => x.FirstName + " " + x.LastName).FirstOrDefault();
            response.Guid = _db.SYS_Products.Where(x => x.ProductId == drug.ProductId).Select(x => x.Guid).FirstOrDefault();
            response.drugIngredient = _db.DG_DrugIngredients.Where(x => x.DrugId == drug.DrugId && x.IsActive == true)
            .Select(x => new SaveDrugIngredientRequestDTO
            {
                DrugIngredientId = x.DrugIngredientId,
                DrugId = x.DrugId,
                IngredientName = x.IngredientName,
                IngredientStrength = x.IngredientStrength,
                Status = x.Status,
            }).ToList();
            return response;
        }

        public string SaveDrug(SaveDrugRequestDTO request, long UserId, long OrganizationId)
        {
            using var tx = _db.Database.BeginTransaction();
            try
            {
                PD_Drug drug;
                var nowUtc = DateTime.UtcNow;
                var guid = Guid.NewGuid();

                if (request.DrugId == 0)
                {
                    var catalogId = ResolveCatalogIdForSave(request);
                    var catalog = _db.PD_Catalogs.AsNoTracking()
                        .FirstOrDefault(x => x.CatalogId == catalogId && x.IsActive == true);
                    if (catalog == null)
                        return "Selected catalog does not exist or is inactive.";

                    request.CatalogId = catalogId;
                    request.IsCustom = catalogId == EmpowerCatalogId ? null : true;

                    var product = new SYS_Product
                    {
                        ProductType = "Drug",
                        IsActive = true,
                        ProductName = request.Name,
                        CreatedDate = nowUtc,
                        CreatedBy = UserId,
                        Guid = guid.ToString(),
                        OrganizationId = OrganizationId,
                        Status = "Active"
                    };
                    _db.SYS_Products.Add(product);
                    _db.SaveChanges();

                    drug = _mapper.Map<PD_Drug>(request);

                    if (request.IsCustom.HasValue)
                        drug.IsCustom = request.IsCustom;
                    drug.CatalogId = catalogId;
                    drug.DosageForm = request.DosageForm.ToUpper();

                    if (request.Markup.HasValue)
                        drug.Markup = request.Markup.Value;
                    drug.ProductId = product.ProductId;
                    drug.CreatedBy = UserId;
                    drug.CreatedDate = nowUtc;
                    drug.IsActive = true;
                    drug.Status = "Active";

                    _db.PD_Drugs.Add(drug);
                    _db.SaveChanges();

                    decimal? markupPercent = null;
                    var normalizedMarkupType = string.IsNullOrWhiteSpace(request.MarkupType) ? "Percentage" : request.MarkupType.Trim();
                    if (!string.Equals(normalizedMarkupType, "Percentage", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(normalizedMarkupType, "Amount", StringComparison.OrdinalIgnoreCase))
                    {
                        return "Invalid markup type. Please use Percentage or Amount.";
                    }

                    if (request.Markup.HasValue && request.Markup.Value < 0)
                    {
                        return "Markup cannot be negative. Please enter a value greater than or equal to 0.";
                    }

                    if (request.Markup.HasValue && string.Equals(normalizedMarkupType, "Amount", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!request.Price.HasValue || request.Price.Value <= 0)
                        {
                            return "To use Amount markup, enter a pharmacy price greater than 0.";
                        }
                    }

                    if (request.Markup.HasValue && request.Price.HasValue && request.Price.Value > 0)
                    {
                        if (string.Equals(normalizedMarkupType, "Amount", StringComparison.OrdinalIgnoreCase))
                        {

                            markupPercent = (decimal)request.Markup.Value / request.Price.Value * 100m;
                        }
                        else
                        {

                            markupPercent = request.Markup.Value;
                        }
                    }

                    if (markupPercent.HasValue)
                    {
                        const decimal maxMarkupPercent = 99999.9999m;
                        if (markupPercent.Value > maxMarkupPercent || markupPercent.Value < 0)
                        {
                            return "Markup is too high for the selected price. Please reduce markup so the calculated markup percent stays between 0 and 99,999.9999.";
                        }
                    }

                    var pharmToGlobal = new PC_PHARMTOGLOBAL
                    {
                        DrugId = drug.DrugId,
                        PharmacyPrice = request.Price,
                        MarkupPercent = markupPercent,
                        MarkupType = normalizedMarkupType,
                        IsActive = true,
                        CreatedBy = UserId,
                        CreatedAt = nowUtc
                    };
                    _db.PC_PHARMTOGLOBALs.Add(pharmToGlobal);
                    _db.SaveChanges();

                    var gaToClinic = new PC_GATOCLINIC
                    {
                        PharmToGlobalId = pharmToGlobal.PharmToGlobalId,
                        SuggestedRetailPrice = (decimal)request.SuggestedRetail,
                        IsActive = true,
                        CreatedBy = UserId,
                        CreatedAt = nowUtc
                    };
                    _db.PC_GATOCLINICs.Add(gaToClinic);
                    _db.SaveChanges();

                    if (request.IsCustom == true && request.SuggestedRetail.HasValue)
                    {
                        var drugId = drug.DrugId;
                        var facilityIds = _db.SYS_Facilities
                            .AsNoTracking()
                            .Where(f => f.IsActive == true)
                            .Select(f => f.FacilityId)
                            .ToList();

                        if (facilityIds.Count > 0)
                        {
                            var existingFacilityIds = _db.PC_CLINICTOPATIENTs
                                .AsNoTracking()
                                .Where(c => c.DrugId == drugId
                                            && c.IsActive == true
                                            && facilityIds.Contains(c.FacilityId))
                                .Select(c => c.FacilityId)
                                .Distinct()
                                .ToList();

                            var missingFacilityIds = facilityIds
                                .Where(fid => !existingFacilityIds.Contains(fid))
                                .ToList();

                            if (missingFacilityIds.Count > 0)
                            {
                                var newRows = missingFacilityIds.Select(fid => new PC_CLINICTOPATIENT
                                {
                                    DrugId = drugId,
                                    FacilityId = fid,
                                    GAtoClinicId = gaToClinic.GAtoClinicId,
                                    ClinicSuggestedRetailPrice = request.SuggestedRetail.Value,
                                    IsActive = true,
                                    CreatedBy = UserId,
                                    CreatedAt = nowUtc
                                }).ToList();

                                _db.PC_CLINICTOPATIENTs.AddRange(newRows);
                                _db.SaveChanges();
                            }
                        }
                    }

                    tx.Commit();

                    _auditService.LogEntityChange(
                        action: "Create",
                        entityType: "PD_Drug",
                        entityId: drug.DrugId,
                        newValues: drug,
                        userId: UserId,
                        description: $"Drug '{drug.Name}' created - Brand: {drug.BrandName}, Generic: {drug.GenericName}, Price: ${request.Price}",
                        module: "Product"
                    );

                    return "Drug Added Successfully";
                }
                else
                {
                    var catalogId = ResolveCatalogIdForSave(request);
                    var catalog = _db.PD_Catalogs.AsNoTracking()
                        .FirstOrDefault(x => x.CatalogId == catalogId && x.IsActive == true);
                    if (catalog == null)
                        return "Selected catalog does not exist or is inactive.";

                    request.CatalogId = catalogId;
                    request.IsCustom = catalogId == EmpowerCatalogId ? null : true;

                    drug = _db.PD_Drugs.FirstOrDefault(x => x.DrugId == request.DrugId);
                    if (drug == null) return "Drug not found.";

                    _mapper.Map(request, drug);

                    if (request.Markup.HasValue)
                        drug.Markup = request.Markup.Value;

                    if (request.IsCustom.HasValue)
                        drug.IsCustom = request.IsCustom;
                    else
                        drug.IsCustom = null;
                    drug.CatalogId = catalogId;

                    var product = _db.SYS_Products
                        .FirstOrDefault(x => x.ProductId == drug.ProductId && x.ProductName != request.Name);
                    if (product != null)
                    {
                        product.ProductName = request.Name;
                    }

                    _db.SaveChanges();

                    var ptg = _db.PC_PHARMTOGLOBALs
                        .FirstOrDefault(x => x.DrugId == drug.DrugId && x.IsActive == true);

                    decimal? markupPercentUpdate = null;
                    var normalizedMarkupTypeUpdate = string.IsNullOrWhiteSpace(request.MarkupType)
                        ? (ptg?.MarkupType ?? "Percentage")
                        : request.MarkupType.Trim();
                    if (!string.Equals(normalizedMarkupTypeUpdate, "Percentage", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(normalizedMarkupTypeUpdate, "Amount", StringComparison.OrdinalIgnoreCase))
                    {
                        return "Invalid markup type. Please use Percentage or Amount.";
                    }

                    if (request.Markup.HasValue && request.Markup.Value < 0)
                    {
                        return "Markup cannot be negative. Please enter a value greater than or equal to 0.";
                    }

                    if (request.Markup.HasValue && string.Equals(normalizedMarkupTypeUpdate, "Amount", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!request.Price.HasValue || request.Price.Value <= 0)
                        {
                            return "To use Amount markup, enter a pharmacy price greater than 0.";
                        }
                    }

                    if (request.Markup.HasValue && request.Price.HasValue && request.Price.Value > 0)
                    {
                        if (string.Equals(normalizedMarkupTypeUpdate, "Amount", StringComparison.OrdinalIgnoreCase))
                        {
                            markupPercentUpdate = (decimal)request.Markup.Value / request.Price.Value * 100m;
                        }
                        else
                        {
                            markupPercentUpdate = request.Markup.Value;
                        }
                    }

                    if (markupPercentUpdate.HasValue)
                    {
                        const decimal maxMarkupPercent = 99999.9999m;
                        if (markupPercentUpdate.Value > maxMarkupPercent || markupPercentUpdate.Value < 0)
                        {
                            return "Markup is too high for the selected price. Please reduce markup so the calculated markup percent stays between 0 and 99,999.9999.";
                        }
                    }

                    if (ptg == null)
                    {
                        ptg = new PC_PHARMTOGLOBAL
                        {
                            DrugId = drug.DrugId,
                            PharmacyPrice = request.Price,
                            MarkupPercent = markupPercentUpdate,
                            MarkupType = normalizedMarkupTypeUpdate,
                            IsActive = true,
                            CreatedBy = UserId,
                            CreatedAt = nowUtc
                        };
                        _db.PC_PHARMTOGLOBALs.Add(ptg);
                        _db.SaveChanges();
                    }
                    else
                    {
                        ptg.PharmacyPrice = request.Price;
                        ptg.MarkupPercent = markupPercentUpdate;
                        if (!string.IsNullOrWhiteSpace(request.MarkupType))
                        {
                            ptg.MarkupType = normalizedMarkupTypeUpdate;
                        }
                        ptg.ModifiedBy = UserId;
                        ptg.ModifiedAt = nowUtc;
                        _db.SaveChanges();
                    }

                    var gac = _db.PC_GATOCLINICs
                        .FirstOrDefault(x => x.PharmToGlobalId == ptg.PharmToGlobalId && x.IsActive == true);

                    if (gac == null)
                    {
                        gac = new PC_GATOCLINIC
                        {
                            PharmToGlobalId = ptg.PharmToGlobalId,
                            SuggestedRetailPrice = request.SuggestedRetail ?? 0,
                            IsActive = true,
                            CreatedBy = UserId,
                            CreatedAt = nowUtc
                        };
                        _db.PC_GATOCLINICs.Add(gac);
                    }
                    else
                    {
                        gac.SuggestedRetailPrice = (decimal)request.SuggestedRetail;
                        gac.ModifiedBy = UserId;
                        gac.ModifiedAt = nowUtc;
                    }

                    _db.SaveChanges();

                    if (request.IsCustom == true && request.SuggestedRetail.HasValue)
                    {
                        var drugId = drug.DrugId;
                        var facilityIds = _db.SYS_Facilities
                            .AsNoTracking()
                            .Where(f => f.IsActive == true)
                            .Select(f => f.FacilityId)
                            .ToList();

                        if (facilityIds.Count > 0)
                        {
                            var existingFacilityIds = _db.PC_CLINICTOPATIENTs
                                .AsNoTracking()
                                .Where(c => c.DrugId == drugId
                                            && c.IsActive == true
                                            && facilityIds.Contains(c.FacilityId))
                                .Select(c => c.FacilityId)
                                .Distinct()
                                .ToList();

                            var missingFacilityIds = facilityIds
                                .Where(fid => !existingFacilityIds.Contains(fid))
                                .ToList();

                            if (missingFacilityIds.Count > 0)
                            {
                                var newRows = missingFacilityIds.Select(fid => new PC_CLINICTOPATIENT
                                {
                                    DrugId = drugId,
                                    FacilityId = fid,
                                    GAtoClinicId = gac?.GAtoClinicId,
                                    ClinicSuggestedRetailPrice = request.SuggestedRetail.Value,
                                    IsActive = true,
                                    CreatedBy = UserId,
                                    CreatedAt = nowUtc
                                }).ToList();

                                _db.PC_CLINICTOPATIENTs.AddRange(newRows);
                                _db.SaveChanges();
                            }
                        }
                    }

                    tx.Commit();
                    return "Drug Updated Successfully";
                }
            }
            catch (DbUpdateException ex)
            {
                tx.Rollback();
                var dbError = ex.InnerException?.Message ?? ex.Message;
                if (dbError.Contains("CK_PC_PTG_Percent", StringComparison.OrdinalIgnoreCase) ||
                    dbError.Contains("MarkupPercent", StringComparison.OrdinalIgnoreCase))
                {
                    return "Markup is out of allowed range. Please adjust markup so the calculated markup percent is between 0 and 99,999.9999.";
                }
                if (dbError.Contains("Arithmetic overflow", StringComparison.OrdinalIgnoreCase))
                {
                    return "One of the entered pricing values is too large. Please reduce the amount and try again.";
                }
                return "Unable to save drug pricing due to invalid values. Please review Price, Markup, and Markup Type and try again.";
            }
            catch
            {
                tx.Rollback();
                return "Something went wrong. Please try again later.";
            }
        }

        public string SaveCatalog(SaveCatalogRequestDTO request, long userId)
        {
            if (request == null)
                return "Invalid catalog request.";
            if (string.IsNullOrWhiteSpace(request.CatalogName))
                return "Catalog name is required.";

            var normalizedName = request.CatalogName.Trim();
            var nowUtc = DateTime.UtcNow;
            using var tx = _db.Database.BeginTransaction();
            try
            {
                PD_Catalog catalog;
                if (request.CatalogId <= 0)
                {
                    var duplicateExists = _db.PD_Catalogs.AsNoTracking().Any(x =>
                        x.IsActive == true &&
                        x.CatalogName.ToLower() == normalizedName.ToLower());
                    if (duplicateExists)
                        return "Catalog name already exists.";

                    catalog = new PD_Catalog
                    {
                        CatalogName = normalizedName,
                        Description = request.Description?.Trim(),
                        IsActive = true,
                        IsSystemDefined = false,
                        CreatedBy = userId,
                        CreatedDate = nowUtc
                    };
                    _db.PD_Catalogs.Add(catalog);
                    _db.SaveChanges();
                }
                else
                {
                    catalog = _db.PD_Catalogs.FirstOrDefault(x => x.CatalogId == request.CatalogId)!;
                    if (catalog == null)
                        return "Catalog not found.";

                    if (catalog.IsSystemDefined == true && catalog.CatalogId == EmpowerCatalogId &&
                        !string.Equals(catalog.CatalogName, normalizedName, StringComparison.OrdinalIgnoreCase))
                    {
                        return "Empower catalog name cannot be changed.";
                    }

                    var duplicateExists = _db.PD_Catalogs.AsNoTracking().Any(x =>
                        x.CatalogId != request.CatalogId && x.IsActive == true &&
                        x.CatalogName.ToLower() == normalizedName.ToLower());
                    if (duplicateExists)
                        return "Catalog name already exists.";

                    catalog.CatalogName = normalizedName;
                    catalog.Description = request.Description?.Trim();
                    catalog.ModifiedBy = userId;
                    catalog.ModifiedDate = nowUtc;
                    _db.SaveChanges();
                }

                var requestedFacilityIds = (request.FacilityIds ?? new List<long>())
                    .Where(x => x > 0)
                    .Distinct()
                    .ToList();

                var validFacilityIds = requestedFacilityIds.Count == 0
                    ? new List<long>()
                    : _db.SYS_Facilities.AsNoTracking()
                        .Where(f => f.IsActive == true && requestedFacilityIds.Contains(f.FacilityId))
                        .Select(f => f.FacilityId)
                        .ToList();

                var existingAssignments = _db.PC_CATALOGFACILITYASSIGNMENTs
                    .Where(x => x.CatalogId == catalog.CatalogId && x.IsActive == true)
                    .ToList();

                foreach (var row in existingAssignments.Where(x => !validFacilityIds.Contains(x.FacilityId)))
                {
                    row.IsActive = false;
                    row.ModifiedBy = userId;
                    row.ModifiedDate = nowUtc;
                }

                var existingFacilityIds = existingAssignments.Select(x => x.FacilityId).ToHashSet();
                var newAssignments = validFacilityIds
                    .Where(fid => !existingFacilityIds.Contains(fid))
                    .Select(fid => new PC_CATALOGFACILITYASSIGNMENT
                    {
                        CatalogId = catalog.CatalogId,
                        FacilityId = fid,
                        IsActive = true,
                        CreatedBy = userId,
                        CreatedDate = nowUtc
                    })
                    .ToList();

                if (newAssignments.Count > 0)
                    _db.PC_CATALOGFACILITYASSIGNMENTs.AddRange(newAssignments);

                _db.SaveChanges();
                tx.Commit();
                return request.CatalogId <= 0 ? "Catalog Added Successfully" : "Catalog Updated Successfully";
            }
            catch
            {
                tx.Rollback();
                return "Something went wrong. Please try again later.";
            }
        }

        public List<CatalogResponseDTO> GetAllCatalogs(GetAllCatalogsRequestDTO request)
        {
            var query = _db.PD_Catalogs.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(request?.SearchText))
            {
                var like = $"%{request.SearchText.Trim()}%";
                query = query.Where(c =>
                    EF.Functions.Like(c.CatalogName, like) ||
                    EF.Functions.Like(c.Description ?? "", like));
            }

            if (request?.FacilityId.HasValue == true && request.FacilityId.Value > 0)
            {
                var fid = request.FacilityId.Value;
                query = query.Where(c =>
                    !_db.PC_CATALOGFACILITYASSIGNMENTs.Any(a => a.CatalogId == c.CatalogId && a.IsActive == true)
                    || _db.PC_CATALOGFACILITYASSIGNMENTs.Any(a => a.CatalogId == c.CatalogId && a.FacilityId == fid && a.IsActive == true));
            }

            var catalogs = query.OrderBy(c => c.CatalogId).ToList();
            var catalogIds = catalogs.Select(c => c.CatalogId).ToList();
            var assignments = _db.PC_CATALOGFACILITYASSIGNMENTs.AsNoTracking()
                .Where(a => a.IsActive == true && catalogIds.Contains(a.CatalogId))
                .ToList();

            return catalogs.Select(c => new CatalogResponseDTO
            {
                CatalogId = c.CatalogId,
                CatalogName = c.CatalogName,
                Description = c.Description,
                IsActive = c.IsActive == true,
                IsSystemDefined = c.IsSystemDefined == true,
                FacilityIds = assignments.Where(a => a.CatalogId == c.CatalogId).Select(a => a.FacilityId).Distinct().OrderBy(x => x).ToList()
            }).ToList();
        }

        public string UpdateCatalogStatus(UpdateCatalogStatusRequestDTO request, long userId)
        {
            if (request == null || request.CatalogId <= 0)
                return "Invalid catalog request.";

            var catalog = _db.PD_Catalogs.FirstOrDefault(x => x.CatalogId == request.CatalogId);
            if (catalog == null)
                return "Catalog not found.";

            if (catalog.CatalogId == EmpowerCatalogId && request.IsActive == false)
                return "Empower catalog cannot be inactivated.";

            if (catalog.IsActive == request.IsActive)
                return "Catalog status is already up to date.";

            catalog.IsActive = request.IsActive;
            catalog.ModifiedBy = userId;
            catalog.ModifiedDate = DateTime.UtcNow;
            _db.SaveChanges();
            return "Catalog status updated successfully.";
        }

        public List<GetAllDrugIngredientsResponseDTO> GetAllDrugIngredients(long DrugId)
        {
            List<GetAllDrugIngredientsResponseDTO> response = new List<GetAllDrugIngredientsResponseDTO>();
            List<DG_DrugIngredient> list = _db.DG_DrugIngredients.Where(x => x.DrugId == DrugId && x.IsActive == true).ToList();
            response = _mapper.Map<List<GetAllDrugIngredientsResponseDTO>>(list);
            return response;
        }

        public GetDrugIngredientByIdResponseDTO GetDrugIngredientById(long DrugIngredientId)
        {
            GetDrugIngredientByIdResponseDTO response = new GetDrugIngredientByIdResponseDTO();
            DG_DrugIngredient drugIngredient = _db.DG_DrugIngredients.Where(x => x.DrugIngredientId == DrugIngredientId).FirstOrDefault();
            response = _mapper.Map<GetDrugIngredientByIdResponseDTO>(drugIngredient);
            return response;
        }
        public string SaveDrugIngredient(SaveDrugIngredientRequestDTO request, long UserId)
        {

            try
            {
                DG_DrugIngredient drugIngredient = new DG_DrugIngredient();
                if (request.DrugIngredientId == 0)
                {
                    drugIngredient = _mapper.Map<DG_DrugIngredient>(request);
                    drugIngredient.CreatedBy = UserId;
                    drugIngredient.CreatedDate = DateTime.UtcNow;
                    drugIngredient.IsActive = true;
                    drugIngredient.Status = "Active";
                    _db.DG_DrugIngredients.Add(drugIngredient);
                    _db.SaveChanges();
                    return "Durg Ingredient Added Successfully";

                }
                else
                {
                    drugIngredient = _db.DG_DrugIngredients.Where(x => x.DrugIngredientId == request.DrugIngredientId).FirstOrDefault();
                    _mapper.Map(request, drugIngredient);
                    _db.SaveChanges();
                    return "Durg Ingredient Updated Successfully";

                }
            }
            catch (Exception ex)
            {
                return "Something went wrong. Please try again later.";
            }
        }

        public bool DeleteDrugIngredient(long DrugIngredientId)
        {
            DG_DrugIngredient drugIngredient = _db.DG_DrugIngredients.Where(x => x.DrugIngredientId == DrugIngredientId).FirstOrDefault();
            if (drugIngredient != null)
            {
                drugIngredient.IsActive = false;
                _db.SaveChanges();
                return true;
            }
            else
            {
                return false;
            }
        }

        public GetBundleByIdResponseDTO GetBundleById(GetBundleByIdRequestDTO request)
        {
            GetBundleByIdResponseDTO response = new GetBundleByIdResponseDTO();
            PD_Bundle bundle = _db.PD_Bundles.Where(x => x.ProductId == request.ProductId).FirstOrDefault();
            if (bundle == null) return response;
            response = _mapper.Map<GetBundleByIdResponseDTO>(bundle);
            response.CreatedByName = _db.SYS_UserDetails.Where(x => x.UserId == bundle.CreatedBy).Select(x => x.FirstName + " " + x.LastName).FirstOrDefault();
            response.DrugId = bundle.ActiveDrugId;
            response.DrugVarientsInBundle = _db.PD_DrugVarientsInBundles.Where(x => x.BundleId == bundle.BundleId && x.IsActive == true)
           .Select(x => new SaveDrugVarientsInBundlesDTO
           {
              DrugVarientBundleId = x.DrugVarientBundleId,
              DrugId = x.DrugId,
              Name = x.Name,
              Price = x.Price,
              OrderCount = x.OrderCount,
           }).ToList();
            if (bundle.FacilityId.HasValue && bundle.FacilityId.Value > 0)
                response.FacilityIds = new List<long> { bundle.FacilityId.Value };
            else
                response.FacilityIds = _db.PD_FacilityBundlePrices.AsNoTracking().Where(fbp => fbp.BundleId == bundle.BundleId).Select(fbp => fbp.FacilityId).OrderBy(id => id).ToList();
            return response;
        }

        public GetBundleByIdResponseDTO GetBundleByBundleId(long BundleId)
        {
            GetBundleByIdResponseDTO response = new GetBundleByIdResponseDTO();
            PD_Bundle bundle = _db.PD_Bundles.Where(x => x.BundleId == BundleId).FirstOrDefault();
            if (bundle == null) return response;
            response = _mapper.Map<GetBundleByIdResponseDTO>(bundle);
            response.CreatedByName = _db.SYS_UserDetails.Where(x => x.UserId == bundle.CreatedBy).Select(x => x.FirstName + " " + x.LastName).FirstOrDefault();
            response.DrugId = bundle.ActiveDrugId;
            response.DrugVarientsInBundle = _db.PD_DrugVarientsInBundles.Where(x => x.BundleId == bundle.BundleId && x.IsActive == true)
           .Select(x => new SaveDrugVarientsInBundlesDTO
           {
              DrugVarientBundleId = x.DrugVarientBundleId,
              DrugId = x.DrugId,
              Name = x.Name,
              Price = x.Price,
              OrderCount = x.OrderCount,
           }).ToList();
            if (bundle.FacilityId.HasValue && bundle.FacilityId.Value > 0)
                response.FacilityIds = new List<long> { bundle.FacilityId.Value };
            else
                response.FacilityIds = _db.PD_FacilityBundlePrices.AsNoTracking().Where(fbp => fbp.BundleId == bundle.BundleId).Select(fbp => fbp.FacilityId).OrderBy(id => id).ToList();
            return response;
        }

        public string SaveBundle(SaveBundleRequestDTO request, long UserId, long OrganizationId)
        {

            try
            {
                PD_Bundle bundle = new PD_Bundle();
                Guid guid = Guid.NewGuid();
                if (request.BundleId == 0)
                {
                    SYS_Product product = new SYS_Product();
                    product.ProductId = 0;
                    product.ProductType = "Bundle";
                    product.IsActive = true;
                    product.ProductName = request.Name;
                    product.CreatedDate = DateTime.UtcNow;
                    product.CreatedBy = UserId;
                    product.Guid = guid.ToString();
                    product.Status = "Active";
                    product.OrganizationId = OrganizationId;
                    _db.SYS_Products.Add(product);
                    _db.SaveChanges();

                    bundle = new PD_Bundle();
                    bundle = _mapper.Map<PD_Bundle>(request);
                    bundle.CreatedBy = UserId;
                    bundle.CreatedDate = DateTime.UtcNow;
                    bundle.IsActive = true;
                    bundle.Status = "Active";
                    bundle.ProductId = product.ProductId;

                    var effectiveFacilityIds = (request.FacilityIds != null && request.FacilityIds.Count > 0)
                        ? request.FacilityIds.Where(fid => fid > 0).Distinct().ToList()
                        : (request.FacilityId.HasValue && request.FacilityId.Value > 0 ? new List<long> { request.FacilityId.Value } : null);

                    bundle.FacilityId = (effectiveFacilityIds == null && request.FacilityId.HasValue && request.FacilityId.Value > 0) ? request.FacilityId : null;
                    _db.PD_Bundles.Add(bundle);
                    _db.SaveChanges();

                    _auditService.LogEntityChange(
                        action: "Create",
                        entityType: "PD_Bundle",
                        entityId: bundle.BundleId,
                        newValues: bundle,
                        userId: UserId,
                        description: $"Bundle '{bundle.Name}' created with Price: ${bundle.Price}, Description: {bundle.Description}",
                        module: "Package"
                    );

                    if (effectiveFacilityIds != null && effectiveFacilityIds.Count > 0)
                    {
                        foreach (var fid in effectiveFacilityIds)
                        {
                            if (_db.PD_FacilityBundlePrices.Any(x => x.FacilityId == fid && x.BundleId == bundle.BundleId))
                                continue;
                            _db.PD_FacilityBundlePrices.Add(new PD_FacilityBundlePrice
                            {
                                FacilityId = fid,
                                BundleId = bundle.BundleId,
                                ClinicPrice = request.Price ?? 0,
                                IsRecurring = null,
                                CreatedBy = UserId,
                                CreatedDateUtc = DateTime.UtcNow
                            });
                        }
                        _db.SaveChanges();
                    }

                    return "Bundle Added Successfully";

                }
                else
                {
                    bundle = _db.PD_Bundles.Where(x => x.BundleId == request.BundleId).FirstOrDefault();
                    _mapper.Map(request, bundle);
                    bundle.ActiveDrugId = request.DrugId;

                    var effectiveFacilityIdsUpdate = (request.FacilityIds != null && request.FacilityIds.Count > 0)
                        ? request.FacilityIds.Where(fid => fid > 0).Distinct().ToList()
                        : (request.FacilityId.HasValue && request.FacilityId.Value > 0 ? new List<long> { request.FacilityId.Value } : null);

                    bundle.FacilityId = (effectiveFacilityIdsUpdate == null && request.FacilityId.HasValue && request.FacilityId.Value > 0) ? request.FacilityId : null;
                    _db.SaveChanges();

                    SYS_Product product = _db.SYS_Products.Where(x => x.ProductId == bundle.ProductId && x.ProductName != request.Name).FirstOrDefault();
                    if (product != null)
                    {
                        product.ProductName = request.Name;
                    }

                    if (request.DrugVarientsInBundle.Count != 0)
                    {
                        List<PD_DrugVarientsInBundle> drugsInBundle = _db.PD_DrugVarientsInBundles.Where(x => x.BundleId == bundle.BundleId).ToList();
                        if (drugsInBundle.Count != 0)
                        {
                            _db.PD_DrugVarientsInBundles.RemoveRange(drugsInBundle);
                        }
                        foreach (var drugs in request.DrugVarientsInBundle)
                        {
                            PD_DrugVarientsInBundle newItem = new PD_DrugVarientsInBundle
                            {
                                DrugVarientBundleId = 0,
                                DrugId = request.DrugId,
                                BundleId = request.BundleId,
                                Name = drugs.Name,
                                Price = drugs.Price,
                                OrderCount = drugs.OrderCount,
                                IsActive = true,
                                CreatedBy = UserId,
                                CreatedDate = DateTime.UtcNow,
                            };
                            _db.PD_DrugVarientsInBundles.Add(newItem);
                        }

                        _db.SaveChanges();
                    }

                    _db.SaveChanges();

                    _auditService.LogEntityChange(
                        action: "Update",
                        entityType: "PD_Bundle",
                        entityId: bundle.BundleId,
                        newValues: bundle,
                        userId: UserId,
                        description: $"Bundle '{bundle.Name}' updated - Price: ${bundle.Price}, Description: {bundle.Description}, Image: {bundle.RegularImageURL}",
                        module: "Package"
                    );

                    if (effectiveFacilityIdsUpdate != null && effectiveFacilityIdsUpdate.Count > 0)
                    {
                        var existingFbps = _db.PD_FacilityBundlePrices.Where(x => x.BundleId == bundle.BundleId).ToList();
                        foreach (var row in existingFbps.Where(x => !effectiveFacilityIdsUpdate.Contains(x.FacilityId)))
                        {
                            _db.PD_FacilityBundlePrices.Remove(row);
                        }
                        foreach (var fid in effectiveFacilityIdsUpdate)
                        {
                            var existingFbp = _db.PD_FacilityBundlePrices
                                .FirstOrDefault(x => x.FacilityId == fid && x.BundleId == bundle.BundleId);
                            if (existingFbp == null)
                            {
                                _db.PD_FacilityBundlePrices.Add(new PD_FacilityBundlePrice
                                {
                                    FacilityId = fid,
                                    BundleId = bundle.BundleId,
                                    ClinicPrice = request.Price ?? bundle.Price ?? 0,
                                    IsRecurring = null,
                                    CreatedBy = UserId,
                                    CreatedDateUtc = DateTime.UtcNow
                                });
                            }
                            else
                            {
                                existingFbp.ClinicPrice = request.Price ?? bundle.Price ?? existingFbp.ClinicPrice;
                                existingFbp.ModifiedBy = UserId;
                                existingFbp.ModifiedDateUtc = DateTime.UtcNow;
                            }
                        }
                        _db.SaveChanges();
                    }

                    return "Bundle Updated Successfully";
                }
            }
            catch (Exception ex)
            {
                return "Something went wrong. Please try again later.";
            }
        }

        public List<GetAllDrugsInBundlesResponseDTO> GetAllDrugsInBundles(long BundleId)
        {
            List<GetAllDrugsInBundlesResponseDTO> response = new List<GetAllDrugsInBundlesResponseDTO>();
            // The Where runs on the server; the grouping is done here. EF Core cannot
            // translate a GroupBy whose result selector projects a whole entity
            // (Select(x => x.First())), and UPGRADE-NET8.md flagged this query as
            // unsettled under EF Core 8. The row set is already narrowed to one
            // bundle's active variants, so materialising it first costs nothing.
            List<PD_DrugVarientsInBundle> list = _db.PD_DrugVarientsInBundles.Where(x => x.BundleId == BundleId && x.IsActive == true).ToList().GroupBy(x => x.DrugId).Select(x => x.First()).ToList();
            foreach (var item in list)
            {
                GetAllDrugsInBundlesResponseDTO drug = new GetAllDrugsInBundlesResponseDTO
                {
                    DrugId = item.DrugId,
                    DrugName = _db.PD_Drugs.Where(x => x.DrugId == item.DrugId).Select(x => x.Name).FirstOrDefault(),
                };
                response.Add(drug);
            }
            return response;
        }

        public string SaveDrugInBundle(SaveDrugVarientsInBundleRequestDTO request, long UserId)
        {

            try
            {
                if (request.DrugVarientBundleId == 0 || request.DrugVarientBundleId == null)
                {
                    PD_Drug drug = _db.PD_Drugs.Where(x => x.ProductId == request.ProductId).FirstOrDefault();
                    PD_DrugVarientsInBundle drugVarientsInBundle = new PD_DrugVarientsInBundle();
                    drugVarientsInBundle.DrugVarientBundleId = 0;
                    drugVarientsInBundle.BundleId = request.BundleId;
                    drugVarientsInBundle.DrugId = drug.DrugId;
                    drugVarientsInBundle.Price = drug.Price;
                    drugVarientsInBundle.Name = drug.Name;
                    drugVarientsInBundle.OrderCount = 0;
                    drugVarientsInBundle.CreatedBy = UserId;
                    drugVarientsInBundle.CreatedDate = DateTime.UtcNow;
                    drugVarientsInBundle.IsActive = true;
                    _db.PD_DrugVarientsInBundles.Add(drugVarientsInBundle);
                    _db.SaveChanges();
                    return "Drug Added In Bundle Successfully.";
                }
                else
                {
                    PD_DrugVarientsInBundle drugVarientsInBundle = _db.PD_DrugVarientsInBundles.Where(x => x.DrugVarientBundleId == request.DrugVarientBundleId).FirstOrDefault();
                    drugVarientsInBundle.Price = request.Price;
                    drugVarientsInBundle.OrderCount = request.OrderCount;
                    _db.SaveChanges();
                    return "Drug Updated In Bundle Successfully.";
                }
            }
            catch (Exception ex)
            {
                return "Something went wrong. Please try again later.";
            }
        }

        public bool DeleteDrugVarientFromBundle(long DrugId)
        {
            PD_DrugVarientsInBundle drugVarientBundle = _db.PD_DrugVarientsInBundles.Where(x => x.DrugId == DrugId).FirstOrDefault();
            if (drugVarientBundle != null)
            {
                _db.PD_DrugVarientsInBundles.Remove(drugVarientBundle);
                _db.SaveChanges();
                return true;
            }
            else
            {
                return false;
            }
        }

        #region ClinicToPatient

        public async Task<ClinicToPatientCreateResultDTO> CreateClinicToPatientAsync( ClinicToPatientCreateDTO dto,long? userId,CancellationToken ct = default)
        {
            var nowUtc = DateTime.UtcNow;

            if (dto is null)
                return Fail("Request is required.");

            if (dto.DrugId is null || dto.DrugId <= 0)
                return Fail("DrugId is required.");

            if (dto.FacilityId is null || dto.FacilityId <= 0)
                return Fail("FacilityId is required.");

            if (dto.ClinicSuggestedRetailPrice is null || dto.ClinicSuggestedRetailPrice < 0)
                return Fail("ClinicSuggestedRetailPrice must be a non-negative number.");

            var drugExists = await _db.PD_Drugs
                .AsNoTracking()
                .AnyAsync(d => d.DrugId == dto.DrugId, ct);

            if (!drugExists)
                return Fail("Drug not found.");

            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            try
            {

                var existingActives = await _db.PC_CLINICTOPATIENTs
                    .Where(x => x.FacilityId == dto.FacilityId
                             && x.DrugId == dto.DrugId
                             && x.IsActive == true)
                    .ToListAsync(ct);

                foreach (var row in existingActives)
                {
                    row.IsActive = false;
                    row.ModifiedBy = userId;
                    row.ModifiedAt = nowUtc;
                }

                if (existingActives.Count > 0)
                    await _db.SaveChangesAsync(ct);

                var oldPrice = existingActives.FirstOrDefault()?.ClinicSuggestedRetailPrice;
                var isUpdate = existingActives.Count > 0;

                var entity = new PC_CLINICTOPATIENT
                {
                    DrugId = (long)dto.DrugId,
                    FacilityId = (long)dto.FacilityId,
                    GAtoClinicId = dto.GAtoClinicId,
                    ClinicSuggestedRetailPrice = (decimal)dto.ClinicSuggestedRetailPrice,
                    IsActive = true,
                    CreatedBy = userId,
                    CreatedAt = nowUtc
                };

                _db.PC_CLINICTOPATIENTs.Add(entity);
                await _db.SaveChangesAsync(ct);

                await tx.CommitAsync(ct);

                var drugName = await _db.PD_Drugs
                    .AsNoTracking()
                    .Where(d => d.DrugId == dto.DrugId)
                    .Select(d => d.Name)
                    .FirstOrDefaultAsync(ct);

                var facilityName = await _db.SYS_Facilities
                    .AsNoTracking()
                    .Where(f => f.FacilityId == dto.FacilityId)
                    .Select(f => f.TitleShort ?? f.TitleLong)
                    .FirstOrDefaultAsync(ct);

                await _auditService.LogEntityChangeAsync(
                    action: isUpdate ? "Update" : "Create",
                    entityType: "PC_CLINICTOPATIENT",
                    entityId: entity.ClinicToPatientId,
                    oldValues: isUpdate ? new { ClinicSuggestedRetailPrice = oldPrice } : null,
                    newValues: new { ClinicSuggestedRetailPrice = dto.ClinicSuggestedRetailPrice, FacilityId = dto.FacilityId, DrugId = dto.DrugId },
                    userId: userId,
                    facilityId: dto.FacilityId,
                    description: $"Clinic drug price {(isUpdate ? "updated" : "set")} for Drug '{drugName}' (ID: {dto.DrugId}) at Facility '{facilityName}' (ID: {dto.FacilityId}) - {(isUpdate ? $"Old Price: ${oldPrice}, " : "")}New Price: ${dto.ClinicSuggestedRetailPrice}",
                    module: "Pricing"
                );

                return new ClinicToPatientCreateResultDTO
                {
                    Success = true,
                    ClinicToPatientId = entity.ClinicToPatientId,
                    Message = "Clinic-to-patient price created."
                };
            }
            catch (DbUpdateException)
            {
                await tx.RollbackAsync(ct);
                return Fail("An active price already exists for this Facility and Drug.");
            }
            catch
            {
                await tx.RollbackAsync(ct);
                return Fail("Something went wrong. Please try again later.");
            }
        }

        private static ClinicToPatientCreateResultDTO Fail(string message) =>
            new ClinicToPatientCreateResultDTO { Success = false, Message = message };

        public List<GetAllDrugsWithPricingResponseDTO> GetAllDrugsWithPricing(GetAllProductsRequestDTO request, out int totalDrugCount)
        {
            int pageNumber = (request?.PageNumber > 0) ? request.PageNumber : 1;
            int pageSize = (request?.PageSize > 0) ? request.PageSize : 10;
            var catalogId = (request?.CatalogId.HasValue == true && request.CatalogId.Value > 0)
                ? request.CatalogId.Value
                : EmpowerCatalogId;

            if (request?.FacilityId.HasValue == true && request.FacilityId.Value > 0)
            {
                var facilityId = request.FacilityId.Value;
                var isCatalogAssigned = _db.PC_CATALOGFACILITYASSIGNMENTs.AsNoTracking()
                    .Any(a => a.CatalogId == catalogId && a.FacilityId == facilityId && a.IsActive == true);

                if (!isCatalogAssigned)
                {
                    totalDrugCount = 0;
                    return new List<GetAllDrugsWithPricingResponseDTO>();
                }
            }

            IQueryable<PD_Drug> query = _db.PD_Drugs.AsNoTracking()
                .Where(d => d.IsActive == true && d.CatalogId == catalogId);

            if (request?.FacilityId.HasValue == true && request.FacilityId.Value > 0)
            {
                var facilityId = request.FacilityId.Value;
                var excludedDrugIds = _db.PC_DRUGFACILITYEXCLUSIONS
                    .AsNoTracking()
                    .Where(x => x.FacilityId == facilityId && x.IsActive == true)
                    .Select(x => x.DrugId)
                    .Distinct()
                    .ToList();

                if (excludedDrugIds.Count > 0)
                    query = query.Where(d => !excludedDrugIds.Contains(d.DrugId));
            }

            if (!string.IsNullOrWhiteSpace(request?.Title))
            {
                string like = $"%{request.Title.Trim()}%";
                query = query.Where(d =>
                    EF.Functions.Like(d.Name ?? "", like) ||
                    EF.Functions.Like(d.BrandName ?? "", like) ||
                    EF.Functions.Like(d.GenericName ?? "", like)
                );
            }

            if (!string.IsNullOrWhiteSpace(request?.Status))
            {
                query = query.Where(d => d.Status == request.Status.Trim());
            }

            if (request?.ControlSubstance.HasValue == true)
            {
                query = query.Where(d =>
                    (d.ControlSubstance ?? d.Control_Substance) == request.ControlSubstance.Value);
            }

            totalDrugCount = query.Count();

            var pageDrugs = query
                .OrderBy(d => d.Name)
                .ThenBy(d => d.DrugId)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(d => new
                {
                    d.DrugId,
                    d.CatalogId,
                    d.PackageSize,
                    d.Markup,
                    ControlSubstance = d.ControlSubstance ?? d.Control_Substance,
                    d.DosageForm,
                    d.Name,
                    d.Strenght,
                    d.Status,
                    d.ItemDesignatorID,
                    d.Refrigerated,
                    d.IsCustom
                })
                .ToList();

            if (pageDrugs.Count == 0)
                return new List<GetAllDrugsWithPricingResponseDTO>();

            var drugIds = pageDrugs.Select(x => x.DrugId).ToList();
            var catalogIds = pageDrugs.Select(x => x.CatalogId).Distinct().ToList();
            var catalogNames = _db.PD_Catalogs.AsNoTracking()
                .Where(c => c.IsActive == true && catalogIds.Contains(c.CatalogId))
                .ToDictionary(c => c.CatalogId, c => c.CatalogName);

            var ptgAll = _db.PC_PHARMTOGLOBALs
                .AsNoTracking()
                .Where(p => drugIds.Contains(p.DrugId ?? 0) && p.IsActive == true)
                .ToList();

            var latestPtgByDrug = ptgAll
                .GroupBy(p => p.DrugId ?? 0)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.PharmToGlobalId).First());

            var ptgIds = latestPtgByDrug.Values.Select(p => p.PharmToGlobalId).ToList();

            var gacAll = ptgIds.Count > 0
                ? _db.PC_GATOCLINICs
                    .AsNoTracking()
                    .Where(g => ptgIds.Contains(g.PharmToGlobalId) && g.IsActive == true)
                    .ToList()
                : new List<PC_GATOCLINIC>();

            var latestGacByPtgId = gacAll
                .GroupBy(g => g.PharmToGlobalId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.GAtoClinicId).First());

            Dictionary<long, PC_CLINICTOPATIENT>? latestCtpByDrug = null;
            if (request.FacilityId.HasValue)
            {
                var ctpAll = _db.PC_CLINICTOPATIENTs
                    .AsNoTracking()
                    .Where(c => drugIds.Contains(c.DrugId) && c.FacilityId == request.FacilityId.Value && c.IsActive == true)
                    .ToList();

                latestCtpByDrug = ctpAll
                    .GroupBy(c => c.DrugId)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.ClinicToPatientId).First());
            }

            var result = new List<GetAllDrugsWithPricingResponseDTO>(pageDrugs.Count);
            foreach (var d in pageDrugs)
            {
                latestPtgByDrug.TryGetValue(d.DrugId, out var ptg);
                PC_GATOCLINIC? gac = null;
                if (ptg != null)
                    latestGacByPtgId.TryGetValue(ptg.PharmToGlobalId, out gac);

                PC_CLINICTOPATIENT? ctp = null;
                if (latestCtpByDrug != null)
                    latestCtpByDrug.TryGetValue(d.DrugId, out ctp);

                result.Add(new GetAllDrugsWithPricingResponseDTO
                {
                    DrugId = d.DrugId,
                    CatalogId = d.CatalogId,
                    CatalogName = catalogNames.TryGetValue(d.CatalogId, out var catalogName) ? catalogName : null,
                    PackageSize = d.PackageSize,
                    Markup = d.Markup,
                    ControlSubstance = d.ControlSubstance,
                    DosageForm = d.DosageForm,
                    Name = d.Name,
                    Strenght = d.Strenght,
                    Status = d.Status,
                    ItemDesignatorID = d.ItemDesignatorID,
                    Refrigerated = d.Refrigerated,
                    WholesalePrice = ptg?.WholesalePrice,
                    SuggestedRetail = gac != null ? gac.SuggestedRetailPrice : null,
                    ClinicSuggestedRetailPrice = ctp != null ? ctp.ClinicSuggestedRetailPrice : null,
                    GAtoClinicId = ctp?.GAtoClinicId,
                    MarkupType = ptg?.MarkupType
                    ,
                    IsCustom = d.IsCustom
                });
            }

            return result;
        }

        public List<GetAllDrugsWithPricingResponseDTO> GetAllCustomDrugsWithPricing(GetAllProductsRequestDTO request, out int totalDrugCount)
        {
            int pageNumber = (request?.PageNumber > 0) ? request.PageNumber : 1;
            int pageSize = (request?.PageSize > 0) ? request.PageSize : 10;

            IQueryable<PD_Drug> query = _db.PD_Drugs.AsNoTracking()
                .Where(d => d.IsActive == true && d.IsCustom == true);
            query = ApplyCatalogFilters(query, request?.CatalogId);
            query = ApplyCatalogFacilityVisibility(query, request?.FacilityId);

            if (request?.FacilityId.HasValue == true && request.FacilityId.Value > 0)
            {
                var facilityId = request.FacilityId.Value;
                var excludedDrugIds = _db.PC_DRUGFACILITYEXCLUSIONS
                    .AsNoTracking()
                    .Where(x => x.FacilityId == facilityId && x.IsActive == true)
                    .Select(x => x.DrugId)
                    .Distinct()
                    .ToList();

                if (excludedDrugIds.Count > 0)
                    query = query.Where(d => !excludedDrugIds.Contains(d.DrugId));
            }

            if (!string.IsNullOrWhiteSpace(request?.Title))
            {
                string like = $"%{request.Title.Trim()}%";
                query = query.Where(d =>
                    EF.Functions.Like(d.Name ?? "", like) ||
                    EF.Functions.Like(d.BrandName ?? "", like) ||
                    EF.Functions.Like(d.GenericName ?? "", like)
                );
            }

            if (!string.IsNullOrWhiteSpace(request?.Status))
            {
                query = query.Where(d => d.Status == request.Status.Trim());
            }

            totalDrugCount = query.Count();

            var pageDrugs = query
                .OrderBy(d => d.Name)
                .ThenBy(d => d.DrugId)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(d => new
                {
                    d.DrugId,
                    d.CatalogId,
                    d.PackageSize,
                    d.Markup,
                    ControlSubstance = d.ControlSubstance ?? d.Control_Substance,
                    d.DosageForm,
                    d.Name,
                    d.Strenght,
                    d.Status,
                    d.ItemDesignatorID,
                    d.Refrigerated,
                    d.IsCustom
                })
                .ToList();

            if (pageDrugs.Count == 0)
                return new List<GetAllDrugsWithPricingResponseDTO>();

            var drugIds = pageDrugs.Select(x => x.DrugId).ToList();
            var catalogIds = pageDrugs.Select(x => x.CatalogId).Distinct().ToList();
            var catalogNames = _db.PD_Catalogs.AsNoTracking()
                .Where(c => c.IsActive == true && catalogIds.Contains(c.CatalogId))
                .ToDictionary(c => c.CatalogId, c => c.CatalogName);

            var ptgAll = _db.PC_PHARMTOGLOBALs
                .AsNoTracking()
                .Where(p => drugIds.Contains(p.DrugId ?? 0) && p.IsActive == true)
                .ToList();

            var latestPtgByDrug = ptgAll
                .GroupBy(p => p.DrugId ?? 0)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.PharmToGlobalId).First());

            var ptgIds = latestPtgByDrug.Values.Select(p => p.PharmToGlobalId).ToList();

            var gacAll = ptgIds.Count > 0
                ? _db.PC_GATOCLINICs
                    .AsNoTracking()
                    .Where(g => ptgIds.Contains(g.PharmToGlobalId) && g.IsActive == true)
                    .ToList()
                : new List<PC_GATOCLINIC>();

            var latestGacByPtgId = gacAll
                .GroupBy(g => g.PharmToGlobalId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.GAtoClinicId).First());

            Dictionary<long, PC_CLINICTOPATIENT>? latestCtpByDrug = null;
            if (request.FacilityId.HasValue)
            {
                var ctpAll = _db.PC_CLINICTOPATIENTs
                    .AsNoTracking()
                    .Where(c => drugIds.Contains(c.DrugId) && c.FacilityId == request.FacilityId.Value && c.IsActive == true)
                    .ToList();

                latestCtpByDrug = ctpAll
                    .GroupBy(c => c.DrugId)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.ClinicToPatientId).First());
            }

            var result = new List<GetAllDrugsWithPricingResponseDTO>(pageDrugs.Count);
            foreach (var d in pageDrugs)
            {
                latestPtgByDrug.TryGetValue(d.DrugId, out var ptg);
                PC_GATOCLINIC? gac = null;
                if (ptg != null)
                    latestGacByPtgId.TryGetValue(ptg.PharmToGlobalId, out gac);

                PC_CLINICTOPATIENT? ctp = null;
                if (latestCtpByDrug != null)
                    latestCtpByDrug.TryGetValue(d.DrugId, out ctp);

                result.Add(new GetAllDrugsWithPricingResponseDTO
                {
                    DrugId = d.DrugId,
                    CatalogId = d.CatalogId,
                    CatalogName = catalogNames.TryGetValue(d.CatalogId, out var catalogName) ? catalogName : null,
                    PackageSize = d.PackageSize,
                    Markup = d.Markup,
                    ControlSubstance = d.ControlSubstance,
                    DosageForm = d.DosageForm,
                    Name = d.Name,
                    Strenght = d.Strenght,
                    Status = d.Status,
                    ItemDesignatorID = d.ItemDesignatorID,
                    Refrigerated = d.Refrigerated,
                    WholesalePrice = ptg?.WholesalePrice,
                    SuggestedRetail = gac != null ? gac.SuggestedRetailPrice : null,
                    ClinicSuggestedRetailPrice = ctp != null ? ctp.ClinicSuggestedRetailPrice : null,
                    GAtoClinicId = ctp?.GAtoClinicId,
                    MarkupType = ptg?.MarkupType
                    ,
                    IsCustom = d.IsCustom
                });
            }

            return result;
        }

        public GetDrugByIdResponse2DTO GetDrugById(long drugId, long? facilityId)
        {

            var drug = _db.PD_Drugs
                .AsNoTracking()
                .Where(d => d.DrugId == drugId)
                .Select(d => new
                {
                    d.DrugId,
                    d.ProductId,
                    d.CatalogId,
                    d.CategoryId,
                    d.PharmacyId,
                    d.Type,
                    d.Name,
                    d.BrandName,
                    d.GenericName,
                    d.DosageForm,
                    d.Strenght,
                    d.PackageSize,
                    d.Quantity,
                    d.QuantityUnit,
                    d.Refills,
                    d.ControlSubstance,
                    d.Refrigerated,
                    d.ItemDesignatorID,
                    d.Status,
                    d.IsCustom
                })
                .FirstOrDefault();

            if (drug == null)
                return null!;

            var catalogName = _db.PD_Catalogs.AsNoTracking()
                .Where(c => c.CatalogId == drug.CatalogId && c.IsActive == true)
                .Select(c => c.CatalogName)
                .FirstOrDefault();

            var ptg = _db.PC_PHARMTOGLOBALs
                .AsNoTracking()
                .Where(p => p.DrugId == drugId && p.IsActive == true)
                .OrderByDescending(p => p.PharmToGlobalId)
                .FirstOrDefault();

            PC_GATOCLINIC? gac = null;
            if (ptg != null)
            {
                gac = _db.PC_GATOCLINICs
                    .AsNoTracking()
                    .Where(g => g.PharmToGlobalId == ptg.PharmToGlobalId && g.IsActive == true)
                    .OrderByDescending(g => g.GAtoClinicId)
                    .FirstOrDefault();
            }

            PC_CLINICTOPATIENT? ctp = null;
            if (facilityId.HasValue)
            {
                ctp = _db.PC_CLINICTOPATIENTs
                    .AsNoTracking()
                    .Where(c => c.DrugId == drugId && c.FacilityId == facilityId.Value && c.IsActive == true)
                    .OrderByDescending(c => c.ClinicToPatientId)
                    .FirstOrDefault();
            }

            return new GetDrugByIdResponse2DTO
            {
                DrugId = drug.DrugId,
                ProductId = drug.ProductId,
                CatalogId = drug.CatalogId,
                CatalogName = catalogName,
                CategoryId = drug.CategoryId,
                PharmacyId = drug.PharmacyId,
                Type = drug.Type,
                Name = drug.Name,
                BrandName = drug.BrandName,
                GenericName = drug.GenericName,
                DosageForm = drug.DosageForm,
                Strenght = drug.Strenght,
                PackageSize = drug.PackageSize,
                Quantity = drug.Quantity,
                QuantityUnit = drug.QuantityUnit,
                Refills = drug.Refills,
                ControlSubstance = drug.ControlSubstance,
                Refrigerated = drug.Refrigerated,
                ItemDesignatorID = drug.ItemDesignatorID,
                Status = drug.Status,
                PharmToGlobalId = ptg != null ? ptg.PharmToGlobalId : null,
                PharmacyPrice = ptg?.PharmacyPrice,
                MarkupPercent = ptg?.MarkupPercent,
                WholesalePrice = ptg?.WholesalePrice,
                MarkupType = ptg?.MarkupType,
                GAtoClinicIdGlobal = gac != null ? gac.GAtoClinicId : null,
                SuggestedRetail = gac != null ? gac.SuggestedRetailPrice : null,
                ClinicToPatientId = ctp != null ? ctp.ClinicToPatientId : null,
                GAtoClinicId = ctp?.GAtoClinicId,
                CustomerSuggestedRetailPrice = ctp != null ? ctp.ClinicSuggestedRetailPrice : null,
                IsCustom = drug.IsCustom
            };
        }

        public List<long> GetDrugUnassignedFacilities(long drugId)
        {
            return _db.PC_DRUGFACILITYEXCLUSIONS
                .AsNoTracking()
                .Where(x => x.DrugId == drugId && x.IsActive == true)
                .Select(x => x.FacilityId)
                .Distinct()
                .ToList();
        }

        public bool UnassignDrugFromFacilities(UnassignDrugFromFacilitiesRequestDTO request, long userId)
        {
            if (request == null) return false;

            using var tx = _db.Database.BeginTransaction();
            try
            {
                var nowUtc = DateTime.UtcNow;
                var drugId = request.DrugId;

                var facilityIds = (request.FacilityIds ?? new List<long>())
                    .Where(id => id > 0)
                    .Distinct()
                    .ToList();

                if (facilityIds.Count > 0)
                {
                    facilityIds = _db.SYS_Facilities
                        .AsNoTracking()
                        .Where(f => f.IsActive == true && facilityIds.Contains(f.FacilityId))
                        .Select(f => f.FacilityId)
                        .Distinct()
                        .ToList();
                }

                var existing = _db.PC_DRUGFACILITYEXCLUSIONS
                    .Where(x => x.DrugId == drugId && x.IsActive == true)
                    .ToList();

                foreach (var row in existing)
                {
                    row.IsActive = false;
                    row.ModifiedBy = userId;
                    row.ModifiedAt = nowUtc;
                }

                if (existing.Count > 0)
                    _db.SaveChanges();

                if (facilityIds.Count > 0)
                {
                    var newRows = facilityIds.Select(fid => new PC_DRUGFACILITYEXCLUSION
                    {
                        DrugId = drugId,
                        FacilityId = fid,
                        IsActive = true,
                        CreatedBy = userId,
                        CreatedAt = nowUtc
                    }).ToList();

                    _db.PC_DRUGFACILITYEXCLUSIONS.AddRange(newRows);
                    _db.SaveChanges();
                }

                tx.Commit();
                return true;
            }
            catch
            {
                tx.Rollback();
                return false;
            }
        }

        public string UpdateDrug(UpdateDrugRequestDTO request, long userId, long organizationId)
        {
            try
            {
                var drug = _db.PD_Drugs.FirstOrDefault(x => x.DrugId == request.DrugId);
                if (drug == null) return "Drug not found.";

                drug.CategoryId = request.CategoryId ?? drug.CategoryId;
                drug.Type = request.Type ?? drug.Type;
                drug.Name = request.Name ?? drug.Name;
                drug.BrandName = request.BrandName ?? drug.BrandName;
                drug.GenericName = request.GenericName ?? drug.GenericName;
                drug.DosageForm = request.DosageForm ?? drug.DosageForm;
                drug.Strenght = request.Strenght ?? drug.Strenght;
                drug.PackageSize = request.PackageSize ?? drug.PackageSize;
                drug.Quantity = request.Quantity ?? drug.Quantity;
                drug.QuantityUnit = request.QuantityUnit ?? drug.QuantityUnit;
                drug.Refills = request.Refills ?? drug.Refills;
                drug.ControlSubstance = request.ControlSubstance ?? drug.ControlSubstance;
                drug.Refrigerated = request.Refrigerated ?? drug.Refrigerated;
                drug.ItemDesignatorID = request.ItemDesignatorID ?? drug.ItemDesignatorID;
                drug.Status = request.Status ?? drug.Status;
                drug.ModifiedBy = userId;
                drug.ModifiedDate = DateTime.UtcNow;

                _db.SaveChanges();

                _auditService.LogEntityChange(
                    action: "Update",
                    entityType: "PD_Drug",
                    entityId: drug.DrugId,
                    newValues: drug,
                    userId: userId,
                    description: $"Drug '{drug.Name}' updated - Brand: {drug.BrandName}, Generic: {drug.GenericName}, Status: {drug.Status}",
                    module: "Product"
                );

                return "Drug Updated Successfully";
            }
            catch
            {
                return "Something went wrong. Please try again later.";
            }
        }

        public string DeleteDrug(long drugId, long userId)
        {
            using var tx = _db.Database.BeginTransaction();
            try
            {
                var drug = _db.PD_Drugs.FirstOrDefault(x => x.DrugId == drugId);
                if (drug == null) return "Drug not found.";

                drug.IsActive = false;
                drug.Status = "Inactive";
                drug.ModifiedBy = userId;
                drug.ModifiedDate = DateTime.UtcNow;

                var ptgs = _db.PC_PHARMTOGLOBALs.Where(p => p.DrugId == drugId && p.IsActive == true).ToList();
                foreach (var p in ptgs) { p.IsActive = false; p.ModifiedBy = userId; p.ModifiedAt = DateTime.UtcNow; }

                var ptgIds = ptgs.Select(p => p.PharmToGlobalId).ToList();
                if (ptgIds.Count > 0)
                {
                    var gacs = _db.PC_GATOCLINICs.Where(g => g.IsActive == true && ptgIds.Contains(g.PharmToGlobalId)).ToList();
                    foreach (var g in gacs) { g.IsActive = false; g.ModifiedBy = userId; g.ModifiedAt = DateTime.UtcNow; }
                }

                var ctps = _db.PC_CLINICTOPATIENTs.Where(c => c.IsActive == true && c.DrugId == drugId).ToList();
                foreach (var c in ctps) { c.IsActive = false; c.ModifiedBy = userId; c.ModifiedAt = DateTime.UtcNow; }

                _db.SaveChanges();
                tx.Commit();

                _auditService.LogEntityChange(
                    action: "Delete",
                    entityType: "PD_Drug",
                    entityId: drug.DrugId,
                    oldValues: drug,
                    userId: userId,
                    description: $"Drug '{drug.Name}' deleted",
                    module: "Product"
                );

                return "Drug Deleted Successfully";
            }
            catch
            {
                tx.Rollback();
                return "Something went wrong. Please try again later.";
            }
        }

        public GetBundleByIdResponse2DTO GetBundleById(long bundleId)
        {
            var dto = _db.PD_Bundles.AsNoTracking()
                .Where(b => b.BundleId == bundleId)
                .Select(b => new GetBundleByIdResponse2DTO
                {
                    BundleId = b.BundleId,
                    ProductId = b.ProductId,
                    CategoryId = b.CategoryId,
                    Name = b.Name,
                    Description = b.Description,
                    RegularImageURL = b.RegularImageURL,
                    Price = b.Price,
                    ComparePrice = b.ComparePrice,
                    Status = b.Status,
                    Visits = b.visits
                })
                .FirstOrDefault();
            if (dto != null)
            {
                if (dto.CategoryId.HasValue && dto.CategoryId.Value > 0)
                    dto.CategoryName = _db.PD_Categories.AsNoTracking().Where(c => c.CategoryId == dto.CategoryId.Value).Select(c => c.CategoryName).FirstOrDefault();
                var bundleRow = _db.PD_Bundles.AsNoTracking().Where(b => b.BundleId == bundleId).Select(b => new { b.FacilityId }).FirstOrDefault();
                if (bundleRow?.FacilityId != null && bundleRow.FacilityId.Value > 0)
                    dto.FacilityIds = new List<long> { bundleRow.FacilityId.Value };
                else
                    dto.FacilityIds = _db.PD_FacilityBundlePrices.AsNoTracking().Where(fbp => fbp.BundleId == bundleId).Select(fbp => fbp.FacilityId).OrderBy(id => id).ToList();
            }
            return dto!;
        }

        public string UpdateBundle(UpdateBundleRequestDTO request, long userId, long organizationId)
        {
            try
            {
                var bundle = _db.PD_Bundles.FirstOrDefault(x => x.BundleId == request.BundleId);
                if (bundle == null) return "Bundle not found.";

                bundle.CategoryId = request.CategoryId ?? bundle.CategoryId;
                bundle.Name = request.Name ?? bundle.Name;
                bundle.Description = request.Description ?? bundle.Description;
                bundle.RegularImageURL = request.RegularImageURL ?? bundle.RegularImageURL;
                bundle.Price = request.Price ?? bundle.Price;
                bundle.ComparePrice = request.ComparePrice ?? bundle.ComparePrice;
                bundle.Status = request.Status ?? bundle.Status;
                bundle.ModifiedBy = userId;
                bundle.ModifiedDate = DateTime.UtcNow;

                if (!string.IsNullOrWhiteSpace(request.Name) && bundle.ProductId.HasValue)
                {
                    var product = _db.SYS_Products.FirstOrDefault(p => p.ProductId == bundle.ProductId.Value);
                    if (product != null && product.ProductName != request.Name)
                        product.ProductName = request.Name;
                }

                _db.SaveChanges();
                return "Bundle Updated Successfully";
            }
            catch
            {
                return "Something went wrong. Please try again later.";
            }
        }

        public string DeleteBundle(long bundleId, long userId)
        {
            try
            {
                var bundle = _db.PD_Bundles.FirstOrDefault(x => x.BundleId == bundleId);
                if (bundle == null) return "Bundle not found.";

                bundle.IsActive = false;
                bundle.Status = "Inactive";
                bundle.ModifiedBy = userId;
                bundle.ModifiedDate = DateTime.UtcNow;

                _db.SaveChanges();

                _auditService.LogEntityChange(
                    action: "Delete",
                    entityType: "PD_Bundle",
                    entityId: bundle.BundleId,
                    oldValues: bundle,
                    userId: userId,
                    description: $"Bundle '{bundle.Name}' deleted",
                    module: "Package"
                );

                return "Bundle Deleted Successfully";
            }
            catch
            {
                return "Something went wrong. Please try again later.";
            }
        }

        public List<GetAllBundleByFacilityResponseDTO> GetAllBundlesByFacility(GetAllBundlesByFacilityRequestDTO request, out int totalBundleCount)
        {
            int pageNumber = (request?.PageNumber > 0) ? request.PageNumber : 1;
            int pageSize = (request?.PageSize > 0) ? request.PageSize : 10;

            var baseQuery = _db.PD_Bundles.Include(x=>x.PD_FacilityBundlePrices)
                .AsNoTracking()
                .Where(b => b.IsActive == true);

            baseQuery = baseQuery.Where(b =>
                (b.FacilityId.HasValue && b.FacilityId == request.FacilityId)
                || _db.PD_FacilityBundlePrices.AsNoTracking().Any(fbp => fbp.FacilityId == request.FacilityId && fbp.BundleId == b.BundleId)
            );

            baseQuery = baseQuery.Where(b =>
                b.CategoryId == null
                || _db.PD_FacilityCategories.AsNoTracking().Any(fc => fc.FacilityId == request.FacilityId && fc.CategoryId == b.CategoryId && fc.IsActive == true)
            );

            if (!string.IsNullOrWhiteSpace(request?.Title))
            {
                string like = $"%{request.Title.Trim()}%";
                baseQuery = baseQuery.Where(b =>
                    EF.Functions.Like(b.Name ?? "", like)
                );
            }

            if (!string.IsNullOrWhiteSpace(request?.Status))
            {
                string status = request.Status.Trim();
                baseQuery = baseQuery.Where(b => b.Status == status);
            }

            if (request?.CategoryId.HasValue == true && request.CategoryId.Value > 0)
            {
                baseQuery = baseQuery.Where(b => b.CategoryId == request.CategoryId);
            }

            var query =
                from b in baseQuery
                join fp in _db.PD_FacilityBundlePrices
                              .AsNoTracking()
                              .Where(x => x.FacilityId == request.FacilityId)
                    on b.BundleId equals fp.BundleId into gj
                from p in gj.DefaultIfEmpty()
                join c in _db.PD_Categories.AsNoTracking() on b.CategoryId equals c.CategoryId into catGroup
                from c in catGroup.DefaultIfEmpty()
                select new GetAllBundleByFacilityResponseDTO
                {
                    BundleId = b.BundleId,
                    Name = b.Name,
                    CategoryId = b.CategoryId,
                    CategoryName = c != null ? c.CategoryName : null,

                    Price = b.Price,
                    ComparePrice = b.ComparePrice,

                    ClinicPrice = (p != null && p.ClinicPrice != null) ? p.ClinicPrice : b.Price,

                    Description = b.Description,
                    RegularImageUrl = b.RegularImageURL,
                    Status = b.Status,

                    Duration = b.visits,
                    IsRecurring = p != null ? p.IsRecurring : null
                };

            totalBundleCount = query.Count();

            var result = query
                .OrderBy(x => x.Name)
                .ThenBy(x => x.BundleId)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return result;
        }

        public async Task<bool> EditClinicBundlePrice(EditClinicBundlePriceRequestDTO request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.FacilityId <= 0 || request.BundleId <= 0)
                throw new ArgumentException("FacilityId and BundleId must be > 0.");
            if (request.ClinicPrice < 0)
                throw new ArgumentException("ClinicPrice must be >= 0.");

            if (!_db.PD_Bundles.AsNoTracking().Any(b => b.BundleId == request.BundleId))
                throw new InvalidOperationException("Bundle not found.");
            if (!_db.SYS_Facilities!.AsNoTracking().Any(f => f.FacilityId == request.FacilityId))
                throw new InvalidOperationException("Facility not found.");

            var row = await _db.PD_FacilityBundlePrices
                .FirstOrDefaultAsync(x => x.FacilityId == request.FacilityId && x.BundleId == request.BundleId);

            var oldPrice = row?.ClinicPrice;
            var oldIsRecurring = row?.IsRecurring;
            var isUpdate = row != null;

            if (row == null)
            {
                row = new PD_FacilityBundlePrice
                {
                    FacilityId = request.FacilityId,
                    BundleId = request.BundleId,
                    ClinicPrice = request.ClinicPrice,
                    IsRecurring = request.IsRecurring,
                    CreatedBy = request.UserId,
                    CreatedDateUtc = DateTime.UtcNow
                };
                _db.PD_FacilityBundlePrices.Add(row);
            }
            else
            {
                row.ClinicPrice = request.ClinicPrice;

                if (request.IsRecurring.HasValue)
                {
                    row.IsRecurring = request.IsRecurring.Value;
                }
                row.ModifiedBy = request.UserId;
                row.ModifiedDateUtc = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();

            try
            {
                if (request.IsRecurring.HasValue && oldIsRecurring != request.IsRecurring.Value)
                {
                    var newRecurringValue = request.IsRecurring.Value;
                    var facilityIdAsInt = (int)request.FacilityId;

                    var candidateTreatments = await _db.PT_PatientTreatments
                        .Where(t => t.FacilityId == facilityIdAsInt &&
                                    t.ProductId == request.BundleId &&
                                    (t.IsActive == true || t.IsActive == null) &&
                                    t.Status != "Cancelled" &&
                                    t.Status != "Completed")
                        .ToListAsync();

                    var mutated = new List<PT_PatientTreatment>();
                    var nowUtc = DateTime.UtcNow;

                    foreach (var t in candidateTreatments)
                    {
                        if (newRecurringValue == false)
                        {

                            if (t.IsRecurring == true)
                            {
                                t.IsRecurring = false;
                                t.ModifiedDate = nowUtc;
                                t.ModifiedBy = request.UserId;
                                mutated.Add(t);
                            }
                        }
                        else
                        {

                            if (t.IsRecurring != true && (t.RecurringDurationMonths ?? 0) > 0)
                            {
                                t.IsRecurring = true;
                                t.RecurringStartDate = nowUtc;
                                t.NextRecurringPaymentDate = nowUtc.AddMonths(t.RecurringDurationMonths!.Value);
                                t.RecurringPausedDate = null;
                                t.ModifiedDate = nowUtc;
                                t.ModifiedBy = request.UserId;
                                mutated.Add(t);
                            }
                        }
                    }

                    if (mutated.Count > 0)
                    {
                        await _db.SaveChangesAsync();
                    }

                    if (_notificationService != null)
                    {
                        foreach (var t in mutated)
                        {
                            if (!t.PatientId.HasValue) continue;
                            try
                            {
                                await _notificationService.SendBundleRecurringStatusChangeAsync(
                                    patientId: t.PatientId.Value,
                                    treatmentId: t.PatientTreatmentId,
                                    bundleId: request.BundleId,
                                    isNowEnabled: newRecurringValue,
                                    ct: default);
                            }
                            catch
                            {

                            }
                        }
                    }
                }
            }
            catch (Exception cascadeEx)
            {

                try
                {
                    await _auditService.LogEntityChangeAsync(
                        action: "Update",
                        entityType: "PD_FacilityBundlePrice",
                        entityId: row.FacilityBundlePriceId,
                        oldValues: null,
                        newValues: new { Error = cascadeEx.Message },
                        userId: request.UserId,
                        description: $"Cascade of IsRecurring change to patient treatments failed for Bundle {request.BundleId} at Facility {request.FacilityId}: {cascadeEx.Message}",
                        module: "Pricing");
                }
                catch {  }
            }

            var bundleName = await _db.PD_Bundles
                .AsNoTracking()
                .Where(b => b.BundleId == request.BundleId)
                .Select(b => b.Name)
                .FirstOrDefaultAsync();

            var facilityName = await _db.SYS_Facilities
                .AsNoTracking()
                .Where(f => f.FacilityId == request.FacilityId)
                .Select(f => f.TitleShort ?? f.TitleLong)
                .FirstOrDefaultAsync();

            await _auditService.LogEntityChangeAsync(
                action: isUpdate ? "Update" : "Create",
                entityType: "PD_FacilityBundlePrice",
                entityId: row.FacilityBundlePriceId,
                oldValues: isUpdate ? new { ClinicPrice = oldPrice } : null,
                newValues: new { ClinicPrice = request.ClinicPrice, FacilityId = request.FacilityId, BundleId = request.BundleId },
                userId: request.UserId,
                description: $"Clinic price {(isUpdate ? "updated" : "set")} for Bundle '{bundleName}' (ID: {request.BundleId}) at Facility '{facilityName}' (ID: {request.FacilityId}) - {(isUpdate ? $"Old Price: ${oldPrice}, " : "")}New Price: ${request.ClinicPrice}",
                module: "Pricing"
            );

            return true;
        }

        #endregion

    }
}
