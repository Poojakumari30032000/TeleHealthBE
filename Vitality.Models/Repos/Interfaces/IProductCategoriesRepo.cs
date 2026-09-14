using DudeMeds.Models.DTOs.Categories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vitality.Models.DTOs.ProductCategories;

namespace DudeMeds.Models.Repos.Interfaces
{
    public interface IProductCategoriesRepo
    {
        public List<GetAllCategoriesResponseDTO> GetAllCategories(GetAllCategoriesRequestDTO request, out int totalCategoryCount);
        public GetCategoryByIdResponseDTO GetCategoryById(long CategoryId);
        public string SaveCategory(SaveCategoryRequestDTO request, long UserId);
        public bool DeleteCategory(long CategoryId);
        Task<List<GetAllCategoriesWithBundlesResponseDTO>> GetCategoriesWithBundlesAsync(long facilityId, CancellationToken ct = default);
        List<GetAllCategoriesWithBundlesResponseDTO> GetCategoriesWithBundles();
        Task<List<BundleDTO>> GetBundlesByCategoryAndFacilityAsync(
        long categoryId,
        long facilityId,
        CancellationToken ct = default);
        Task<BundleDTO?> GetFacilityBundleDetailAsync(long facilityId, long bundleId, CancellationToken ct = default);
    }
}
