using AutoMapper;
using DudeMeds.Models.DTOs.Accounts;
using DudeMeds.Models.Repos.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vitality.Models.DTOs.Subscriptions;
using Vitality.Models.EntityClasses;
using Vitality.Models.Repos;
using Vitality.Models.Repos.Services.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Vitality.Models.Repos.Services;

namespace DudeMeds.Models.Repos.Services
{
    public class AccountsRepo : BaseRepo , IAccountsRepo
    {
        private readonly IMapper _mapper;
        private readonly IPasswordHasher _passwordHasher;
        private readonly FacilityStatusService _facilityStatusService;

        public AccountsRepo(IMapper mapper, IPasswordHasher passwordHasher)
        {
            _mapper = mapper;
            _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
            _facilityStatusService = new FacilityStatusService(_db);
        }

        public LoginResponseDTO Login(LoginRequestDTO request)
        {
            var response = new LoginResponseDTO();

            if (string.IsNullOrWhiteSpace(request.Email))
            {
                response.ErrorCode = "EMAIL_REQUIRED";
                response.ErrorMessage = "Email is required.";
                return response;
            }

            if (string.IsNullOrWhiteSpace(request.Password))
            {
                response.ErrorCode = "PASSWORD_REQUIRED";
                response.ErrorMessage = "Password is required.";
                return response;
            }

            var login = _db.SYS_Logins
                           .FirstOrDefault(x => x.Email == request.Email);

            if (login == null)
            {
                response.ErrorCode = "INVALID_CREDENTIALS";
                response.ErrorMessage = "Invalid email or password.";
                return response;
            }

            bool passwordMatches = false;
            if (!string.IsNullOrWhiteSpace(login.Password))
            {

                if (_passwordHasher.IsHashed(login.Password))
                {

                    passwordMatches = _passwordHasher.VerifyPassword(request.Password, login.Password);
                }
                else
                {

                    passwordMatches = login.Password == request.Password;

                    if (passwordMatches)
                    {
                        try
                        {
                            login.Password = _passwordHasher.HashPassword(request.Password);
                           // _db.SaveChanges();
                        }
                        catch (DbUpdateException ex) when (ex.InnerException is SqlException sqlEx && sqlEx.Message.Contains("truncated"))
                        {

                            System.Diagnostics.Debug.WriteLine($"Warning: Password column too small. Please run DATABASE_MIGRATION_UpdatePasswordColumn.sql to fix this.");

                        }
                    }
                }
            }

            if (!passwordMatches)
            {
                response.ErrorCode = "INVALID_CREDENTIALS";
                response.ErrorMessage = "Invalid email or password.";
                return response;
            }

            if (login != null && login.RoleId != 6)
            {
                var user = _db.SYS_UserDetails.FirstOrDefault(x => x.LoginId == login.LoginId);

                if (user == null)
                {
                    response.ErrorCode = "USER_NOT_FOUND";
                    response.ErrorMessage = "User account not found. Please contact your administrator.";
                    return response;
                }

                if (user.IsActive != true || user.Status != "Active")
                {
                    response.ErrorCode = "ACCOUNT_DISABLED";
                    response.ErrorMessage = "Your account has been disabled. Please contact your administrator.";
                    return response;
                }
                if (user != null)
                {
                    response.Guid = user.Guid;
                    response.UserId = user.UserId;
                    response.FirstName = user.FirstName;
                    response.LastName = user.LastName;
                    response.MiddleName = user.MiddleName;
                    response.Address = user.Address;
                    response.AddressType = user.AddressType;
                    response.Status = user.Status;
                    response.SSN = user.SSN;
                    response.NPI = user.NPI;
                    response.CAQHId = user.CAQHId;
                    response.License = user.License;
                    response.StateId = user.StateId;
                    response.CityId = user.CityId;
                    response.Phone = user.Phone;
                    response.LoginId = user.LoginId;
                    response.IsFirstUse = user.IsFirstUse;

                    response.IsFirstQuestionaire = user.IsFirstQuestionaire;

                    response.Email = user.Email;
                    response.OrganizationId = user.OrganizationId;
                    response.RoleId = login.RoleId;

                    response.ProfileUrl = !string.IsNullOrWhiteSpace(user.ProfileUrl) ? user.ProfileUrl : login.ProfileUrl;
                    response.RoleName = _db.LK_Roles.Where(x => x.RoleId == login.RoleId)
                                                    .Select(x => x.RoleName)
                                                    .FirstOrDefault();
                    response.RoleTitle = _db.LK_RoleTitles
                        .Where(x => x.RoleTitleId == user.RoleTitleId && x.IsActive == true)
                        .Select(x => x.RoleTitleName)
                        .FirstOrDefault();

                    var facility = _db.FC_UsersInFacilities.FirstOrDefault(x => x.UserId == user.UserId);
                    if (facility != null)
                    {

                        if (!_facilityStatusService.IsFacilityActive(facility.FacilityId))
                        {

                            response.UserId = null;
                            response.Guid = null;
                            response.FacilityId = null;
                            response.ErrorCode = "FACILITY_DISABLED";
                            response.ErrorMessage = "Your facility has been disabled. Please contact your administrator.";
                            return response;
                        }

                        response.FacilityId = facility.FacilityId;
                        response.FacilityGuid = _db.SYS_Facilities
                                                   .Where(x => x.FacilityId == facility.FacilityId)
                                                   .Select(x => x.Guid)
                                                   .FirstOrDefault();

                        var subscription =
                            (from s in _db.SYS_Subscriptions
                             join f in _db.SYS_Facilities on s.SubscriptionId equals f.SubscriptionPlanId
                             where f.FacilityId == facility.FacilityId
                             select s).FirstOrDefault();

                        if (subscription != null)
                        {
                            response.Subscription = new SaveSubscriptionRequestDTO
                            {
                                SubscriptionId = subscription.SubscriptionId,
                                PlanName = subscription.PlanName,
                                MonthlyPrice = subscription.MonthlyPrice,
                                AnnualPrice = subscription.AnnualPrice,
                                MaxPatients = subscription.MaxPatients,
                                MaxProviders = subscription.MaxProviders,
                                MaxUsers = subscription.MaxUsers,
                                BillingCycle = subscription.BillingCycle,
                                ContractTeam = subscription.ContractTeam,
                                SetupFee = subscription.SetupFee,
                                Status = subscription.Status,
                                Storage = subscription.Storage,
                                SupportLevel = subscription.SupportLevel,
                                TrainingHours = subscription.TrainingHours,
                            };
                        }
                    }
                }
            }
            else if (login != null && login.RoleId == 6)
            {
                var patient = _db.PT_Patients.FirstOrDefault(x => x.LoginId == login.LoginId);

                if (patient == null)
                {
                    response.ErrorCode = "PATIENT_NOT_FOUND";
                    response.ErrorMessage = "Patient account not found. Please contact your administrator.";
                    return response;
                }

                if (patient.IsActive != true || patient.Status != "Active")
                {
                    response.ErrorCode = "ACCOUNT_DISABLED";
                    response.ErrorMessage = "Your account has been disabled. Please contact your administrator.";
                    return response;
                }

                var user = _db.SYS_UserDetails.FirstOrDefault(x => x.LoginId == login.LoginId);
                if (user == null)
                {

                    user = new SYS_UserDetail
                    {
                        LoginId = login.LoginId,
                        FirstName = patient.FirstName,
                        LastName = patient.LastName,
                        Email = patient.Email ?? login.Email,
                        Phone = patient.Phone,
                        Guid = Guid.NewGuid().ToString(),
                        CreatedBy = 2,
                        CreatedDate = DateTime.UtcNow,
                        IsActive = true,
                        Status = "Active",
                        OrganizationId = patient.OrganizationId ?? 1,
                        IsFirstUse = true,
                        IsFirstQuestionaire = true
                    };
                    _db.SYS_UserDetails.Add(user);
                    _db.SaveChanges();

                    if (user.UserId == 0)
                    {
                        response.ErrorCode = "USER_CREATION_FAILED";
                        response.ErrorMessage = "Failed to create user account. Please contact your administrator.";
                        return response;
                    }
                }

                if (patient != null)
                {

                    if (user.UserId == 0)
                    {
                        response.ErrorCode = "INVALID_USER_ID";
                        response.ErrorMessage = "User account configuration error. Please contact your administrator.";
                        return response;
                    }

                    if (patient.FacilityId.HasValue && !_facilityStatusService.IsFacilityActive(patient.FacilityId.Value))
                    {

                        response.UserId = null;
                        response.PatientId = null;
                        response.FacilityId = null;
                        response.ErrorCode = "FACILITY_DISABLED";
                        response.ErrorMessage = "Your facility has been disabled. Please contact your administrator.";
                        return response;
                    }

                    response.UserId = user.UserId;
                    response.PatientId = patient.PatientId;
                    response.IsFirstUse = user.IsFirstUse;
                    response.FirstName = patient.FirstName;
                    response.LastName = patient.LastName;
                    response.LoginId = login.LoginId;
                    response.Email = patient.Email;
                    response.OrganizationId = 1;
                    response.RoleId = 6;
                    response.RoleName = _db.LK_Roles.Where(x => x.RoleId == 6)
                                                    .Select(x => x.RoleName)
                                                    .FirstOrDefault();
                    response.FacilityId = patient.FacilityId;
                    response.FacilityGuid = _db.SYS_Facilities
                                               .Where(x => x.FacilityId == patient.FacilityId)
                                               .Select(x => x.Guid)
                                               .FirstOrDefault();

                    if (user != null)
                    {
                        response.ProfileUrl = !string.IsNullOrWhiteSpace(user.ProfileUrl) ? user.ProfileUrl : login.ProfileUrl;
                    }
                    else
                    {
                        response.ProfileUrl = login.ProfileUrl;
                    }

                    var activeTreatmentsQ = _db.PT_PatientTreatments
                        .Where(t => t.PatientId == patient.PatientId && (t.IsActive ?? false));

                    if (!activeTreatmentsQ.Any())
                    {

                        response.IsFirstQuestionaire = user.IsFirstQuestionaire ?? false;
                    }
                    else
                    {

                        bool allActiveHaveBeenAnswered = !activeTreatmentsQ.Any(t =>
                            !_db.PT_PatientTreatmentInTakeForms
                                .Any(f => f.PatientTreatmentId == t.PatientTreatmentId &&
                                         !string.IsNullOrWhiteSpace(f.Answer)));

                        response.IsFirstQuestionaire = !allActiveHaveBeenAnswered;
                    }

                }
                else
                {

                    response.IsFirstQuestionaire = true;
                }
            }

            return response;
        }

        public ChangePasswordResponseDTO ChangePassword(ChangePasswordRequestDTO request)
        {
            ChangePasswordResponseDTO response = new ChangePasswordResponseDTO();

            if (string.IsNullOrWhiteSpace(request.NewPassword))
            {
                response.Message = "New password cannot be empty";
                response.IsUpdated = false;
                return response;
            }

            if (!request.NewPassword.Equals(request.ConfirmNewPassword))
            {
                response.Message = "New password and confirm password do not match";
                response.IsUpdated = false;
                return response;
            }

            if (request.Code != null)
            {

                var ForgetPassword = _db.SYS_ForgetPasswords.Where(x => x.Code == request.Code).FirstOrDefault();
                if (ForgetPassword == null)
                {
                    response.Message = "Code is Invalid!";
                    response.IsUpdated = false;
                }
                else
                {
                    SYS_Login login = _db.SYS_Logins.Where(x => x.LoginId == ForgetPassword.LoginId).FirstOrDefault();
                    if (login != null)
                    {

                        login.Password = _passwordHasher.HashPassword(request.NewPassword);
                        _db.SaveChanges();
                        response.Message = "Password Updated.";
                        response.IsUpdated = true;
                    }
                    else
                    {
                        response.Message = "User not found";
                        response.IsUpdated = false;
                    }
                }
            }
            else
            {

                SYS_Login login = _db.SYS_Logins.Where(x => x.LoginId == request.LoginId).FirstOrDefault();
                if (login == null)
                {
                    response.Message = "User not found";
                    response.IsUpdated = false;
                    return response;
                }

                bool currentPasswordMatches;
                if (!string.IsNullOrWhiteSpace(login.Password))
                {
                    if (_passwordHasher.IsHashed(login.Password))
                    {

                        currentPasswordMatches = _passwordHasher.VerifyPassword(request.CurrentPassword, login.Password);
                    }
                    else
                    {

                        currentPasswordMatches = login.Password == request.CurrentPassword;
                    }
                }
                else
                {
                    currentPasswordMatches = false;
                }

                if (currentPasswordMatches)
                {

                    login.Password = _passwordHasher.HashPassword(request.NewPassword);
                    _db.SaveChanges();
                    response.Message = "Password Updated.";
                    response.IsUpdated = true;
                }
                else
                {
                    response.Message = "Current password does not match";
                    response.IsUpdated = false;
                }
            }

            return response;
        }

        public string? GenerateForgotPasswordCode(string Email)
        {
            string AuthCode = "";
            SYS_Login login = _db.SYS_Logins.Where(x => x.Email == Email).FirstOrDefault();
            if (login != null)
            {
                List<SYS_ForgetPassword> AlreadyAttempts = _db.SYS_ForgetPasswords.Where(x => x.LoginId == login.LoginId).ToList();
                _db.SYS_ForgetPasswords.RemoveRange(AlreadyAttempts);
                _db.SaveChanges();

                SYS_ForgetPassword forgotPassword = new SYS_ForgetPassword();
                AuthCode = GeneratePasswordResetCode();
                forgotPassword.Code = AuthCode;
                forgotPassword.LoginId = login.LoginId;
                _db.SYS_ForgetPasswords.Add(forgotPassword);
                _db.SaveChanges();

            }
            return AuthCode;
        }
    }
}
