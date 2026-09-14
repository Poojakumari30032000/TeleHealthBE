using DudeMeds.Models.DTOs.Categories;
using DudeMeds.Models.DTOs.Conditions;
using DudeMeds.Models.DTOs.Products;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using Vitality.Models.DTOs.Bundles;
using Vitality.Models.DTOs.ClinicToPatient;
using Vitality.Models.DTOs.Products;

namespace DudeMeds.Models.Repos.Interfaces
{
    public interface IProductsRepo
    {

        public GetProductDashboardTilesResponseDTO GetProductDashboardTiles(long Drug);
        public List<GetAllProductsResponseDTO> GetAllProducts(GetAllProductsRequestDTO request, out int totalProductCount);
        public List<GetAllBundleResponseDTO> GetAllBundles(GetAllBundlesRequestDTO request, out int totalProductCount);
        public GetProductInfoByNameResponseDTO GetProductInfoByName(GetProductInfoByNameRequestDTO request);
        public bool DeleteProduct(DeleteProductRequestDTO request);
        public bool UpdateProductStatus(UpdateProductStatusRequestDTO request);
        public List<GetAllProductsForShoppingResponseDTO> GetAllProductsForShopping(long OrganizationId, GetAllProductsForShoppingRequestDTO request);

        public GetDrugByIdResponseDTO GetDrugById(long DrugId);
        public string SaveDrug(SaveDrugRequestDTO request, long UserId, long OrganizationId);
        public string SaveCatalog(SaveCatalogRequestDTO request, long userId);
        public List<CatalogResponseDTO> GetAllCatalogs(GetAllCatalogsRequestDTO request);
        public string UpdateCatalogStatus(UpdateCatalogStatusRequestDTO request, long userId);

        public List<GetAllDrugIngredientsResponseDTO> GetAllDrugIngredients(long DrugId);
        public GetDrugIngredientByIdResponseDTO GetDrugIngredientById(long DrugIngredientId);
        public string SaveDrugIngredient(SaveDrugIngredientRequestDTO request, long UserId);
        public bool DeleteDrugIngredient(long DrugIngredientId);

        public GetBundleByIdResponseDTO GetBundleById(GetBundleByIdRequestDTO request);
        public string SaveBundle(SaveBundleRequestDTO request, long UserId, long OrganizationId);
        public List<GetAllDrugsInBundlesResponseDTO> GetAllDrugsInBundles(long BundleId);
        public string SaveDrugInBundle(SaveDrugVarientsInBundleRequestDTO request, long UserId);
        public bool DeleteDrugVarientFromBundle(long DrugId);
        GetBundleByIdResponseDTO GetBundleByBundleId(long BundleId);

        Task<ClinicToPatientCreateResultDTO> CreateClinicToPatientAsync(ClinicToPatientCreateDTO dto, long? userId, CancellationToken ct = default);

        public List<GetAllDrugsWithPricingResponseDTO> GetAllDrugsWithPricing(GetAllProductsRequestDTO request, out int totalDrugCount);

        public List<GetAllDrugsForGlobalAdminResponseDTO> GetAllDrugsForGlobalAdmin(GetAllProductsRequestDTO request, out int totalProductCount);

        byte[] GenerateDrugBulkImportTemplate(long catalogId);
        Task<BulkImportDrugsExcelResponseDto> ImportDrugsFromExcelAsync(
            Stream excelStream,
            long catalogId,
            long userId,
            long organizationId,
            CancellationToken ct = default);

        public List<GetAllDrugsForGlobalAdminResponseDTO> GetAllCustomDrugsForGlobalAdmin(GetAllProductsRequestDTO request, out int totalProductCount);
        public List<GetAllDrugsWithPricingResponseDTO> GetAllCustomDrugsWithPricing(GetAllProductsRequestDTO request, out int totalDrugCount);

        GetDrugByIdResponse2DTO GetDrugById(long drugId, long? facilityId);
        string UpdateDrug(UpdateDrugRequestDTO request, long userId, long organizationId);
        string DeleteDrug(long drugId, long userId);

        List<long> GetDrugUnassignedFacilities(long drugId);

        bool UnassignDrugFromFacilities(UnassignDrugFromFacilitiesRequestDTO request, long userId);

        GetBundleByIdResponse2DTO GetBundleById(long bundleId);
        string UpdateBundle(UpdateBundleRequestDTO request, long userId, long organizationId);
        string DeleteBundle(long bundleId, long userId);
        public List<GetAllBundleByFacilityResponseDTO> GetAllBundlesByFacility(GetAllBundlesByFacilityRequestDTO request, out int totalProductCount);
        Task<bool> EditClinicBundlePrice(EditClinicBundlePriceRequestDTO request);
    }
}
