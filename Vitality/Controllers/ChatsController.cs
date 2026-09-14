using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Vitality.Helper;
using Vitality.Models.DTOs.Chats;
using Vitality.Models.Repos.Services;

namespace Vitality.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ChatsController : ControllerBase
    {
        private readonly ChatsRepo _chatsRepo;

        public ChatsController(ChatsRepo chatsRepo)
        {
            _chatsRepo = chatsRepo;

        }

        [HttpGet]
        [Route("getAllUsersforChat")]
        public async Task<ApiResponse<List<GetAllUsersforChatResponseDTO>>> GetAllUsersforChat(
         [FromQuery] GetAllUsersforChatRequestDTO request,
         CancellationToken ct)
        {
            var response = new ApiResponse<List<GetAllUsersforChatResponseDTO>>();
            try
            {
                var result = await _chatsRepo.GetAllUsersforChatAsync(request, ct);
                response.Data = result;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpGet]
        [Route("getUsersForCompose")]
        public async Task<ApiResponse<List<GetAllUsersforChatResponseDTO>>> GetUsersForCompose(
         [FromQuery] GetAllUsersforChatRequestDTO request,
         CancellationToken ct)
        {
            var response = new ApiResponse<List<GetAllUsersforChatResponseDTO>>();
            try
            {

                var result = await _chatsRepo.GetAllUsersforChatAsync(request, ct);
                response.Data = result;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpGet]
        [Route("getChatChannels")]
        public async Task<ApiResponse<GetChatChannelsWrapperResponseDTO>> GetChatChannels(
            [FromQuery] GetChatChannelsRequestDTO request,
            CancellationToken ct)
        {
            var response = new ApiResponse<GetChatChannelsWrapperResponseDTO>();
            try
            {
                var result = await _chatsRepo.GetChatChannelsAsync(request, ct);
                response.Data = result;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpGet]
        [Route("getChannelMessages")]
        public async Task<ApiResponse<List<ChatMessageResponseDTO>>> GetChannelMessages(
            [FromQuery] GetChannelMessagesRequestDTO request,
            CancellationToken ct)
        {
            var response = new ApiResponse<List<ChatMessageResponseDTO>>();
            try
            {
                if (!request.ChannelId.HasValue)
                {
                    response.Message = "Channel ID is required.";
                    return response;
                }

                var userId = Convert.ToInt64(User.FindFirst("UserId")?.Value ?? "0");
                var pageNumber = request.PageNumber ?? 1;
                var pageSize = request.PageSize ?? 50;

                var result = await _chatsRepo.GetChannelMessagesAsync(
                    request.ChannelId.Value,
                    userId,
                    pageNumber,
                    pageSize,
                    request.IndividualReceiverId,
                    ct);
                response.Data = result;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpGet]
        [Route("markDirectMessagesAsRead")]
        public async Task<ApiResponse<bool>> MarkDirectMessagesAsRead(
            [FromQuery] string recipientId,
            CancellationToken ct)
        {
            var response = new ApiResponse<bool>();
            try
            {
                if (string.IsNullOrEmpty(recipientId))
                {
                    response.Message = "Recipient ID is required.";
                    return response;
                }

                var userId = User.FindFirst("UserId")?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    response.Message = "User is not authenticated.";
                    return response;
                }

                await _chatsRepo.GetMessagesAsync(userId, recipientId, 1, 1);

                response.Data = true;
                response.Status = 1;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpGet]
        [Route("markChannelMessagesAsRead")]
        public async Task<ApiResponse<bool>> MarkChannelMessagesAsRead(
            [FromQuery] long channelId,
            [FromQuery] long? individualReceiverId,
            CancellationToken ct)
        {
            var response = new ApiResponse<bool>();
            try
            {
                var userId = Convert.ToInt64(User.FindFirst("UserId")?.Value ?? "0");
                if (userId == 0)
                {
                    response.Message = "User is not authenticated.";
                    return response;
                }

                await _chatsRepo.GetChannelMessagesAsync(channelId, userId, 1, 1, individualReceiverId, ct);

                response.Data = true;
                response.Status = 1;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

    }

}
