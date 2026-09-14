using AutoMapper;
using DudeMeds.Models.DTOs.DropDown;
using DudeMeds.Models.DTOs.Products;
using DudeMeds.Models.Repos.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Vitality.Models.DTOs.DropDown;
using Vitality.Models.EntityClasses;
using Vitality.Models.Repos;
using Vitality.Models.CommonMethods;

namespace DudeMeds.Models.Repos.Services
{
    public class DropDownsRepo : BaseRepo , IDropDownsRepo
    {
        private readonly IMapper _mapper;
        public DropDownsRepo(IMapper mapper)
        {
            _mapper = mapper;
        }

        public List<GetAllCountryResponseDTO> GetAllCountry()
        {
            List<GetAllCountryResponseDTO> response = new List<GetAllCountryResponseDTO>();
            List<SYS_Country> list = _db.SYS_Countries.ToList();
            response = _mapper.Map<List<GetAllCountryResponseDTO>>(list);
            return response;
        }

        public List<DropdownResponseDTO> GetUSCities()
        {
            List<SYS_State> USStates = _db.SYS_States.Where(x => x.CountryId == 231).ToList();
            List<DropdownResponseDTO> response = new List<DropdownResponseDTO>();
            List<SYS_City> list = _db.SYS_Cities.ToList();
            foreach (var item in list)
            {
                if (USStates.Select(x => x.Id).Contains(item.StateId))
                {
                    DropdownResponseDTO responseDTO = new DropdownResponseDTO();
                    responseDTO.Id = item.Id;
                    responseDTO.Name = item.Name + " - " + USStates.Where(x => x.Id == item.StateId).FirstOrDefault().Name;
                    response.Add(responseDTO);
                }
            }
            return response.OrderBy(x => x.Name).ToList();
        }

        public DropdownResponseDTO GetUSState(int CityId)
        {
            DropdownResponseDTO response = new DropdownResponseDTO();
            SYS_City city = _db.SYS_Cities.Where(x => x.Id == CityId).FirstOrDefault();
            SYS_State state = _db.SYS_States.Where(x => x.Id == city.StateId).FirstOrDefault();

            response.Id = state.Id;
            response.Name = state.Name;
            response.ShortName = state.ShortName;

            return response;

        }

        public List<DropdownResponseDTO> GetCitiesByStateId(int StateId)
        {
            List<DropdownResponseDTO> response = new List<DropdownResponseDTO>();
            List<SYS_City> cities = _db.SYS_Cities.Where(x => x.StateId == StateId).ToList();

            foreach (var city in cities)
            {
                DropdownResponseDTO responseDTO = new DropdownResponseDTO();
                responseDTO.Id = city.Id;
                responseDTO.Name = city.Name;
                response.Add(responseDTO);
            }

            return response.OrderBy(x => x.Name).ToList();
        }

        public List<DropdownResponseDTO> GetAllUSARegion()
        {
            List<DropdownResponseDTO> response = new List<DropdownResponseDTO>();
            List<SYS_State> list = _db.SYS_States.Where(x => x.CountryId == 231).ToList();
            foreach (var item in list)
            {
                DropdownResponseDTO responseDTO = new DropdownResponseDTO();
                responseDTO.Id = item.Id;
                responseDTO.Name = item.Name;
                responseDTO.ShortName = item.ShortName;
                response.Add(responseDTO);
            }
            return response.OrderBy(x => x.Name).ToList();
        }

        public List<DropdownResponseDTO> GetAllRoles()
        {
            return _db.LK_Roles
                .AsNoTracking()
                .OrderBy(x => x.RoleName)
                .Select(x => new DropdownResponseDTO
                {
                    Id = x.RoleId,
                    Name = x.RoleName
                })
                .ToList();
        }

        public List<DropdownResponseDTO> GetAllRoleTitles()
        {
            return _db.LK_RoleTitles
                .AsNoTracking()
                .Where(x => x.IsActive == true)
                .OrderBy(x => x.RoleTitleName)
                .Select(x => new DropdownResponseDTO
                {
                    Id = x.RoleTitleId,
                    Name = x.RoleTitleName
                })
                .ToList();
        }

        public int SaveRoleTitle(SaveRoleTitleRequestDTO request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.RoleTitleName))
                return -1;

            var title = request.RoleTitleName.Trim();
            var duplicate = _db.LK_RoleTitles
                .AsNoTracking()
                .Any(r => r.RoleTitleName != null
                          && r.RoleTitleName.ToLower() == title.ToLower()
                          && r.RoleTitleId != request.RoleTitleId);
            if (duplicate) return -2;

            if (request.RoleTitleId > 0)
            {
                var existing = _db.LK_RoleTitles.FirstOrDefault(x => x.RoleTitleId == request.RoleTitleId);
                if (existing == null) return -1;
                existing.RoleTitleName = title;
                if (request.IsActive.HasValue) existing.IsActive = request.IsActive.Value;
                _db.SaveChanges();
                return existing.RoleTitleId;
            }

            var item = new LK_RoleTitle
            {
                RoleTitleName = title,
                IsActive = request.IsActive ?? true
            };
            _db.LK_RoleTitles.Add(item);
            _db.SaveChanges();
            return item.RoleTitleId;
        }

        public bool DeleteRoleTitle(int roleTitleId)
        {
            if (roleTitleId <= 0) return false;
            if (_db.SYS_UserDetails.AsNoTracking().Any(x => x.RoleTitleId == roleTitleId))
                return false;

            var item = _db.LK_RoleTitles.FirstOrDefault(x => x.RoleTitleId == roleTitleId);
            if (item == null) return false;
            _db.LK_RoleTitles.Remove(item);
            _db.SaveChanges();
            return true;
        }

        public List<GetAllProviderResponseDTO> GetAllDoctors(GetAllProviderRequestDTO request)
        {
            List<GetAllProviderResponseDTO> response = new List<GetAllProviderResponseDTO>();

            IQueryable<SYS_UserDetail> query = _db.SYS_UserDetails
                .AsNoTracking()
                .Include(x => x.Login)
                .Where(x => x.Login.RoleId == 4 && x.IsActive == true && x.Status == "Active");

            if (request.IsSupervisor == true)
            {
                query = query.Where(x => x.IsSupervisorRequired == false);
            }

            if (request.CategoryId.HasValue)
            {
                var providerIdsInCategory = _db.UR_ProviderCategories
                    .AsNoTracking()
                    .Where(pc => pc.CategoryId == request.CategoryId.Value && pc.ProviderId.HasValue)
                    .Select(pc => pc.ProviderId.Value)
                    .Distinct();

                query = query.Where(x => providerIdsInCategory.Contains(x.UserId));
            }

            if (request.IsAssign == true && request.FacilityId.HasValue)
            {

                var assignedProviderIds = _db.FC_UsersInFacilities
                    .AsNoTracking()
                    .Where(uf => uf.FacilityId == request.FacilityId.Value &&
                                 uf.IsAssign == true &&
                                 uf.UserId.HasValue)
                    .Select(uf => uf.UserId.Value)
                    .Distinct();

                query = query.Where(x => assignedProviderIds.Contains(x.UserId));
            }
            else if (request.IsAssign == false && request.FacilityId.HasValue)
            {

                var assignedProviderIds = _db.FC_UsersInFacilities
                    .AsNoTracking()
                    .Where(uf => uf.FacilityId == request.FacilityId.Value &&
                                 uf.IsAssign == true &&
                                 uf.UserId.HasValue)
                    .Select(uf => uf.UserId.Value)
                    .Distinct();

                query = query.Where(x => !assignedProviderIds.Contains(x.UserId));
            }

            var providers = query.ToList();

            foreach (var provider in providers)
            {
                GetAllProviderResponseDTO responseDTO = new GetAllProviderResponseDTO
                {
                    ProviderId = provider.UserId,
                    Name = $"{provider.FirstName} {provider.LastName}".Trim(),
                    ProfileUrl = !string.IsNullOrWhiteSpace(provider.ProfileUrl)
                        ? provider.ProfileUrl
                        : provider.Login?.ProfileUrl,
                    Bio = provider.Bio
                };
                response.Add(responseDTO);
            }

            return response.OrderBy(x => x.Name).ToList();
        }

        public List<GetAllProviderResponseDTO> GetAllUnAssignedProviders(GetAllUnAssignedProvidersRequestDTO request)
        {
            List<GetAllProviderResponseDTO> response = new List<GetAllProviderResponseDTO>();
            List<long?> excludedUserIds = new List<long?>();
            excludedUserIds = _db.FC_UsersInFacilities.Where(x => x.FacilityId == request.FacilityId).Select(x => x.UserId).ToList();
            List<SYS_UserDetail> list = _db.SYS_UserDetails
                .Include(x => x.Login)
                .Where(x => !excludedUserIds.Contains((long)x.UserId) && x.IsActive == true && x.Status == "Active" && x.Login.RoleId == 4)
                .ToList();

            foreach (var item in list)
            {
                GetAllProviderResponseDTO responseDTO = new GetAllProviderResponseDTO
                {
                    ProviderId = item.UserId,
                    Name = $"{item.FirstName} {item.LastName}".Trim(),
                    ProfileUrl = !string.IsNullOrWhiteSpace(item.ProfileUrl)
                        ? item.ProfileUrl
                        : item.Login?.ProfileUrl,
                    Bio = item.Bio
                };
                response.Add(responseDTO);
            }
            return response.OrderBy(x => x.Name).ToList();
        }

        public List<GetProviderScheduledSlotsResponseDTO> GetProviderScheduledSlots(GetProviderScheduledSlotsRequestDTO request)
        {

            var requestDateOnly = request.Date!.Value.Date;
            int? viewerOffsetMinutes = request.ClientTimezoneOffsetMinutes;

            DateTime dayStart;
            DateTime dayEnd;
            DateTime? userUtcNow = null;
            bool filterByUtcRange = viewerOffsetMinutes.HasValue;
            bool isViewingToday = request.ClientCurrentTime.HasValue
                && requestDateOnly == request.ClientCurrentTime.Value.Date;

            if (filterByUtcRange)
            {

                dayStart = requestDateOnly.AddMinutes(-viewerOffsetMinutes.Value);
                dayEnd = dayStart.AddDays(1);
                if (request.ClientCurrentTime.HasValue)
                    userUtcNow = request.ClientCurrentTime.Value.AddMinutes(-viewerOffsetMinutes.Value);
            }
            else
            {
                dayStart = requestDateOnly;
                dayEnd = dayStart.AddDays(1);
                if (isViewingToday)
                    userUtcNow = request.ClientCurrentTime!.Value;
            }

            var query =
                from s in _db.UR_ProviderScheduledSlots.AsNoTracking()
                join u in _db.SYS_UserDetails.AsNoTracking()
                    on s.ProviderId equals u.UserId
                where s.IsActive == true
                      && s.SlotDate.HasValue

                      && (s.Status == "Available" || s.Status == null)
                      && !_db.PT_PatientAppointmentSlots
                              .Any(ap => ap.ProviderScheduledSlotId == s.ProviderScheduledSlotId)
                select new { Slot = s, User = u };

            if (filterByUtcRange)
            {

                var slotDateMin = dayStart.Date;
                var slotDateMax = dayEnd.Date;
                query = query.Where(x =>
                    x.Slot.SlotDate.Value.Date >= slotDateMin
                    && x.Slot.SlotDate.Value.Date <= slotDateMax);
            }
            else
            {
                TimeSpan minStart = isViewingToday ? request.ClientCurrentTime!.Value.TimeOfDay : TimeSpan.Zero;
                query = query.Where(x =>
                    x.Slot.SlotDate.Value.Date >= dayStart
                    && x.Slot.SlotDate.Value.Date < dayEnd
                    && x.Slot.StartTime >= minStart);
            }

            if (request.ProviderId.HasValue)
            {

                var providerId = request.ProviderId.Value;

                if (request.CategoryId.HasValue)
                {
                    var isInCategory = _db.UR_ProviderCategories
                        .AsNoTracking()
                        .Any(pc => pc.ProviderId == providerId && pc.CategoryId == request.CategoryId.Value);

                    if (!isInCategory)
                    {

                        return new List<GetProviderScheduledSlotsResponseDTO>();
                    }
                }

                if (request.FacilityId.HasValue)
                {
                    var isAssignedToFacility = _db.FC_UsersInFacilities
                        .AsNoTracking()
                        .Any(uf => uf.UserId == providerId &&
                                   uf.FacilityId == request.FacilityId.Value &&
                                   uf.IsAssign == true);

                    if (!isAssignedToFacility)
                    {

                        return new List<GetProviderScheduledSlotsResponseDTO>();
                    }
                }

                query = query.Where(x => x.Slot.ProviderId == providerId);
            }
            else
            {

                var categoryProviderIds =
                    _db.UR_ProviderCategories
                       .AsNoTracking()
                       .Where(c => c.CategoryId == request.CategoryId && c.ProviderId.HasValue)
                       .Select(c => c.ProviderId!.Value)
                       .Distinct();

                IQueryable<long> facilityProviderIds = Enumerable.Empty<long>().AsQueryable();
                if (request.FacilityId.HasValue)
                {
                    facilityProviderIds =
                        _db.FC_UsersInFacilities
                           .AsNoTracking()
                           .Where(pf => pf.FacilityId == request.FacilityId.Value &&
                                        pf.IsAssign == true &&
                                        pf.UserId.HasValue)
                           .Select(pf => pf.UserId!.Value)
                           .Distinct();
                }

                var eligibleProviderIds = request.FacilityId.HasValue
                    ? categoryProviderIds.Where(pid => facilityProviderIds.Contains(pid))
                    : categoryProviderIds;

                query = query.Where(x => x.Slot.ProviderId.HasValue &&
                                         eligibleProviderIds.Contains(x.Slot.ProviderId.Value));
            }

            var list = query
                .OrderBy(x => x.Slot.ProviderId)
                .ThenBy(x => x.Slot.StartTime)
                .ToList();

            var dayStartUtc = dayStart;
            var dayEndUtc = dayEnd;
            var minUtcForToday = userUtcNow;
            var applyTodayFilter = filterByUtcRange && isViewingToday && minUtcForToday.HasValue;
            var nowUtc = userUtcNow ?? DateTime.UtcNow;
            var filteredList = filterByUtcRange
                ? list.Where(x =>
                {
                    if (!x.Slot.SlotDate.HasValue) return false;
                    var slotUtc = x.Slot.SlotDate.Value.Add(x.Slot.StartTime);
                    if (slotUtc < nowUtc) return false;
                    if (slotUtc < dayStartUtc || slotUtc >= dayEndUtc) return false;
                    if (applyTodayFilter && slotUtc < minUtcForToday!.Value) return false;
                    return true;
                })
                : list.Where(x =>
                {
                    if (!x.Slot.SlotDate.HasValue) return false;
                    var slotUtc = x.Slot.SlotDate.Value.Add(x.Slot.StartTime);
                    return slotUtc >= nowUtc;
                });

            int offset = viewerOffsetMinutes ?? 0;
            return filteredList.Select(x =>
            {
                DateTime? slotDate = x.Slot.SlotDate;
                TimeSpan startTime = x.Slot.StartTime;
                TimeSpan endTime = x.Slot.EndTime;
                if (offset != 0 && slotDate.HasValue)
                {
                    var utcStart = slotDate.Value.Add(startTime);
                    var utcEnd = slotDate.Value.Add(endTime);
                    var localStart = utcStart.AddMinutes(offset);
                    var localEnd = utcEnd.AddMinutes(offset);
                    slotDate = localStart.Date;
                    startTime = localStart.TimeOfDay;
                    endTime = localEnd.TimeOfDay;
                }
                var startDateTime = DateTime.Today.Add(startTime);
                var endDateTime = DateTime.Today.Add(endTime);

                return new
                {
                    x.Slot.ProviderScheduledSlotId,
                    SlotDate = slotDate,
                    StartTime = startDateTime.ToString("h:mm tt"),
                    EndTime = endDateTime.ToString("h:mm tt"),
                    x.Slot.Duration,
                    x.Slot.ProviderId,
                    x.Slot.FacilityId,
                    ProviderName =
                        ((x.User.FirstName ?? string.Empty).Trim()
                         + " " + (x.User.LastName ?? string.Empty).Trim()).Trim()
                };
            })
            .OrderBy(x => x.SlotDate)
            .ThenBy(x => DateTime.ParseExact(x.StartTime, "h:mm tt", null))
            .Select(x => new GetProviderScheduledSlotsResponseDTO
            {
                ProviderScheduledSlotId = x.ProviderScheduledSlotId,
                SlotDate = x.SlotDate,
                StartTime = x.StartTime,
                EndTime = x.EndTime,
                Duration = x.Duration,
                ProviderId = x.ProviderId,
                FacilityId = x.FacilityId,
                ProviderName = x.ProviderName
            })
            .ToList();
        }

        public List<GetAllFacilitiesDropDownResponseDTO> GetAllFacilities(long organizationId)
        {

            var query = _db.SYS_Facilities
                .AsNoTracking()
                .Where(x => x.IsActive == true
                            && x.Status != null
                            && x.Status.Trim().ToLower() == "active");

            if (organizationId != 0)
                query = query.Where(x => x.OrganizationId == organizationId);

            var response = query
                .OrderBy(f => f.TitleLong)
                .Select(f => new GetAllFacilitiesDropDownResponseDTO
                {
                    FacilityId = f.FacilityId,
                    Titlelong = f.TitleLong,
                    Titleshort = f.TitleShort,
                    Guid = f.Guid,
                })
                .ToList();
            return response;
        }

        public List<GetAllFacilitiesDropDownResponseDTO> GetActiveFacilitiesByCategoryId(long categoryId)
        {
            var response = (from f in _db.SYS_Facilities.AsNoTracking()
                            join fc in _db.PD_FacilityCategories.AsNoTracking() on f.FacilityId equals fc.FacilityId
                            join o in _db.SYS_Organizations.AsNoTracking() on f.OrganizationId equals o.OrganizationId into orgGroup
                            from o in orgGroup.DefaultIfEmpty()
                            where fc.CategoryId == categoryId
                                  && fc.IsActive == true
                                  && f.IsActive == true && f.Status == "Active"
                            orderby f.TitleLong
                            select new GetAllFacilitiesDropDownResponseDTO
                            {
                                FacilityId = f.FacilityId,
                                Titlelong = f.TitleLong,
                                Titleshort = f.TitleShort,
                                Guid = f.Guid,
                                OrganizationId = f.OrganizationId,
                                OrganizationName = o != null ? o.TitleLong : null,
                            })
                            .Distinct()
                            .ToList();
            return response;
        }

        public List<GetAllFacilitiesDropDownResponseDTO> GetAllFacilitiesByProviderId(long ProviderId)
        {
            List<GetAllFacilitiesDropDownResponseDTO> response = new List<GetAllFacilitiesDropDownResponseDTO>();
            List<SYS_Facility> list = new List<SYS_Facility>();

             list =     (from f in _db.SYS_Facilities
                        join fu in _db.FC_UsersInFacilities on f.FacilityId equals fu.FacilityId
                        join u in _db.SYS_UserDetails on fu.UserId equals u.UserId
                        where u.UserId == ProviderId
                        select f)
                       .Distinct()
                       .AsNoTracking()
                       .ToList();

            foreach (var item in list)
            {
                GetAllFacilitiesDropDownResponseDTO responseDTO = new GetAllFacilitiesDropDownResponseDTO();
                   responseDTO.FacilityId = item.FacilityId;
                responseDTO.Titlelong = item.TitleLong;
                responseDTO.Titleshort = item.TitleShort;
                responseDTO.Guid = item.Guid;
                responseDTO.OrganizationId = item.OrganizationId;
                responseDTO.OrganizationName = _db.SYS_Organizations.Where(x => x.OrganizationId == item.OrganizationId).Select(x => x.TitleLong).FirstOrDefault();
                response.Add(responseDTO);
            }
            return response.OrderBy(x => x.Titlelong).ToList();
        }
        public List<GetAllProductsDropDownResponseDTO> GetAllProducts(GetAllProductsDropDownRequestDTO request)
        {
            List<GetAllProductsDropDownResponseDTO> response = new List<GetAllProductsDropDownResponseDTO>();
            var products = new List<SYS_Product>();
            List<long> excludedProductIds = new List<long>();
            if (request.IsBundle == false)
            {
                excludedProductIds = (from BD in _db.PD_DrugVarientsInBundles join D in _db.PD_Drugs on BD.DrugId equals D.DrugId where BD.BundleId == request.BundleId select D.ProductId).ToList();
                products = _db.SYS_Products.Where(x => !excludedProductIds.Contains((long)x.ProductId) && x.IsActive == true && x.ProductType != "Bundle").ToList();
            }
            else
            {
                products = _db.SYS_Products.Where(x => x.IsActive == true).ToList();
            }
            foreach (var product in products)
            {
                var dto = new GetAllProductsDropDownResponseDTO();
                if (product.ProductType == "Drug")
                {
                    PD_Drug drug = new PD_Drug();
                    if (request.CategoryId != null)
                    {
                        drug = _db.PD_Drugs.Where(d => d.ProductId == product.ProductId && d.CategoryId == request.CategoryId).FirstOrDefault();
                    }
                    else
                    {
                        drug = _db.PD_Drugs.Where(d => d.ProductId == product.ProductId).FirstOrDefault();
                    }
                    if (drug != null)
                    {
                        dto.ProductId = drug.ProductId;
                        dto.ProductName = drug.Name;
                        dto.CategoryId = drug.CategoryId;
                        dto.CategoryName = _db.PD_Categories.Where(x => x.CategoryId == drug.CategoryId).Select(x => x.CategoryName).FirstOrDefault();
                        dto.ProductType = "Drug";
                        dto.DrugType = drug.Type;
                    }
                    else
                    {
                        continue;
                    }
                }
                else if (product.ProductType == "Bundle")
                {
                    PD_Bundle bundle = _db.PD_Bundles.Where(b => b.ProductId == product.ProductId).FirstOrDefault();
                    dto.ProductId = bundle.ProductId;
                    dto.ProductName = bundle.Name;
                    dto.ProductType = "Bundle";
                }
                else
                {
                    continue;
                }

                response.Add(dto);
            }

            return response;
        }

        public List<GetAllDrugsDropDownResponseDTO> GetAllDrugs(GetAllProductsDropDownRequestDTO request)
        {
            const long empowerCatalogId = 1;
            var catalogId = (request?.CatalogId.HasValue == true && request.CatalogId.Value > 0)
                ? request.CatalogId.Value
                : empowerCatalogId;
            var searchTerm = request?.SearchTerm?.Trim();

            if (string.IsNullOrWhiteSpace(searchTerm))
                return new List<GetAllDrugsDropDownResponseDTO>();

            var like = $"%{searchTerm}%";

            if (request?.FacilityId.HasValue == true && request.FacilityId.Value > 0)
            {
                var facilityId = request.FacilityId.Value;
                var isCatalogAssigned = _db.PC_CATALOGFACILITYASSIGNMENTs.AsNoTracking()
                    .Any(a => a.CatalogId == catalogId && a.FacilityId == facilityId && a.IsActive == true);
                if (!isCatalogAssigned)
                    return new List<GetAllDrugsDropDownResponseDTO>();
            }

            var drugs = (from d in _db.PD_Drugs.AsNoTracking()
                         join c in _db.PD_Categories.AsNoTracking()
                             on d.CategoryId equals c.CategoryId into cj
                         from c in cj.DefaultIfEmpty()
                         where d.IsActive == true
                           && d.CatalogId == catalogId
                            && d.DosageForm != "Supplies"
                            && d.Status == "Active"
                            && (!request.CategoryId.HasValue || d.CategoryId == request.CategoryId.Value)
                            && EF.Functions.Like(d.Name ?? "", like)
                         orderby d.Name, d.DrugId
                         select new
                         {
                             d.DrugId,
                             d.ProductId,
                             d.CategoryId,
                             CategoryName = c != null ? c.CategoryName : null,
                             d.Name,
                             d.Type,
                             d.PackageSize,
                             d.Markup,
                             d.ControlSubstance,
                             d.SuggestedRetail,
                             d.Price,
                             d.ComparePrice,
                             d.Strenght,
                             d.Refrigerated,
                             d.ItemDesignatorID,
                             d.DosageForm
                         })
                         .ToList();

            if (drugs.Count == 0)
                return new List<GetAllDrugsDropDownResponseDTO>();

            var drugIds = drugs.Select(x => x.DrugId).ToList();

            var ptgAll = _db.PC_PHARMTOGLOBALs
                .AsNoTracking()
                .Where(p => drugIds.Contains(p.DrugId ?? 0) && p.IsActive == true)
                .ToList();

            var latestPtgByDrug = ptgAll
                .GroupBy(p => p.DrugId ?? 0)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.PharmToGlobalId).First());

            return drugs.Select(d =>
            {
                latestPtgByDrug.TryGetValue(d.DrugId, out var ptg);
                return new GetAllDrugsDropDownResponseDTO
                {
                    DrugId = d.DrugId,
                    ProductId = d.ProductId,
                    CategoryId = d.CategoryId,
                    CategoryName = d.CategoryName,
                    ProductName = d.Name,
                    ProductType = "Drug",
                    DrugType = d.Type,
                    PackageSize = d.PackageSize,
                    Markup = d.Markup,
                    ControlSubstance = d.ControlSubstance,
                    SuggestedRetail = d.SuggestedRetail,
                    Price = d.Price,
                    ComparePrice = d.ComparePrice,
                    Strenght = d.Strenght,
                    Refrigerated = d.Refrigerated,
                    ItemDesignatorID = d.ItemDesignatorID,
                    DosageForm = d.DosageForm,
                    WholesalePrice = ptg?.WholesalePrice
                };
            }).ToList();
        }

        public List<GetAllDrugsDropDownResponseDTO> GetAllCustomDrugs(GetAllProductsDropDownRequestDTO request)
        {

            return GetAllDrugs(request);
        }

        public async Task<List<GetAllDrugsDropDownResponseDTO>> GetAllDrugsAndSuppliesAsync(GetAllProductsDropDownRequestDTO request, CancellationToken ct = default)
        {
            const long empowerCatalogId = 1;
            var catalogId = (request?.CatalogId.HasValue == true && request.CatalogId.Value > 0)
                ? request.CatalogId.Value
                : empowerCatalogId;
            var searchTerm = request?.SearchTerm?.Trim();

            if (string.IsNullOrWhiteSpace(searchTerm))
                return new List<GetAllDrugsDropDownResponseDTO>();

            var like = $"%{searchTerm}%";

            var drugs = await (from d in _db.PD_Drugs.AsNoTracking()
                               join c in _db.PD_Categories.AsNoTracking()
                                   on d.CategoryId equals c.CategoryId into cj
                               from c in cj.DefaultIfEmpty()
                               where d.IsActive == true
                                 && d.CatalogId == catalogId
                                  && d.Status == "Active"
                                  && (!request.CategoryId.HasValue || d.CategoryId == request.CategoryId.Value)
                                  && EF.Functions.Like(d.Name ?? "", like)
                               orderby d.Name, d.DrugId
                               select new
                               {
                                   d.DrugId,
                                   d.ProductId,
                                   d.CategoryId,
                                   CategoryName = c != null ? c.CategoryName : null,
                                   d.Name,
                                   d.Type,
                                   d.PackageSize,
                                   d.Markup,
                                   d.ControlSubstance,
                                   d.SuggestedRetail,
                                   d.Price,
                                   d.ComparePrice,
                                   d.Strenght,
                                   d.Refrigerated,
                                   d.ItemDesignatorID,
                                   d.DosageForm
                               })
                               .ToListAsync(ct);

            if (drugs.Count == 0)
                return new List<GetAllDrugsDropDownResponseDTO>();

            var drugIds = drugs.Select(x => x.DrugId).ToList();

            Dictionary<long, decimal>? clinicPriceByDrug = null;
            if (request.FacilityId.HasValue)
            {
                var ctpAll = await _db.PC_CLINICTOPATIENTs
                    .AsNoTracking()
                    .Where(cp => drugIds.Contains(cp.DrugId) && cp.FacilityId == request.FacilityId.Value && cp.IsActive == true)
                    .ToListAsync(ct);

                clinicPriceByDrug = ctpAll
                    .GroupBy(cp => cp.DrugId)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(cp => cp.ClinicToPatientId).First().ClinicSuggestedRetailPrice);
            }

            return drugs.Select(d =>
            {
                decimal? price = d.Price;
                if (clinicPriceByDrug != null && clinicPriceByDrug.TryGetValue(d.DrugId, out var clinicPrice))
                    price = clinicPrice;

                return new GetAllDrugsDropDownResponseDTO
                {
                    DrugId = d.DrugId,
                    ProductId = d.ProductId,
                    CategoryId = d.CategoryId,
                    CategoryName = d.CategoryName,
                    ProductName = d.Name,
                    ProductType = "Drug",
                    DrugType = d.Type,
                    PackageSize = d.PackageSize,
                    Markup = d.Markup,
                    ControlSubstance = d.ControlSubstance,
                    SuggestedRetail = d.SuggestedRetail,
                    Price = price,
                    ComparePrice = d.ComparePrice,
                    Strenght = d.Strenght,
                    Refrigerated = d.Refrigerated,
                    ItemDesignatorID = d.ItemDesignatorID,
                    DosageForm = d.DosageForm
                };
            }).ToList();
        }

        public async Task<List<GetAllDrugsDropDownResponseDTO>> GetAllCustomDrugsAndSuppliesAsync(GetAllProductsDropDownRequestDTO request, CancellationToken ct = default)
        {

            return await GetAllDrugsAndSuppliesAsync(request, ct);
        }

        public List<GetAllDrugsDropDownResponseDTO> GetAllDrugsSupplies(GetAllProductsDropDownRequestDTO request)
        {
            const long empowerCatalogId = 1;
            var catalogId = (request?.CatalogId.HasValue == true && request.CatalogId.Value > 0)
                ? request.CatalogId.Value
                : empowerCatalogId;
            var searchTerm = request?.SearchTerm?.Trim();

            if (string.IsNullOrWhiteSpace(searchTerm))
                return new List<GetAllDrugsDropDownResponseDTO>();

            var like = $"%{searchTerm}%";

            if (request?.FacilityId.HasValue == true && request.FacilityId.Value > 0)
            {
                var facilityId = request.FacilityId.Value;
                var isCatalogAssigned = _db.PC_CATALOGFACILITYASSIGNMENTs.AsNoTracking()
                    .Any(a => a.CatalogId == catalogId && a.FacilityId == facilityId && a.IsActive == true);
                if (!isCatalogAssigned)
                    return new List<GetAllDrugsDropDownResponseDTO>();
            }

            var drugs = (from d in _db.PD_Drugs.AsNoTracking()
                         join c in _db.PD_Categories.AsNoTracking()
                             on d.CategoryId equals c.CategoryId into cj
                         from c in cj.DefaultIfEmpty()
                         where d.IsActive == true
                           && d.CatalogId == catalogId
                            && d.DosageForm == "Supplies"
                            && d.Status == "Active"
                            && (!request.CategoryId.HasValue || d.CategoryId == request.CategoryId.Value)
                            && EF.Functions.Like(d.Name ?? "", like)
                         orderby d.Name, d.DrugId
                         select new
                         {
                             d.DrugId,
                             d.ProductId,
                             d.CategoryId,
                             CategoryName = c != null ? c.CategoryName : null,
                             d.Name,
                             d.Type,
                             d.PackageSize,
                             d.Markup,
                             d.ControlSubstance,
                             d.SuggestedRetail,
                             d.Price,
                             d.ComparePrice,
                             d.Strenght,
                             d.Refrigerated,
                             d.ItemDesignatorID,
                             d.DosageForm
                         })
                         .ToList();

            if (drugs.Count == 0)
                return new List<GetAllDrugsDropDownResponseDTO>();

            var drugIds = drugs.Select(x => x.DrugId).ToList();

            var ptgAll = _db.PC_PHARMTOGLOBALs
                .AsNoTracking()
                .Where(p => drugIds.Contains(p.DrugId ?? 0) && p.IsActive == true)
                .ToList();

            var latestPtgByDrug = ptgAll
                .GroupBy(p => p.DrugId ?? 0)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.PharmToGlobalId).First());

            return drugs.Select(d =>
            {
                latestPtgByDrug.TryGetValue(d.DrugId, out var ptg);
                return new GetAllDrugsDropDownResponseDTO
                {
                    DrugId = d.DrugId,
                    ProductId = d.ProductId,
                    CategoryId = d.CategoryId,
                    CategoryName = d.CategoryName,
                    ProductName = d.Name,
                    ProductType = "Drug",
                    DrugType = d.Type,
                    PackageSize = d.PackageSize,
                    Markup = d.Markup,
                    ControlSubstance = d.ControlSubstance,
                    SuggestedRetail = d.SuggestedRetail,
                    Price = d.Price,
                    ComparePrice = d.ComparePrice,
                    Strenght = d.Strenght,
                    Refrigerated = d.Refrigerated,
                    ItemDesignatorID = d.ItemDesignatorID,
                    DosageForm = d.DosageForm,
                    WholesalePrice = ptg?.WholesalePrice
                };
            }).ToList();
        }

        public List<GetAllDrugsDropDownResponseDTO> GetAllCustomDrugsSupplies(GetAllProductsDropDownRequestDTO request)
        {

            return GetAllDrugsSupplies(request);
        }

        public List<GetAllProductsDropDownResponseDTO> GetAllQuestionnaireProducts(GetAllQuestionnaireProductsDropDownRequestDTO request)
        {
            List<GetAllProductsDropDownResponseDTO> response = new List<GetAllProductsDropDownResponseDTO>();
                List<long> excludedProductIds = new List<long>();
                if (request.QuestionnaireId == null)
                {
                    excludedProductIds = _db.SYS_QuestionnairesInProducts.Select(x => x.ProductId).ToList();
                }
                else
                {
                    excludedProductIds = _db.SYS_QuestionnairesInProducts.Where(x => x.QuestionnaireId != request.QuestionnaireId).Select(x => x.ProductId).ToList();
                }
                List<SYS_Product> products = _db.SYS_Products.Where(x => !excludedProductIds.Contains((long)x.ProductId) && x.IsActive == true).ToList();
                foreach (var product in products)
                {
                    var dto = new GetAllProductsDropDownResponseDTO();
                    if (product.ProductType == "Drug")
                    {
                        PD_Drug drug = _db.PD_Drugs.Where(d => d.ProductId == product.ProductId).FirstOrDefault();
                        if (drug != null)
                        {
                            dto.ProductId = drug.ProductId;
                            dto.ProductName = drug.Name;
                            dto.CategoryId = drug.CategoryId;
                            dto.CategoryName = _db.PD_Categories.Where(x => x.CategoryId == drug.CategoryId).Select(x => x.CategoryName).FirstOrDefault();
                            dto.DrugType = drug.Type;
                            dto.ProductType = "Drug";
                        }
                        else
                        {
                            continue;
                        }
                    }
                    else if (product.ProductType == "Bundle")
                    {
                        PD_Bundle bundle = bundle = _db.PD_Bundles.Where(b => b.ProductId == product.ProductId).FirstOrDefault();
                        if (bundle != null)
                        {
                            dto.ProductId = bundle.ProductId;
                            dto.ProductName = bundle.Name;
                            dto.ProductType = "Bundle";
                        }
                        else
                        {
                            continue;
                        }
                }
                    else
                    {
                        continue;
                    }
                    response.Add(dto);
            }
            return response;
        }

        public List<GetAllInTakeFormProductsDropDownResponseDTO> GetAllInTakeFormProducts(GetAllInTakeFormProductsDropDownRequestDTO request)
        {
            List<GetAllInTakeFormProductsDropDownResponseDTO> response = new List<GetAllInTakeFormProductsDropDownResponseDTO>();
                var products = _db.SYS_Products.Where(x => x.IsActive == true).ToList();
                foreach (var product in products)
                {
                    var dto = new GetAllInTakeFormProductsDropDownResponseDTO();
                    if (product.ProductType == "Drug")
                    {
                        PD_Drug drug = new PD_Drug();
                        if (request.CategoryId != null)
                        {
                            drug = _db.PD_Drugs.Where(d => d.ProductId == product.ProductId && d.CategoryId == request.CategoryId).FirstOrDefault();
                        }
                        else
                        {
                            drug = _db.PD_Drugs.Where(d => d.ProductId == product.ProductId).FirstOrDefault();
                        }
                        if (drug != null)
                        {

                            dto.ProductId = drug.ProductId;
                            dto.ProductName = drug.Name;
                            dto.CategoryId = drug.CategoryId;
                            dto.CategoryName = _db.PD_Categories.Where(x => x.CategoryId == drug.CategoryId).Select(x => x.CategoryName).FirstOrDefault();
                            dto.ProductType = "Drug";
                            dto.DrugType = drug.Type;
                            dto.Price = drug.Price;
                            dto.Quantity = drug.Quantity;
                            dto.QuantityUnit = drug.QuantityUnit;
                            dto.ImageURL = drug.RegularImageURL;
                        }
                        else
                        {
                            continue;
                        }

                    }
                    else if (product.ProductType == "Bundle")
                    {
                        PD_Bundle bundle = _db.PD_Bundles.Where(b => b.ProductId == product.ProductId).FirstOrDefault();
                        dto.ProductId = bundle.ProductId;
                        dto.ProductName = bundle.Name;
                        dto.ProductType = "Bundle";
                    }
                    else if (product.ProductType == "LabTest")
                    {
                        var labTest = _db.PD_LabTests.Where(l => l.ProductId == product.ProductId).FirstOrDefault();
                        if (labTest != null)
                        {
                            dto.ProductId = labTest.ProductId;
                            dto.ProductName = labTest.Name;
                            dto.ProductType = labTest.ProductType;
                        }
                    }
                    else if (product.ProductType == "DigitalProduct")
                    {
                        var digitalProduct = _db.PD_DigitalProducts.Where(dp => dp.ProductId == product.ProductId).FirstOrDefault();
                        if (digitalProduct != null)
                        {
                            dto.ProductId = digitalProduct.ProductId;
                            dto.ProductName = digitalProduct.Name;
                            dto.ProductType = digitalProduct.ProductType;
                        }
                    }
                    else
                    {
                        continue;
                    }

                    response.Add(dto);
                }

            return response;
        }

        public List<GetAllCategoriesDropDownResponseDTO> GetAllCategories(long? facilityId = null)
        {
            List<GetAllCategoriesDropDownResponseDTO> response = new List<GetAllCategoriesDropDownResponseDTO>();
            List<PD_Category> list;

            if (facilityId.HasValue && facilityId.Value > 0)
            {
                var categoryIdsForFacility = _db.PD_FacilityCategories
                    .AsNoTracking()
                    .Where(fc => fc.FacilityId == facilityId.Value && fc.IsActive == true)
                    .Select(fc => fc.CategoryId)
                    .ToList();
                list = _db.PD_Categories
                    .Where(c => c.IsActive == true && categoryIdsForFacility.Contains(c.CategoryId))
                    .ToList();
            }
            else
            {
                list = _db.PD_Categories.Where(x => x.IsActive == true).ToList();
            }

            foreach (var item in list)
            {
                var responseDTO = new GetAllCategoriesDropDownResponseDTO
                {
                    CategoryId = item.CategoryId,
                    CategoryName = item.CategoryName,
                    ImageURL = item.ImageURL
                };
                response.Add(responseDTO);
            }
            return response.OrderBy(x => x.CategoryName).ToList();
        }

        public List<GetAllConditionsDropDownResponseDTO> GetAllConditions(long CategoryId)
        {
            List<GetAllConditionsDropDownResponseDTO> response = new List<GetAllConditionsDropDownResponseDTO>();
            List<PD_Condition> list = new List<PD_Condition>();
            list = _db.PD_Conditions.Where(x => x.CategoryId == CategoryId && x.IsActive == true).ToList();
            foreach (var item in list)
            {
                GetAllConditionsDropDownResponseDTO responseDTO = new GetAllConditionsDropDownResponseDTO();
                responseDTO.ConditionId = item.ConditionId;
                responseDTO.ConditionName = item.ConditionName;
                responseDTO.ImageURL = item.ImageURL;
                response.Add(responseDTO);
            }
            return response.OrderBy(x => x.ConditionName).ToList();
        }

        public List<GetAllPharmaciesDropDownResponseDTO> GetAllPharmacies()
        {
            List<GetAllPharmaciesDropDownResponseDTO> response = new List<GetAllPharmaciesDropDownResponseDTO>();
            List<SYS_Pharmacy> list = new List<SYS_Pharmacy>();
            list = _db.SYS_Pharmacies.Where(x => x.IsActive == true).ToList();
            foreach (var item in list)
            {
                GetAllPharmaciesDropDownResponseDTO responseDTO = new GetAllPharmaciesDropDownResponseDTO();
                responseDTO.PharmacyId = item.PharmacyId;
                responseDTO.PharmacyName = item.PharmacyName;
                response.Add(responseDTO);
            }
            return response.OrderBy(x => x.PharmacyName).ToList();
        }

        public List<GetAllProviderGroupsDropDownResponseDTO> GetAllProviderGroups()
        {
            List<GetAllProviderGroupsDropDownResponseDTO> response = new List<GetAllProviderGroupsDropDownResponseDTO>();
            List<SYS_ProviderGroup> list = _db.SYS_ProviderGroups.Where(x => x.IsActive == true).ToList();
            foreach (var item in list)
            {
                GetAllProviderGroupsDropDownResponseDTO responseDTO = new GetAllProviderGroupsDropDownResponseDTO();
                responseDTO.GroupId = item.GroupId;
                responseDTO.GroupName = item.GroupName;
                response.Add(responseDTO);
            }
            return response.OrderBy(x => x.GroupName).ToList();
        }

        public async Task<List<GetAllPatientsDropDownResponseDTO>> GetAllPatientsAsync(long? facilityId, long? providerId)
        {
            var activePatients = _db.PT_Patients
                .AsNoTracking()
                .Where(p => p.IsActive == true && p.Status == "Active");

            if (providerId.HasValue && providerId.Value > 0)
            {
                var pid = providerId.Value;

                var response = await (
                    from a in _db.PT_PatientAppointmentSlots.AsNoTracking()
                    where a.IsActive == true
                       && a.ProviderId == pid
                       && a.PatientId != null
                    join p in activePatients on a.PatientId!.Value equals p.PatientId
                    select new GetAllPatientsDropDownResponseDTO
                    {
                        PatientId = p.PatientId,
                        PatientName = ((p.FirstName ?? "") + " " + (p.LastName ?? "")).Trim(),
                        FacilityId = a.FacilityId
                    })
                    .Distinct()
                    .OrderBy(x => x.PatientName)
                    .ThenBy(x => x.FacilityId)
                    .ToListAsync();

                return response;
            }

            if (facilityId.HasValue)
                activePatients = activePatients.Where(p => p.FacilityId == facilityId.Value);

            return await activePatients
                .Select(p => new GetAllPatientsDropDownResponseDTO
                {
                    PatientId = p.PatientId,
                    PatientName = ((p.FirstName ?? "") + " " + (p.LastName ?? "")).Trim(),
                    FacilityId = p.FacilityId
                })
                .OrderBy(x => x.PatientName)
                .ToListAsync();
        }

        public List<GetAllPatientTreatmentsDropDownResponseDTO> GetAllPatientTreatments(long? PatientId)
        {
            List<GetAllPatientTreatmentsDropDownResponseDTO> response = new List<GetAllPatientTreatmentsDropDownResponseDTO>();
            List<PT_PatientTreatment> list = _db.PT_PatientTreatments.Where(x => x.PatientId == PatientId && x.IsActive == true).ToList();
            foreach (var item in list)
            {
                PT_PatientPrescription patientPrescription = _db.PT_PatientPrescriptions.Where(x => x.PatientTreatmentId == item.PatientTreatmentId).FirstOrDefault();
                if (patientPrescription != null)
                {
                    continue;
                }
                else
                {
                    GetAllPatientTreatmentsDropDownResponseDTO responseDTO = new GetAllPatientTreatmentsDropDownResponseDTO();
                    responseDTO.PatientTreatmentId = item.PatientTreatmentId;
                    responseDTO.TreatmentDate = item.CreatedDate;
                    response.Add(responseDTO);
                }
            }
            return response.OrderBy(x => x.TreatmentDate).ToList();
        }

        public List<GetAllPatientOrdersDropDownResponseDTO> GetAllPatientOrders(long? PatientTreatmentId)
        {
            List<GetAllPatientOrdersDropDownResponseDTO> response = new List<GetAllPatientOrdersDropDownResponseDTO>();
            List<PT_PatientOrder> list = _db.PT_PatientOrders.Where(x => x.PatientTreamentId == PatientTreatmentId && x.IsActive == true).ToList();
            foreach (var item in list)
            {
                GetAllPatientOrdersDropDownResponseDTO responseDTO = new GetAllPatientOrdersDropDownResponseDTO();
                responseDTO.PatientOrderId = item.PatientOrderId;
                responseDTO.OrderDate = CommonMethods.ToLocalTime(item.CreatedDate);
                response.Add(responseDTO);
            }
            return response.OrderBy(x => x.OrderDate).ToList();
        }

        public List<GetAllSubscriptionsDropDownResponseDTO> GetAllSubscriptions()
        {
            List<GetAllSubscriptionsDropDownResponseDTO> response = new List<GetAllSubscriptionsDropDownResponseDTO>();
            List<SYS_Subscription> list = _db.SYS_Subscriptions.Where(x => x.IsActive == true && x.Status == "Active").ToList();
            foreach (var item in list)
            {
                GetAllSubscriptionsDropDownResponseDTO responseDTO = new GetAllSubscriptionsDropDownResponseDTO();
                responseDTO.SubscriptionId = item.SubscriptionId;
                responseDTO.PlanName = item.PlanName;
                response.Add(responseDTO);
            }
            return response.OrderBy(x => x.PlanName).ToList();
        }

        public async Task<List<GetBundleByIdResponse2DTO>> GetAllBundlesAsync(long facilityId, CancellationToken ct = default)
        {

            var query = _db.PD_Bundles
                .AsNoTracking()
                .Where(b => b.IsActive == true
                    && (b.Status == null || (b.Status != "Inactive" && b.Status != "Archived"))
                    && ((b.FacilityId == facilityId) || _db.PD_FacilityBundlePrices.AsNoTracking().Any(fbp => fbp.FacilityId == facilityId && fbp.BundleId == b.BundleId))
                    && (b.CategoryId == null || _db.PD_FacilityCategories.AsNoTracking().Any(fc => fc.FacilityId == facilityId && fc.CategoryId == b.CategoryId && fc.IsActive == true))
                )
                .Select(b => new GetBundleByIdResponse2DTO
                {
                    BundleId = b.BundleId,
                    ProductId = b.ProductId,
                    CategoryId = b.CategoryId,
                    Name = b.Name,
                    Description = b.Description,
                    RegularImageURL = b.RegularImageURL,

                    Price = (facilityId > 0
                                ? _db.PD_FacilityBundlePrices.AsNoTracking()
                                      .Where(fp => fp.FacilityId == facilityId && fp.BundleId == b.BundleId)
                                      .Select(fp => (decimal?)fp.ClinicPrice)
                                      .FirstOrDefault()
                                : null)
                            ?? b.Price,

                    ComparePrice = b.ComparePrice,
                    Status = b.Status,
                    Visits = b.visits
                })
                .OrderBy(x => x.Name)
                .ThenBy(x => x.BundleId);

            return await query.ToListAsync(ct);
        }
        public List<GetProviderScheduledSlotsResponseDTO> GetProviderScheduledSlotsByProvider(GetProviderScheduledSlotsByProviderRequestDTO request)
        {
            var providerId = request.ProviderId ?? 0;
            var requestDateOnly = request.Date!.Value.Date;
            int? viewerOffsetMinutes = request.ClientTimezoneOffsetMinutes;

            DateTime dayStart;
            DateTime dayEnd;
            bool filterByUtcRange = viewerOffsetMinutes.HasValue;

            if (filterByUtcRange)
            {
                dayStart = requestDateOnly.AddMinutes(-viewerOffsetMinutes.Value);
                dayEnd = dayStart.AddDays(1);
            }
            else
            {
                dayStart = requestDateOnly;
                dayEnd = dayStart.AddDays(1);
            }

            var query =
                from s in _db.UR_ProviderScheduledSlots.AsNoTracking()
                join u in _db.SYS_UserDetails.AsNoTracking()
                    on s.ProviderId equals u.UserId
                join a0 in _db.PT_PatientAppointmentSlots.AsNoTracking()
                    on s.ProviderScheduledSlotId equals a0.ProviderScheduledSlotId into appts
                from a in appts.DefaultIfEmpty()

                where s.IsActive == true
                   && s.ProviderId == providerId
                   && s.SlotDate.HasValue
                   && (!request.FacilityId.HasValue || s.FacilityId == request.FacilityId.Value)
                   && a == null
                select new { Slot = s, User = u };

            if (filterByUtcRange)
                query = query.Where(x =>
                    x.Slot.SlotDate.Value.Date >= dayStart.Date
                    && x.Slot.SlotDate.Value.Date <= dayEnd.Date);
            else
                query = query.Where(x =>
                    x.Slot.SlotDate.Value.Date >= dayStart
                    && x.Slot.SlotDate.Value.Date < dayEnd);

            var list = query.ToList();
            int offset = viewerOffsetMinutes ?? 0;
            var dayStartUtcByProvider = dayStart;
            var dayEndUtcByProvider = dayEnd;
            var nowUtc = DateTime.UtcNow;
            var listForResponse = filterByUtcRange
                ? list.Where(x =>
                {
                    if (!x.Slot.SlotDate.HasValue) return false;
                    var slotUtc = x.Slot.SlotDate.Value.Add(x.Slot.StartTime);
                    return slotUtc >= nowUtc && slotUtc >= dayStartUtcByProvider && slotUtc < dayEndUtcByProvider;
                })
                : list.Where(x =>
                {
                    if (!x.Slot.SlotDate.HasValue) return false;
                    var slotUtc = x.Slot.SlotDate.Value.Add(x.Slot.StartTime);
                    return slotUtc >= nowUtc;
                });
            return listForResponse.Select(x =>
            {
                DateTime? slotDate = x.Slot.SlotDate;
                TimeSpan startTime = x.Slot.StartTime;
                TimeSpan endTime = x.Slot.EndTime;
                if (offset != 0 && slotDate.HasValue)
                {
                    var utcStart = slotDate.Value.Add(startTime);
                    var utcEnd = slotDate.Value.Add(endTime);
                    var localStart = utcStart.AddMinutes(offset);
                    var localEnd = utcEnd.AddMinutes(offset);
                    slotDate = localStart.Date;
                    startTime = localStart.TimeOfDay;
                    endTime = localEnd.TimeOfDay;
                }
                var startDateTime = DateTime.Today.Add(startTime);
                var endDateTime = DateTime.Today.Add(endTime);

                return new
                {
                    x.Slot.ProviderScheduledSlotId,
                    SlotDate = slotDate,
                    StartTime = startDateTime.ToString("h:mm tt"),
                    EndTime = endDateTime.ToString("h:mm tt"),
                    x.Slot.Duration,
                    x.Slot.ProviderId,
                    x.Slot.FacilityId,
                    ProviderName = (
                        ((x.User.FirstName ?? string.Empty).Trim()
                        + " "
                        + (x.User.LastName ?? string.Empty).Trim()).Trim()
                    )
                };
            })
            .OrderBy(x => x.SlotDate)
            .ThenBy(x => DateTime.ParseExact(x.StartTime, "h:mm tt", null))
            .Select(x => new GetProviderScheduledSlotsResponseDTO
            {
                ProviderScheduledSlotId = x.ProviderScheduledSlotId,
                SlotDate = x.SlotDate,
                StartTime = x.StartTime,
                EndTime = x.EndTime,
                Duration = x.Duration,
                ProviderId = x.ProviderId,
                FacilityId = x.FacilityId,
                ProviderName = x.ProviderName
            })
            .ToList();
        }

        public List<CatalogResponseDTO> GetAllCatalogs(GetAllCatalogsRequestDTO request)
        {
            var query = _db.PD_Catalogs.AsNoTracking().Where(x=>x.IsActive == true);

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
                   _db.PC_CATALOGFACILITYASSIGNMENTs.Any(a => a.CatalogId == c.CatalogId && a.FacilityId == fid && a.IsActive == true));
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

            }).ToList();
        }
    }
}
