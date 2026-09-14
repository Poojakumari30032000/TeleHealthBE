using AutoMapper;
using Dapper;
using DudeMeds.Models.DTOs.Users;
using DudeMeds.Models.Repos.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Square.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vitality.Models.DTOs.Users;
using Vitality.Models.EntityClasses;
using Vitality.Models.Enums;
using Vitality.Models.Repos;
using Vitality.Models.Repos.Services.Auth;
using Vitality.Models.Repos.Services.Audit;

namespace DudeMeds.Models.Repos.Services
{
    public class UsersRepo : BaseRepo, IUsersRepo
    {
        private readonly IMapper _mapper;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IAuditService _auditService;
        private readonly IProviderHoursRepo _providerHoursRepo;

        public UsersRepo(IMapper mapper, IPasswordHasher passwordHasher, IAuditService auditService, IProviderHoursRepo providerHoursRepo)
        {
            _mapper = mapper;
            _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
            _auditService = auditService;
            _providerHoursRepo = providerHoursRepo;
        }

        public List<GetAllUsersResponseDTO> GetAllUsers(GetAllUsersRequestDTO request, out int totalUserCount)
        {

            var pageSize = (request?.PageSize ?? 0) > 0 ? request.PageSize : 25;
            var pageNumber = (request?.PageNumber ?? 0) > 0 ? request.PageNumber : 1;
            var title = string.IsNullOrWhiteSpace(request?.Title) ? null : request.Title.Trim();

            IQueryable<SYS_UserDetail> baseQuery = _db.SYS_UserDetails
                .AsNoTracking();

            if (request?.RequestingRoleId == (int)UserRole.ClinicAdmin)
            {
                baseQuery = baseQuery.Where(ur =>
                    !_db.SYS_Logins
                        .AsNoTracking()
                        .Any(ln => ln.LoginId == ur.LoginId && ln.RoleId == (int)UserRole.Provider));
            }

            if (request?.OrganizationId.HasValue == true)
                baseQuery = baseQuery.Where(ur => ur.OrganizationId == request.OrganizationId.Value);

            if (request?.Status != null && !string.IsNullOrWhiteSpace(request.Status))
                baseQuery = baseQuery.Where(ur => ur.Status == request.Status);

            if (!string.IsNullOrEmpty(title))
            {
                baseQuery = baseQuery.Where(ur =>
                    ((ur.FirstName ?? "") + " " + (ur.LastName ?? "")).Contains(title) ||
                    (ur.Email ?? "").Contains(title) ||
                    (ur.Phone ?? "").Contains(title));
            }

            if (request?.CategoryId.HasValue == true)
            {
                var categoryId = request.CategoryId.Value;
                baseQuery = baseQuery.Where(ur =>
                    _db.UR_ProviderCategories
                        .AsNoTracking()
                        .Any(pc => pc.ProviderId == ur.UserId && pc.CategoryId == categoryId));
            }

            if (request?.RoleId.HasValue == true)
            {
                var roleId = request.RoleId.Value;
                baseQuery = baseQuery.Where(ur =>
                    _db.SYS_Logins
                        .AsNoTracking()
                        .Any(ln => ln.LoginId == ur.LoginId && ln.RoleId == roleId));
            }

            if (request?.FacilityId.HasValue == true)
            {
                var facilityId = request.FacilityId.Value;
                baseQuery = baseQuery.Where(ur =>
                    _db.FC_UsersInFacilities
                        .AsNoTracking()
                        .Any(uf => uf.UserId == ur.UserId && uf.FacilityId == facilityId && uf.IsAssign == true));
            }

            totalUserCount = baseQuery.Count();

            var pageRows = baseQuery
                .OrderBy(ur => ur.FirstName)
                .ThenBy(ur => ur.UserId)
                .Skip(pageSize * (pageNumber - 1))
                .Take(pageSize)
                .Select(ur => new GetAllUsersResponseDTO
                {
                    Guid = ur.Guid,
                    UserId = ur.UserId,
                    UserName = ((ur.FirstName ?? "").Trim() + " " + (ur.LastName ?? "").Trim()).Trim(),
                    DOB = ur.DOB,
                    Email = ur.Email,
                    ProviderType = ur.ProviderType,
                    Phone = ur.Phone,
                    Address = ur.Address,
                    StateId = ur.StateId,
                    CityId = ur.CityId,
                    ZipCode = ur.ZipCode,
                    Status = ur.Status,
                    RoleId = _db.SYS_Logins
                        .AsNoTracking()
                        .Where(ln => ln.LoginId == ur.LoginId)
                        .Select(ln => (int?)ln.RoleId)
                        .FirstOrDefault() ?? 0,
                    RoleTitleId = ur.RoleTitleId,
                    RoleTitleName = _db.LK_RoleTitles
                        .AsNoTracking()
                        .Where(rt => rt.RoleTitleId == ur.RoleTitleId && rt.IsActive == true)
                        .Select(rt => rt.RoleTitleName)
                        .FirstOrDefault(),
                    IsSupervisorRequired = ur.IsSupervisorRequired,
                    UserRole = _db.LK_Roles
                        .AsNoTracking()
                        .Where(r => r.RoleId == _db.SYS_Logins
                            .AsNoTracking()
                            .Where(ln => ln.LoginId == ur.LoginId)
                            .Select(ln => ln.RoleId)
                            .FirstOrDefault())
                        .Select(r => r.RoleName)
                        .FirstOrDefault(),
                    StateName = _db.SYS_States
                        .AsNoTracking()
                        .Where(s => s.Id == ur.StateId)
                        .Select(s => s.Name)
                        .FirstOrDefault(),
                    CityName = _db.SYS_Cities
                        .AsNoTracking()
                        .Where(c => c.Id == ur.CityId)
                        .Select(c => c.Name)
                        .FirstOrDefault(),
                    CategoryId = new List<long>(),
                    CategoryName = new List<string>()
                })
                .ToList();

            if (pageRows.Count == 0)
                return pageRows;

            var userIds = pageRows.Select(u => u.UserId).ToList();
            var categories = _db.UR_ProviderCategories
                .AsNoTracking()
                .Where(pc => pc.ProviderId != null && userIds.Contains(pc.ProviderId.Value))
                .Join(_db.PD_Categories.AsNoTracking(),
                    pc => pc.CategoryId,
                    cat => cat.CategoryId,
                    (pc, cat) => new
                    {
                        ProviderId = pc.ProviderId.Value,
                        CategoryId = cat.CategoryId,
                        CategoryName = cat.CategoryName
                    })
                .ToList()
                .GroupBy(x => x.ProviderId)
                .ToDictionary(
                    g => g.Key,
                    g => new
                    {
                        Ids = g.Select(y => y.CategoryId).Distinct().ToList(),
                        Names = g.Select(y => y.CategoryName).Distinct().ToList()
                    });

            foreach (var user in pageRows)
            {
                if (categories.TryGetValue(user.UserId, out var catInfo))
                {
                    user.CategoryId.AddRange(catInfo.Ids);
                    user.CategoryName.AddRange(catInfo.Names);
                }
            }

            return pageRows;
        }

        public GetUserByIdResponseDTO GetUserById(long userId)
        {
            var data = _db.SYS_UserDetails.AsNoTracking()
                .FirstOrDefault(x => x.UserId == userId);
            if (data == null) return null;

            var dto = _mapper.Map<GetUserByIdResponseDTO>(data);
            dto.RoleTitleName = _db.LK_RoleTitles.AsNoTracking()
                .Where(rt => rt.RoleTitleId == data.RoleTitleId && rt.IsActive == true)
                .Select(rt => rt.RoleTitleName)
                .FirstOrDefault();

            dto.ProviderCategories = _db.UR_ProviderCategories.AsNoTracking()
                .Where(pc => pc.ProviderId == userId)
                .Join(_db.PD_Categories.AsNoTracking(),
                      pc => pc.CategoryId,
                      c => c.CategoryId,
                      (pc, c) => new ProviderCategory
                      {
                          CategoryId = c.CategoryId,
                          CategoryName = c.CategoryName
                      })
                .ToList();

            dto.ProviderLicense = _db.UR_ProviderStateLicenses.AsNoTracking()
                .Where(pl => pl.ProviderId == userId)
                .Select(pl => new ProviderStateLicenseDTO
                {
                    StateId = pl.StateId,
                    StateLicense = pl.StateLicense
                })
                .ToList();

            return dto;
        }

        public GetUserByIdResponseDTO GetUserByIdLinq(long UserId)
        {

            var data = _db.SYS_UserDetails.Where(x => x.UserId == UserId).FirstOrDefault();
            GetUserByIdResponseDTO getUserByIdResponseDTO = new GetUserByIdResponseDTO();
            getUserByIdResponseDTO = _mapper.Map<GetUserByIdResponseDTO>(data);
            return getUserByIdResponseDTO;
        }

        public long SaveUser(SaveUserRequestDTO request, long OrganizationId, long UserId)
        {
            try
            {

                var normalizedEmail = (request.Email ?? string.Empty).Trim().ToLowerInvariant();

                if (string.IsNullOrWhiteSpace(normalizedEmail))
                    return -1;

                bool emailInUse;
                if (request.UserId == 0)
                {
                    emailInUse = _db.SYS_Logins
                        .AsNoTracking()
                        .Any(l => l.Email != null && l.Email.ToLower() == normalizedEmail);
                    if (emailInUse) return 0;
                }

                SYS_UserDetail user = new SYS_UserDetail();
                Guid guid = Guid.NewGuid();

                if (request.UserId == 0)
                {

                    user = _mapper.Map<SYS_UserDetail>(request);
                    user.Guid = guid.ToString();
                    user.CreatedBy = UserId;
                    user.CreatedDate = DateTime.UtcNow;
                    user.IsActive = true;
                    user.Status = "Active";
                    user.OrganizationId = OrganizationId;

                    user.RoleTitleId = request.RoleId == (int)UserRole.GlobalAdmin ? request.RoleTitleId : null;

                    user.Login = new SYS_Login
                    {
                        Email = request.Email?.Trim(),
                        Password = _passwordHasher.HashPassword("AdminUser@123"),
                        RoleId = request.RoleId,
                        ProfileUrl = request.ProfileUrl
                    };

                    _db.SYS_UserDetails.Add(user);
                    _db.SaveChanges();

                    _auditService.LogEntityChange(
                        action: "Create",
                        entityType: "SYS_UserDetail",
                        entityId: user.UserId,
                        newValues: new { user.FirstName, user.LastName, user.Email, user.Status },
                        userId: UserId,
                        description: $"User '{user.FirstName} {user.LastName}' (Email: {user.Login?.Email}) created with Role ID {request.RoleId}",
                        module: "User"
                    );

                    if (request.RoleId == 3 || request.RoleId == 5)
                    {
                        FC_UsersInFacility facilityAdmin = new FC_UsersInFacility
                        {
                            FacilityUserId = 0,
                            UserId = user.UserId,
                            FacilityId = request.FacilityId,
                            OrganizationId = OrganizationId,
                            IsAssign = true,
                            CreatedBy = UserId,
                            CreatedDate = DateTime.UtcNow
                        };

                        if (request.AddressType == "Same as Clinic")
                        {
                            SYS_Facility facility = _db.SYS_Facilities
                                                       .FirstOrDefault(x => x.FacilityId == request.FacilityId);
                            if (facility != null)
                            {
                                user.Address = facility.Address;
                                user.StateId = facility.StateId;
                                user.CityId = facility.CityId;
                                user.ZipCode = facility.ZipCode;
                                _db.SaveChanges();
                            }
                        }

                        _db.Add(facilityAdmin);
                        _db.SaveChanges();
                    }

                    if (request.IsSupervisorRequired == true && request.SupervisorId?.Count > 0)
                    {
                        foreach (var supervisorId in request.SupervisorId)
                        {
                            var newItem = new SYS_SupervisorProvider
                            {
                                SupervisorProviderId = 0,
                                SupervisorId = supervisorId,
                                ProviderId = user.UserId,
                            };
                            _db.SYS_SupervisorProviders.Add(newItem);
                        }
                        _db.SaveChanges();
                    }

                    if (request.RoleId == 4 && request.CategoryId?.Count > 0)
                    {
                        foreach (var categoryId in request.CategoryId)
                        {
                            var newItem = new UR_ProviderCategory
                            {
                                ProviderCategoryId = 0,
                                CategoryId = categoryId,
                                ProviderId = user.UserId,
                            };
                            _db.UR_ProviderCategories.Add(newItem);
                        }
                        _db.SaveChanges();
                    }

                    if (request.RoleId == 4 && request.ProviderLicense != null && request.ProviderLicense.Count > 0)
                    {
                        foreach (var license in request.ProviderLicense)
                        {
                            if (license.StateId.HasValue && !string.IsNullOrWhiteSpace(license.StateLicense))
                            {
                                var newLicense = new UR_ProviderStateLicense
                                {
                                    ProviderStateLicenseId = 0,
                                    ProviderId = user.UserId,
                                    StateId = license.StateId.Value,
                                    StateLicense = license.StateLicense.Trim()
                                };
                                _db.UR_ProviderStateLicenses.Add(newLicense);
                            }
                        }
                        _db.SaveChanges();
                    }

                    if (request.RoleId == (int)UserRole.Provider)
                    {
                        _providerHoursRepo
                            .EnsureTemplateExistsAsync(user.UserId, OrganizationId, UserId)
                            .GetAwaiter().GetResult();
                    }

                    return user.UserId;
                }
                else
                {

                    var userToUpdate = _db.SYS_UserDetails
                                          .Include(x => x.Login)
                                          .FirstOrDefault(x => x.UserId == request.UserId);
                    if (userToUpdate == null) return -1;

                    _mapper.Map(request, userToUpdate);
                    userToUpdate.RoleTitleId = request.RoleId == (int)UserRole.GlobalAdmin ? request.RoleTitleId : null;
                    userToUpdate.ModifiedBy = UserId;
                    userToUpdate.ModifiedDate = DateTime.UtcNow;

                    if (userToUpdate.Login == null)
                        userToUpdate.Login = new SYS_Login();

                    userToUpdate.Login.Email = request.Email?.Trim();
                    userToUpdate.Login.RoleId = request.RoleId;
                    userToUpdate.Login.ProfileUrl = request.ProfileUrl;

                    if (request.IsSupervisorRequired == true && request.SupervisorId?.Count > 0)
                    {
                        var existing = _db.SYS_SupervisorProviders
                                          .Where(x => x.ProviderId == request.UserId)
                                          .ToList();
                        if (existing.Count > 0)
                        {
                            _db.SYS_SupervisorProviders.RemoveRange(existing);
                            _db.SaveChanges();
                        }

                        foreach (var supervisorId in request.SupervisorId)
                        {
                            var newItem = new SYS_SupervisorProvider
                            {
                                SupervisorProviderId = 0,
                                SupervisorId = supervisorId,
                                ProviderId = userToUpdate.UserId,
                            };
                            _db.SYS_SupervisorProviders.Add(newItem);
                        }
                        _db.SaveChanges();
                    }

                    if (request.RoleId == 4 && request.CategoryId?.Count > 0)
                    {
                        var existing = _db.UR_ProviderCategories
                                          .Where(x => x.ProviderId == request.UserId)
                                          .ToList();
                        if (existing.Count > 0)
                        {
                            _db.UR_ProviderCategories.RemoveRange(existing);
                            _db.SaveChanges();
                        }

                        foreach (var categoryId in request.CategoryId)
                        {
                            var newItem = new UR_ProviderCategory
                            {
                                ProviderCategoryId = 0,
                                CategoryId = categoryId,
                                ProviderId = userToUpdate.UserId,
                            };
                            _db.UR_ProviderCategories.Add(newItem);
                        }
                        _db.SaveChanges();
                    }

                    if (request.RoleId == 4)
                    {

                        var existingLicenses = _db.UR_ProviderStateLicenses
                                                  .Where(x => x.ProviderId == request.UserId)
                                                  .ToList();
                        if (existingLicenses.Count > 0)
                        {
                            _db.UR_ProviderStateLicenses.RemoveRange(existingLicenses);
                            _db.SaveChanges();
                        }

                        if (request.ProviderLicense != null && request.ProviderLicense.Count > 0)
                        {
                            foreach (var license in request.ProviderLicense)
                            {
                                if (license.StateId.HasValue && !string.IsNullOrWhiteSpace(license.StateLicense))
                                {
                                    var newLicense = new UR_ProviderStateLicense
                                    {
                                        ProviderStateLicenseId = 0,
                                        ProviderId = userToUpdate.UserId,
                                        StateId = license.StateId.Value,
                                        StateLicense = license.StateLicense.Trim()
                                    };
                                    _db.UR_ProviderStateLicenses.Add(newLicense);
                                }
                            }
                            _db.SaveChanges();
                        }
                    }

                    _db.SaveChanges();

                    _auditService.LogEntityChange(
                        action: "Update",
                        entityType: "SYS_UserDetail",
                        entityId: userToUpdate.UserId,
                        newValues: new { userToUpdate.FirstName, userToUpdate.LastName, userToUpdate.Email, userToUpdate.Status },
                        userId: UserId,
                        description: $"User '{userToUpdate.FirstName} {userToUpdate.LastName}' (ID: {userToUpdate.UserId}) updated",
                        module: "User"
                    );

                    return 0;
                }
            }
            catch (Exception)
            {
                return -1;
            }
        }

        public long SaveClinicAdmins()
        {
            try
            {
                List<SYS_Facility> facilitiesList = new();
                facilitiesList = _db.SYS_Facilities.Where(x => x.FacilityId > 78).ToList();
                foreach(var item in facilitiesList)
                {
                    SYS_UserDetail user = new SYS_UserDetail();
                    Guid guid = Guid.NewGuid();
                    user.Guid = guid.ToString();
                    user.CreatedBy = 2;
                    user.CreatedDate = DateTime.UtcNow;
                    user.IsActive = true;
                    user.Status = "Active";
                    user.OrganizationId = 1;

                    user.Login = new SYS_Login();
                    user.Login.Email = item.Email;

                    user.Login.Password = _passwordHasher.HashPassword("vitality");
                    user.Login.RoleId = 3;
                    _db.SYS_UserDetails.Add(user);
                    _db.SaveChanges();

                    FC_UsersInFacility facilityAdmin = new FC_UsersInFacility();
                    facilityAdmin.FacilityUserId = 0;
                    facilityAdmin.UserId = user.UserId;
                    facilityAdmin.FacilityId = item.FacilityId;
                    facilityAdmin.OrganizationId = 1;
                    facilityAdmin.IsAssign = true;
                    facilityAdmin.CreatedBy = 2;
                    facilityAdmin.CreatedDate = DateTime.UtcNow;

                    SYS_Facility facility = _db.SYS_Facilities.Where(x => x.FacilityId == item.FacilityId).FirstOrDefault();
                    user.Address = facility.Address;
                    user.StateId = facility.StateId;
                    user.CityId = facility.CityId;
                    user.ZipCode = facility.ZipCode;
                    _db.SaveChanges();

                    _db.Add(facilityAdmin);
                    _db.SaveChanges();

                }

                return 1;
            }
            catch (Exception ex)
            {
                return -1;
            }
        }
        public async Task<bool> ActiveUserExistsAsync(string email, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;

            var normalizedEmail = email.Trim().ToLowerInvariant();

            var loginEmailTaken = await _db.SYS_Logins
                .AsNoTracking()
                .AnyAsync(l => l.Email != null && l.Email.Trim().ToLower() == normalizedEmail, ct);
            if (loginEmailTaken)
                return true;

            var facilityEmailTaken = await _db.SYS_Facilities
                .AsNoTracking()
                .AnyAsync(f => f.Email != null && f.Email.Trim().ToLower() == normalizedEmail, ct);
            return facilityEmailTaken;
        }

        public bool SetStripePlatformCustomerId(long userId, string stripePlatformCustomerId)
        {
            if (string.IsNullOrWhiteSpace(stripePlatformCustomerId) || !stripePlatformCustomerId.TrimStart().StartsWith("cus_", StringComparison.OrdinalIgnoreCase))
                return false;
            var user = _db.SYS_UserDetails.FirstOrDefault(x => x.UserId == userId);
            if (user == null) return false;
            user.StripePlatformCustomerId = stripePlatformCustomerId.Trim();
            _db.SaveChanges();
            return true;
        }

        public bool DeleteUser(long UserId)
        {
            SYS_UserDetail user = _db.SYS_UserDetails.Where(x => x.UserId == UserId).FirstOrDefault();
            if (user != null)
            {
                user.IsActive = false;
                user.Status = "InActive";
                user.ModifiedDate = DateTime.UtcNow;

                List<FC_UsersInFacility> usersInFacility = _db.FC_UsersInFacilities.Where(x => x.UserId == UserId).ToList();
                if(usersInFacility.Count > 0)
                {
                    _db.FC_UsersInFacilities.RemoveRange(usersInFacility);
                }

                _db.SaveChanges();

                _auditService.LogEntityChange(
                    action: "Delete",
                    entityType: "SYS_UserDetail",
                    entityId: UserId,
                    oldValues: new { user.FirstName, user.LastName, user.Email, user.Status },
                    description: $"User '{user.FirstName} {user.LastName}' (ID: {UserId}) deleted",
                    module: "User"
                );

                return true;
            }
            else
            {
                return false;
            }
        }

        public bool ActivateUser(ActivateUserRequestDTO request)
        {
            try
            {
                SYS_UserDetail user = new SYS_UserDetail();
                user = _db.SYS_UserDetails.Where(x => x.UserId == request.UserId).FirstOrDefault();
                if (user != null)
                {
                    if (request.IsActive)
                    {
                        user.Status = "Active";
                    }
                    else
                    {
                        user.Status = "InActive";
                    }

                    _db.SaveChanges();

                    _auditService.LogEntityChange(
                        action: "Update",
                        entityType: "SYS_UserDetail",
                        entityId: request.UserId,
                        oldValues: new { Status = user.IsActive == true ? "Active" : "InActive" },
                        newValues: new { Status = request.IsActive == true ? "Active" : "InActive" },
                        userId: request.UserId,
                        description: $"User '{user.FirstName} {user.LastName}' (ID: {request.UserId}) {(request.IsActive == true ? "activated" : "deactivated")}",
                        module: "User"
                    );

                    return true;
                }
                else
                {
                    return false;
                }

            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public bool UpdateUserPassword(UpdateUserPasswordRequestDTO request)
        {
            try
            {
                SYS_UserDetail user = new SYS_UserDetail();
                user = _db.SYS_UserDetails.Include(x => x.Login).Where(x => x.UserId == request.UserId).FirstOrDefault();
                if (user != null && user.Login != null)
                {

                    if (!string.IsNullOrWhiteSpace(request.NewPassowrd))
                    {
                        user.Login.Password = _passwordHasher.HashPassword(request.NewPassowrd);
                    }

                    _db.SaveChanges();
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public bool UpdateUserCardCredentials(UpdateUserCardCredentialsRequestDTO request)
        {
            try
            {

                SYS_UserDetail user = _db.SYS_UserDetails.Include(x => x.SYS_UserCards).Where(x => x.UserId == request.UserId).FirstOrDefault();

                if (user != null)
                {
                    var newCard = new SYS_UserCard
                    {
                        SquareCardId = request.SquareCardId,
                        SquareClientId = request.SquareClientId,
                        CardHolderName = request.CardHolderName,
                        ExpirationMonth = request.ExpirationMonth,
                        Currency = request.Currency,
                        ExpirationYear = request.ExpirationYear,
                        CardBrand = request.CardBrand,
                        Last4 = request.Last4,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UserId = user.UserId
                    };

                    var defaultCard = user.SYS_UserCards.FirstOrDefault(c => c.IsDefault);
                    if (defaultCard != null)
                    {
                        newCard.IsDefault = true;
                        defaultCard.IsDefault = false;
                    }
                    else
                    {
                        newCard.IsDefault = true;
                    }

                    user.SYS_UserCards.Add(newCard);

                    _db.SaveChanges();

                    _auditService.LogEntityChange(
                        action: "Create",
                        entityType: "SYS_UserCard",
                        entityId: newCard.CardId,
                        newValues: new { newCard.CardBrand, newCard.Last4, newCard.ExpirationMonth, newCard.ExpirationYear, newCard.CardHolderName },
                        userId: request.UserId,
                        description: $"Card payment updated for User ID {request.UserId} - Brand: {newCard.CardBrand}, Last4: {newCard.Last4}, Exp: {newCard.ExpirationMonth}/{newCard.ExpirationYear}",
                        module: "Payment"
                    );

                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public bool UpdateUserProfile(UpdateUserProfileRequestDto request)
        {
            try
            {
                SYS_UserDetail user = new SYS_UserDetail();
                user = _db.SYS_UserDetails.Include(x => x.Login).Where(x => x.UserId == request.UserId).FirstOrDefault();
                if (user != null)
                {
                    user.Login.ProfileUrl = request.ProfileUrl;

                    _db.SaveChanges();
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                return false;
            }
        }
        public async Task<bool> UpdateUserProfileUrlAsync(UpdateUserProfileUrlRequestDTO request)
        {
            try
            {
                var user = await _db.SYS_UserDetails.Include(x=>x.Login)
                    .FirstOrDefaultAsync(x => x.UserId == request.UserId);

                if (user == null)
                {
                    return false;
                }
                user.Login.ProfileUrl = request.ProfileUrl;
                user.ProfileUrl = request.ProfileUrl;
                user.ModifiedDate = DateTime.UtcNow;
                await _db.SaveChangesAsync();

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public string AssignUserToFacility(List<AssignUserToFacilityRequestDTO> requestList, long UserId, long OrganizationId)
        {
            if (requestList == null || requestList.Count == 0)
                return "No changes.";

            using var tx = _db.Database.BeginTransaction();
            try
            {

                var pairs = requestList
                    .Select(r => new { r.UserId, r.FacilityId })
                    .Distinct()
                    .ToList();

                var userIds = pairs.Select(p => p.UserId).Distinct().ToList();
                var facilityIds = pairs.Select(p => p.FacilityId).Distinct().ToList();

                var existing = _db.FC_UsersInFacilities
                    .Where(x =>
                        x.OrganizationId == OrganizationId &&
                        x.UserId != null && userIds.Contains(x.UserId.Value) &&
                        x.FacilityId != null && facilityIds.Contains(x.FacilityId.Value))
                    .ToList();

                int assigned = 0, unassigned = 0, created = 0;

                foreach (var req in requestList)
                {
                    var matches = existing
                        .Where(x => x.UserId == req.UserId
                                 && x.FacilityId == req.FacilityId
                                 && x.OrganizationId == OrganizationId)
                        .ToList();

                    if (req.IsAssign)
                    {
                        if (matches.Count == 0)
                        {

                            var newItem = new FC_UsersInFacility
                            {
                                OrganizationId = OrganizationId,
                                FacilityId = req.FacilityId,
                                UserId = req.UserId,
                                IsAssign = true,
                                CreatedDate = DateTime.UtcNow,
                                CreatedBy = UserId
                            };
                            _db.FC_UsersInFacilities.Add(newItem);
                            created++;
                        }
                        else
                        {

                            foreach (var m in matches)
                                m.IsAssign = true;
                        }
                        assigned++;
                    }
                    else
                    {

                        if (matches.Count > 0)
                        {
                            foreach (var m in matches)
                                m.IsAssign = false;
                            unassigned++;
                        }

                    }
                }

                _db.SaveChanges();
                tx.Commit();

                return $"Assignments updated. Created: {created}, Set assigned: {assigned}, Set unassigned: {unassigned}.";
            }
            catch (Exception ex)
            {
                try { tx.Rollback(); } catch {  }
                return ex.ToString();
            }
        }

        public bool CheckEmailExist(CheckEmailExistRequestDTO request)
        {
            var login = _db.SYS_Logins.Join(_db.SYS_UserDetails, ln => ln.LoginId, ur => ur.LoginId, (ln, ur) => new { ln, ur })
                         .Where(joined => joined.ur.IsActive == true && joined.ln.Email == request.Email).Count();
            if (login > 0)
                return true;
            else
                return false;
        }

        public List<long> GetGlobalAdminIds()
        {
            var adminUserIds = _db.SYS_UserDetails
                .Where(u => u.IsActive == true && u.Login != null && u.Login.RoleId == (int)UserRole.GlobalAdmin)
                .Select(u => u.UserId)
                .ToList();
            return adminUserIds;
        }

        public List<(long UserId, string Email)> GetGlobalAdminUsersWithEmail()
        {
            var list = _db.SYS_UserDetails
                .AsNoTracking()
                .Where(u => u.IsActive == true && u.Status == "Active" && u.Login != null && u.Login.RoleId == (int)UserRole.GlobalAdmin)
                .Where(u => u.Login != null && u.Login.Email != null && u.Login.Email.Trim() != "")
                .Select(u => new { u.UserId, Email = u.Login!.Email!.Trim() })
                .ToList();
            return list.Select(x => (x.UserId, x.Email)).ToList();
        }

        public List<(long UserId, string Email)> GetTechSupportUsersWithEmail()
        {
            var list = _db.SYS_UserDetails
                .AsNoTracking()
                .Where(u => u.IsActive == true && u.Status == "Active" && u.Login != null && u.Login.RoleId == (int)UserRole.TechSupport)
                .Where(u => u.Login != null && u.Login.Email != null && u.Login.Email.Trim() != "")
                .Select(u => new { u.UserId, Email = u.Login!.Email!.Trim() })
                .ToList();
            return list.Select(x => (x.UserId, x.Email)).ToList();
        }

    }
}
