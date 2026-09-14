using Microsoft.EntityFrameworkCore;
using OpenTokSDK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Vitality.Models.DTOs.Chats;
using Vitality.Models.EntityClasses;
using Vitality.Models.Enums;

namespace Vitality.Models.Repos.Services
{
    public class ChatsRepo : BaseRepo
    {
        private class UserLite
        {
            public long? UserId { get; set; }
            public string? FirstName { get; set; }
            public string? LastName { get; set; }
            public int? RoleId { get; set; }
        }

        private class ChannelMessageLite
        {
            public long ChannelId { get; set; }
            public long Id { get; set; }
            public long? SenderId { get; set; }
            public string? Content { get; set; }
            public DateTime CreatedDate { get; set; }
        }

        public async Task<List<GetAllUsersforChatResponseDTO>> GetAllUsersforChatAsync(
            GetAllUsersforChatRequestDTO request,
            CancellationToken ct = default)
        {
            try
            {
                var final = new List<GetAllUsersforChatResponseDTO>();

                if (request == null)
                {
                    throw new ArgumentNullException(nameof(request), "Request cannot be null.");
                }

                bool isGlobalAdmin = request.RoleId == (int)UserRole.GlobalAdmin;
                bool isClinicAdmin = request.RoleId == (int)UserRole.ClinicAdmin;
                bool isCustomerSupport = request.RoleId == (int)UserRole.CustomerSupport;

                IQueryable<UserLite> uq;

                if (isClinicAdmin)
                {

                    uq = _db.SYS_UserDetails
                        .AsNoTracking()
                        .Where(u =>
                            u.UserId != request.UserId &&
                            u.IsActive == true &&
                            u.Status == "Active" &&
                            u.Login != null &&
                            u.Login.RoleId == (int)UserRole.GlobalAdmin &&
                            u.Login.RoleId != (int)UserRole.Patient)
                        .Select(u => new UserLite
                        {
                            UserId = u.UserId,
                            FirstName = u.FirstName,
                            LastName = u.LastName,
                            RoleId = u.Login != null ? (int?)u.Login.RoleId : null
                        });
                }
                else if (isCustomerSupport)
                {
                    uq = _db.SYS_UserDetails
                        .AsNoTracking()
                        .Where(u =>
                            u.UserId != request.UserId &&
                            u.IsActive == true &&
                            u.Status == "Active" &&
                            u.Login != null &&
                            u.Login.RoleId != (int)UserRole.Patient &&
                            (
                                u.Login.RoleId != (int)UserRole.GlobalAdmin &&
                                u.Login.RoleId != (int)UserRole.Provider &&
                                _db.FC_UsersInFacilities.Any(f => f.UserId == u.UserId && f.FacilityId == request.FacilityId)
                            ))
                        .Select(u => new UserLite
                        {
                            UserId = u.UserId,
                            FirstName = u.FirstName,
                            LastName = u.LastName,
                            RoleId = u.Login != null ? (int?)u.Login.RoleId : null
                        });
                }
                else if (request.RoleId == (int)UserRole.Provider)
                {

                    uq = _db.SYS_UserDetails
                        .AsNoTracking()
                        .Where(u =>
                            u.UserId != request.UserId &&
                            u.IsActive == true &&
                            u.Status == "Active" &&
                            u.Login != null &&
                            u.Login.RoleId == (int)UserRole.GlobalAdmin &&
                            u.Login.RoleId != (int)UserRole.Patient)
                        .Select(u => new UserLite
                        {
                            UserId = u.UserId,
                            FirstName = u.FirstName,
                            LastName = u.LastName,
                            RoleId = u.Login != null ? (int?)u.Login.RoleId : null
                        });
                }
                else if (request.RoleId == (int)UserRole.Patient)
                {

                    uq = _db.SYS_UserDetails
                        .AsNoTracking()
                        .Where(u => false)
                        .Select(u => new UserLite
                        {
                            UserId = u.UserId,
                            FirstName = u.FirstName,
                            LastName = u.LastName,
                            RoleId = u.Login != null ? (int?)u.Login.RoleId : null
                        });
                }
                else
                {

                    uq = _db.SYS_UserDetails
                        .AsNoTracking()
                        .Where(u =>
                            u.UserId != request.UserId &&
                            u.IsActive == true &&
                            u.Status == "Active" &&
                            u.Login != null &&
                            u.Login.RoleId != (int)UserRole.Patient)
                        .Select(u => new UserLite
                        {
                            UserId = u.UserId,
                            FirstName = u.FirstName,
                            LastName = u.LastName,
                            RoleId = u.Login != null ? (int?)u.Login.RoleId : null
                        });
                }

                var usersCore = await uq.Distinct().ToListAsync(ct);

                usersCore = usersCore.Where(u => u != null && u.UserId.HasValue).ToList();
                var usersCoreIds = usersCore
                    .Select(u => u.UserId)
                    .Where(id => id.HasValue)
                    .Select(id => id!.Value)
                    .Distinct()
                    .ToList();

                var roleTitleByUserId = usersCoreIds.Count == 0
                    ? new Dictionary<long, string?>()
                    : await (from ud in _db.SYS_UserDetails.AsNoTracking()
                             join rt0 in _db.LK_RoleTitles.AsNoTracking() on ud.RoleTitleId equals rt0.RoleTitleId into rtj
                             from rt in rtj.DefaultIfEmpty()
                             where usersCoreIds.Contains(ud.UserId)
                             select new
                             {
                                 ud.UserId,
                                 RoleTitleName = rt != null && rt.IsActive == true ? rt.RoleTitleName : null
                             })
                        .ToDictionaryAsync(x => x.UserId, x => x.RoleTitleName, ct);

                var userFacilitiesMap = new Dictionary<long, string>();
                if (isGlobalAdmin && usersCore.Count > 0)
                {
                    var userIds = usersCore
                        .Select(u => u.UserId)
                        .Where(id => id.HasValue)
                        .Select(id => id!.Value)
                        .Distinct()
                        .ToList();

                    if (userIds.Count > 0)
                    {
                        try
                        {
                            var ufRows = await (
                                from uf in _db.FC_UsersInFacilities.AsNoTracking()
                                join fac in _db.SYS_Facilities.AsNoTracking() on uf.FacilityId equals fac.FacilityId
                                where uf.UserId.HasValue && userIds.Contains(uf.UserId.Value)
                                select new
                                {
                                    UserId = uf.UserId.Value,
                                    FacilityName = fac != null ? fac.TitleShort : null
                                }
                            ).ToListAsync(ct);

                            userFacilitiesMap = ufRows
                                .Where(x => x != null)
                                .GroupBy(x => x.UserId)
                                .ToDictionary(
                                    g => g.Key,
                                    g => string.Join(", ",
                                        g.Select(x => x.FacilityName)
                                         .Where(s => !string.IsNullOrWhiteSpace(s))
                                         .Distinct()
                                         .OrderBy(s => s))
                                );
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error loading user facilities: {ex.Message}");

                        }
                    }
                }

                foreach (var u in usersCore)
                {
                    if (u == null || !u.UserId.HasValue)
                        continue;

                    try
                    {
                        var roleId = u.RoleId ?? 0;
                        var roleEnum = (UserRole)roleId;
                        var roleName = EnumHelper.GetDescription(roleEnum);

                        string? facilityName = null;
                        if (isGlobalAdmin && u.UserId.HasValue)
                            userFacilitiesMap.TryGetValue(u.UserId.Value, out facilityName);

                        final.Add(new GetAllUsersforChatResponseDTO
                        {
                            UserId = u.UserId,
                            UserName = BuildDisplayName(u.FirstName, u.LastName,
                                u.UserId.HasValue && roleTitleByUserId.ContainsKey(u.UserId.Value)
                                    ? roleTitleByUserId[u.UserId.Value]
                                    : null),
                            UserType = roleName ?? "Unknown",
                            FacilityName = facilityName
                        });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error mapping user {u.UserId}: {ex.Message}");

                        continue;
                    }
                }

                List<GetAllUsersforChatResponseDTO> patients = new List<GetAllUsersforChatResponseDTO>();

                try
                {

                    patients = new List<GetAllUsersforChatResponseDTO>();

                    patients = patients.Where(p => p != null && p.UserId.HasValue).ToList();
                    final.AddRange(patients);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading patients: {ex.Message}");

                }

                final = final
                    .GroupBy(x => x.UserId)
                    .Select(g => g.First())
                    .OrderByDescending(x => x.UserId)
                    .ToList();

                return final;
            }
            catch (ArgumentNullException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetAllUsersforChatAsync: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");

                return new List<GetAllUsersforChatResponseDTO>();
            }
        }

        private static DateTime EnsureUtc(DateTime d)
        {
            return d.Kind == DateTimeKind.Utc ? d : DateTime.SpecifyKind(d, DateTimeKind.Utc);
        }

        public async Task<List<OnlineUserDTO>> GetOnlineUserInfoAsync(string userId)
        {

            var raw = await _db.SYS_Chats
                .Where(x =>
                    (x.ReceiverId.ToString() == userId || x.SenderId.ToString() == userId)
                    && x.IsActive == true)
                .GroupBy(x =>
                    x.ReceiverId.ToString() == userId
                        ? x.SenderId
                        : x.ReceiverId)
                .Select(g => new
                {
                    UserId = g.Key,
                    LastMessage = g.OrderByDescending(x => x.CreatedDate)
                                   .Select(x => x.Content)
                                   .FirstOrDefault(),
                    CreatedDate = g.OrderByDescending(x => x.CreatedDate)
                                   .Select(x => x.CreatedDate)
                                   .FirstOrDefault(),
                    UnreadCount = g.Count(x =>
                        x.ReceiverId.ToString() == userId &&
                        x.IsRead == false)
                })
                .ToListAsync();

            if (raw == null || raw.Count == 0)
                return new List<OnlineUserDTO>();

            var userIdsLong = raw.Select(r => r.UserId).ToList();

            var userDetails = await (
                from ud in _db.SYS_UserDetails.AsNoTracking()
                where userIdsLong.Contains(ud.UserId)
                join fcf in _db.FC_UsersInFacilities.AsNoTracking()
                    on ud.UserId equals fcf.UserId into fcfj
                from fcf in fcfj.DefaultIfEmpty()
                join fac in _db.SYS_Facilities.AsNoTracking()
                    on fcf.FacilityId equals fac.FacilityId into facj
                from fac in facj.DefaultIfEmpty()
                select new
                {
                    UserId = ud.UserId,
                    FirstName = ud.FirstName,
                    LastName = ud.LastName,
                    FacilityId = (long?)fcf.FacilityId,
                    FacilityName = fac.TitleShort,
                    PatientId = (long?)null,
                    IsPatient = false
                })
                .ToListAsync();

            var patientDetails = await (
                from ud in _db.SYS_UserDetails.AsNoTracking()
                join login in _db.SYS_Logins.AsNoTracking() on ud.LoginId equals login.LoginId
                join p in _db.PT_Patients.AsNoTracking() on ud.LoginId equals p.LoginId
                join fac in _db.SYS_Facilities.AsNoTracking()
                    on p.FacilityId equals fac.FacilityId into facj
                from fac in facj.DefaultIfEmpty()
                where userIdsLong.Contains(ud.UserId) && login.RoleId == 6
                select new
                {
                    UserId = ud.UserId,
                    FirstName = p.FirstName,
                    LastName = p.LastName,
                    FacilityId = (long?)p.FacilityId,
                    FacilityName = fac != null ? fac.TitleShort : null,
                    PatientId = (long?)p.PatientId,
                    IsPatient = true
                })
                .ToListAsync();

            var allDetails = userDetails.Concat(patientDetails).ToList();

            var result = raw
                .Select(r =>
                {
                    var det = allDetails
                        .Where(d => d.UserId == r.UserId)
                        .FirstOrDefault();

                    var facilityId = det?.FacilityId;
                    var facilityName = det?.FacilityName;
                    var userName = det != null
                        ? ((det.FirstName ?? "") + " " + (det.LastName ?? "")).Trim()
                        : "Unknown";

                    var createdUtc = EnsureUtc(r.CreatedDate);
                    return new OnlineUserDTO
                    {
                        UserId = r.UserId.ToString(),
                        UserName = userName,
                        ConnectionId = null,
                        IsOnline = false,
                        LastMessage = r.LastMessage,
                        LastMsgTime = null,
                        CreatedDate = createdUtc,
                        ProfilePic = null,
                        IsTyping = false,
                        UnreadCount = r.UnreadCount,
                        FacilityId = facilityId,
                        FacilityName = facilityName,
                        PatientId = det?.PatientId
                    };
                })
                .OrderByDescending(x => x.CreatedDate)
                .ToList();

            return result;
        }

        public async Task<long> SaveMessageAsync(ChatMessageRequestDTO request, long? UserId)
        {
            var message = new SYS_Chat
            {
                SenderId = UserId,
                ReceiverId = Convert.ToInt64(request.ReceiverId),
                Content = request.Content,
                CreatedDate = DateTime.UtcNow,
                IsActive = true,
                CreatedBy = UserId,
                IsRead = false,
            };

            _db.SYS_Chats.Add(message);
            await _db.SaveChangesAsync();
            return message.Id;
        }

        public async Task<List<ChatMessageResponseDTO>> GetMessagesAsync(
            string userId1,
            string userId2,
            int pageNumber = 1,
            int pageSize = 10)
        {
            int skip = (pageNumber - 1) * pageSize;

            var chatListQuery = _db.SYS_Chats
                .Where(m =>
                    ((m.SenderId.ToString() == userId1 && m.ReceiverId.ToString() == userId2) ||
                     (m.SenderId.ToString() == userId2 && m.ReceiverId.ToString() == userId1))
                    && m.IsActive == true)
                .OrderByDescending(m => m.CreatedDate)
                .ThenBy(m => m.Id);

            var allChatUserIds = await _db.SYS_Chats
                .Where(m =>
                    ((m.SenderId.ToString() == userId1 && m.ReceiverId.ToString() == userId2) ||
                     (m.SenderId.ToString() == userId2 && m.ReceiverId.ToString() == userId1))
                    && m.IsActive == true)
                .Select(c => new { c.SenderId, c.ReceiverId })
                .Distinct()
                .ToListAsync();

            var allSenderIds = allChatUserIds.Select(c => c.SenderId).Distinct().ToList();
            var allReceiverIds = allChatUserIds.Select(c => c.ReceiverId).Distinct().ToList();
            var allUserIds = allSenderIds.Concat(allReceiverIds).Distinct().ToList();

            var regularUsers = await (
                from ud in _db.SYS_UserDetails.AsNoTracking()
                where allUserIds.Contains(ud.UserId)
                join login in _db.SYS_Logins.AsNoTracking() on ud.LoginId equals login.LoginId
                join rt0 in _db.LK_RoleTitles.AsNoTracking() on ud.RoleTitleId equals rt0.RoleTitleId into rtj
                from rt in rtj.DefaultIfEmpty()
                where login.RoleId != 6
                select new { ud.UserId, ud.FirstName, ud.LastName, RoleTitleName = rt != null && rt.IsActive == true ? rt.RoleTitleName : null, IsPatient = false, PatientId = (long?)null }
            ).ToListAsync();

            var patientUsers = await (
                from ud in _db.SYS_UserDetails.AsNoTracking()
                join login in _db.SYS_Logins.AsNoTracking() on ud.LoginId equals login.LoginId
                join p in _db.PT_Patients.AsNoTracking() on ud.LoginId equals p.LoginId
                where allUserIds.Contains(ud.UserId) && login.RoleId == 6
                select new
                {
                    UserId = ud.UserId,
                    p.FirstName,
                    p.LastName,
                    RoleTitleName = (string?)null,
                    IsPatient = true,
                    PatientId = (long?)p.PatientId
                }
            ).ToListAsync();

            var allUserInfo = regularUsers.Concat(patientUsers).ToList();

            var pagedChats = await chatListQuery
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync();

            if (pagedChats.Count == 0)
            {
                return new List<ChatMessageResponseDTO>();
            }

            var chatList = (
                from chat in pagedChats
                let senderInfo = allUserInfo.FirstOrDefault(u => u.UserId == chat.SenderId)
                let receiverInfo = allUserInfo.FirstOrDefault(u => u.UserId == chat.ReceiverId)
                select new ChatMessageResponseDTO
                {
                    Id = chat.Id,
                    SenderId = chat.SenderId.ToString(),
                    SenderName = senderInfo != null
                        ? BuildDisplayName(senderInfo.FirstName, senderInfo.LastName, senderInfo.IsPatient == true ? null : senderInfo.RoleTitleName)
                        : "Unknown",
                    ReceiverId = chat.ReceiverId.ToString(),
                    ReceiverName = receiverInfo != null
                        ? BuildDisplayName(receiverInfo.FirstName, receiverInfo.LastName, receiverInfo.IsPatient == true ? null : receiverInfo.RoleTitleName)
                        : "Unknown",
                    PatientId =
                        (senderInfo != null && senderInfo.IsPatient == true) ? senderInfo.PatientId :
                        (receiverInfo != null && receiverInfo.IsPatient == true) ? receiverInfo.PatientId :
                        null,
                    Content = chat.Content,
                    CreatedDate = EnsureUtc(chat.CreatedDate),
                    SentAt = EnsureUtc(chat.CreatedDate),
                    Time = EnsureUtc(chat.CreatedDate).ToString("o"),
                    IsRead = chat.IsRead,
                    Type = "text"
                })
                .OrderBy(m => m.CreatedDate)
                .ToList();

            var messagesToMarkRead = await _db.SYS_Chats
                .Where(x =>
                    x.ReceiverId.ToString() == userId1 &&
                    x.SenderId.ToString() == userId2 &&
                    x.IsRead == false)
                .ToListAsync();

            if (messagesToMarkRead.Any())
            {
                foreach (var message in messagesToMarkRead)
                {
                    message.IsRead = true;
                }
                await _db.SaveChangesAsync();
            }

            foreach (var msg in chatList)
            {
                if (msg.ReceiverId == userId1 &&
                    messagesToMarkRead.Any(x => x.Id == msg.Id))
                {
                    msg.IsRead = true;
                }
            }

            return chatList;
        }

        public async Task<GetChatChannelsWrapperResponseDTO> GetChatChannelsAsync(
            GetChatChannelsRequestDTO request,
            CancellationToken ct = default)
        {
            var userId = request.UserId ?? 0;
            var roleId = request.RoleId ?? 0;
            var roleEnum = (UserRole)roleId;
            var result = new List<GetChatChannelsResponseDTO>();

            bool? canViewChannels = null;
            if (roleEnum == UserRole.ClinicAdmin && request.FacilityId.HasValue)
            {
                var facility = await _db.SYS_Facilities
                    .AsNoTracking()
                    .Where(f => f.FacilityId == request.FacilityId.Value)
                    .Select(f => f.CanViewChannels)
                    .FirstOrDefaultAsync(ct);
                canViewChannels = facility;

                if (canViewChannels == false)
                {
                    return new GetChatChannelsWrapperResponseDTO
                    {
                        Channels = new List<GetChatChannelsResponseDTO>(),
                        CanViewChannels = false
                    };
                }
            }
            else
            {

                canViewChannels = true;
            }

            var userChannels = await _db.SYS_ChatChannelParticipants
                .AsNoTracking()
                .Where(p => p.UserId == userId && p.IsActive == true)
                .Join(_db.SYS_ChatChannels.AsNoTracking(),
                    p => p.ChannelId,
                    c => c.ChannelId,
                    (p, c) => new { Channel = c, Participant = p })
                .Where(x => x.Channel.IsActive == true)
                .Select(x => x.Channel)
                .ToListAsync(ct);

            var filteredChannels = userChannels.Where(channel =>
            {
                if (channel.ChannelType == "Treatment")
                {
                    if (!(roleEnum == UserRole.Patient ||
                          roleEnum == UserRole.Provider ||
                          roleEnum == UserRole.GlobalAdmin ||
                          roleEnum == UserRole.ClinicAdmin))
                    {
                        return false;
                    }

                    if (roleEnum == UserRole.ClinicAdmin && channel.FacilityId != request.FacilityId)
                        return false;

                    return true;
                }

                if (channel.ChannelType == "Individual")
                {

                    return true;
                }

                return false;
            }).ToList();

            var treatmentChannels = filteredChannels
                .Where(c => c.ChannelType == "Treatment")
                .GroupBy(c => c.PatientId ?? -c.ChannelId)
                .Select(g => g.OrderBy(c => c.ChannelId).First())
                .ToList();

            var nonTreatmentChannels = filteredChannels
                .Where(c => c.ChannelType != "Treatment")
                .ToList();

            var visibleChannels = treatmentChannels.Concat(nonTreatmentChannels).ToList();

            var visibleChannelIds = visibleChannels.Select(c => c.ChannelId).Distinct().ToList();
            var channelMessages = visibleChannelIds.Count == 0
                ? new List<ChannelMessageLite>()
                : await _db.SYS_Chats
                    .AsNoTracking()
                    .Where(m =>
                        m.ChannelId.HasValue &&
                        visibleChannelIds.Contains(m.ChannelId.Value) &&
                        m.IsActive == true &&
                        m.MessageType != "IndividualInChannel" &&
                        (m.MessageType == "Channel" || m.MessageType == null))
                    .Select(m => new ChannelMessageLite
                    {
                        ChannelId = m.ChannelId!.Value,
                        Id = m.Id,
                        SenderId = m.SenderId,
                        Content = m.Content,
                        CreatedDate = m.CreatedDate
                    })
                    .ToListAsync(ct);

            var allMessageIds = channelMessages.Select(m => m.Id).ToList();
            var readMessageIds = allMessageIds.Count == 0
                ? new HashSet<long>()
                : (await _db.SYS_ChatReadReceipts
                    .AsNoTracking()
                    .Where(r => r.UserId == userId && r.IsActive == true && allMessageIds.Contains(r.ChatId))
                    .Select(r => r.ChatId)
                    .ToListAsync(ct))
                    .ToHashSet();

            var lastMessageByChannel = channelMessages
                .GroupBy(m => m.ChannelId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(x => x.CreatedDate).ThenByDescending(x => x.Id).First());

            var unreadByChannel = channelMessages
                .GroupBy(m => m.ChannelId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Count(x => x.SenderId != userId && !readMessageIds.Contains(x.Id)));

            foreach (var channel in visibleChannels)
            {
                lastMessageByChannel.TryGetValue(channel.ChannelId, out var lastMessage);
                unreadByChannel.TryGetValue(channel.ChannelId, out var unreadCount);

                result.Add(new GetChatChannelsResponseDTO
                {
                    ChannelId = channel.ChannelId,
                    ChannelName = channel.ChannelName,
                    ChannelType = channel.ChannelType,
                    TreatmentId = channel.TreatmentId,
                    PatientId = channel.PatientId,
                    LastMessage = lastMessage?.Content,
                    LastMessageDate = lastMessage != null ? EnsureUtc(lastMessage.CreatedDate) : (DateTime?)null,
                    LastMessageTime = lastMessage != null ? FormatChannelDate(lastMessage.CreatedDate) : null,
                    UnreadCount = unreadCount,
                    ChannelCreatedDate = channel.CreatedDate
                });
            }

            if (roleEnum == UserRole.GlobalAdmin)
            {
                var individualMessages = await _db.SYS_Chats
                    .AsNoTracking()
                    .Where(m => m.SenderId == userId &&
                               m.MessageType == "IndividualInChannel" &&
                               m.IsActive == true)
                    .GroupBy(m => m.IndividualReceiverId)
                    .Select(g => new
                    {
                        ReceiverId = g.Key,
                        LastMessage = g.OrderByDescending(x => x.CreatedDate)
                                      .Select(x => x.Content)
                                      .FirstOrDefault(),
                        CreatedDate = g.OrderByDescending(x => x.CreatedDate)
                                     .Select(x => x.CreatedDate)
                                     .FirstOrDefault(),
                        UnreadCount = 0
                    })
                    .ToListAsync(ct);

                var receiverUserIds = individualMessages
                    .Where(x => x.ReceiverId.HasValue)
                    .Select(x => x.ReceiverId!.Value)
                    .Distinct()
                    .ToList();

                var patientIdByUserId = new Dictionary<long, long>();
                if (receiverUserIds.Count > 0)
                {
                    var patientRows = await (
                        from ud in _db.SYS_UserDetails.AsNoTracking()
                        join p in _db.PT_Patients.AsNoTracking() on ud.LoginId equals p.LoginId
                        where receiverUserIds.Contains(ud.UserId)
                        select new { ud.UserId, p.PatientId }
                    ).ToListAsync(ct);

                    patientIdByUserId = patientRows.ToDictionary(x => x.UserId, x => x.PatientId);
                }

                var receiverNames = await GetUserNamesMapAsync(receiverUserIds, ct);

                foreach (var msg in individualMessages)
                {
                    if (msg.ReceiverId.HasValue)
                    {
                        var receiverName = receiverNames.TryGetValue(msg.ReceiverId.Value, out var name)
                            ? name
                            : "Unknown";
                        patientIdByUserId.TryGetValue(msg.ReceiverId.Value, out var patientId);

                        result.Add(new GetChatChannelsResponseDTO
                        {
                            ChannelId = null,
                            ChannelName = $"Individual - {receiverName}",
                            ChannelType = "IndividualMessage",
                            IsIndividualMessage = true,
                            IndividualReceiverId = msg.ReceiverId,
                            IndividualReceiverName = receiverName,
                            PatientId = patientId,
                            LastMessage = msg.LastMessage,
                            LastMessageDate = EnsureUtc(msg.CreatedDate),
                            LastMessageTime = msg.CreatedDate != default(DateTime) ? FormatChannelDate(msg.CreatedDate) : null,
                            UnreadCount = 0,
                            ChannelCreatedDate = null
                        });
                    }
                }
            }

            if (roleEnum == UserRole.ClinicAdmin)
            {

                var individualMessagesForClinicAdmin = await _db.SYS_Chats
                    .AsNoTracking()
                    .Where(m => m.IndividualReceiverId == userId &&
                               m.MessageType == "IndividualInChannel" &&
                               m.IsActive == true)
                    .GroupBy(m => m.SenderId)
                    .Select(g => new
                    {
                        SenderId = g.Key,
                        LastMessage = g.OrderByDescending(x => x.CreatedDate)
                                      .Select(x => x.Content)
                                      .FirstOrDefault(),
                        CreatedDate = g.OrderByDescending(x => x.CreatedDate)
                                     .Select(x => x.CreatedDate)
                                     .FirstOrDefault(),
                        UnreadCount = g.Count(x => x.IsRead == false)
                    })
                    .ToListAsync(ct);

                var senderIds = individualMessagesForClinicAdmin
                    .Where(x => x.SenderId.HasValue)
                    .Select(x => x.SenderId!.Value)
                    .Distinct()
                    .ToList();
                var senderNames = await GetUserNamesMapAsync(senderIds, ct);

                foreach (var msg in individualMessagesForClinicAdmin)
                {
                    if (msg.SenderId.HasValue)
                    {
                        var senderName = senderNames.TryGetValue(msg.SenderId.Value, out var name)
                            ? name
                            : "Unknown";
                        result.Add(new GetChatChannelsResponseDTO
                        {
                            ChannelId = null,
                            ChannelName = $"Message from {senderName}",
                            ChannelType = "IndividualMessage",
                            IsIndividualMessage = true,
                            IndividualReceiverId = userId,
                            IndividualReceiverName = senderName,
                            LastMessage = msg.LastMessage,
                            LastMessageDate = EnsureUtc(msg.CreatedDate),
                            LastMessageTime = msg.CreatedDate != default(DateTime) ? FormatChannelDate(msg.CreatedDate) : null,
                            UnreadCount = msg.UnreadCount,
                            ChannelCreatedDate = null
                        });
                    }
                }
            }

            var treatmentPatientIds = result
                .Where(x => x.ChannelType == "Treatment" && x.PatientId.HasValue)
                .Select(x => x.PatientId!.Value)
                .Distinct()
                .ToList();

            if (treatmentPatientIds.Count > 0)
            {

                var patientImageRows = await (
                    from p in _db.PT_Patients.AsNoTracking()
                    join ud in _db.SYS_UserDetails.AsNoTracking() on p.LoginId equals ud.LoginId
                    where treatmentPatientIds.Contains(p.PatientId)
                    select new
                    {
                        p.PatientId,
                        ProfilePic = !string.IsNullOrWhiteSpace(ud.PatientPicture)
                            ? ud.PatientPicture
                            : ud.ProfileUrl
                    }
                ).ToListAsync(ct);

                var patientProfileById = patientImageRows
                    .GroupBy(x => x.PatientId)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(x => x.ProfilePic).FirstOrDefault(pic => !string.IsNullOrWhiteSpace(pic))
                    );

                foreach (var channel in result)
                {
                    if (channel.ChannelType == "Treatment" &&
                        channel.PatientId.HasValue &&
                        patientProfileById.TryGetValue(channel.PatientId.Value, out var pic))
                    {
                        channel.PatientProfilePic = pic;
                    }
                }
            }

            var orderedChannels = result.OrderByDescending(x => x.ChannelCreatedDate ?? x.LastMessageDate ?? DateTime.MinValue).ToList();

            return new GetChatChannelsWrapperResponseDTO
            {
                Channels = orderedChannels,
                CanViewChannels = canViewChannels
            };
        }

        private async Task<Dictionary<long, string>> GetUserNamesMapAsync(List<long> userIds, CancellationToken ct)
        {
            if (!userIds.Any())
                return new Dictionary<long, string>();

            var users = await (
                from u in _db.SYS_UserDetails.AsNoTracking()
                join login in _db.SYS_Logins.AsNoTracking() on u.LoginId equals login.LoginId into lj
                from login in lj.DefaultIfEmpty()
                join rt0 in _db.LK_RoleTitles.AsNoTracking() on u.RoleTitleId equals rt0.RoleTitleId into rtj
                from rt in rtj.DefaultIfEmpty()
                where userIds.Contains(u.UserId)
                select new
                {
                    u.UserId,
                    u.FirstName,
                    u.LastName,
                    LoginRoleId = login != null ? (int?)login.RoleId : null,
                    LoginId = login != null ? (long?)login.LoginId : null,
                    RoleTitleName = rt != null && rt.IsActive == true ? rt.RoleTitleName : null
                }).ToListAsync(ct);

            var patientLoginIds = users
                .Where(u => u.LoginRoleId == (int)UserRole.Patient && u.LoginId.HasValue)
                .Select(u => u.LoginId!.Value)
                .Distinct()
                .ToList();

            var patientNamesByLoginId = patientLoginIds.Count == 0
                ? new Dictionary<long, string>()
                : await _db.PT_Patients
                    .AsNoTracking()
                    .Where(p => p.LoginId.HasValue && patientLoginIds.Contains(p.LoginId.Value))
                    .Select(p => new
                    {
                        LoginId = p.LoginId!.Value,
                        Name = ((p.FirstName ?? "") + " " + (p.LastName ?? "")).Trim()
                    })
                    .ToDictionaryAsync(x => x.LoginId, x => x.Name, ct);

            return users.ToDictionary(
                x => x.UserId,
                x =>
                {
                    if (x.LoginRoleId == (int)UserRole.Patient && x.LoginId.HasValue &&
                        patientNamesByLoginId.TryGetValue(x.LoginId.Value, out var patientName) &&
                        !string.IsNullOrWhiteSpace(patientName))
                    {
                        return patientName;
                    }

                    return BuildDisplayName(x.FirstName, x.LastName, x.RoleTitleName);
                });
        }

        private static string FormatChannelDate(DateTime msgDate)
        {
            if (msgDate.Date == DateTime.Today)
                return msgDate.ToString("hh:mm tt");
            if (msgDate.Date == DateTime.Today.AddDays(-1))
                return "Yesterday";
            return msgDate.ToString("yyyy-MM-dd");
        }

        public async Task<List<ChatMessageResponseDTO>> GetChannelMessagesAsync(
            long channelId,
            long userId,
            int pageNumber = 1,
            int pageSize = 50,
            long? individualReceiverId = null,
            CancellationToken ct = default)
        {
            int skip = (pageNumber - 1) * pageSize;

            IQueryable<SYS_Chat> messagesQuery;

            if (individualReceiverId.HasValue)
            {

                messagesQuery = _db.SYS_Chats
                    .AsNoTracking()
                    .Where(m => m.ChannelId == channelId &&
                               m.IsActive == true &&
                               m.MessageType == "IndividualInChannel" &&
                               ((m.SenderId == userId && m.IndividualReceiverId == individualReceiverId.Value) ||
                                (m.SenderId == individualReceiverId.Value && m.IndividualReceiverId == userId)));
            }
            else
            {

                messagesQuery = _db.SYS_Chats
                    .AsNoTracking()
                    .Where(m => m.ChannelId == channelId &&
                               m.IsActive == true &&
                               (m.MessageType == "Channel" || m.MessageType == null));
            }

            var messages = await messagesQuery
                .OrderByDescending(m => m.CreatedDate)
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync(ct);

            if (!messages.Any())
                return new List<ChatMessageResponseDTO>();

            var senderIds = messages.Select(m => m.SenderId).Distinct().ToList();

            var userInfo = await GetUsersInfoAsync(senderIds);

            var viewerRoleId = await (from ud in _db.SYS_UserDetails.AsNoTracking()
                                      join login in _db.SYS_Logins.AsNoTracking() on ud.LoginId equals login.LoginId
                                      where ud.UserId == userId
                                      select (int?)login.RoleId)
                                     .FirstOrDefaultAsync(ct);

            var senderRoleByUserId = await (from ud in _db.SYS_UserDetails.AsNoTracking()
                                            join login in _db.SYS_Logins.AsNoTracking() on ud.LoginId equals login.LoginId
                                            where senderIds.Contains(ud.UserId)
                                            select new { ud.UserId, login.RoleId })
                                           .ToDictionaryAsync(x => x.UserId, x => x.RoleId, ct);

            var channel = await _db.SYS_ChatChannels
                .AsNoTracking()
                .Where(c => c.ChannelId == channelId)
                .FirstOrDefaultAsync(ct);

            var individualReceiverIds = messages
                .Where(m => m.IndividualReceiverId.HasValue)
                .Select(m => m.IndividualReceiverId.Value)
                .Distinct()
                .ToList();

            var receiverNames = new Dictionary<long, string>();
            foreach (var receiverId in individualReceiverIds)
            {
                receiverNames[receiverId] = await GetUserNameAsync(receiverId);
            }

            var messageIds = messages.Select(m => m.Id).ToList();
            var readReceipts = !individualReceiverId.HasValue
                ? await _db.SYS_ChatReadReceipts
                    .AsNoTracking()
                    .Where(r => r.UserId == userId &&
                               r.IsActive == true &&
                               messageIds.Contains(r.ChatId))
                    .Select(r => r.ChatId)
                    .ToListAsync(ct)
                : new List<long>();

            var result = messages.Select(m =>
            {
                var senderInfo = userInfo.FirstOrDefault(u => u.UserId == m.SenderId);
            var senderName = !m.SenderId.HasValue
                    ? "TelehealthUS"
                    : senderInfo.UserId != 0
                    ? senderInfo.DisplayName
                    : "Unknown";

                var createdUtc = EnsureUtc(m.CreatedDate);

                if (m.SenderId.HasValue &&
                    senderRoleByUserId.TryGetValue(m.SenderId.Value, out var senderRoleId) &&
                    senderRoleId == (int)UserRole.Provider &&
                    (viewerRoleId == (int)UserRole.ClinicAdmin || viewerRoleId == (int)UserRole.Patient))
                {
                    senderName = "Provider";
                }

                var msgDate = m.CreatedDate;
                string timeStr = "";
                if (msgDate.Date == DateTime.Today)
                {
                    timeStr = "Today";
                }
                else if (msgDate.Date == DateTime.Today.AddDays(-1))
                {
                    timeStr = "Yesterday";
                }
                else
                {
                    timeStr = msgDate.ToString("yyyy-MM-dd");
                }

                bool isRead;
                if (individualReceiverId.HasValue)
                {

                    isRead = m.IsRead ?? false;
                }
                else
                {

                    isRead = m.SenderId == userId || readReceipts.Contains(m.Id);
                }

                var responseMessage = new ChatMessageResponseDTO
                {
                    Id = m.Id,
                    SenderId = m.SenderId.ToString(),
                    SenderName = senderName,
                    PatientId = channel?.PatientId,
                    ChannelId = channelId,
                    ChannelName = channel?.ChannelName,
                    Content = m.Content,
                    CreatedDate = createdUtc,
                    SentAt = createdUtc,
                    Time = createdUtc.ToString("o"),
                    IsRead = isRead,
                    Type = "text",
                    MessageType = m.MessageType ?? "Channel"
                };

                if (m.IndividualReceiverId.HasValue && receiverNames.ContainsKey(m.IndividualReceiverId.Value))
                {
                    responseMessage.IndividualReceiverId = m.IndividualReceiverId.Value;
                    responseMessage.IndividualReceiverName = receiverNames[m.IndividualReceiverId.Value];
                }

                return responseMessage;
            }).OrderBy(m => m.CreatedDate).ToList();

            IQueryable<SYS_Chat> messagesToMarkReadQuery;
            if (individualReceiverId.HasValue)
            {

                messagesToMarkReadQuery = _db.SYS_Chats
                    .Where(m => m.ChannelId == channelId &&
                               m.SenderId != userId &&
                               m.IndividualReceiverId == userId &&
                               m.IsRead == false &&
                               m.IsActive == true &&
                               m.MessageType == "IndividualInChannel");
            }
            else
            {

                messagesToMarkReadQuery = _db.SYS_Chats
                    .Where(m => m.ChannelId == channelId &&
                               m.SenderId != userId &&
                               m.IsActive == true &&
                               (m.MessageType == "Channel" || m.MessageType == null));
            }

            var messagesToMarkRead = await messagesToMarkReadQuery.ToListAsync(ct);

            if (messagesToMarkRead.Any())
            {

                var existingReceipts = await _db.SYS_ChatReadReceipts
                    .Where(r => r.UserId == userId &&
                               r.IsActive == true &&
                               messagesToMarkRead.Select(m => m.Id).Contains(r.ChatId))
                    .Select(r => r.ChatId)
                    .ToListAsync(ct);

                var receiptsToCreate = messagesToMarkRead
                    .Where(m => !existingReceipts.Contains(m.Id))
                    .Select(m => new SYS_ChatReadReceipt
                    {
                        ChatId = m.Id,
                        UserId = userId,
                        ReadDate = DateTime.UtcNow,
                        IsActive = true
                    })
                    .ToList();

                if (receiptsToCreate.Any())
                {
                    _db.SYS_ChatReadReceipts.AddRange(receiptsToCreate);
                }

                if (individualReceiverId.HasValue)
                {
                    foreach (var msg in messagesToMarkRead)
                    {
                        msg.IsRead = true;
                    }
                }

                await _db.SaveChangesAsync(ct);
            }

            return result;
        }

        public async Task<long> SaveChannelMessageAsync(
            long channelId,
            long senderId,
            string content,
            long? individualReceiverId = null,
            CancellationToken ct = default)
        {
            var message = new SYS_Chat
            {
                SenderId = senderId,
                ChannelId = channelId,
                IndividualReceiverId = individualReceiverId,
                Content = content,
                CreatedDate = DateTime.UtcNow,
                IsActive = true,
                CreatedBy = senderId,
                IsRead = false,
                MessageType = individualReceiverId.HasValue ? "IndividualInChannel" : "Channel"
            };

            _db.SYS_Chats.Add(message);
            await _db.SaveChangesAsync(ct);
            return message.Id;
        }

        private async Task<string> GetUserNameAsync(long userId)
        {
            var user = await (from u in _db.SYS_UserDetails.AsNoTracking()
                              join login in _db.SYS_Logins.AsNoTracking() on u.LoginId equals login.LoginId into lj
                              from login in lj.DefaultIfEmpty()
                              join rt0 in _db.LK_RoleTitles.AsNoTracking() on u.RoleTitleId equals rt0.RoleTitleId into rtj
                              from rt in rtj.DefaultIfEmpty()
                              where u.UserId == userId
                              select new
                              {
                                  u.FirstName,
                                  u.LastName,
                                  LoginRoleId = login != null ? (int?)login.RoleId : null,
                                  LoginId = login != null ? (long?)login.LoginId : null,
                                  RoleTitleName = rt != null && rt.IsActive == true ? rt.RoleTitleName : null
                              }).FirstOrDefaultAsync();

            if (user != null)
            {
                if (user.LoginRoleId == (int)UserRole.Patient && user.LoginId.HasValue)
                {
                    var patient = await _db.PT_Patients
                        .AsNoTracking()
                        .Where(p => p.LoginId == user.LoginId.Value)
                        .Select(p => $"{p.FirstName} {p.LastName}")
                        .FirstOrDefaultAsync();
                    return patient ?? "Unknown";
                }
                return BuildDisplayName(user.FirstName, user.LastName, user.RoleTitleName);
            }
            return "Unknown";
        }

        private async Task<List<(long UserId, string DisplayName)>> GetUsersInfoAsync(List<long?> userIds)
        {
            var validUserIds = userIds.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();

            if (!validUserIds.Any())
                return new List<(long, string)>();

            var regularUsers = await (
                from ud in _db.SYS_UserDetails.AsNoTracking()
                where validUserIds.Contains(ud.UserId)
                join login in _db.SYS_Logins.AsNoTracking() on ud.LoginId equals login.LoginId
                join rt0 in _db.LK_RoleTitles.AsNoTracking() on ud.RoleTitleId equals rt0.RoleTitleId into rtj
                from rt in rtj.DefaultIfEmpty()
                where login.RoleId != (int)UserRole.Patient
                select new
                {
                    ud.UserId,
                    DisplayName = BuildDisplayName(ud.FirstName, ud.LastName, rt != null && rt.IsActive == true ? rt.RoleTitleName : null)
                }).ToListAsync();

            var patientUserIds = await _db.SYS_UserDetails
                .AsNoTracking()
                .Where(u => validUserIds.Contains(u.UserId))
                .Join(_db.SYS_Logins.AsNoTracking(),
                    ud => ud.LoginId,
                    login => login.LoginId,
                    (ud, login) => new { ud.UserId, ud.LoginId, login.RoleId })
                .Where(x => x.RoleId == (int)UserRole.Patient)
                .Select(u => u.LoginId)
                .ToListAsync();

            var patients = new List<(long UserId, string DisplayName)>();
            if (patientUserIds.Any())
            {
                var patientData = await _db.PT_Patients
                    .AsNoTracking()
                    .Where(p => patientUserIds.Contains(p.LoginId))
                    .Join(_db.SYS_UserDetails.AsNoTracking(),
                        p => p.LoginId,
                        ud => ud.LoginId,
                        (p, ud) => new { ud.UserId, p.FirstName, p.LastName })
                    .Select(p => new { p.UserId, p.FirstName, p.LastName })
                    .ToListAsync();

                patients = patientData.Select(p => (p.UserId, BuildDisplayName(p.FirstName, p.LastName, null))).ToList();
            }

            var result = regularUsers.Select(u => (u.UserId, u.DisplayName))
                .Concat(patients)
                .ToList();

            return result;
        }

        private static string BuildDisplayName(string? firstName, string? lastName, string? roleTitleName)
        {
            var baseName = $"{firstName ?? ""} {lastName ?? ""}".Trim();
            if (string.IsNullOrWhiteSpace(baseName))
                baseName = "Unknown";
            if (!string.IsNullOrWhiteSpace(roleTitleName))
                return $"{baseName} ({roleTitleName.Trim()})";
            return baseName;
        }
    }
}
