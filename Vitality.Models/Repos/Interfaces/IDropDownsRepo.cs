using DudeMeds.Models.DTOs.DropDown;
using DudeMeds.Models.DTOs.Products;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vitality.Models.DTOs.DropDown;

namespace DudeMeds.Models.Repos.Interfaces
{
    public interface IDropDownsRepo
    {
        public List<GetAllCountryResponseDTO> GetAllCountry();
        public List<DropdownResponseDTO> GetUSCities();
        public DropdownResponseDTO GetUSState(int CityId);
        public List<DropdownResponseDTO> GetAllUSARegion();
        public List<DropdownResponseDTO> GetAllRoles();
        public List<DropdownResponseDTO> GetAllRoleTitles();
        public int SaveRoleTitle(SaveRoleTitleRequestDTO request);
        public bool DeleteRoleTitle(int roleTitleId);
        public List<DropdownResponseDTO> GetCitiesByStateId(int StateId);
        public List<GetAllProviderResponseDTO> GetAllDoctors(GetAllProviderRequestDTO request);
        public List<GetProviderScheduledSlotsResponseDTO> GetProviderScheduledSlots(GetProviderScheduledSlotsRequestDTO request);
        public List<GetAllFacilitiesDropDownResponseDTO> GetAllFacilities(long OrganizationId);
        public List<GetAllFacilitiesDropDownResponseDTO> GetActiveFacilitiesByCategoryId(long categoryId);
        public List<GetAllProductsDropDownResponseDTO> GetAllProducts(GetAllProductsDropDownRequestDTO request);
        public List<GetAllDrugsDropDownResponseDTO> GetAllDrugs(GetAllProductsDropDownRequestDTO request);
        public List<GetAllDrugsDropDownResponseDTO> GetAllDrugsSupplies(GetAllProductsDropDownRequestDTO request);
        public Task<List<GetAllDrugsDropDownResponseDTO>> GetAllDrugsAndSuppliesAsync(GetAllProductsDropDownRequestDTO request, CancellationToken ct = default);

        public List<GetAllDrugsDropDownResponseDTO> GetAllCustomDrugs(GetAllProductsDropDownRequestDTO request);
        public List<GetAllDrugsDropDownResponseDTO> GetAllCustomDrugsSupplies(GetAllProductsDropDownRequestDTO request);
        public Task<List<GetAllDrugsDropDownResponseDTO>> GetAllCustomDrugsAndSuppliesAsync(GetAllProductsDropDownRequestDTO request, CancellationToken ct = default);
        public List<GetAllProductsDropDownResponseDTO> GetAllQuestionnaireProducts(GetAllQuestionnaireProductsDropDownRequestDTO request);
        public List<GetAllInTakeFormProductsDropDownResponseDTO> GetAllInTakeFormProducts(GetAllInTakeFormProductsDropDownRequestDTO request);
        public List<GetAllCategoriesDropDownResponseDTO> GetAllCategories(long? facilityId = null);
        public List<GetAllConditionsDropDownResponseDTO> GetAllConditions(long CategoryId);
        public List<GetAllPharmaciesDropDownResponseDTO> GetAllPharmacies();
        public List<GetAllProviderGroupsDropDownResponseDTO> GetAllProviderGroups();
        public Task<List<GetAllPatientsDropDownResponseDTO>> GetAllPatientsAsync(long? FacilityId, long? providerId);
        public List<GetAllPatientTreatmentsDropDownResponseDTO> GetAllPatientTreatments(long? PatientId);
        public List<GetAllPatientOrdersDropDownResponseDTO> GetAllPatientOrders(long? PatientTreatmentId);
        public List<GetAllProviderResponseDTO> GetAllUnAssignedProviders(GetAllUnAssignedProvidersRequestDTO request);
        public List<GetAllSubscriptionsDropDownResponseDTO> GetAllSubscriptions();
        List<GetAllFacilitiesDropDownResponseDTO> GetAllFacilitiesByProviderId(long ProviderId);
        Task<List<GetBundleByIdResponse2DTO>> GetAllBundlesAsync(long facilityId, CancellationToken ct = default);
        List<GetProviderScheduledSlotsResponseDTO> GetProviderScheduledSlotsByProvider(GetProviderScheduledSlotsByProviderRequestDTO request);
        List<CatalogResponseDTO> GetAllCatalogs(GetAllCatalogsRequestDTO request);
    }
}
