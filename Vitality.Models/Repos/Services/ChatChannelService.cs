using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Vitality.Models.EntityClasses;
using Vitality.Models.Enums;

namespace Vitality.Models.Repos.Services
{
    public class ChatChannelService : BaseRepo
    {
        private const string SystemWelcomeMessage =
            "👏Welcome to Your Patient Portal!\n\n" +
            "We're excited to have you on board.\n\n" +
            "This portal is your secure, all-in-one place to:\n\n" +
            "- Access your treatment plan\n" +
            "- Message your care team\n" +
            "- View and schedule appointments\n" +
            "- Track prescriptions and refills\n" +
            "- Receive important updates and support\n\n" +
            "Your health journey is personal - and we're here to support you every step of the way.\n\n" +
            "If you have any questions, feel free to message us directly through the portal.\n\n" +
            "Let's make progress together.";

        public async Task<long?> CreateTreatmentChannelAsync(
            long treatmentId,
            long patientId,
            long? providerId,
            long? facilityId,
            long? createdBy)
        {
            try
            {

                var existingChannel = await _db.SYS_ChatChannels
                    .AsNoTracking()
                    .Where(c =>
                        c.PatientId == patientId &&
                        c.ChannelType == "Treatment" &&
                        c.IsActive == true)
                    .OrderBy(c => c.ChannelId)
                    .FirstOrDefaultAsync();

                if (existingChannel != null)
                {
                    await EnsureChannelParticipantsAsync(existingChannel.ChannelId, patientId, providerId, facilityId);
                    return existingChannel.ChannelId;
                }

                var patient = await _db.PT_Patients
                    .AsNoTracking()
                    .Where(p => p.PatientId == patientId)
                    .Select(p => new { p.FirstName, p.LastName })
                    .FirstOrDefaultAsync();

                string clinicOwnerName = "Unknown Owner";
                if (facilityId.HasValue && facilityId.Value > 0)
                {
                    var clinicOwner = await _db.SYS_Facilities
                        .AsNoTracking()
                        .Where(uf => uf.FacilityId == facilityId.Value)

                        .Select(x => x.TitleLong)
                        .FirstOrDefaultAsync();

                    if (!string.IsNullOrEmpty(clinicOwner))
                    {
                        clinicOwnerName = clinicOwner;
                    }
                }

                string patientName = patient != null
                    ? $"{patient.FirstName} {patient.LastName}"
                    : $"Patient {patientId}";

                string channelName = $"{clinicOwnerName}-{patientName}";

                var channel = new SYS_ChatChannel
                {
                    ChannelName = channelName,
                    ChannelType = "Treatment",
                    TreatmentId = treatmentId,
                    PatientId = patientId,
                    ProviderId = providerId,
                    FacilityId = facilityId,
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = createdBy
                };

                _db.SYS_ChatChannels.Add(channel);
                await _db.SaveChangesAsync();

                var participants = new List<SYS_ChatChannelParticipant>();

                var patientLoginId = await _db.PT_Patients
                    .AsNoTracking()
                    .Where(p => p.PatientId == patientId)
                    .Select(p => p.LoginId)
                    .FirstOrDefaultAsync();

                if (patientLoginId.HasValue)
                {
                    var patientUserId = await _db.SYS_UserDetails
                        .AsNoTracking()
                        .Where(u => u.LoginId == patientLoginId.Value)
                        .Select(u => u.UserId)
                        .FirstOrDefaultAsync();

                    if (patientUserId > 0)
                    {
                        participants.Add(new SYS_ChatChannelParticipant
                        {
                            ChannelId = channel.ChannelId,
                            UserId = patientUserId,
                            IsActive = true,
                            JoinedDate = DateTime.UtcNow
                        });
                    }
                }

                if (providerId.HasValue && providerId.Value > 0)
                {
                    participants.Add(new SYS_ChatChannelParticipant
                    {
                        ChannelId = channel.ChannelId,
                        UserId = providerId.Value,
                        IsActive = true,
                        JoinedDate = DateTime.UtcNow
                    });
                }

                if (facilityId.HasValue && facilityId.Value > 0)
                {

                    var clinicAdmins = await _db.FC_UsersInFacilities
                        .AsNoTracking()
                        .Where(uf => uf.FacilityId == facilityId.Value)
                        .Join(_db.SYS_UserDetails.AsNoTracking(),
                            uf => uf.UserId,
                            ud => ud.UserId,
                            (uf, ud) => new { ud.UserId, ud.Login })
                        .Where(x => x.Login != null && x.Login.RoleId == (int)UserRole.ClinicAdmin)
                        .Select(x => x.UserId)
                        .Distinct()
                        .ToListAsync();

                    foreach (var adminUserId in clinicAdmins)
                    {
                        if (adminUserId > 0)
                        {
                            participants.Add(new SYS_ChatChannelParticipant
                            {
                                ChannelId = channel.ChannelId,
                                UserId = adminUserId,
                                IsActive = true,
                                JoinedDate = DateTime.UtcNow
                            });
                        }
                    }
                }

                var globalAdmins = await _db.SYS_UserDetails
                    .AsNoTracking()
                    .Where(u => u.Login != null &&
                                u.Login.RoleId == (int)UserRole.GlobalAdmin &&
                                u.IsActive == true &&
                                u.Status == "Active")
                    .Select(u => u.UserId)
                    .ToListAsync();

                foreach (var adminUserId in globalAdmins)
                {
                    participants.Add(new SYS_ChatChannelParticipant
                    {
                        ChannelId = channel.ChannelId,
                        UserId = adminUserId,
                        IsActive = true,
                        JoinedDate = DateTime.UtcNow
                    });
                }

                if (participants.Any())
                {
                    _db.SYS_ChatChannelParticipants.AddRange(participants);
                    await _db.SaveChangesAsync();
                }

                var participantUserIds = participants
                    .Where(p => p.UserId.HasValue && p.UserId.Value > 0)
                    .Select(p => p.UserId)
                    .Distinct()
                    .ToList();
                await AddSystemWelcomeMessageAsync(channel.ChannelId, participantUserIds);

                return channel.ChannelId;
            }
            catch (Exception ex)
            {

                Console.WriteLine($"Error creating treatment channel: {ex.Message}");
                return null;
            }
        }

        public async Task<long?> CreateIndividualChannelAsync(
            long globalAdminUserId,
            long targetUserId,
            long? createdBy)
        {
            try
            {

                var existingChannel = await _db.SYS_ChatChannels
                    .AsNoTracking()
                    .Where(c => c.ChannelType == "Individual" && c.IsActive == true)
                    .Join(_db.SYS_ChatChannelParticipants.AsNoTracking(),
                        c => c.ChannelId,
                        p => p.ChannelId,
                        (c, p) => new { Channel = c, Participant = p })
                    .Where(x => x.Participant.UserId == globalAdminUserId || x.Participant.UserId == targetUserId)
                    .GroupBy(x => x.Channel.ChannelId)
                    .Where(g => g.Count() == 2)
                    .Select(g => g.Key)
                    .FirstOrDefaultAsync();

                if (existingChannel > 0)
                {
                    return existingChannel;
                }

                var globalAdminName = await _db.SYS_UserDetails
                    .AsNoTracking()
                    .Where(u => u.UserId == globalAdminUserId)
                    .Select(u => $"{u.FirstName} {u.LastName}")
                    .FirstOrDefaultAsync();

                var targetUserName = await GetUserNameAsync(targetUserId);

                string channelName = $"Individual - {globalAdminName} & {targetUserName}";

                var channel = new SYS_ChatChannel
                {
                    ChannelName = channelName,
                    ChannelType = "Individual",
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = createdBy
                };

                _db.SYS_ChatChannels.Add(channel);
                await _db.SaveChangesAsync();

                var participants = new List<SYS_ChatChannelParticipant>
                {
                    new SYS_ChatChannelParticipant
                    {
                        ChannelId = channel.ChannelId,
                        UserId = globalAdminUserId,
                        IsActive = true,
                        JoinedDate = DateTime.UtcNow
                    },
                    new SYS_ChatChannelParticipant
                    {
                        ChannelId = channel.ChannelId,
                        UserId = targetUserId,
                        IsActive = true,
                        JoinedDate = DateTime.UtcNow
                    }
                };

                _db.SYS_ChatChannelParticipants.AddRange(participants);
                await _db.SaveChangesAsync();

                var participantUserIds = participants
                    .Where(p => p.UserId.HasValue && p.UserId.Value > 0)
                    .Select(p => p.UserId)
                    .Distinct()
                    .ToList();
                await AddSystemWelcomeMessageAsync(channel.ChannelId, participantUserIds);

                return channel.ChannelId;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating individual channel: {ex.Message}");
                return null;
            }
        }

        private async Task<string> GetUserNameAsync(long userId)
        {

            var user = await _db.SYS_UserDetails
                .AsNoTracking()
                .Where(u => u.UserId == userId)
                .Select(u => new { u.FirstName, u.LastName, u.Login })
                .FirstOrDefaultAsync();

            if (user != null)
            {

                if (user.Login != null && user.Login.RoleId == (int)UserRole.Patient)
                {
                    var patient = await _db.PT_Patients
                        .AsNoTracking()
                        .Where(p => p.LoginId == user.Login.LoginId)
                        .Select(p => $"{p.FirstName} {p.LastName}")
                        .FirstOrDefaultAsync();

                    return patient ?? "Unknown";
                }

                return $"{user.FirstName} {user.LastName}";
            }

            return "Unknown";
        }

        private async Task AddSystemWelcomeMessageAsync(long channelId, List<long?> participantUserIds)
        {
            var exists = await _db.SYS_Chats
                .AsNoTracking()
                .AnyAsync(m =>
                    m.ChannelId == channelId &&
                    m.IsActive == true &&
                    m.Content == SystemWelcomeMessage &&
                    (m.MessageType == "Channel" || m.MessageType == null));

            if (exists)
                return;

            var systemMessage = new SYS_Chat
            {
                SenderId = null,
                ChannelId = channelId,
                Content = SystemWelcomeMessage,
                CreatedDate = DateTime.UtcNow,
                IsActive = true,
                CreatedBy = null,
                IsRead = true,
                MessageType = "Channel"
            };

            _db.SYS_Chats.Add(systemMessage);
            await _db.SaveChangesAsync();

            if (participantUserIds.Count == 0)
                return;

            var receipts = participantUserIds
                .Where(userId => userId.HasValue)
                .Select(userId => new SYS_ChatReadReceipt
                {
                    ChatId = systemMessage.Id,
                    UserId = userId!.Value,
                    ReadDate = DateTime.UtcNow,
                    IsActive = true
                })
                .ToList();

            _db.SYS_ChatReadReceipts.AddRange(receipts);
            await _db.SaveChangesAsync();
        }

        private async Task EnsureChannelParticipantsAsync(long channelId, long patientId, long? providerId, long? facilityId)
        {
            var existingParticipantUserIds = await _db.SYS_ChatChannelParticipants
                .AsNoTracking()
                .Where(p => p.ChannelId == channelId && p.IsActive == true && p.UserId.HasValue)
                .Select(p => p.UserId!.Value)
                .Distinct()
                .ToListAsync();

            var participantsToAdd = new List<SYS_ChatChannelParticipant>();

            var patientLoginId = await _db.PT_Patients
                .AsNoTracking()
                .Where(p => p.PatientId == patientId)
                .Select(p => p.LoginId)
                .FirstOrDefaultAsync();

            if (patientLoginId.HasValue)
            {
                var patientUserId = await _db.SYS_UserDetails
                    .AsNoTracking()
                    .Where(u => u.LoginId == patientLoginId.Value)
                    .Select(u => u.UserId)
                    .FirstOrDefaultAsync();

                if (patientUserId > 0 && !existingParticipantUserIds.Contains(patientUserId))
                {
                    participantsToAdd.Add(new SYS_ChatChannelParticipant
                    {
                        ChannelId = channelId,
                        UserId = patientUserId,
                        IsActive = true,
                        JoinedDate = DateTime.UtcNow
                    });
                }
            }

            if (providerId.HasValue && providerId.Value > 0 && !existingParticipantUserIds.Contains(providerId.Value))
            {
                participantsToAdd.Add(new SYS_ChatChannelParticipant
                {
                    ChannelId = channelId,
                    UserId = providerId.Value,
                    IsActive = true,
                    JoinedDate = DateTime.UtcNow
                });
            }

            if (facilityId.HasValue && facilityId.Value > 0)
            {
                var clinicAdmins = await _db.FC_UsersInFacilities
                    .AsNoTracking()
                    .Where(uf => uf.FacilityId == facilityId.Value)
                    .Join(_db.SYS_UserDetails.AsNoTracking(),
                        uf => uf.UserId,
                        ud => ud.UserId,
                        (uf, ud) => new { ud.UserId, ud.Login })
                    .Where(x => x.Login != null && x.Login.RoleId == (int)UserRole.ClinicAdmin)
                    .Select(x => x.UserId)
                    .Distinct()
                    .ToListAsync();

                foreach (var adminUserId in clinicAdmins)
                {
                    if (adminUserId > 0 && !existingParticipantUserIds.Contains(adminUserId))
                    {
                        participantsToAdd.Add(new SYS_ChatChannelParticipant
                        {
                            ChannelId = channelId,
                            UserId = adminUserId,
                            IsActive = true,
                            JoinedDate = DateTime.UtcNow
                        });
                    }
                }
            }

            var globalAdmins = await _db.SYS_UserDetails
                .AsNoTracking()
                .Where(u => u.Login != null &&
                            u.Login.RoleId == (int)UserRole.GlobalAdmin &&
                            u.IsActive == true &&
                            u.Status == "Active")
                .Select(u => u.UserId)
                .ToListAsync();

            foreach (var adminUserId in globalAdmins)
            {
                if (!existingParticipantUserIds.Contains(adminUserId))
                {
                    participantsToAdd.Add(new SYS_ChatChannelParticipant
                    {
                        ChannelId = channelId,
                        UserId = adminUserId,
                        IsActive = true,
                        JoinedDate = DateTime.UtcNow
                    });
                }
            }

            if (participantsToAdd.Any())
            {
                _db.SYS_ChatChannelParticipants.AddRange(participantsToAdd);
                await _db.SaveChangesAsync();
            }
        }
    }
}
