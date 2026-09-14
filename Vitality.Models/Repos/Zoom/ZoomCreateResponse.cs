using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Vitality.Models.Repos.Zoom
{
    public sealed class ZoomCreateResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("uuid")] public string? Uuid { get; set; }
        [JsonPropertyName("join_url")] public string? JoinUrl { get; set; }
        [JsonPropertyName("start_url")] public string? StartUrl { get; set; }
        [JsonPropertyName("password")] public string? Password { get; set; }
    }

    public sealed class ZoomListMeetingsResponse
    {
        [JsonPropertyName("meetings")] public ZoomMeetingItem[] Meetings { get; set; } = Array.Empty<ZoomMeetingItem>();
    }

    public sealed class ZoomMeetingItem
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("uuid")] public string? Uuid { get; set; }
        [JsonPropertyName("topic")] public string? Topic { get; set; }
        [JsonPropertyName("start_time")] public string? StartTime { get; set; }
        [JsonPropertyName("timezone")] public string? Timezone { get; set; }
        [JsonPropertyName("duration")] public int Duration { get; set; }
        [JsonPropertyName("join_url")] public string? JoinUrl { get; set; }
        [JsonPropertyName("password")] public string? Password { get; set; }
    }

    internal sealed class ZoomErrorResponse
    {
        [JsonPropertyName("code")] public int? Code { get; set; }
        [JsonPropertyName("message")] public string? Message { get; set; }
    }
}
