using DudeMeds.Models.DTOs.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vitality.Models.DTOs.Users;

namespace DudeMeds.Models.Repos.Interfaces
{
    public interface IUsersRepo
    {
        public List<GetAllUsersResponseDTO> GetAllUsers(GetAllUsersRequestDTO request, out int totalUserCount);
        public GetUserByIdResponseDTO GetUserById(long UserId);
        public long SaveUser(SaveUserRequestDTO request, long OrganizationId, long UserId);
        public bool DeleteUser(long UserId);
        public bool ActivateUser(ActivateUserRequestDTO request);
        public bool UpdateUserPassword(UpdateUserPasswordRequestDTO request);
        public bool UpdateUserCardCredentials(UpdateUserCardCredentialsRequestDTO request);
        public bool UpdateUserProfile(UpdateUserProfileRequestDto request);
        public Task<bool> UpdateUserProfileUrlAsync(UpdateUserProfileUrlRequestDTO request);
        public string AssignUserToFacility(List<AssignUserToFacilityRequestDTO> requestList, long UserId, long OrganizationId);
        public bool CheckEmailExist(CheckEmailExistRequestDTO request);
        GetUserByIdResponseDTO GetUserByIdLinq(long UserId);
        public List<long> GetGlobalAdminIds();

        public List<(long UserId, string Email)> GetGlobalAdminUsersWithEmail();

        public List<(long UserId, string Email)> GetTechSupportUsersWithEmail();
        public long SaveClinicAdmins();

        Task<bool> ActiveUserExistsAsync(string email, CancellationToken ct = default);

        bool SetStripePlatformCustomerId(long userId, string stripePlatformCustomerId);
    }
}
