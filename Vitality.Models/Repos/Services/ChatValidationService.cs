using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using Vitality.Models.EntityClasses;
using Vitality.Models.Enums;

namespace Vitality.Models.Repos.Services
{
    public class ChatValidationService : BaseRepo
    {

        public async Task<ChatValidationResult> ValidateMessagePermissionAsync(long senderId, long receiverId)
        {

            var sender = await GetUserInfoAsync(senderId);
            if (sender == null)
            {
                return new ChatValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Sender not found."
                };
            }

            var receiver = await GetUserInfoAsync(receiverId);
            if (receiver == null)
            {
                return new ChatValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Receiver not found."
                };
            }

            var senderRole = (UserRole)sender.RoleId;
            var receiverRole = (UserRole)receiver.RoleId;

            if (senderRole == UserRole.Patient)
            {

                if (receiverRole != UserRole.GlobalAdmin)
                {
                    return new ChatValidationResult
                    {
                        IsValid = false,
                        ErrorMessage = "Patients can only send direct messages to Global Administrators."
                    };
                }

                var gaInitiatedConversation = await _db.SYS_Chats
                    .AsNoTracking()
                    .Where(c =>
                        c.SenderId == receiverId &&
                        c.ReceiverId == senderId &&
                        c.ChannelId == null)
                    .AnyAsync();

                if (!gaInitiatedConversation)
                {
                    return new ChatValidationResult
                    {
                        IsValid = false,
                        ErrorMessage = "Patients cannot initiate direct messages. You can only respond to messages from Global Administrators."
                    };
                }

                return new ChatValidationResult { IsValid = true };
            }

            if (senderRole == UserRole.Provider)
            {
                if (receiverRole != UserRole.GlobalAdmin)
                {
                    return new ChatValidationResult
                    {
                        IsValid = false,
                        ErrorMessage = "Providers can only send direct messages to Global Administrators."
                    };
                }
                return new ChatValidationResult { IsValid = true };
            }

            if (senderRole == UserRole.ClinicAdmin)
            {
                if (receiverRole != UserRole.GlobalAdmin)
                {
                    return new ChatValidationResult
                    {
                        IsValid = false,
                        ErrorMessage = "Clinic Administrators can only send direct messages to Global Administrators."
                    };
                }
                return new ChatValidationResult { IsValid = true };
            }

            if (senderRole == UserRole.GlobalAdmin)
            {
                return new ChatValidationResult { IsValid = true };
            }

            return new ChatValidationResult { IsValid = true };
        }

        public async Task<ChatValidationResult> ValidateChannelMessagePermissionAsync(
            long senderId,
            long channelId,
            long? individualReceiverId = null)
        {

            var sender = await _db.SYS_UserDetails
                .AsNoTracking()
                .Where(u => u.UserId == senderId && u.IsActive == true)
                .Select(u => new { u.UserId, RoleId = u.Login != null ? u.Login.RoleId : (int?)null })
                .FirstOrDefaultAsync();

            if (sender == null)
            {
                return new ChatValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Sender not found."
                };
            }

            if (!sender.RoleId.HasValue)
            {
                return new ChatValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Sender role not found."
                };
            }

            var senderRole = (UserRole)sender.RoleId.Value;

            var isParticipant = await _db.SYS_ChatChannelParticipants
                .AsNoTracking()
                .AnyAsync(p => p.ChannelId == channelId &&
                             p.UserId == senderId &&
                             p.IsActive == true);

            if (!isParticipant)
            {
                return new ChatValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "You are not a participant in this channel."
                };
            }

            var channel = await _db.SYS_ChatChannels
                .AsNoTracking()
                .Where(c => c.ChannelId == channelId && c.IsActive == true)
                .FirstOrDefaultAsync();

            if (channel == null)
            {
                return new ChatValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Channel not found."
                };
            }

            if (individualReceiverId.HasValue)
            {

                if (senderRole != UserRole.GlobalAdmin)
                {
                    return new ChatValidationResult
                    {
                        IsValid = false,
                        ErrorMessage = "Only global administrators can send individual messages within channels."
                    };
                }

                var receiverIsParticipant = await _db.SYS_ChatChannelParticipants
                    .AsNoTracking()
                    .AnyAsync(p => p.ChannelId == channelId &&
                                 p.UserId == individualReceiverId.Value &&
                                 p.IsActive == true);

                if (!receiverIsParticipant)
                {
                    return new ChatValidationResult
                    {
                        IsValid = false,
                        ErrorMessage = "The recipient is not a participant in this channel."
                    };
                }
            }

            return new ChatValidationResult { IsValid = true };
        }

        private async Task<UserInfo?> GetUserInfoAsync(long userId)
        {

            var user = await _db.SYS_UserDetails
                .AsNoTracking()
                .Where(u => u.UserId == userId && u.IsActive == true)
                .Select(u => new UserInfo
                {
                    UserId = u.UserId,
                    RoleId = u.Login != null ? u.Login.RoleId : (int?)null
                })
                .FirstOrDefaultAsync();

            if (user != null && user.RoleId.HasValue && user.RoleId != (int)UserRole.Patient)
            {
                return user;
            }

            if (user != null && user.RoleId == (int)UserRole.Patient)
            {
                var loginId = await _db.SYS_UserDetails
                    .AsNoTracking()
                    .Where(u => u.UserId == userId)
                    .Select(u => u.LoginId)
                    .FirstOrDefaultAsync();

                if (loginId.HasValue)
                {
                    var patient = await _db.PT_Patients
                        .AsNoTracking()
                        .Where(p => p.LoginId == loginId.Value && p.IsActive == true)
                        .Select(p => new UserInfo
                        {
                            UserId = p.PatientId,
                            RoleId = (int)UserRole.Patient
                        })
                        .FirstOrDefaultAsync();

                    return patient;
                }
            }

            return null;
        }

        private async Task<bool> IsPatientAssociatedWithProviderAsync(long providerId, long patientUserId)
        {

            var patientLoginId = await _db.SYS_UserDetails
                .AsNoTracking()
                .Where(u => u.UserId == patientUserId)
                .Select(u => u.LoginId)
                .FirstOrDefaultAsync();

            if (!patientLoginId.HasValue)
                return false;

            var actualPatientId = await _db.PT_Patients
                .AsNoTracking()
                .Where(p => p.LoginId == patientLoginId.Value)
                .Select(p => p.PatientId)
                .FirstOrDefaultAsync();

            if (actualPatientId == 0)
                return false;

            return await _db.PT_PatientAppointmentSlots
                .AsNoTracking()
                .AnyAsync(a =>
                    a.ProviderId == providerId &&
                    a.PatientId == actualPatientId &&
                    a.IsActive == true);
        }

        private async Task<long?> GetUserFacilityAsync(long userId)
        {

            var facility = await _db.FC_UsersInFacilities
                .AsNoTracking()
                .Where(uf => uf.UserId == userId)
                .Select(uf => (long?)uf.FacilityId)
                .FirstOrDefaultAsync();

            return facility;
        }

        private async Task<long?> GetPatientFacilityAsync(long patientId)
        {
            var patient = await _db.PT_Patients
                .AsNoTracking()
                .Where(p => p.PatientId == patientId)
                .Select(p => (long?)p.FacilityId)
                .FirstOrDefaultAsync();

            return patient;
        }

        private class UserInfo
        {
            public long UserId { get; set; }
            public int? RoleId { get; set; }
        }
    }

    public class ChatValidationResult
    {
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
