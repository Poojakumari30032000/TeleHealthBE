using AutoMapper;
using CsvHelper;
using CsvHelper.Configuration;
using Dapper;
using DudeMeds.Models.DTOs.Facilities;
using DudeMeds.Models.DTOs.Patients;
using DudeMeds.Models.DTOs.Users;
using DudeMeds.Models.Repos.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Formats.Asn1;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Vitality.Models.CommonMethods;
using Vitality.Models.DTOs.Facilities;
using Vitality.Models.EntityClasses;
using Vitality.Models.Enums;
using Vitality.Models.Repos;
using Vitality.Models.Repos.Services;
using Vitality.Models.Repos.Services.Audit;
using Vitality.Models.Repos.Services.Auth;
using Vitality.Services.Email;

namespace DudeMeds.Models.Repos.Services
{
    public partial class FacilitiesRepo : BaseRepo, IFacilitiesRepo
    {
        private readonly IMapper _mapper;
        private readonly IAuditService _auditService;
        private readonly IUsersRepo _usersRepo;
        private readonly ILogger<FacilitiesRepo>? _logger;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IMailSender _mailSender;
        private readonly INotificationService _notificationService;
        private const string FrontendBaseUrl = "https://www.telehealthus.com";

        public FacilitiesRepo(
            IMapper mapper,
            IAuditService auditService,
            IUsersRepo usersRepo,
            IPasswordHasher passwordHasher,
            IMailSender mailSender,
            ILogger<FacilitiesRepo>? logger = null,
            INotificationService notificationService = null)
        {
            _mapper = mapper;
            _auditService = auditService;
            _usersRepo = usersRepo ?? throw new ArgumentNullException(nameof(usersRepo));
            _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
            _mailSender = mailSender ?? throw new ArgumentNullException(nameof(mailSender));
            _logger = logger;
            _notificationService = notificationService;
        }

        public List<GetAllFacilitiesResponseDTO> GetAllFacilities(
    GetAllFacilitiesRequestDTO request, out int totalFacilityCount)
        {
            var pageSize = request.PageSize > 0 ? request.PageSize : 25;
                var pageNumber = request.PageNumber > 0 ? request.PageNumber : 1;
                var title = string.IsNullOrWhiteSpace(request.Title) ? null : request.Title.Trim();

                IQueryable<SYS_Facility> baseQuery = _db.SYS_Facilities
                    .AsNoTracking();

            baseQuery = baseQuery.Where(fc => fc.IsApproved != false);

            if (!string.IsNullOrWhiteSpace(request.Title))
            {

                baseQuery = baseQuery.Where(fc =>
                    (fc.TitleLong ?? "").Contains(title) ||
                    (fc.TitleShort ?? "").Contains(title)
                );
            }

            if (!string.IsNullOrWhiteSpace(request.Status))
                    baseQuery = baseQuery.Where(fc => fc.Status == request.Status);

                totalFacilityCount = baseQuery.Count();

                var page = baseQuery
          .OrderBy(fc => fc.TitleLong)
          .ThenBy(fc => fc.FacilityId)
          .Skip(pageSize * (pageNumber - 1))
          .Take(pageSize)
          .Select(fc => new GetAllFacilitiesResponseDTO
          {
              Guid = fc.Guid,
              FacilityId = fc.FacilityId,
              SubscriptionPlanId = fc.SubscriptionPlanId,
              TitleLong = fc.TitleLong,
              TitleShort = fc.TitleShort,
              Email = fc.Email,
              Phone = fc.Phone,
              Fax = fc.Fax,
              BillingAddressType = fc.BillingAddressType,
              Address = fc.Address,
              CityId = fc.CityId,
              CityName = _db.SYS_Cities.Where(c => c.Id == fc.CityId).Select(c => c.Name).FirstOrDefault(),
              StateId = fc.StateId,
              StateName = _db.SYS_States.Where(s => s.Id == fc.StateId).Select(s => s.Name).FirstOrDefault(),
              ZipCode = fc.ZipCode,
              BillingAddress = fc.BillingAddress,
              BillingCityId = fc.BillingCityId,
              BillingStateId = fc.BillingStateId,
              BillingZipCode = fc.BillingZipCode,
              FedearlTaxId = fc.FedearlTaxId,
              NPI = fc.NPI,
              Status = fc.Status,
              FacilityContactName = fc.FacilityContactName,
              FacilityContactEmail = fc.FacilityContactEmail,
              FacilityContactPhone = fc.FacilityContactPhone,
              FacilityAdminCount = (
                  from uif in _db.FC_UsersInFacilities
                  join ud in _db.SYS_UserDetails on uif.UserId equals ud.UserId
                  join l in _db.SYS_Logins on ud.LoginId equals l.LoginId
                  where uif.FacilityId == fc.FacilityId && l.RoleId == 3
                  select uif.UserId).Count(),
              CustomerSupportCount = (
                  from uif in _db.FC_UsersInFacilities
                  join ud in _db.SYS_UserDetails on uif.UserId equals ud.UserId
                  join l in _db.SYS_Logins on ud.LoginId equals l.LoginId
                  where uif.FacilityId == fc.FacilityId && l.RoleId == 5
                  select uif.UserId).Count(),
              CanViewChannels = fc.CanViewChannels,
              IsExternal = fc.IsExternal,
              IsBillable = fc.IsBillable

          })
          .ToList();

            return page;
        }

        public GetFacilityByIdResponseDTO? GetFacilityById(long facilityId)
        {

            static string? JoinParts(params string?[] parts)
            {
                var pieces = parts?
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Select(p => p!.Trim())
                    .ToArray();

                return (pieces != null && pieces.Length > 0)
                    ? string.Join(", ", pieces)
                    : null;
            }

            var row =
                (from fc in _db.SYS_Facilities.AsNoTracking()
                 where fc.FacilityId == facilityId

                 join c in _db.SYS_Cities.AsNoTracking() on fc.CityId equals c.Id into cj
                 from c in cj.DefaultIfEmpty()

                 join s in _db.SYS_States.AsNoTracking() on fc.StateId equals s.Id into sj
                 from s in sj.DefaultIfEmpty()

                 join bc in _db.SYS_Cities.AsNoTracking() on fc.BillingCityId equals bc.Id into bcj
                 from bc in bcj.DefaultIfEmpty()

                 join bs in _db.SYS_States.AsNoTracking() on fc.BillingStateId equals bs.Id into bsj
                 from bs in bsj.DefaultIfEmpty()

                 select new
                 {
                     fc,
                     CityName = c != null ? c.Name : null,
                     StateShort = s != null ? s.ShortName : null,
                     BillingCityName = bc != null ? bc.Name : null,
                     BillingStateShort = bs != null ? bs.ShortName : null,
                 })
                .FirstOrDefault();

            if (row == null) return null;

            var modeLabel = row.fc.PaymentModeId.HasValue
                ? ((Vitality.Models.Enums.FacilityPaymentMode)row.fc.PaymentModeId.Value).ToString()
                : null;

            return new GetFacilityByIdResponseDTO
            {
                Guid = row.fc.Guid,
                FacilityId = row.fc.FacilityId,
                SubscriptionPlanId = row.fc.SubscriptionPlanId,
                TitleLong = row.fc.TitleLong,
                TitleShort = row.fc.TitleShort,
                Email = row.fc.Email,
                Phone = row.fc.Phone,
                Fax = row.fc.Fax,
                BillingAddressType = row.fc.BillingAddressType,
                Address = row.fc.Address,
                CityId = row.fc.CityId,
                StateId = row.fc.StateId,
                ZipCode = row.fc.ZipCode,
                BillingAddress = row.fc.BillingAddress,
                BillingCityId = row.fc.BillingCityId,
                BillingStateId = row.fc.BillingStateId,
                BillingZipCode = row.fc.BillingZipCode,
                FedearlTaxId = row.fc.FedearlTaxId,
                NPI = row.fc.NPI,
                Status = row.fc.Status,
                FacilityContactName = row.fc.FacilityContactName,
                FacilityContactEmail = row.fc.FacilityContactEmail,
                FacilityContactPhone = row.fc.FacilityContactPhone,
                CanViewChannels = row.fc.CanViewChannels,
                IsExternal = row.fc.IsExternal,
                IsBillable = row.fc.IsBillable,

                CompleteAddress = JoinParts(row.fc.Address, row.CityName, row.StateShort),
                CompleteBillingAddress = JoinParts(row.fc.BillingAddress, row.BillingCityName, row.BillingStateShort),
                PaymentMode = modeLabel,
                PaymentModeId = row.fc.PaymentModeId,
            };
        }

        public GetFacilityPaymentModeResponseDTO? GetFacilityPaymentMode(long facilityId)
        {
            var fc = _db.SYS_Facilities.AsNoTracking()
                .Where(f => f.FacilityId == facilityId)
                .Select(f => new { f.FacilityId, f.PaymentModeId })
                .FirstOrDefault();
            if (fc == null) return null;

            return new GetFacilityPaymentModeResponseDTO
            {
                FacilityId = fc.FacilityId,
                PaymentModeId = fc.PaymentModeId,
                PaymentModeName = fc.PaymentModeId.HasValue
                    ? ((Vitality.Models.Enums.FacilityPaymentMode)fc.PaymentModeId.Value).ToString()
                    : string.Empty
            };
        }
        public async Task<SaveFacilityResult> SaveFacilityAsync(SaveFacilityRequestDTO request, long UserId, long OrganizationId)
        {
            try
            {
                SYS_Facility facility = new SYS_Facility();
                Guid guid = Guid.NewGuid();
                if (request.FacilityId == 0)
                {

                    var emailTrimmed = request.Email?.Trim();
                    if (!string.IsNullOrWhiteSpace(emailTrimmed))
                    {
                        var emailLower = emailTrimmed.ToLowerInvariant();
                        bool emailExists = await _db.SYS_Facilities
                            .AsNoTracking()
                            .AnyAsync(f => f.FacilityId != request.FacilityId
                                && f.Email != null
                                && f.Email.Trim().ToLower() == emailLower);
                        if (emailExists)
                        {
                            return new SaveFacilityResult
                            {
                                Message = "A clinic with this email already exists. Please use a different email."
                            };
                        }

                        bool loginEmailExists = await _db.SYS_Logins
                            .AsNoTracking()
                            .AnyAsync(l => l.Email != null && l.Email.Trim().ToLower() == emailLower);
                        if (loginEmailExists)
                        {
                            return new SaveFacilityResult
                            {
                                Message = "A user with this email already exists. Please use a different email."
                            };
                        }
                    }
                    facility = _mapper.Map<SYS_Facility>(request);
                    facility.Guid = guid.ToString();
                    facility.CreatedBy = UserId;
                    facility.CreatedDate = DateTime.UtcNow;
                    facility.OrganizationId = OrganizationId;
                    facility.IsActive = true;
                    facility.Status = "Active";
                    facility.IsExternal = false;
                    facility.IsBillable = request.IsBillable ?? false;

                    facility.IsApproved = true;
                    if(request.BillingAddressType == "Same as Facility")
                    {
                        facility.BillingAddress = request.Address;
                        facility.BillingCityId = request.CityId;
                        facility.BillingStateId = request.StateId;
                        facility.BillingZipCode = request.ZipCode;
                    }
                    _db.SYS_Facilities.Add(facility);
                    _db.SaveChanges();

                    if (!string.IsNullOrWhiteSpace(emailTrimmed))
                    {
                        var clinicAdminFirstName = request.ClinicAdminFirstName?.Trim();
                        var clinicAdminLastName = request.ClinicAdminLastName?.Trim();
                        var tempPassword = CommonMethods.GenerateRandomTemporaryPassword();

                        var clinicAdminUser = new SYS_UserDetail
                        {
                            Guid = Guid.NewGuid().ToString(),
                            FirstName = string.IsNullOrWhiteSpace(clinicAdminFirstName) ? "Clinic" : clinicAdminFirstName,
                            LastName = string.IsNullOrWhiteSpace(clinicAdminLastName) ? "Admin" : clinicAdminLastName,
                            Email = emailTrimmed,
                            Phone = request.Phone?.Trim(),
                            AddressType = "Same as Clinic",
                            Address = facility.Address,
                            StateId = facility.StateId,
                            CityId = facility.CityId,
                            ZipCode = facility.ZipCode,
                            CreatedBy = UserId,
                            CreatedDate = DateTime.UtcNow,
                            OrganizationId = OrganizationId,
                            IsActive = true,
                            Status = "Active",
                            Login = new SYS_Login
                            {
                                Email = emailTrimmed,
                                Password = _passwordHasher.HashPassword(tempPassword),
                                RoleId = 3
                            }
                        };

                        _db.SYS_UserDetails.Add(clinicAdminUser);
                        _db.SaveChanges();

                        _db.FC_UsersInFacilities.Add(new FC_UsersInFacility
                        {
                            UserId = clinicAdminUser.UserId,
                            FacilityId = facility.FacilityId,
                            OrganizationId = OrganizationId,
                            IsAssign = true,
                            CreatedBy = UserId,
                            CreatedDate = DateTime.UtcNow
                        });
                        _db.SaveChanges();

                        await _notificationService.SendClinicAdminCredentialEmailAsync(clinicAdminUser, facility, tempPassword);
                    }

                    SeedClinicDrugPricesFromGlobal(facility.FacilityId, UserId);

                    await _auditService.LogEntityChangeAsync(
                        action: "Create",
                        entityType: "SYS_Facility",
                        entityId: facility.FacilityId,
                        newValues: facility,
                        userId: UserId,
                        facilityId: facility.FacilityId,
                        organizationId: OrganizationId,
                        description: $"Facility '{facility.TitleLong}' created",
                        module: "Facility"
                    );

                    return new SaveFacilityResult
                    {
                        Message = "Facility Created Successfully",
                        FacilityId = facility.FacilityId
                    };

                }
                else
                {
                    var oldFacility = _db.SYS_Facilities.AsNoTracking()
                        .FirstOrDefault(x => x.FacilityId == request.FacilityId);

                    facility = _db.SYS_Facilities.Where(x => x.FacilityId == request.FacilityId).FirstOrDefault();
                    var existingPaymentModeId = facility?.PaymentModeId ?? 1;
                    _mapper.Map(request, facility);
                    facility!.PaymentModeId = request.PaymentModeId ?? existingPaymentModeId;
                    var preservedIsBillable = facility.IsBillable;
                    _mapper.Map(request, facility);

                    facility.IsBillable = request.IsBillable ?? preservedIsBillable;
                    facility.ModifiedBy = UserId;
                    facility.ModifiedDate = DateTime.UtcNow;
                    _db.SaveChanges();

                    await _auditService.LogEntityChangeAsync(
                        action: "Update",
                        entityType: "SYS_Facility",
                        entityId: facility.FacilityId,
                        oldValues: oldFacility,
                        newValues: facility,
                        userId: UserId,
                        description: $"Facility '{facility.TitleLong}' updated"
                    );

                    return new SaveFacilityResult
                    {
                        Message = "Facility Updated Successfully",
                        FacilityId = facility.FacilityId
                    };

                }
            }
            catch(Exception ex)
            {
                return new SaveFacilityResult { Message = "Something went wrong. Please try agmin later." };
            }
        }

        private void SeedClinicDrugPricesFromGlobal(long facilityId, long createdByUserId)
        {
            var latestPharmToGlobal = _db.PC_PHARMTOGLOBALs
                .AsNoTracking()
                .Where(p => p.IsActive == true && p.DrugId.HasValue)
                .GroupBy(p => p.DrugId!.Value)
                .Select(g => new
                {
                    DrugId = g.Key,
                    PharmToGlobalId = g.Max(x => x.PharmToGlobalId)
                });

            var gaToClinicPrices = (
                from ptg in latestPharmToGlobal
                join gac in _db.PC_GATOCLINICs.AsNoTracking().Where(g => g.IsActive == true)
                    on ptg.PharmToGlobalId equals gac.PharmToGlobalId
                select new
                {
                    ptg.DrugId,
                    gac.GAtoClinicId,
                    gac.SuggestedRetailPrice
                }).ToList();

            if (gaToClinicPrices.Count == 0)
                return;

            var now = DateTime.UtcNow;
            var clinicToPatientRows = gaToClinicPrices.Select(x => new PC_CLINICTOPATIENT
            {
                DrugId = x.DrugId,
                FacilityId = facilityId,
                GAtoClinicId = x.GAtoClinicId,
                ClinicSuggestedRetailPrice = x.SuggestedRetailPrice,
                IsActive = true,
                CreatedBy = createdByUserId,
                CreatedAt = now
            }).ToList();

            _db.PC_CLINICTOPATIENTs.AddRange(clinicToPatientRows);
            _db.SaveChanges();
        }

        public async Task<SaveFacilityResult> ExternalClinicSignupAsync(
            ClinicSignupRequestDTO request,
            long organizationId,
            CancellationToken ct = default)
        {
            if (request == null)
                return new SaveFacilityResult { Message = "Invalid payload." };

            var email = request.Email?.Trim();
            if (string.IsNullOrWhiteSpace(email))
                return new SaveFacilityResult { Message = "Clinic email is required." };

            if (string.IsNullOrWhiteSpace(request.ClinicAdminFirstName) || string.IsNullOrWhiteSpace(request.ClinicAdminLastName))
                return new SaveFacilityResult { Message = "Clinic admin first and last name are required." };

            if (string.IsNullOrWhiteSpace(request.Password) || string.IsNullOrWhiteSpace(request.ConfirmPassword))
                return new SaveFacilityResult { Message = "Password and confirm password are required." };

            if (!string.Equals(request.Password, request.ConfirmPassword, StringComparison.Ordinal))
                return new SaveFacilityResult { Message = "Password and confirm password do not match." };

            if (request.Password.Length < 8)
                return new SaveFacilityResult { Message = "Password must be at least 8 characters." };

            var emailLower = email.ToLowerInvariant();
            var facilityEmailExists = await _db.SYS_Facilities
                .AsNoTracking()
                .AnyAsync(x => x.Email != null && x.Email.Trim().ToLower() == emailLower, ct);
            if (facilityEmailExists)
                return new SaveFacilityResult { Message = "A clinic with this email already exists." };

            var loginEmailExists = await _db.SYS_Logins
                .AsNoTracking()
                .AnyAsync(x => x.Email != null && x.Email.Trim().ToLower() == emailLower, ct);
            if (loginEmailExists)
                return new SaveFacilityResult { Message = "A user with this email already exists." };

            using var tx = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                var facility = new SYS_Facility
                {
                    Guid = Guid.NewGuid().ToString(),
                    SubscriptionPlanId = request.SubscriptionPlanId,
                    TitleLong = request.TitleLong?.Trim(),
                    TitleShort = request.TitleShort?.Trim(),
                    Email = email,
                    Phone = request.Phone?.Trim(),
                    Fax = request.Fax?.Trim(),
                    BillingAddressType = request.BillingAddressType,
                    Address = request.Address,
                    CityId = request.CityId,
                    StateId = request.StateId,
                    ZipCode = request.ZipCode?.Trim(),
                    BillingAddress = request.BillingAddress,
                    BillingCityId = request.BillingCityId,
                    BillingStateId = request.BillingStateId,
                    BillingZipCode = request.BillingZipCode?.Trim(),
                    FedearlTaxId = request.FedearlTaxId?.Trim(),
                    NPI = request.NPI?.Trim(),
                    FacilityContactName = request.FacilityContactName?.Trim(),
                    FacilityContactEmail = request.FacilityContactEmail?.Trim(),
                    FacilityContactPhone = request.FacilityContactPhone?.Trim(),
                    OrganizationId = organizationId,
                    IsActive = false,
                    Status = "InActive",
                    IsExternal = true,
                    CanViewChannels = false,

                    IsApproved = false,
                    PaymentModeId = null,
                    CreatedDate = DateTime.UtcNow
                };

                if (request.BillingAddressType == "Same as Facility")
                {
                    facility.BillingAddress = request.Address;
                    facility.BillingCityId = request.CityId;
                    facility.BillingStateId = request.StateId;
                    facility.BillingZipCode = request.ZipCode;
                }

                _db.SYS_Facilities.Add(facility);
                await _db.SaveChangesAsync(ct);

                var clinicAdmin = new SYS_UserDetail
                {
                    Guid = Guid.NewGuid().ToString(),
                    FirstName = request.ClinicAdminFirstName?.Trim(),
                    LastName = request.ClinicAdminLastName?.Trim(),
                    Email = email,
                    Phone = request.Phone?.Trim(),
                    AddressType = "Same as Clinic",
                    Address = facility.Address,
                    StateId = facility.StateId,
                    CityId = facility.CityId,
                    ZipCode = facility.ZipCode,
                    OrganizationId = organizationId,
                    IsActive = false,
                    Status = "InActive",
                    CreatedDate = DateTime.UtcNow,
                    Login = new SYS_Login
                    {
                        Email = email,
                        Password = _passwordHasher.HashPassword(request.Password),
                        RoleId = (int)UserRole.ClinicAdmin
                    }
                };
                _db.SYS_UserDetails.Add(clinicAdmin);
                await _db.SaveChangesAsync(ct);

                _db.FC_UsersInFacilities.Add(new FC_UsersInFacility
                {
                    UserId = clinicAdmin.UserId,
                    FacilityId = facility.FacilityId,
                    OrganizationId = organizationId,
                    IsAssign = true,
                    CreatedDate = DateTime.UtcNow
                });
                await _db.SaveChangesAsync(ct);

                SeedClinicDrugPricesFromGlobal(facility.FacilityId, clinicAdmin.UserId);

                await tx.CommitAsync(ct);

                await _notificationService.SendExternalClinicSignupNotificationToGlobalAdminsAsync(facility, clinicAdmin, ct);
                await _notificationService.SendClinicPendingApprovalEmailAsync(facility, clinicAdmin, ct);

                return new SaveFacilityResult
                {
                    FacilityId = facility.FacilityId,
                    Message = "Clinic sign-up submitted successfully. Your account is pending approval."
                };
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                _logger?.LogError(ex, "External clinic signup failed for email {Email}", request.Email);
                return new SaveFacilityResult { Message = "Unable to complete clinic sign-up. Please try again." };
            }
        }

        public async Task<SaveFacilityResult> ApproveExternalClinicAsync(long facilityId, long approvedByUserId, bool? canViewChannels, bool? isBillable, int paymentModeId, CancellationToken ct = default)
        {
            var facility = await _db.SYS_Facilities
                .FirstOrDefaultAsync(x => x.FacilityId == facilityId, ct);
            if (facility == null)
                return new SaveFacilityResult { Message = "Clinic not found." };

            using var tx = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                facility.IsActive = true;
                facility.Status = "Active";
                facility.CanViewChannels = canViewChannels ?? true;
                facility.IsBillable = isBillable ?? facility.IsBillable ?? false;
                facility.PaymentModeId = paymentModeId;

                facility.IsApproved = true;
                facility.ModifiedBy = approvedByUserId > 0 ? approvedByUserId : null;
                facility.ModifiedDate = DateTime.UtcNow;

                var clinicAdmins = await (
                    from uif in _db.FC_UsersInFacilities
                    join u in _db.SYS_UserDetails on uif.UserId equals u.UserId
                    join l in _db.SYS_Logins on u.LoginId equals l.LoginId
                    where uif.FacilityId == facilityId && l.RoleId == (int)UserRole.ClinicAdmin
                    select u
                ).ToListAsync(ct);

                foreach (var user in clinicAdmins)
                {
                    user.IsActive = true;
                    user.Status = "Active";
                    user.ModifiedBy = approvedByUserId > 0 ? approvedByUserId : null;
                    user.ModifiedDate = DateTime.UtcNow;
                }

                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                foreach (var clinicAdmin in clinicAdmins)
                {
                    await _notificationService.SendClinicApprovedEmailAsync(facility, clinicAdmin, ct);
                }

                return new SaveFacilityResult
                {
                    FacilityId = facilityId,
                    Message = "Clinic approved successfully."
                };
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                _logger?.LogError(ex, "Clinic approval failed for FacilityId {FacilityId}", facilityId);
                return new SaveFacilityResult { Message = "Unable to approve clinic. Please try again." };
            }
        }

        public async Task<bool> UpdateFacilityBillingByFacilityIDAsync(
            UpdateFacilityBillingRequestDTO request,
            long modifiedByUserId,
            CancellationToken ct = default)
        {
            if (request == null || request.FacilityId <= 0)
                return false;

            var facility = await _db.SYS_Facilities
                .FirstOrDefaultAsync(x => x.FacilityId == request.FacilityId, ct);
            if (facility == null)
                return false;

            var oldIsBillable = facility.IsBillable;
            facility.IsBillable = request.IsBillable;
            facility.ModifiedBy = modifiedByUserId > 0 ? modifiedByUserId : facility.ModifiedBy;
            facility.ModifiedDate = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            _auditService.LogEntityChange(
                action: "Update",
                entityType: "SYS_Facility",
                entityId: facility.FacilityId,
                oldValues: new { IsBillable = oldIsBillable },
                newValues: new { IsBillable = facility.IsBillable },
                description: $"Facility '{facility.TitleLong}' IsBillable changed from '{oldIsBillable}' to '{facility.IsBillable}'.",
                module: "Facility"
            );

            return true;
        }

        public async Task<(List<GetAllFacilitiesResponseDTO> Items, int TotalCount)> GetPendingExternalClinicsAsync(
            int pageSize,
            int pageNumber,
            CancellationToken ct = default)
        {
            var ps = pageSize > 0 ? pageSize : 25;
            var pn = pageNumber > 0 ? pageNumber : 1;

            var baseQuery = _db.SYS_Facilities
                .AsNoTracking()
                .Where(fc =>
                    fc.IsApproved == false
                    && fc.IsExternal == true);

            var totalCount = await baseQuery.CountAsync(ct);

            var items = await baseQuery
                .OrderByDescending(fc => fc.CreatedDate)
                .ThenBy(fc => fc.FacilityId)
                .Skip(ps * (pn - 1))
                .Take(ps)
                .Select(fc => new GetAllFacilitiesResponseDTO
                {
                    Guid = fc.Guid,
                    FacilityId = fc.FacilityId,
                    SubscriptionPlanId = fc.SubscriptionPlanId,
                    TitleLong = fc.TitleLong,
                    TitleShort = fc.TitleShort,
                    Email = fc.Email,
                    Phone = fc.Phone,
                    Fax = fc.Fax,
                    BillingAddressType = fc.BillingAddressType,
                    Address = fc.Address,
                    CityId = fc.CityId,
                    CityName = _db.SYS_Cities.Where(c => c.Id == fc.CityId).Select(c => c.Name).FirstOrDefault(),
                    StateId = fc.StateId,
                    StateName = _db.SYS_States.Where(s => s.Id == fc.StateId).Select(s => s.Name).FirstOrDefault(),
                    ZipCode = fc.ZipCode,
                    BillingAddress = fc.BillingAddress,
                    BillingCityId = fc.BillingCityId,
                    BillingStateId = fc.BillingStateId,
                    BillingZipCode = fc.BillingZipCode,
                    FedearlTaxId = fc.FedearlTaxId,
                    NPI = fc.NPI,
                    Status = fc.Status,
                    FacilityContactName = fc.FacilityContactName,
                    FacilityContactEmail = fc.FacilityContactEmail,
                    FacilityContactPhone = fc.FacilityContactPhone,
                    FacilityAdminCount = (
                        from uif in _db.FC_UsersInFacilities
                        join ud in _db.SYS_UserDetails on uif.UserId equals ud.UserId
                        join l in _db.SYS_Logins on ud.LoginId equals l.LoginId
                        where uif.FacilityId == fc.FacilityId && l.RoleId == 3
                        select uif.UserId).Count(),
                    CustomerSupportCount = (
                        from uif in _db.FC_UsersInFacilities
                        join ud in _db.SYS_UserDetails on uif.UserId equals ud.UserId
                        join l in _db.SYS_Logins on ud.LoginId equals l.LoginId
                        where uif.FacilityId == fc.FacilityId && l.RoleId == 5
                        select uif.UserId).Count(),
                    CanViewChannels = fc.CanViewChannels,
                    IsExternal = fc.IsExternal,
                    IsBillable = fc.IsBillable
                })
                .ToListAsync(ct);

            return (items, totalCount);
        }

        public bool DeleteFacility(long FacilityId)
        {
            try
            {
                var facility = _db.SYS_Facilities.Where(x => x.FacilityId == FacilityId).FirstOrDefault();
                if (facility != null)
                {
                    var oldFacility = _db.SYS_Facilities.AsNoTracking()
                        .FirstOrDefault(x => x.FacilityId == FacilityId);

                    facility.IsActive = false;
                    _db.SaveChanges();

                    _auditService.LogEntityChange(
                        action: "Delete",
                        entityType: "SYS_Facility",
                        entityId: FacilityId,
                        oldValues: oldFacility,
                        description: $"Facility '{facility.TitleLong}' deleted",
                        module: "Facility"
                    );

                    return true;
                }
                else
                {
                    return false;
                }

            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> UpdateFacilityStatusAsync(UpdateFacilityStatusRequestDTO request, CancellationToken ct = default)
        {
            try
            {
                var facility = await _db.SYS_Facilities
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.FacilityId == request.FacilityId, ct);

                if (facility == null)
                {
                    return false;
                }

                var facilityToUpdate = await _db.SYS_Facilities
                    .FirstOrDefaultAsync(x => x.FacilityId == request.FacilityId, ct);

                if (facilityToUpdate == null)
                {
                    return false;
                }

                var oldStatus = facilityToUpdate.Status;
                var oldIsActive = facilityToUpdate.IsActive;

                facilityToUpdate.Status = request.Status;

                bool isActivating = false;
                bool isDeactivating = false;

                if (string.Equals(request.Status, "Active", StringComparison.OrdinalIgnoreCase))
                {
                    facilityToUpdate.IsActive = true;

                    if (oldIsActive != true || !string.Equals(oldStatus, "Active", StringComparison.OrdinalIgnoreCase))
                    {
                        isActivating = true;
                    }
                }
                else
                {

                    facilityToUpdate.IsActive = false;

                    if (oldIsActive == true && string.Equals(oldStatus, "Active", StringComparison.OrdinalIgnoreCase))
                    {
                        isDeactivating = true;
                    }
                }

                await _db.SaveChangesAsync(ct);

                _logger?.LogInformation($"Facility {request.FacilityId} status update: isDeactivating={isDeactivating}, isActivating={isActivating}, oldStatus={oldStatus}, newStatus={request.Status}");

                if (isDeactivating)
                {
                    try
                    {
                        var modifiedByUserId = facilityToUpdate.ModifiedBy ?? facilityToUpdate.CreatedBy ?? 0;
                        var utcNow = DateTime.UtcNow;

                        var pausedTreatmentCount = await _db.Database.ExecuteSqlRawAsync(
                            @"UPDATE PT_PatientTreatments
                              SET TreatmentStatus = N'Paused',
                                  ModifiedDate = {0},
                                  ModifiedBy = {1}
                              WHERE FacilityId = {2}
                                AND TreatmentStatus = N'Active'
                                AND (IsActive = 1 OR IsActive IS NULL)",
                            new object[] { utcNow, modifiedByUserId, request.FacilityId },
                            ct);

                        _logger?.LogInformation($"Bulk paused {pausedTreatmentCount} active treatments for facility {request.FacilityId} due to facility deactivation.");

                        var inactivatedUserCount = await _db.Database.ExecuteSqlRawAsync(
                            @"UPDATE SYS_UserDetails
                              SET IsActive = 0,
                                  Status = N'Inactive'
                              WHERE UserId IN (
                                  SELECT DISTINCT uf.UserId
                                  FROM FC_UsersInFacilities uf
                                  INNER JOIN SYS_UserDetails ud ON uf.UserId = ud.UserId
                                  INNER JOIN SYS_Logins l ON ud.LoginId = l.LoginId
                                  WHERE uf.FacilityId = {0}
                                    AND uf.IsAssign = 1
                                    AND uf.UserId IS NOT NULL
                                    AND l.RoleId != 4
                              )
                              AND (IsActive = 1 OR Status = N'Active')",
                            new object[] { request.FacilityId },
                            ct);

                        _logger?.LogInformation($"Bulk inactivated {inactivatedUserCount} users for facility {request.FacilityId} due to facility deactivation.");

                        var inactivatedPatientCount = await _db.Database.ExecuteSqlRawAsync(
                            @"UPDATE PT_Patients
                              SET IsActive = 0,
                                  Status = N'Inactive'
                              WHERE FacilityId = {0}
                                AND (IsActive = 1 OR Status = N'Active')",
                            new object[] { request.FacilityId },
                            ct);

                        _logger?.LogInformation($"Bulk inactivated {inactivatedPatientCount} patients for facility {request.FacilityId} due to facility deactivation.");
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, $"Error during deactivation bulk updates for facility {request.FacilityId}: {ex.Message}");

                    }
                }
                else if (isActivating)
                {
                    try
                    {
                        var modifiedByUserId = facilityToUpdate.ModifiedBy ?? facilityToUpdate.CreatedBy ?? 0;
                        var utcNow = DateTime.UtcNow;

                        var reactivatedTreatmentCount = await _db.Database.ExecuteSqlRawAsync(
                            @"UPDATE PT_PatientTreatments
                              SET TreatmentStatus = N'Active',
                                  ModifiedDate = {0},
                                  ModifiedBy = {1}
                              WHERE FacilityId = {2}
                                AND TreatmentStatus = N'Paused'
                                AND (IsActive = 1 OR IsActive IS NULL)",
                            new object[] { utcNow, modifiedByUserId, request.FacilityId },
                            ct);

                        _logger?.LogInformation($"Bulk reactivated {reactivatedTreatmentCount} paused treatments for facility {request.FacilityId} due to facility reactivation.");

                        var reactivatedUserCount = await _db.Database.ExecuteSqlRawAsync(
                            @"UPDATE SYS_UserDetails
                              SET IsActive = 1,
                                  Status = N'Active'
                              WHERE UserId IN (
                                  SELECT DISTINCT uf.UserId
                                  FROM FC_UsersInFacilities uf
                                  INNER JOIN SYS_UserDetails ud ON uf.UserId = ud.UserId
                                  INNER JOIN SYS_Logins l ON ud.LoginId = l.LoginId
                                  WHERE uf.FacilityId = {0}
                                    AND uf.IsAssign = 1
                                    AND uf.UserId IS NOT NULL
                                    AND l.RoleId != 4
                              )
                              AND (IsActive = 0 OR Status = N'Inactive')",
                            new object[] { request.FacilityId },
                            ct);

                        _logger?.LogInformation($"Bulk reactivated {reactivatedUserCount} users for facility {request.FacilityId} due to facility reactivation.");

                        var reactivatedPatientCount = await _db.Database.ExecuteSqlRawAsync(
                            @"UPDATE PT_Patients
                              SET IsActive = 1,
                                  Status = N'Active'
                              WHERE FacilityId = {0}
                                AND (IsActive = 0 OR Status = N'Inactive')",
                            new object[] { request.FacilityId },
                            ct);

                        _logger?.LogInformation($"Bulk reactivated {reactivatedPatientCount} patients for facility {request.FacilityId} due to facility reactivation.");
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, $"Error during reactivation bulk updates for facility {request.FacilityId}: {ex.Message}");

                    }
                }

                string treatmentUpdateNote = "";
                if (isDeactivating)
                {
                    var treatmentCount = await _db.PT_PatientTreatments
                        .AsNoTracking()
                        .CountAsync(t => t.FacilityId == request.FacilityId && t.TreatmentStatus == "Paused", ct);
                    var userCount = await _db.FC_UsersInFacilities
                        .AsNoTracking()
                        .Where(uf => uf.FacilityId == request.FacilityId && uf.UserId.HasValue && uf.IsAssign == true)
                        .Select(uf => uf.UserId!.Value)
                        .Distinct()
                        .Join(_db.SYS_UserDetails.AsNoTracking(),
                            userId => userId,
                            user => user.UserId,
                            (userId, user) => user)
                        .CountAsync(u => u.IsActive == false || u.Status == "Inactive", ct);
                    var patientCount = await _db.PT_Patients
                        .AsNoTracking()
                        .CountAsync(p => p.FacilityId == request.FacilityId && (p.IsActive == false || p.Status == "Inactive"), ct);
                    treatmentUpdateNote = $" {treatmentCount} treatments paused, {userCount} users inactivated, {patientCount} patients inactivated.";
                }
                else if (isActivating)
                {
                    var treatmentCount = await _db.PT_PatientTreatments
                        .AsNoTracking()
                        .CountAsync(t => t.FacilityId == request.FacilityId && t.TreatmentStatus == "Active", ct);
                    var userCount = await _db.FC_UsersInFacilities
                        .AsNoTracking()
                        .Where(uf => uf.FacilityId == request.FacilityId && uf.UserId.HasValue && uf.IsAssign == true)
                        .Select(uf => uf.UserId!.Value)
                        .Distinct()
                        .Join(_db.SYS_UserDetails.AsNoTracking(),
                            userId => userId,
                            user => user.UserId,
                            (userId, user) => user)
                        .CountAsync(u => u.IsActive == true && u.Status == "Active", ct);
                    var patientCount = await _db.PT_Patients
                        .AsNoTracking()
                        .CountAsync(p => p.FacilityId == request.FacilityId && p.IsActive == true && p.Status == "Active", ct);
                    treatmentUpdateNote = $" {treatmentCount} treatments reactivated, {userCount} users reactivated, {patientCount} patients reactivated.";
                }

                _auditService.LogEntityChange(
                    action: "Update",
                    entityType: "SYS_Facility",
                    entityId: request.FacilityId,
                    oldValues: new { Status = oldStatus, IsActive = oldIsActive },
                    newValues: new { Status = request.Status, IsActive = facilityToUpdate.IsActive },
                    description: $"Facility '{facility.TitleLong}' status changed from '{oldStatus}' to '{request.Status}'. IsActive: {oldIsActive} -> {facilityToUpdate.IsActive}.{treatmentUpdateNote}",
                    module: "Facility"
                );

                return true;

            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error updating facility status for facility {request.FacilityId}: {ex.Message}");
                return false;
            }
        }

        public async Task<BulkImportFacilitiesResultDTO> ImportFacilitiesFromCsvAsync(
         Stream csvStream,
         long userId = 2,
         long organizationId = 1)
        {
            var result = new BulkImportFacilitiesResultDTO();

            var cfg = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                DetectDelimiter = true,
                IgnoreBlankLines = true,
                HeaderValidated = null,
                MissingFieldFound = null,
                PrepareHeaderForMatch = args => (args.Header ?? string.Empty).Trim()
            };

            List<FacilityCsvRow> rows;
            using (var reader = new StreamReader(csvStream))
            using (var csv = new CsvReader(reader, cfg))
            {
                rows = csv.GetRecords<FacilityCsvRow>().ToList();
            }

            if (rows.Count == 0) return result;

            string Key(FacilityCsvRow r)
                => $"{(r.TitleLong ?? "").Trim().ToUpperInvariant()}|{(r.Address ?? "").Trim().ToUpperInvariant()}";

            var seenInFile = new HashSet<string>();
            var cleaned = new List<FacilityCsvRow>();

            foreach (var r in rows)
            {

                if (string.IsNullOrWhiteSpace(r.TitleLong) || string.IsNullOrWhiteSpace(r.Address))
                {
                    result.InvalidRows++;
                    continue;
                }

                var k = Key(r);
                if (!seenInFile.Add(k))
                {
                    result.SkippedDuplicateInFile++;
                    continue;
                }

                cleaned.Add(r);
            }

            if (cleaned.Count == 0) return result;

            var existing = await _db.SYS_Facilities
                .AsNoTracking()
                .Select(x => new { x.FacilityId, x.TitleLong, x.Address })
                .ToListAsync();

            var existingKeys = new HashSet<string>(
                existing
                    .Where(x => !string.IsNullOrWhiteSpace(x.TitleLong) && !string.IsNullOrWhiteSpace(x.Address))
                    .Select(x => $"{x.TitleLong!.Trim().ToUpperInvariant()}|{x.Address!.Trim().ToUpperInvariant()}"));

            var toInsert = new List<SYS_Facility>();
            var now = DateTime.UtcNow;

            foreach (var r in cleaned)
            {
                var k = Key(r);
                if (existingKeys.Contains(k))
                {
                    result.SkippedExisting++;
                    continue;
                }

                var entity = new SYS_Facility
                {

                    SubscriptionPlanId = null,
                    TitleLong = r.TitleLong?.Trim(),
                    TitleShort = r.TitleShort?.Trim(),
                    Email = r.Email?.Trim(),
                    Phone = r.Phone?.Trim(),
                    Fax = r.Fax?.Trim(),
                    Address = r.Address?.Trim(),
                    CityId = r.CityId,
                    StateId = r.StateId,
                    ZipCode = r.ZipCode?.Trim(),

                    BillingAddressType = r.BillingAddressType?.Trim(),
                    BillingAddress = r.BillingAddress?.Trim(),
                    BillingCityId = r.BillingCityId,
                    BillingStateId = r.BillingStateId,
                    BillingZipCode = r.BillingZipCode?.Trim(),

                    FedearlTaxId = r.FedearlTaxId?.Trim(),
                    NPI = r.NPI?.Trim(),

                    FacilityContactName = r.FacilityContactName?.Trim(),
                    FacilityContactEmail = r.FacilityContactEmail?.Trim(),
                    FacilityContactPhone = r.FacilityContactPhone?.Trim(),

                    OrganizationId = organizationId,
                    IsActive = true,
                    Status = "Active",
                    CreatedBy = userId,
                    CreatedDate = now,
                    Guid = Guid.NewGuid().ToString()
                };

                if (!string.IsNullOrWhiteSpace(entity.BillingAddressType) &&
                    entity.BillingAddressType.Equals("Same as Facility", StringComparison.OrdinalIgnoreCase))
                {
                    entity.BillingAddress = entity.Address;
                    entity.BillingCityId = entity.CityId;
                    entity.BillingStateId = entity.StateId;
                    entity.BillingZipCode = entity.ZipCode;
                }

                toInsert.Add(entity);
                existingKeys.Add(k);
            }

            const int batchSize = 500;
            for (int i = 0; i < toInsert.Count; i += batchSize)
            {
                var slice = toInsert.Skip(i).Take(batchSize).ToList();
                _db.SYS_Facilities.AddRange(slice);
                await _db.SaveChangesAsync();
                result.Inserted += slice.Count;
            }

            return result;
        }

        private static SYS_Facility BuildNewFacility(FacilityCsvRowDTO r, long userId, long orgId)
        {
            var now = DateTime.UtcNow;
            var entity = new SYS_Facility
            {
                TitleLong = r.FacilityName?.Trim(),
                Address = r.Address?.Trim(),
                CityId = r.CityId,
                StateId = r.StateId,
                ZipCode = r.ZipCode?.Trim(),
                Email = r.Email?.Trim(),
                Phone = r.Phone?.Trim(),
                Guid = Guid.NewGuid().ToString(),
                CreatedBy = userId,
                CreatedDate = now,
                OrganizationId = orgId,
                IsActive = true,
                Status = "Active"
            };

            if (string.Equals(r.BillingAddressType, "Same as Facility", StringComparison.OrdinalIgnoreCase))
            {
                entity.BillingAddress = entity.Address;
                entity.BillingCityId = entity.CityId;
                entity.BillingStateId = entity.StateId;
                entity.BillingZipCode = entity.ZipCode;
            }
            else
            {
                entity.BillingAddress = string.IsNullOrWhiteSpace(r.BillingAddress) ? null : r.BillingAddress.Trim();
                entity.BillingCityId = r.BillingCityId;
                entity.BillingStateId = r.BillingStateId;
                entity.BillingZipCode = string.IsNullOrWhiteSpace(r.BillingZipCode) ? null : r.BillingZipCode.Trim();
            }

            return entity;
        }

        private static void ApplyRowToExisting(SYS_Facility entity, FacilityCsvRowDTO r, long userId)
        {
            var now = DateTime.UtcNow;

            entity.CityId = r.CityId;
            entity.StateId = r.StateId;
            entity.ZipCode = string.IsNullOrWhiteSpace(r.ZipCode) ? null : r.ZipCode.Trim();
            entity.Email = string.IsNullOrWhiteSpace(r.Email) ? null : r.Email.Trim();
            entity.Phone = string.IsNullOrWhiteSpace(r.Phone) ? null : r.Phone.Trim();

            if (string.Equals(r.BillingAddressType, "Same as Facility", StringComparison.OrdinalIgnoreCase))
            {
                entity.BillingAddress = entity.Address;
                entity.BillingCityId = entity.CityId;
                entity.BillingStateId = entity.StateId;
                entity.BillingZipCode = entity.ZipCode;
            }
            else
            {
                entity.BillingAddress = string.IsNullOrWhiteSpace(r.BillingAddress) ? null : r.BillingAddress.Trim();
                entity.BillingCityId = r.BillingCityId;
                entity.BillingStateId = r.BillingStateId;
                entity.BillingZipCode = string.IsNullOrWhiteSpace(r.BillingZipCode) ? null : r.BillingZipCode.Trim();
            }

            entity.ModifiedBy = userId;
            entity.ModifiedDate = now;

            if (entity.IsActive != true) entity.IsActive = true;
            if (!string.Equals(entity.Status, "Active", StringComparison.OrdinalIgnoreCase)) entity.Status = "Active";
        }

        public async Task<bool> AssignCategoriesAsync(long facilityId, IEnumerable<long> categoryIds, long userId, CancellationToken ct = default)
        {
            var ids = (categoryIds ?? Enumerable.Empty<long>()).Distinct().ToList();

            var existing = await _db.PD_FacilityCategories
                .Where(x => x.FacilityId == facilityId)
                .ToListAsync(ct);

            var activeIds = existing.Where(x => x.IsActive == true).Select(x => x.CategoryId).ToHashSet();
            var toAdd = ids.Except(activeIds).ToList();
            var toDeactivate = existing.Where(x => x.IsActive == true && !ids.Contains(x.CategoryId)).ToList();

            foreach (var row in toDeactivate)
            {
                row.IsActive = false;
                row.ModifiedBy = userId;
                row.ModifiedDate = DateTime.UtcNow;
            }

            foreach (var catId in toAdd)
            {
                var previously = existing.FirstOrDefault(x => x.CategoryId == catId);
                if (previously != null)
                {
                    previously.IsActive = true;
                    previously.ModifiedBy = userId;
                    previously.ModifiedDate = DateTime.UtcNow;
                }
                else
                {
                    _db.PD_FacilityCategories.Add(new PD_FacilityCategory
                    {
                        FacilityId = facilityId,
                        CategoryId = catId,
                        IsActive = true,
                        CreatedBy = userId,
                        CreatedDate = DateTime.UtcNow
                    });
                }
            }

            await _db.SaveChangesAsync(ct);

            if (toAdd.Any())
            {

                var bundlesInCategories = await _db.PD_Bundles
                    .AsNoTracking()
                    .Where(b => b.IsActive == true && b.CategoryId.HasValue && toAdd.Contains(b.CategoryId.Value) && b.FacilityId == null)
                    .ToListAsync(ct);

                var existingBundlePrices = await _db.PD_FacilityBundlePrices
                    .Where(fbp => fbp.FacilityId == facilityId)
                    .ToListAsync(ct);

                foreach (var bundle in bundlesInCategories)
                {
                    var existingPrice = existingBundlePrices.FirstOrDefault(ebp => ebp.BundleId == bundle.BundleId);

                    if (existingPrice == null)
                    {

                        _db.PD_FacilityBundlePrices.Add(new PD_FacilityBundlePrice
                        {
                            FacilityId = facilityId,
                            BundleId = bundle.BundleId,
                            ClinicPrice = bundle.Price ?? 0,
                            IsRecurring = true,
                            CreatedBy = userId,
                            CreatedDateUtc = DateTime.UtcNow
                        });
                    }
                    else
                    {

                        existingPrice.IsRecurring = true;
                        existingPrice.ModifiedBy = userId;
                        existingPrice.ModifiedDateUtc = DateTime.UtcNow;
                    }
                }

                await _db.SaveChangesAsync(ct);
            }

            return true;
        }

        public async Task<bool> UnassignCategoriesAsync(long facilityId, IEnumerable<long> categoryIds, long userId, CancellationToken ct = default)
        {
            var ids = (categoryIds ?? Enumerable.Empty<long>()).ToList();
            if (ids.Count == 0) return true;

            var rows = await _db.PD_FacilityCategories
                .Where(x => x.FacilityId == facilityId && ids.Contains(x.CategoryId) && x.IsActive == true)
                .ToListAsync(ct);

            foreach (var row in rows)
            {
                row.IsActive = false;
                row.ModifiedBy = userId;
                row.ModifiedDate = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync(ct);
            return true;
        }

        public async Task<List<GetAssignedCategoryResponseDTO>> GetAssignedCategoriesAsync(long facilityId, CancellationToken ct = default)
        {
            var query =
                from fc in _db.PD_FacilityCategories.AsNoTracking()
                join c in _db.PD_Categories.AsNoTracking() on fc.CategoryId equals c.CategoryId
                where fc.FacilityId == facilityId && fc.IsActive == true && c.IsActive == true
                orderby c.CategoryName
                select new GetAssignedCategoryResponseDTO
                {
                    CategoryId = c.CategoryId,
                    CategoryName = c.CategoryName,
                    CategoryDescription = c.CategoryDescription,
                    ImageURL = c.ImageURL
                };

            return await query.ToListAsync(ct);
        }
    }
}
