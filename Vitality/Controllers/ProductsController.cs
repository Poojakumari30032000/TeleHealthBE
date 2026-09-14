using AutoMapper;
using DudeMeds.Models.DTOs.Categories;
using DudeMeds.Models.DTOs.Common;
using DudeMeds.Models.DTOs.Conditions;
using DudeMeds.Models.DTOs.Products;
using DudeMeds.Models.Repos.Interfaces;
using DudeMeds.Models.Repos.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Threading;
using Vitality.Helper;
using Vitality.Models.DTOs.Bundles;
using Vitality.Models.DTOs.ClinicToPatient;
using Vitality.Models.DTOs.Products;
using Vitality.Models.EntityClasses;
using Vitality.Models.Repos.Services;
using Vitality.Filters;
using Vitality.Models.Security;

namespace DudeMeds.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductsController : ControllerBase
    {
        public IConfiguration _configuration;
        private readonly IMapper _mapper;
        private readonly IProductsRepo _IProductsRepo;
        private readonly INotificationService _notificationService;
        private readonly MainContext _db;

        public ProductsController(
            IConfiguration config,
            IMapper IMapper,
            IProductsRepo IProductsRepo,
            INotificationService notificationService,
            MainContext db)
        {
            _configuration = config;
            _mapper = IMapper;
            _IProductsRepo = IProductsRepo;
            _notificationService = notificationService;
            _db = db;
        }

        [HttpGet]
        [Route("getProductDashboardTiles")]
        [RequiresPermission(Permissions.Product.View)]
        public ApiResponse<GetProductDashboardTilesResponseDTO> GetProductDashboardTiles([FromQuery] GetByIdRequestDTO request)
        {
            ApiResponse<GetProductDashboardTilesResponseDTO> response = new ApiResponse<GetProductDashboardTilesResponseDTO>();
            try
            {
                GetProductDashboardTilesResponseDTO result = new GetProductDashboardTilesResponseDTO();
                result = _IProductsRepo.GetProductDashboardTiles(request.Id);
                response.Data = result;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpGet]
        [Route("getAllProducts")]
        [RequiresPermission(Permissions.Product.View)]
        public ApiResponse<List<GetAllProductsResponseDTO>> GetAllProducts([FromQuery] GetAllProductsRequestDTO request)
        {
            ApiResponse<List<GetAllProductsResponseDTO>> response = new ApiResponse<List<GetAllProductsResponseDTO>>();
            try
            {
                List<GetAllProductsResponseDTO> result = new List<GetAllProductsResponseDTO>();
                result = _IProductsRepo.GetAllProducts(request, out int totalProductCount);
                int totalPages = (int)Math.Ceiling((double)totalProductCount / request.PageSize);
                response.Data = result;
                response.TotalEntityCount = totalProductCount;
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
        [Route("getAllBundles")]
        [RequiresPermission(Permissions.Product.View)]
        public ApiResponse<List<GetAllBundleResponseDTO>> GetAllBundles([FromQuery] GetAllBundlesRequestDTO request)
        {
            ApiResponse<List<GetAllBundleResponseDTO>> response = new ApiResponse<List<GetAllBundleResponseDTO>>();
            try
            {
                List<GetAllBundleResponseDTO> result = new List<GetAllBundleResponseDTO>();
                result = _IProductsRepo.GetAllBundles(request, out int totalProductCount);
                int totalPages = (int)Math.Ceiling((double)totalProductCount / request.PageSize);
                response.Data = result;
                response.TotalEntityCount = totalProductCount;
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
        [Route("getAllBundlesByFacilities")]
        [RequiresPermission(Permissions.Product.View)]
        public ApiResponse<List<GetAllBundleByFacilityResponseDTO>> GetAllBundles([FromQuery] GetAllBundlesByFacilityRequestDTO request)
        {
            ApiResponse<List<GetAllBundleByFacilityResponseDTO>> response = new ApiResponse<List<GetAllBundleByFacilityResponseDTO>>();
            try
            {
                List<GetAllBundleByFacilityResponseDTO> result = new List<GetAllBundleByFacilityResponseDTO>();
                result = _IProductsRepo.GetAllBundlesByFacility(request, out int totlalBundlesCount);
                int totalPages = (int)Math.Ceiling((double)totlalBundlesCount / request.PageSize);
                response.Data = result;
                response.TotalEntityCount = totlalBundlesCount;
                response.TotalPages = totalPages;
                response.Data = result;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }
        [HttpPost]
        [Route("editClinicBundlePrice")]
        public async Task<ApiResponse<bool>> EditClinicBundlePrice([FromBody] EditClinicBundlePriceRequestDTO request)
        {
            var response = new ApiResponse<bool>();
            try
            {
                var ok = await _IProductsRepo.EditClinicBundlePrice(request);
                response.Data = ok;
            }
            catch (Exception ex)
            {
                response.Data = false;
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpGet]
        [Route("getAllDrugsForGlobalAdmin")]
        [RequiresPermission(Permissions.Product.View)]
        public ApiResponse<List<GetAllDrugsForGlobalAdminResponseDTO>> GetAllDrugsForGlobalAdmin([FromQuery] GetAllProductsRequestDTO request)
        {
            ApiResponse<List<GetAllDrugsForGlobalAdminResponseDTO>> response = new ApiResponse<List<GetAllDrugsForGlobalAdminResponseDTO>>();
            try
            {
                List<GetAllDrugsForGlobalAdminResponseDTO> result = new List<GetAllDrugsForGlobalAdminResponseDTO>();
                result = _IProductsRepo.GetAllDrugsForGlobalAdmin(request, out int totalProductCount);
                int totalPages = (int)Math.Ceiling((double)totalProductCount / request.PageSize);
                response.Data = result;
                response.TotalEntityCount = totalProductCount;
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
        [Route("getAllCustomDrugsForGlobalAdmin")]
        [RequiresPermission(Permissions.Product.View)]
        public ApiResponse<List<GetAllDrugsForGlobalAdminResponseDTO>> GetAllCustomDrugsForGlobalAdmin([FromQuery] GetAllProductsRequestDTO request)
        {
            var response = new ApiResponse<List<GetAllDrugsForGlobalAdminResponseDTO>>();
            try
            {
                var result = _IProductsRepo.GetAllCustomDrugsForGlobalAdmin(request, out int totalProductCount);
                int totalPages = (int)Math.Ceiling((double)totalProductCount / request.PageSize);
                response.Data = result;
                response.TotalEntityCount = totalProductCount;
                response.TotalPages = totalPages;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpPost]
        [Route("saveCatalog")]
        public ApiResponse<bool> SaveCatalog([FromBody] SaveCatalogRequestDTO request)
        {
            var response = new ApiResponse<bool>();
            try
            {
                var userId = Convert.ToInt64(User.FindFirst("UserId").Value);
                var message = _IProductsRepo.SaveCatalog(request, userId);
                response.Message = message;
                response.Data = message == "Catalog Added Successfully" || message == "Catalog Updated Successfully";
            }
            catch (Exception ex)
            {
                response.Data = false;
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpGet]
        [Route("getAllCatalogs")]
        [RequiresPermission(Permissions.Product.View)]
        public ApiResponse<List<CatalogResponseDTO>> GetAllCatalogs([FromQuery] GetAllCatalogsRequestDTO request)
        {
            var response = new ApiResponse<List<CatalogResponseDTO>>();
            try
            {
                response.Data = _IProductsRepo.GetAllCatalogs(request);
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpPost]
        [Route("updateCatalogStatus")]
        public ApiResponse<bool> UpdateCatalogStatus([FromBody] UpdateCatalogStatusRequestDTO request)
        {
            var response = new ApiResponse<bool>();
            try
            {
                var userId = Convert.ToInt64(User.FindFirst("UserId").Value);
                var message = _IProductsRepo.UpdateCatalogStatus(request, userId);
                response.Message = message;
                response.Data = string.Equals(message, "Catalog status updated successfully.", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                response.Data = false;
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpGet]
        [Route("getProductInfoByName")]
        public ApiResponse<GetProductInfoByNameResponseDTO> GetProductInfoByName([FromQuery] GetProductInfoByNameRequestDTO request)
        {
            ApiResponse<GetProductInfoByNameResponseDTO> response = new ApiResponse<GetProductInfoByNameResponseDTO>();
            try
            {
                GetProductInfoByNameResponseDTO result = new GetProductInfoByNameResponseDTO();
                result = _IProductsRepo.GetProductInfoByName(request);
                response.Data = result;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpPost]
        [Route("deleteProduct")]
        [RequiresPermission(Permissions.Product.Delete)]
        public ApiResponse<bool> DeleteProduct([FromBody] DeleteProductRequestDTO request)
        {
            ApiResponse<bool> response = new ApiResponse<bool>();
            var result = _IProductsRepo.DeleteProduct(request);
            response.Data = result;
            return response;
        }

        [HttpPost]
        [Route("updateProductStatus")]
        [RequiresPermission(Permissions.Product.Edit)]
        public ApiResponse<bool> UpdateProductStatus([FromBody] UpdateProductStatusRequestDTO request)
        {
            ApiResponse<bool> response = new ApiResponse<bool>();
            var result = _IProductsRepo.UpdateProductStatus(request);
            response.Data = result;
            return response;
        }

        [HttpGet]
        [Route("getAllProductsForShopping")]
        public ApiResponse<List<GetAllProductsForShoppingResponseDTO>> GetAllProductsForShopping([FromQuery] GetAllProductsForShoppingRequestDTO request)
        {
            ApiResponse<List<GetAllProductsForShoppingResponseDTO>> response = new ApiResponse<List<GetAllProductsForShoppingResponseDTO>>();
            try
            {
                List<GetAllProductsForShoppingResponseDTO> result = new List<GetAllProductsForShoppingResponseDTO>();
                var OrganizationId = 1;
                result = _IProductsRepo.GetAllProductsForShopping(OrganizationId, request);
                response.Data = result;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpGet]
        [Route("getDrugById")]
        [RequiresPermission(Permissions.Product.View)]
        public ApiResponse<GetDrugByIdResponseDTO> GetDrugById([FromQuery] GetByIdRequestDTO request)
        {
            ApiResponse<GetDrugByIdResponseDTO> response = new ApiResponse<GetDrugByIdResponseDTO>();
            try
            {
                GetDrugByIdResponseDTO result = new GetDrugByIdResponseDTO();
                result = _IProductsRepo.GetDrugById(request.Id);
                response.Data = result;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpGet]
        [Route("bulkImportDrugsTemplate")]
        [RequiresPermission(Permissions.Product.Add)]
        public IActionResult DownloadDrugBulkImportTemplate([FromQuery] long catalogId)
        {
            try
            {
                var bytes = _IProductsRepo.GenerateDrugBulkImportTemplate(catalogId);
                const string fileName = "Drug_Bulk_Import_Template.xlsx";
                return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost]
        [Route("bulkImportDrugsExcel")]
        [RequiresPermission(Permissions.Product.Add)]
        public async Task<ApiResponse<BulkImportDrugsExcelResponseDto>> BulkImportDrugsExcel(
            [FromForm] BulkUploadDrugsRequestDTO request)
        {
            var resp = new ApiResponse<BulkImportDrugsExcelResponseDto>();
            try
            {
                if (request?.File == null || request.File.Length == 0)
                {
                    resp.Status = 0;
                    resp.Message = "Excel file is required (.xlsx).";
                    return resp;
                }

                if (request.CatalogId <= 0)
                {
                    resp.Status = 0;
                    resp.Message = "CatalogId is required.";
                    return resp;
                }

                var ext = Path.GetExtension(request.File.FileName);
                if (!string.Equals(ext, ".xlsx", StringComparison.OrdinalIgnoreCase))
                {
                    resp.Status = 0;
                    resp.Message = "Only .xlsx files are supported.";
                    return resp;
                }

                var organizationId = Convert.ToInt64(User.FindFirst("OrganizationId").Value);
                var userId = Convert.ToInt64(User.FindFirst("UserId").Value);

                await using var stream = request.File.OpenReadStream();
                var result = await _IProductsRepo.ImportDrugsFromExcelAsync(
                    stream,
                    request.CatalogId,
                    userId,
                    organizationId,
                    HttpContext.RequestAborted);

                resp.Data = result;
                resp.Status = 1;

                var failed = result.DrugsFailed.Count;
                var parseErr = result.ParseAndValidationErrors.Count;

                if (result.ImportSucceeded)
                {
                    resp.Message = failed + parseErr > 0
                        ? $"Import finished with partial success: {result.DrugsCreated} drug(s) created. Review failed rows in the summary."
                        : $"Import completed: {result.DrugsCreated} drug(s) created.";
                }
                else if (parseErr > 0 && result.DrugRowsRead == 0)
                {
                    resp.Status = 0;
                    resp.Message = "The workbook could not be imported. Fix the issues listed below and try again.";
                }
                else
                {
                    resp.Message = "No drugs were created. Review the error list for details.";
                    resp.Status = result.ProcessingCompleted ? 1 : 0;
                }
            }
            catch (Exception ex)
            {
                resp.Status = 0;
                resp.Message = ex.Message;
            }

            return resp;
        }

        [HttpPost]
        [Route("saveDrug")]
        [RequiresPermission(Permissions.Product.Add, Permissions.Product.Edit)]
        public async Task<ApiResponse<bool>> SaveDrug([FromBody] SaveDrugRequestDTO request)
        {
            ApiResponse<bool> response = new ApiResponse<bool>();
            try
            {
                var OrganizationId = Convert.ToInt64(User.FindFirst("OrganizationId").Value);
                var UserId = Convert.ToInt64(User.FindFirst("UserId").Value);

                bool isGlobalAdmin = false;
                decimal? oldWholesalePrice = null;
                decimal? newWholesalePrice = null;
                bool wholesalePriceChanged = false;
                bool isCustomForNotification = false;
                string? oldDrugName = null;
                bool drugNameChanged = false;

                try
                {

                    if (request.DrugId > 0)
                    {
                        var existingDrug = await _db.PD_Drugs
                            .AsNoTracking()
                            .FirstOrDefaultAsync(d => d.DrugId == request.DrugId);

                        if (existingDrug != null)
                        {
                            isCustomForNotification = existingDrug.IsCustom == true;
                            oldDrugName = existingDrug.Name;
                            if (!string.IsNullOrWhiteSpace(request.Name))
                            {
                                drugNameChanged = !string.Equals(
                                    (oldDrugName ?? string.Empty).Trim(),
                                    request.Name.Trim(),
                                    StringComparison.OrdinalIgnoreCase);
                            }
                        }
                    }

                    var currentUser = await _db.SYS_UserDetails
                        .AsNoTracking()
                        .Include(u => u.Login)
                        .FirstOrDefaultAsync(u => u.UserId == UserId);

                    if (currentUser?.Login != null && currentUser.Login.RoleId == 2)
                    {
                        isGlobalAdmin = true;

                        if (request.DrugId > 0)
                        {
                            var existingPtg = await _db.PC_PHARMTOGLOBALs
                                .AsNoTracking()
                                .FirstOrDefaultAsync(x => x.DrugId == request.DrugId && x.IsActive == true);

                            oldWholesalePrice = existingPtg?.WholesalePrice;
                            newWholesalePrice = ComputeProjectedWholesaleAfterSave(request, existingPtg);
                            wholesalePriceChanged = HasMeaningfulWholesalePriceChange(oldWholesalePrice, newWholesalePrice);
                        }
                    }
                }
                catch
                {

                }

                string res = _IProductsRepo.SaveDrug(request, UserId, OrganizationId);
                if (res == "Drug Added Successfully" || res == "Drug Updated Successfully")
                {
                    response.Message = res;
                    response.Status = 1;
                    response.Data = true;
                    response.Success = true;

                    if (isGlobalAdmin && wholesalePriceChanged && request.DrugId > 0)
                    {

                        var serviceProvider = HttpContext.RequestServices;
                        var productName = request.Name ?? "Drug";
                        var oldPriceValue = oldWholesalePrice ?? 0m;
                        var newPriceValue = newWholesalePrice ?? 0m;
                        var capturedDrugId = request.DrugId;
                        var capturedIsCustom = isCustomForNotification;

                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                using var scope = serviceProvider.CreateScope();
                                var db = scope.ServiceProvider.GetRequiredService<MainContext>();
                                var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

                                var unassignedFacilityIds = await db.PC_DRUGFACILITYEXCLUSIONS
                                    .AsNoTracking()
                                    .Where(x => x.DrugId == capturedDrugId && x.IsActive == true)
                                    .Select(x => x.FacilityId)
                                    .ToListAsync();

                                var facilities = await db.SYS_Facilities
                                    .AsNoTracking()
                                    .Where(f => f.IsActive == true && !unassignedFacilityIds.Contains(f.FacilityId))
                                    .Select(f => f.FacilityId)
                                    .ToListAsync();

                                var emailTasks = facilities.Select(facilityId =>
                                    notificationService.SendPriceChangeNotificationAsync(
                                        facilityId: facilityId,
                                        productName: productName,
                                        oldPrice: oldPriceValue,
                                        newPrice: newPriceValue,
                                        drugId: capturedDrugId,
                                        isCustom: capturedIsCustom,
                                        ct: CancellationToken.None
                                    )
                                );

                                await Task.WhenAll(emailTasks);
                            }
                            catch (Exception emailEx)
                            {

                            }
                        });
                    }

                    if (drugNameChanged && request.DrugId > 0 && !string.IsNullOrWhiteSpace(request.Name) && request.IsCustom != true)
                    {
                        var serviceProvider = HttpContext.RequestServices;
                        var newDrugName = request.Name!.Trim();
                        var capturedDrugId = request.DrugId;
                        var capturedIsCustom = isCustomForNotification;
                        var capturedPreviousName = string.IsNullOrWhiteSpace(oldDrugName) ? null : oldDrugName.Trim();

                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                using var scope = serviceProvider.CreateScope();
                                var db = scope.ServiceProvider.GetRequiredService<MainContext>();
                                var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

                                var facilities = await GetActiveFacilityIdsAssignedToDrugAsync(db, capturedDrugId, CancellationToken.None);

                                var emailTasks = facilities.Select(facilityId =>
                                    notificationService.SendMedicineCatalogUpdateAsync(
                                        facilityId: facilityId,
                                        action: "Updated",
                                        productName: newDrugName,
                                        drugId: capturedDrugId,
                                        isCustom: capturedIsCustom,
                                        previousDrugName: capturedPreviousName,
                                        ct: CancellationToken.None
                                    )
                                );

                                await Task.WhenAll(emailTasks);
                            }
                            catch
                            {

                            }
                        });
                    }
                }
                else
                {
                    response.Message = res;
                    response.Data = false;
                    response.Status = 0;
                    response.Success = false;
                }
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
                response.Status = 0;
                response.Data = false;
                response.Success = false;
            }
            return response;
        }

        private static decimal? ComputeProjectedWholesaleAfterSave(SaveDrugRequestDTO request, PC_PHARMTOGLOBAL? existingPtg)
        {
            var pharmacyPrice = request.Price;
            decimal? markupPercentUpdate = null;
            var normalizedMarkupType = string.IsNullOrWhiteSpace(request.MarkupType)
                ? (existingPtg?.MarkupType ?? "Percentage")
                : request.MarkupType.Trim();

            if (request.Markup.HasValue && request.Price.HasValue && request.Price.Value > 0)
            {
                if (string.Equals(normalizedMarkupType, "Amount", StringComparison.OrdinalIgnoreCase))
                    markupPercentUpdate = (decimal)request.Markup.Value / request.Price.Value * 100m;
                else
                    markupPercentUpdate = request.Markup.Value;
            }

            if (!pharmacyPrice.HasValue || !markupPercentUpdate.HasValue)
                return null;

            return Math.Round(pharmacyPrice.Value * (1 + markupPercentUpdate.Value / 100m), 4, MidpointRounding.AwayFromZero);
        }

        private static bool HasMeaningfulWholesalePriceChange(decimal? oldWholesale, decimal? newWholesale)
        {
            var oldR = oldWholesale.HasValue ? Math.Round(oldWholesale.Value, 4) : (decimal?)null;
            var newR = newWholesale.HasValue ? Math.Round(newWholesale.Value, 4) : (decimal?)null;
            if (oldR == null && newR == null)
                return false;
            if (oldR == null || newR == null)
                return true;
            return oldR.Value != newR.Value;
        }

        private static async Task<List<long>> GetActiveFacilityIdsAssignedToDrugAsync(MainContext db, long drugId, CancellationToken ct = default)
        {
            var unassignedFacilityIds = await db.PC_DRUGFACILITYEXCLUSIONS
                .AsNoTracking()
                .Where(x => x.DrugId == drugId && x.IsActive == true)
                .Select(x => x.FacilityId)
                .ToListAsync(ct);

            return await db.SYS_Facilities
                .AsNoTracking()
                .Where(f => f.IsActive == true && !unassignedFacilityIds.Contains(f.FacilityId))
                .Select(f => f.FacilityId)
                .ToListAsync(ct);
        }

        [HttpPost]
        [Route("saveCustomDrug")]
        public async Task<ApiResponse<bool>> SaveCustomDrug([FromBody] SaveDrugRequestDTO request)
        {
            var response = new ApiResponse<bool>();

            string? previousDrugName = null;
            bool drugNameChanged = false;
            string? newDrugName = !string.IsNullOrWhiteSpace(request.Name) ? request.Name.Trim() : null;

            if (request.DrugId > 0 && !string.IsNullOrWhiteSpace(newDrugName))
            {
                var existingDrug = await _db.PD_Drugs
                    .AsNoTracking()
                    .FirstOrDefaultAsync(d => d.DrugId == request.DrugId);

                previousDrugName = string.IsNullOrWhiteSpace(existingDrug?.Name) ? null : existingDrug!.Name.Trim();
                var oldName = (existingDrug?.Name ?? string.Empty).Trim();
                drugNameChanged = !string.Equals(oldName, newDrugName, StringComparison.OrdinalIgnoreCase);
            }

            if (request.CatalogId == 1)
            {
                response.Message = "Custom drugs cannot be saved in Empower catalog.";
                response.Data = false;
                response.Status = 0;
                response.Success = false;
                return response;
            }

            if (!request.CatalogId.HasValue || request.CatalogId.Value <= 0)
                request.CatalogId = 2;

            request.IsCustom = true;
            response = await SaveDrug(request);

            if (response?.Status == 1 && response.Data == true && drugNameChanged && request.DrugId > 0 && !string.IsNullOrWhiteSpace(newDrugName))
            {
                var serviceProvider = HttpContext.RequestServices;
                var capturedDrugId = request.DrugId;
                var capturedIsCustom = true;
                var capturedPreviousName = previousDrugName;
                var capturedNewDrugName = newDrugName!;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var scope = serviceProvider.CreateScope();
                        var db = scope.ServiceProvider.GetRequiredService<MainContext>();
                        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

                        var facilities = await GetActiveFacilityIdsAssignedToDrugAsync(db, capturedDrugId, CancellationToken.None);

                        var emailTasks = facilities.Select(facilityId =>
                            notificationService.SendMedicineCatalogUpdateAsync(
                                facilityId: facilityId,
                                action: "Updated",
                                productName: capturedNewDrugName,
                                drugId: capturedDrugId,
                                isCustom: capturedIsCustom,
                                previousDrugName: capturedPreviousName,
                                ct: CancellationToken.None
                            )
                        );

                        await Task.WhenAll(emailTasks);
                    }
                    catch
                    {

                    }
                });
            }

            return response;
        }

        [HttpGet]
        [Route("GetDrugUnassignedFacilities")]
        [RequiresPermission(Permissions.Product.View)]
        public ApiResponse<List<long>> GetDrugUnassignedFacilities([FromQuery] long drugId)
        {
            var response = new ApiResponse<List<long>>();
            try
            {
                response.Data = _IProductsRepo.GetDrugUnassignedFacilities(drugId);
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpPost]
        [Route("UnassignDrugFromFacilities")]
        [RequiresPermission(Permissions.Product.Edit)]
        public ApiResponse<bool> UnassignDrugFromFacilities([FromBody] UnassignDrugFromFacilitiesRequestDTO request)
        {
            var response = new ApiResponse<bool>();
            try
            {
                var userId = Convert.ToInt64(User.FindFirst("UserId").Value);
                var ok = _IProductsRepo.UnassignDrugFromFacilities(request, userId);
                response.Data = ok;
                response.Message = ok ? "Drug facility visibility updated." : "Failed to update drug facility visibility.";
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
                response.Data = false;
            }
            return response;
        }

        [HttpGet]
        [Route("getAllDrugIngredients")]
        [RequiresPermission(Permissions.Product.View)]
        public ApiResponse<List<GetAllDrugIngredientsResponseDTO>> GetAllDrugIngredients([FromQuery] GetByIdRequestDTO request)
        {
            ApiResponse<List<GetAllDrugIngredientsResponseDTO>> response = new ApiResponse<List<GetAllDrugIngredientsResponseDTO>>();
            try
            {
                List<GetAllDrugIngredientsResponseDTO> result = new List<GetAllDrugIngredientsResponseDTO>();
                result = _IProductsRepo.GetAllDrugIngredients(request.Id);
                response.Data = result;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpGet]
        [Route("getDrugIngredientById")]
        [RequiresPermission(Permissions.Product.View)]
        public ApiResponse<GetDrugIngredientByIdResponseDTO> GetDrugIngredientById([FromQuery] GetByIdRequestDTO request)
        {
            ApiResponse<GetDrugIngredientByIdResponseDTO> response = new ApiResponse<GetDrugIngredientByIdResponseDTO>();
            try
            {
                GetDrugIngredientByIdResponseDTO result = new GetDrugIngredientByIdResponseDTO();
                result = _IProductsRepo.GetDrugIngredientById(request.Id);
                response.Data = result;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpPost]
        [Route("saveDrugIngredient")]
        [RequiresPermission(Permissions.Product.Add, Permissions.Product.Edit)]
        public ApiResponse<bool> SaveDrugIngredient([FromBody] SaveDrugIngredientRequestDTO request)
        {
            ApiResponse<bool> response = new ApiResponse<bool>();
            try
            {
                var UserId = Convert.ToInt64(User.FindFirst("UserId").Value);
                string res = _IProductsRepo.SaveDrugIngredient(request, UserId);
                if (res == "Durg Ingredient Added Successfully" || res == "Durg Ingredient Updated Successfully")
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
        [Route("deleteDrugIngredient")]
        [RequiresPermission(Permissions.Product.Delete)]
        public ApiResponse<bool> DeleteDrugIngredient([FromBody] GetByIdRequestDTO request)
        {
            ApiResponse<bool> response = new ApiResponse<bool>();
            bool res = _IProductsRepo.DeleteDrugIngredient(request.Id);
            response.Data = res;
            return response;
        }

        [HttpGet]
        [Route("getBundleById")]
        [RequiresPermission(Permissions.Product.View)]
        public ApiResponse<GetBundleByIdResponseDTO> GetBundleById([FromQuery] GetBundleByIdRequestDTO request)
        {
            ApiResponse<GetBundleByIdResponseDTO> response = new ApiResponse<GetBundleByIdResponseDTO>();
            try
            {
                GetBundleByIdResponseDTO result = new GetBundleByIdResponseDTO();
                result = _IProductsRepo.GetBundleById(request);
                response.Data = result;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpPost]
        [Route("saveBundle")]
        [RequiresPermission(Permissions.Product.Add, Permissions.Product.Edit)]
        public async Task<ApiResponse<bool>> SaveBundle([FromBody] SaveBundleRequestDTO request)
        {
            ApiResponse<bool> response = new ApiResponse<bool>();
            try
            {
                var OrganizationId = Convert.ToInt64(User.FindFirst("OrganizationId").Value);
                var UserId = Convert.ToInt64(User.FindFirst("UserId").Value);

                bool isGlobalAdmin = false;
                decimal? oldPrice = null;
                bool priceChanged = false;

                try
                {
                    var currentUser = await _db.SYS_UserDetails
                        .AsNoTracking()
                        .Include(u => u.Login)
                        .FirstOrDefaultAsync(u => u.UserId == UserId);

                    if (currentUser?.Login != null && currentUser.Login.RoleId == 2)
                    {
                        isGlobalAdmin = true;

                        if (request.BundleId > 0)
                        {
                            var existingBundle = await _db.PD_Bundles
                                .AsNoTracking()
                                .FirstOrDefaultAsync(b => b.BundleId == request.BundleId);

                            if (existingBundle != null && existingBundle.Price.HasValue)
                            {
                                oldPrice = existingBundle.Price;
                                if (request.Price.HasValue && request.Price.Value != oldPrice.Value)
                                {
                                    priceChanged = true;
                                }
                            }
                        }
                    }
                }
                catch
                {

                }

                string res = _IProductsRepo.SaveBundle(request, UserId, OrganizationId);
                if (res == "Bundle Added Successfully" || res == "Bundle Updated Successfully")
                {
                    response.Message = res;
                    response.Data = true;

                    if (isGlobalAdmin && priceChanged && oldPrice.HasValue && request.Price.HasValue)
                    {

                        var serviceProvider = HttpContext.RequestServices;
                        var productName = request.Name ?? "Package";
                        var oldPriceValue = oldPrice.Value;
                        var newPriceValue = request.Price.Value;

                        _ = Task.Run(async () =>
                        {
                            try
                            {

                                using var scope = serviceProvider.CreateScope();
                                var db = scope.ServiceProvider.GetRequiredService<MainContext>();
                                var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

                                var facilityIds = await db.PD_FacilityBundlePrices
                                    .AsNoTracking()
                                    .Where(fbp => fbp.BundleId == request.BundleId)
                                    .Select(fbp => fbp.FacilityId)
                                    .Distinct()
                                    .ToListAsync();

                                if (facilityIds.Count == 0) return;

                                var emailTasks = facilityIds.Select(facilityId =>
                                    notificationService.SendPriceChangeNotificationAsync(
                                        facilityId: facilityId,
                                        productName: productName,
                                        oldPrice: oldPriceValue,
                                        newPrice: newPriceValue,
                                        drugId: null,
                                        isCustom: false,
                                        ct: CancellationToken.None
                                    )
                                );

                                await Task.WhenAll(emailTasks);
                            }
                            catch
                            {

                            }
                        });
                    }
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

        [HttpGet]
        [Route("getAllDrugsInBundles")]
        [RequiresPermission(Permissions.Product.View)]
        public ApiResponse<List<GetAllDrugsInBundlesResponseDTO>> GetAllDrugsInBundles([FromQuery] GetByIdRequestDTO request)
        {
            ApiResponse<List<GetAllDrugsInBundlesResponseDTO>> response = new ApiResponse<List<GetAllDrugsInBundlesResponseDTO>>();
            try
            {
                List<GetAllDrugsInBundlesResponseDTO> result = new List<GetAllDrugsInBundlesResponseDTO>();
                result = _IProductsRepo.GetAllDrugsInBundles(request.Id);
                response.Data = result;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpPost]
        [Route("saveDrugInBundle")]
        [RequiresPermission(Permissions.Product.Edit)]
        public ApiResponse<bool> SaveDrugInBundle([FromBody] SaveDrugVarientsInBundleRequestDTO request)
        {
            ApiResponse<bool> response = new ApiResponse<bool>();
            try
            {
                var UserId = Convert.ToInt64(User.FindFirst("UserId").Value);
                string res = _IProductsRepo.SaveDrugInBundle(request, UserId);
                if (res == "Drug Added In Bundle Successfully." || res == "Drug Updated In Bundle Successfully.")
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
        [Route("deleteDrugVarientFromBundle")]
        [RequiresPermission(Permissions.Product.Delete, Permissions.Product.Edit)]
        public ApiResponse<bool> DeleteDrugVarientFromBundle([FromBody] GetByIdRequestDTO request)
        {
            ApiResponse<bool> response = new ApiResponse<bool>();
            bool res = _IProductsRepo.DeleteDrugVarientFromBundle(request.Id);
            response.Data = res;
            return response;
        }

        #region ClinicToPatient - PharmToGlobal
        [HttpPost("CreateClinicToPatient")]
        public ApiResponse<bool> CreateClinicToPatient([FromBody] ClinicToPatientCreateDTO request)
        {
            var response = new ApiResponse<bool>();
            try
            {

                var UserId = 2L;
                var OrganizationId = 2L;

                var result = _IProductsRepo
                    .CreateClinicToPatientAsync(request, UserId, CancellationToken.None)
                    .GetAwaiter().GetResult();

                response.Message = string.IsNullOrWhiteSpace(result.Message)
                    ? (result.Success ? "Clinic-to-patient price created." : "Failed to create clinic-to-patient price.")
                    : result.Message;

                response.Data = result.Success;
                response.Status = 200;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
                response.Data = false;
                response.Status = 500;

            }
            return response;
        }

        [HttpGet]
        [Route("GetAllDrugsForClinicByFacilityId")]
        [RequiresPermission(Permissions.Product.View)]
        public ApiResponse<List<GetAllDrugsWithPricingResponseDTO>> GetAllDrugsForClinicByFacilityId([FromQuery] GetAllProductsRequestDTO request)
        {
            var response = new ApiResponse<List<GetAllDrugsWithPricingResponseDTO>>();
            try
            {
                var result = _IProductsRepo.GetAllDrugsWithPricing(request, out int totalCount);
                int totalPages = (int)Math.Ceiling((double)totalCount / (request?.PageSize > 0 ? request.PageSize : 10));

                response.Data = result;
                response.TotalEntityCount = totalCount;
                response.TotalPages = totalPages;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpGet]
        [Route("GetAllCustomDrugsForClinicByFacilityId")]
        [RequiresPermission(Permissions.Product.View)]
        public ApiResponse<List<GetAllDrugsWithPricingResponseDTO>> GetAllCustomDrugsForClinicByFacilityId([FromQuery] GetAllProductsRequestDTO request)
        {
            var response = new ApiResponse<List<GetAllDrugsWithPricingResponseDTO>>();
            try
            {
                var result = _IProductsRepo.GetAllCustomDrugsWithPricing(request, out int totalCount);
                int totalPages = (int)Math.Ceiling((double)totalCount / (request?.PageSize > 0 ? request.PageSize : 10));

                response.Data = result;
                response.TotalEntityCount = totalCount;
                response.TotalPages = totalPages;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }
        #endregion

        [HttpGet("GetDrugByIdNew")]
        [RequiresPermission(Permissions.Product.View)]
        public ApiResponse<GetDrugByIdResponse2DTO> GetDrugByIdNew(long id, [FromQuery] long? facilityId)
        {
            var response = new ApiResponse<GetDrugByIdResponse2DTO>();
            try
            {
                response.Data = _IProductsRepo.GetDrugById(id, facilityId);
                if (response.Data == null) response.Message = "Drug not found.";
            }
            catch (Exception ex) { response.Message = ex.Message; }
            return response;
        }

        [HttpPost("UpdateDrug")]
        [RequiresPermission(Permissions.Product.Edit)]
        public ApiResponse<bool> UpdateDrug([FromBody] UpdateDrugRequestDTO request)
        {
            var response = new ApiResponse<bool>();
            try
            {
                var UserId = 2L; var OrganizationId = 2L;
                string res = _IProductsRepo.UpdateDrug(request, UserId, OrganizationId);
                response.Message = res;
                response.Data = res.Contains("Successfully");
            }
            catch (Exception ex) { response.Message = ex.Message; response.Data = false; }
            return response;
        }

        [HttpDelete("DeleteDrug")]
        [RequiresPermission(Permissions.Product.Delete)]
        public ApiResponse<bool> DeleteDrug(long id)
        {
            var response = new ApiResponse<bool>();
            try
            {
                var UserId = 2L;
                string res = _IProductsRepo.DeleteDrug(id, UserId);
                response.Message = res;
                response.Data = res.Contains("Successfully");
            }
            catch (Exception ex) { response.Message = ex.Message; response.Data = false; }
            return response;
        }

        [HttpGet]
        [Route("GetBundleByIdNew")]
        [RequiresPermission(Permissions.Product.View)]
        public ApiResponse<GetBundleByIdResponse2DTO> GetBundleByIdNew(long id)
        {
            var response = new ApiResponse<GetBundleByIdResponse2DTO>();
            try
            {
                response.Data = _IProductsRepo.GetBundleById(id);
                if (response.Data == null) response.Message = "Bundle not found.";
            }
            catch (Exception ex) { response.Message = ex.Message; }
            return response;
        }

        [HttpPost("UpdateBundle")]
        [RequiresPermission(Permissions.Product.Edit)]
        public async Task<ApiResponse<bool>> UpdateBundle([FromBody] UpdateBundleRequestDTO request)
        {
            var response = new ApiResponse<bool>();
            try
            {
                var UserId = Convert.ToInt64(User.FindFirst("UserId")?.Value ?? "0");
                var OrganizationId = Convert.ToInt64(User.FindFirst("OrganizationId")?.Value ?? "0");

                bool isGlobalAdmin = false;
                decimal? oldPrice = null;
                bool priceChanged = false;
                try
                {
                    var currentUser = await _db.SYS_UserDetails
                        .AsNoTracking()
                        .Include(u => u.Login)
                        .FirstOrDefaultAsync(u => u.UserId == UserId);
                    if (currentUser?.Login != null && currentUser.Login.RoleId == 2)
                    {
                        isGlobalAdmin = true;
                        if (request.BundleId > 0 && request.Price.HasValue)
                        {
                            var existing = await _db.PD_Bundles
                                .AsNoTracking()
                                .FirstOrDefaultAsync(b => b.BundleId == request.BundleId);
                            if (existing?.Price.HasValue == true && request.Price.Value != existing.Price.Value)
                            {
                                oldPrice = existing.Price;
                                priceChanged = true;
                            }
                        }
                    }
                }
                catch {  }

                string res = _IProductsRepo.UpdateBundle(request, UserId, OrganizationId);
                response.Message = res;
                response.Data = res.Contains("Successfully");

                if (response.Data && isGlobalAdmin && priceChanged && oldPrice.HasValue && request.Price.HasValue)
                {
                    var serviceProvider = HttpContext.RequestServices;
                    var productName = request.Name ?? "Package";
                    var oldPriceValue = oldPrice.Value;
                    var newPriceValue = request.Price.Value;
                    var bundleId = request.BundleId;

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            using var scope = serviceProvider.CreateScope();
                            var db = scope.ServiceProvider.GetRequiredService<MainContext>();
                            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

                            var facilityIds = await db.PD_FacilityBundlePrices
                                .AsNoTracking()
                                .Where(fbp => fbp.BundleId == bundleId)
                                .Select(fbp => fbp.FacilityId)
                                .Distinct()
                                .ToListAsync();

                            if (facilityIds.Count == 0) return;

                            var emailTasks = facilityIds.Select(facilityId =>
                                notificationService.SendPriceChangeNotificationAsync(
                                    facilityId: facilityId,
                                    productName: productName,
                                    oldPrice: oldPriceValue,
                                    newPrice: newPriceValue,
                                    drugId: null,
                                    isCustom: false,
                                    ct: CancellationToken.None
                                ));
                            await Task.WhenAll(emailTasks);
                        }
                        catch {  }
                    });
                }
            }
            catch (Exception ex) { response.Message = ex.Message; response.Data = false; }
            return response;
        }

        [HttpDelete("DeleteBundle")]
        [RequiresPermission(Permissions.Product.Delete)]
        public ApiResponse<bool> DeleteBundle(long id)
        {
            var response = new ApiResponse<bool>();
            try
            {
                var UserId = 2L;
                string res = _IProductsRepo.DeleteBundle(id, UserId);
                response.Message = res;
                response.Data = res.Contains("Successfully");
            }
            catch (Exception ex) { response.Message = ex.Message; response.Data = false; }
            return response;
        }
    }
}
