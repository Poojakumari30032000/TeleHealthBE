using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Vitality.Models.DTOs.Chats;
using Vitality.Models.EntityClasses;
using Vitality.Models.Enums;
using Vitality.Models.Repos.Services;
using Vitality.Services.Notifications;

namespace Vitality
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly ChatsRepo _chatsRepo;
        private readonly ChatValidationService _validationService;
        private readonly INotificationService _notificationService;
        private readonly MainContext _db;
        private static readonly ConcurrentDictionary<string, OnlineUserDTO> _onlineUsers
            = new ConcurrentDictionary<string, OnlineUserDTO>();

        public ChatHub(ChatsRepo chatsRepo, ChatValidationService validationService, INotificationService notificationService, MainContext db)
        {
            _chatsRepo = chatsRepo;
            _validationService = validationService;
            _notificationService = notificationService;
            _db = db;
        }

        public override async Task OnConnectedAsync()
        {
            try
            {
                string userId = Context.UserIdentifier;
                if (string.IsNullOrEmpty(userId))
                {
                    await Clients.Caller.SendAsync("ConnectionError", "User not authenticated");
                    Context.Abort();
                    return;
                }

                var chatUsers = await _chatsRepo.GetOnlineUserInfoAsync(userId);

                var currentUserInfo = chatUsers.FirstOrDefault(u => u.UserId == userId)
                    ?? new OnlineUserDTO
                    {
                        UserId = userId,
                        UserName = "Unknown",
                        ConnectionId = Context.ConnectionId,
                        IsOnline = true
                    };

                currentUserInfo.ConnectionId = Context.ConnectionId;
                currentUserInfo.IsOnline = true;

                _onlineUsers.AddOrUpdate(userId, currentUserInfo, (key, oldValue) => currentUserInfo);

                foreach (var user in chatUsers.Where(u => u.UserId != userId))
                {
                    if (_onlineUsers.TryGetValue(user.UserId, out var existingUser))
                    {
                        user.ConnectionId = existingUser.ConnectionId;
                        user.IsOnline = existingUser.IsOnline ?? false;
                    }
                    else
                    {
                        user.ConnectionId = null;
                        user.IsOnline = false;
                    }
                    _onlineUsers.AddOrUpdate(user.UserId, user, (key, oldValue) => user);
                }

                await Clients.All.SendAsync("UserOnlineNotification", userId, true);

                foreach (var user in chatUsers)
                {
                    if (_onlineUsers.TryGetValue(user.UserId, out var cachedUser))
                    {
                        user.ConnectionId = cachedUser.ConnectionId;
                        user.IsOnline = cachedUser.IsOnline;
                    }
                }

                var onlineUsersList = chatUsers.ToList();
                await Clients.Caller.SendAsync("ReceiveOnlineUsers", onlineUsersList);

                await base.OnConnectedAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in OnConnectedAsync: {ex.Message}");
                await Clients.Caller.SendAsync("ConnectionError", ex.Message);
                throw;
            }
        }

        public async Task GetOnlineUsersList(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new HubException("User ID is invalid.");
            }

            try
            {
                var chatUsers = await _chatsRepo.GetOnlineUserInfoAsync(userId);

                foreach (var user in chatUsers)
                {
                    if (_onlineUsers.TryGetValue(user.UserId, out var cachedUser))
                    {
                        user.ConnectionId = cachedUser.ConnectionId;
                        user.IsOnline = cachedUser.IsOnline;
                    }
                }

                await Clients.User(userId).SendAsync("ReceiveOnlineUsers", chatUsers.ToList());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetOnlineUsersList: {ex.Message}");
                throw new HubException($"Failed to get online users: {ex.Message}");
            }
        }

        public async Task SendMessage(ChatMessageRequestDTO request)
        {
            string senderId = Context.UserIdentifier;
            if (string.IsNullOrEmpty(senderId))
                throw new HubException("Sender is not authenticated.");

            if (request?.ReceiverId == null)
                throw new HubException("Invalid receiver ID");

            try
            {

                var validationResult = await _validationService.ValidateMessagePermissionAsync(
                    Convert.ToInt64(senderId),
                    Convert.ToInt64(request.ReceiverId));

                if (!validationResult.IsValid)
                {
                    await Clients.Caller.SendAsync("MessageError", validationResult.ErrorMessage ?? "You are not allowed to send messages to this user.");
                    throw new HubException(validationResult.ErrorMessage ?? "Message permission denied.");
                }

                OnlineUserDTO senderInfo;
                if (!_onlineUsers.TryGetValue(senderId, out senderInfo))
                {
                    var list = await _chatsRepo.GetOnlineUserInfoAsync(senderId);
                    senderInfo = list.FirstOrDefault(u => u.UserId == senderId);

                    if (senderInfo == null && long.TryParse(senderId, out long parsedSenderId))
                    {
                        var retrievedSenderName = await GetUserNameAsync(parsedSenderId);
                        senderInfo = new OnlineUserDTO
                        {
                            UserId = senderId,
                            UserName = retrievedSenderName,
                            FacilityId = null,
                            FacilityName = null
                        };
                    }

                    if (senderInfo == null)
                    {
                        senderInfo = new OnlineUserDTO
                        {
                            UserId = senderId,
                            UserName = "Unknown",
                            FacilityId = null,
                            FacilityName = null
                        };
                    }
                }

                if ((string.IsNullOrWhiteSpace(senderInfo.UserName) ||
                    string.Equals(senderInfo.UserName, "Unknown", StringComparison.OrdinalIgnoreCase)) &&
                    long.TryParse(senderId, out long senderIdParsedForName))
                {
                    var resolvedSenderName = await GetUserNameAsync(senderIdParsedForName);
                    if (!string.IsNullOrWhiteSpace(resolvedSenderName) &&
                        !string.Equals(resolvedSenderName, "Unknown", StringComparison.OrdinalIgnoreCase))
                    {
                        senderInfo.UserName = resolvedSenderName;
                        _onlineUsers.AddOrUpdate(senderId, senderInfo, (key, oldValue) => senderInfo);
                    }
                }

                long? deliveredPatientId = null;
                var senderIdLong = Convert.ToInt64(senderId);
                var receiverIdLong = Convert.ToInt64(request.ReceiverId);
                var patientRows = await (
                    from ud in _db.SYS_UserDetails.AsNoTracking()
                    join p in _db.PT_Patients.AsNoTracking() on ud.LoginId equals p.LoginId
                    where ud.UserId == senderIdLong || ud.UserId == receiverIdLong
                    select new { ud.UserId, p.PatientId }
                ).ToListAsync();

                var patientByUserId = patientRows.ToDictionary(x => x.UserId, x => (long?)x.PatientId);
                if (patientByUserId.TryGetValue(senderIdLong, out var senderPatientId))
                    deliveredPatientId = senderPatientId;
                else if (patientByUserId.TryGetValue(receiverIdLong, out var receiverPatientId))
                    deliveredPatientId = receiverPatientId;

                var delivered = new ChatDeliveredMessageDTO
                {
                    SenderId = senderId,
                    ReceiverId = request.ReceiverId,
                    Content = request.Content ?? string.Empty,
                    SentAt = DateTime.UtcNow,
                    FacilityId = senderInfo.FacilityId,
                    FacilityName = senderInfo.FacilityName,
                    PatientId = deliveredPatientId
                };

                var newMessageId = await _chatsRepo.SaveMessageAsync(request, Convert.ToInt64(senderId));
                delivered.Id = newMessageId;

                await Clients.User(senderId).SendAsync("ReceiveMessage", delivered);
                await Clients.User(request.ReceiverId).SendAsync("ReceiveMessage", delivered);

                try
                {

                    bool isReceiverOnline = _onlineUsers.TryGetValue(request.ReceiverId, out var onlineReceiver)
                        && (onlineReceiver?.IsOnline ?? false);

                    if (!isReceiverOnline)
                    {

                        string? receiverEmail = null;
                        if (long.TryParse(request.ReceiverId, out long receiverIdLongParsed))
                        {

                            var user = await _db.SYS_UserDetails
                                .AsNoTracking()
                                .Where(u => u.UserId == receiverIdLongParsed)
                                .Select(u => new { u.Email })
                                .FirstOrDefaultAsync();

                            if (user != null && !string.IsNullOrWhiteSpace(user.Email))
                            {
                                receiverEmail = user.Email;
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(receiverEmail))
                        {
                            var messagePreview = request.Content?.Length > 100
                                ? request.Content.Substring(0, 100) + "..."
                                : request.Content ?? string.Empty;

                            var notificationSenderName = await GetSenderDisplayNameForRecipientAsync(
                                senderUserId: Convert.ToInt64(senderId),
                                recipientUserId: receiverIdLongParsed,
                                fallbackSenderName: senderInfo.UserName);

                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    await _notificationService.SendMessageNotificationAsync(
                                        recipientEmail: receiverEmail,
                                        senderName: notificationSenderName,
                                        messagePreview: messagePreview,
                                        ct: default
                                    );
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Error sending message notification: {ex.Message}");
                                }
                            });
                        }
                    }
                }
                catch (Exception ex)
                {

                    Console.WriteLine($"Error preparing message notification: {ex.Message}");
                }

                await Clients.Caller.SendAsync("MessageSent", "Message sent successfully");

                await GetOnlineUsersList(senderId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SendMessage: {ex.Message}");
                throw new HubException($"Failed to send message: {ex.Message}");
            }
        }

        public async Task DeleteMessage(long messageId)
        {
            string userId = Context.UserIdentifier;
            if (string.IsNullOrEmpty(userId))
                throw new HubException("User is not authenticated.");

            if (messageId <= 0)
                throw new HubException("Invalid message id.");

            long currentUserId = Convert.ToInt64(userId);

            var chat = await _db.SYS_Chats
                .FirstOrDefaultAsync(c => c.Id == messageId && c.IsActive == true);

            if (chat == null)
                throw new HubException("Message not found.");

            if (!chat.SenderId.HasValue || chat.SenderId.Value != currentUserId)
                throw new HubException("You are not allowed to delete this message.");

            chat.Content = "This message was deleted";
            await _db.SaveChangesAsync();

            var payload = new
            {
                id = chat.Id,
                senderId = chat.SenderId,
                receiverId = chat.ReceiverId,
                content = chat.Content,
                channelId = chat.ChannelId,
                messageType = chat.MessageType,
                individualReceiverId = chat.IndividualReceiverId
            };

            if (!chat.ChannelId.HasValue)
            {
                if (chat.SenderId.HasValue)
                    await Clients.User(chat.SenderId.Value.ToString()).SendAsync("MessageDeleted", payload);

                if (chat.ReceiverId.HasValue)
                    await Clients.User(chat.ReceiverId.Value.ToString()).SendAsync("MessageDeleted", payload);

                return;
            }

            var channelId = chat.ChannelId.Value;

            if (string.Equals(chat.MessageType, "IndividualInChannel", StringComparison.OrdinalIgnoreCase) &&
                chat.IndividualReceiverId.HasValue)
            {
                if (chat.SenderId.HasValue)
                    await Clients.User(chat.SenderId.Value.ToString()).SendAsync("MessageDeleted", payload);

                await Clients.User(chat.IndividualReceiverId.Value.ToString()).SendAsync("MessageDeleted", payload);
                return;
            }

            var participantIds = await _db.SYS_ChatChannelParticipants
                .AsNoTracking()
                .Where(p => p.ChannelId == channelId && p.IsActive == true)
                .Select(p => p.UserId)
                .ToListAsync();

            foreach (var participantId in participantIds)
            {
                if (!participantId.HasValue) continue;
                await Clients.User(participantId.Value.ToString()).SendAsync("MessageDeleted", payload);
            }
        }

        public async Task LoadMessages(string recipientID, int pageNumber = 1, int pageSize = 10)
        {
            string userId = Context.UserIdentifier;
            if (string.IsNullOrEmpty(userId))
            {
                await Clients.Caller.SendAsync("MessageError", "User is not authenticated.");
                throw new HubException("User is not authenticated.");
            }

            if (string.IsNullOrEmpty(recipientID))
            {
                await Clients.Caller.SendAsync("MessageError", "Recipient ID is invalid.");
                throw new HubException("Recipient ID is invalid.");
            }

            try
            {
                Console.WriteLine($"ChatHub: Loading messages for user {userId} with recipient {recipientID}, page {pageNumber}");
                var messages = await _chatsRepo.GetMessagesAsync(userId, recipientID, pageNumber, pageSize);
                Console.WriteLine($"ChatHub: Loaded {messages.Count} messages");

                if (messages.Count == 0 && pageNumber == 1)
                {
                    Console.WriteLine($"ChatHub: No messages found for conversation between {userId} and {recipientID}");
                }

                await Clients.Caller.SendAsync("ReceiveMessageList", messages);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in LoadMessages: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                await Clients.Caller.SendAsync("MessageError", $"Failed to load messages: {ex.Message}");
                throw new HubException($"Failed to load messages: {ex.Message}");
            }
        }

        public async Task NotifyTyping(string recipientUserId)
        {
            string senderId = Context.UserIdentifier;
            if (string.IsNullOrEmpty(senderId) || string.IsNullOrEmpty(recipientUserId))
                return;

            try
            {
                var recipientConnectionId = _onlineUsers.Values
                    .FirstOrDefault(x => x.UserId == recipientUserId)?.ConnectionId;

                if (!string.IsNullOrEmpty(recipientConnectionId))
                {
                    await Clients.Client(recipientConnectionId)
                        .SendAsync("NotifyTypingToUser", senderId);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in NotifyTyping: {ex.Message}");
            }
        }

        public async Task NotifyNotTyping(string recipientUserId)
        {
            string senderId = Context.UserIdentifier;
            if (string.IsNullOrEmpty(senderId) || string.IsNullOrEmpty(recipientUserId))
                return;

            try
            {
                var recipientConnectionId = _onlineUsers.Values
                    .FirstOrDefault(x => x.UserId == recipientUserId)?.ConnectionId;

                if (!string.IsNullOrEmpty(recipientConnectionId))
                {
                    await Clients.Client(recipientConnectionId)
                        .SendAsync("NotifyNotTypingToUser", senderId);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in NotifyNotTyping: {ex.Message}");
            }
        }

        public async Task SendChannelMessage(ChatMessageRequestDTO request)
        {
            string senderId = Context.UserIdentifier;
            if (string.IsNullOrEmpty(senderId))
                throw new HubException("Sender is not authenticated.");

            if (!request.ChannelId.HasValue)
                throw new HubException("Invalid channel ID");

            try
            {
                long channelId = request.ChannelId.Value;
                long senderIdLong = Convert.ToInt64(senderId);
                long? individualReceiverId = request.IndividualReceiverId;
                var channelPatientId = await _db.SYS_ChatChannels
                    .AsNoTracking()
                    .Where(c => c.ChannelId == channelId)
                    .Select(c => c.PatientId)
                    .FirstOrDefaultAsync();

                var validationResult = await _validationService.ValidateChannelMessagePermissionAsync(
                    senderIdLong,
                    channelId,
                    individualReceiverId);

                if (!validationResult.IsValid)
                {
                    await Clients.Caller.SendAsync("MessageError", validationResult.ErrorMessage ?? "You are not allowed to send messages to this channel.");
                    throw new HubException(validationResult.ErrorMessage ?? "Channel message permission denied.");
                }

                OnlineUserDTO senderInfo;
                if (!_onlineUsers.TryGetValue(senderId, out senderInfo))
                {
                    var list = await _chatsRepo.GetOnlineUserInfoAsync(senderId);
                    senderInfo = list.FirstOrDefault(u => u.UserId == senderId);

                    if (senderInfo == null)
                    {
                        var retrievedSenderName = await GetUserNameAsync(senderIdLong);
                        senderInfo = new OnlineUserDTO
                        {
                            UserId = senderId,
                            UserName = retrievedSenderName,
                            FacilityId = null,
                            FacilityName = null
                        };
                    }

                    if (senderInfo == null)
                    {
                        senderInfo = new OnlineUserDTO
                        {
                            UserId = senderId,
                            UserName = "Unknown",
                            FacilityId = null,
                            FacilityName = null
                        };
                    }
                }

                if ((string.IsNullOrWhiteSpace(senderInfo.UserName) ||
                    string.Equals(senderInfo.UserName, "Unknown", StringComparison.OrdinalIgnoreCase)) &&
                    long.TryParse(senderId, out long senderIdParsedForName))
                {
                    var resolvedSenderName = await GetUserNameAsync(senderIdParsedForName);
                    if (!string.IsNullOrWhiteSpace(resolvedSenderName) &&
                        !string.Equals(resolvedSenderName, "Unknown", StringComparison.OrdinalIgnoreCase))
                    {
                        senderInfo.UserName = resolvedSenderName;
                        _onlineUsers.AddOrUpdate(senderId, senderInfo, (key, oldValue) => senderInfo);
                    }
                }

                var newMessageId = await _chatsRepo.SaveChannelMessageAsync(
                    channelId,
                    senderIdLong,
                    request.Content ?? string.Empty,
                    individualReceiverId);

                var participants = await _db.SYS_ChatChannelParticipants
                    .AsNoTracking()
                    .Where(p => p.ChannelId == channelId && p.IsActive == true)
                    .Select(p => p.UserId)
                    .ToListAsync();

                if (individualReceiverId.HasValue)
                {

                    var senderUserIdLong = senderIdLong;
                    var receiverUserIdLong = individualReceiverId.Value;

                    var senderViewSenderName = await GetSenderDisplayNameForRecipientAsync(
                        senderUserIdLong,
                        senderUserIdLong,
                        senderInfo.UserName);
                    var receiverViewSenderName = await GetSenderDisplayNameForRecipientAsync(
                        senderUserIdLong,
                        receiverUserIdLong,
                        senderInfo.UserName);

                    var deliveredToSender = new ChatDeliveredMessageDTO
                    {
                        Id = newMessageId,
                        SenderId = senderId,
                        SenderName = senderViewSenderName,
                        ReceiverId = channelId.ToString(),
                        Content = request.Content ?? string.Empty,
                        SentAt = DateTime.UtcNow,
                        FacilityId = senderInfo.FacilityId,
                        FacilityName = senderInfo.FacilityName,
                        PatientId = channelPatientId,
                        ChannelId = channelId,
                        MessageType = "IndividualInChannel",
                        IndividualReceiverId = individualReceiverId
                    };

                    var deliveredToReceiver = new ChatDeliveredMessageDTO
                    {
                        Id = newMessageId,
                        SenderId = senderId,
                        SenderName = receiverViewSenderName,
                        ReceiverId = channelId.ToString(),
                        Content = request.Content ?? string.Empty,
                        SentAt = DateTime.UtcNow,
                        FacilityId = senderInfo.FacilityId,
                        FacilityName = senderInfo.FacilityName,
                        PatientId = channelPatientId,
                        ChannelId = channelId,
                        MessageType = "IndividualInChannel",
                        IndividualReceiverId = individualReceiverId
                    };

                    await Clients.User(senderId).SendAsync("ReceiveChannelMessage", deliveredToSender);
                    await Clients.User(individualReceiverId.Value.ToString()).SendAsync("ReceiveChannelMessage", deliveredToReceiver);
                }
                else
                {

                    foreach (var participantId in participants)
                    {
                        if (participantId.HasValue)
                        {
                            var senderDisplayName = await GetSenderDisplayNameForRecipientAsync(
                                senderIdLong,
                                participantId.Value,
                                senderInfo.UserName);

                            var delivered = new ChatDeliveredMessageDTO
                            {
                                Id = newMessageId,
                                SenderId = senderId,
                                SenderName = senderDisplayName,
                                ReceiverId = channelId.ToString(),
                                Content = request.Content ?? string.Empty,
                                SentAt = DateTime.UtcNow,
                                FacilityId = senderInfo.FacilityId,
                                FacilityName = senderInfo.FacilityName,
                                PatientId = channelPatientId,
                                ChannelId = channelId,
                                MessageType = "Channel",
                                IndividualReceiverId = null
                            };

                            await Clients.User(participantId.Value.ToString()).SendAsync("ReceiveChannelMessage", delivered);
                        }
                    }
                }

                try
                {
                    var emailRecipients = individualReceiverId.HasValue
                        ? new[] { (long?)individualReceiverId.Value }
                        : (IEnumerable<long?>)participants;

                    foreach (var participantId in emailRecipients.Where(p => p.HasValue && p.Value.ToString() != senderId))
                    {
                        bool isOnline = _onlineUsers.TryGetValue(participantId.Value.ToString(), out var onlineUser)
                            && (onlineUser?.IsOnline ?? false);

                        if (!isOnline)
                        {

                            string? participantEmail = null;
                            var user = await _db.SYS_UserDetails
                                .AsNoTracking()
                                .Where(u => u.UserId == participantId.Value)
                                .Select(u => new { u.Email })
                                .FirstOrDefaultAsync();

                            if (user != null && !string.IsNullOrWhiteSpace(user.Email))
                            {
                                participantEmail = user.Email;
                            }

                            if (!string.IsNullOrWhiteSpace(participantEmail))
                            {
                                var messagePreview = request.Content?.Length > 100
                                    ? request.Content.Substring(0, 100) + "..."
                                    : request.Content ?? string.Empty;

                                var senderDisplayNameForParticipant = await GetSenderDisplayNameForRecipientAsync(
                                    senderIdLong,
                                    participantId.Value,
                                    senderInfo.UserName);

                                string capturedSenderName = senderDisplayNameForParticipant;
                                string capturedParticipantEmail = participantEmail;

                                _ = Task.Run(async () =>
                                {
                                    try
                                    {
                                        await _notificationService.SendMessageNotificationAsync(
                                            recipientEmail: capturedParticipantEmail,
                                            senderName: capturedSenderName,
                                            messagePreview: messagePreview,
                                            ct: default
                                        );
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine($"Error sending channel message notification: {ex.Message}");
                                    }
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error preparing channel message notification: {ex.Message}");
                }

                await Clients.Caller.SendAsync("MessageSent", "Channel message sent successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SendChannelMessage: {ex.Message}");
                throw new HubException($"Failed to send channel message: {ex.Message}");
            }
        }

        public async Task LoadChannelMessages(long channelId, int pageNumber = 1, int pageSize = 50, long? individualReceiverId = null)
        {
            string userId = Context.UserIdentifier;
            if (string.IsNullOrEmpty(userId))
            {
                await Clients.Caller.SendAsync("MessageError", "User is not authenticated.");
                throw new HubException("User is not authenticated.");
            }

            try
            {
                long userIdLong = Convert.ToInt64(userId);
                var messages = await _chatsRepo.GetChannelMessagesAsync(channelId, userIdLong, pageNumber, pageSize, individualReceiverId);

                await Clients.Caller.SendAsync("ReceiveChannelMessageList", messages);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in LoadChannelMessages: {ex.Message}");
                await Clients.Caller.SendAsync("MessageError", $"Failed to load channel messages: {ex.Message}");
                throw new HubException($"Failed to load channel messages: {ex.Message}");
            }
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            try
            {
                string userId = Context.UserIdentifier;
                if (!string.IsNullOrEmpty(userId))
                {
                    _onlineUsers.TryRemove(userId, out _);
                    await Clients.All.SendAsync("UserOnlineNotification", userId, false);
                }

                await base.OnDisconnectedAsync(exception);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in OnDisconnectedAsync: {ex.Message}");
            }
        }

        private async Task<string> GetUserNameAsync(long userId)
        {
            try
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

                    if (user.LoginRoleId == 6 && user.LoginId.HasValue)
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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting user name for userId {userId}: {ex.Message}");
            }
            return "Unknown";
        }

        private async Task<string> GetSenderDisplayNameForRecipientAsync(long senderUserId, long recipientUserId, string? fallbackSenderName)
        {
            var resolvedSenderName = fallbackSenderName;
            if (string.IsNullOrWhiteSpace(resolvedSenderName) ||
                string.Equals(resolvedSenderName, "Unknown", StringComparison.OrdinalIgnoreCase))
            {
                resolvedSenderName = await GetUserNameAsync(senderUserId);
            }

            var senderRoleId = await GetUserRoleIdAsync(senderUserId);
            if (senderRoleId == (int)UserRole.Provider)
            {
                var recipientRoleId = await GetUserRoleIdAsync(recipientUserId);
                if (recipientRoleId == (int)UserRole.ClinicAdmin ||
                    recipientRoleId == (int)UserRole.Patient)
                {
                    return "Provider";
                }
            }

            if (string.IsNullOrWhiteSpace(resolvedSenderName) ||
                string.Equals(resolvedSenderName, "Unknown", StringComparison.OrdinalIgnoreCase))
            {
                return "Someone";
            }

            return resolvedSenderName.Trim();
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

        private async Task<int?> GetUserRoleIdAsync(long userId)
        {
            return await (from ud in _db.SYS_UserDetails.AsNoTracking()
                          join login in _db.SYS_Logins.AsNoTracking() on ud.LoginId equals login.LoginId
                          where ud.UserId == userId
                          select (int?)login.RoleId)
                         .FirstOrDefaultAsync();
        }
    }
}
