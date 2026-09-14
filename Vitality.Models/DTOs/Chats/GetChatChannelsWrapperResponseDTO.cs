using System;
using System.Collections.Generic;

namespace Vitality.Models.DTOs.Chats
{
    public class GetChatChannelsWrapperResponseDTO
    {
        public List<GetChatChannelsResponseDTO> Channels { get; set; } = new List<GetChatChannelsResponseDTO>();
        public bool? CanViewChannels { get; set; }
    }
}
