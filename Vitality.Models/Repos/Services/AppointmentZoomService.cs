using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vitality.Models.Repos.Interfaces;
using Vitality.Models.EntityClasses;

namespace Vitality.Models.Repos.Zoom
{

    public sealed class AppointmentZoomService : BaseRepo, IAppointmentZoomService
    {
        private readonly IZoomTokenProvider _tokenProvider;
        private readonly ZoomOptions _opts;
        private readonly IHttpClientFactory _httpFactory;

        public AppointmentZoomService(
            IZoomTokenProvider tokenProvider,
            IOptions<ZoomOptions> opts,
            IHttpClientFactory httpFactory)
        {
            _tokenProvider = tokenProvider;
            _opts = opts.Value;
            _httpFactory = httpFactory;
        }

        public async Task<bool> EnsureZoomMeetingForAppointmentAsync(long appointmentSlotId, CancellationToken ct = default)
        {
            var appt = await _db.PT_PatientAppointmentSlots
                .FirstOrDefaultAsync(a => a.PatientAppointmentSlotId == appointmentSlotId, ct);

            if (appt == null || appt.IsActive != true) return false;
            if (!_opts.Enabled) return true;
            if (!string.IsNullOrWhiteSpace(appt.ZoomMeetingId)) return true;

            if (!appt.StartDate.HasValue)
            {
                appt.ZoomStatus = "Skipped:NoStartDate";
                await _db.SaveChangesAsync(ct);
                return true;
            }

            var hostEmail = (_opts.DefaultHostEmail ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(hostEmail))
            {
                appt.ZoomStatus = "Error:MissingHostEmail";
                await _db.SaveChangesAsync(ct);
                return false;
            }

            var tz = string.IsNullOrWhiteSpace(_opts.DefaultTimeZone) ? "Asia/Karachi" : _opts.DefaultTimeZone!;
            var local = Combine(appt.StartDate!.Value, appt.StartTime);
            var safeLocal = EnsureFuture(local, TimeSpan.FromMinutes(2), tz);
            var startUtc = ToUtc(safeLocal, tz);
            var startUtcStr = startUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");

            var duration = Math.Max(1, appt.Duration ?? 30);
            var topic = string.IsNullOrWhiteSpace(appt.Title) ? $"Appointment #{appointmentSlotId}" : appt.Title!.Trim();

            var passcode = GeneratePasscode();

            var payload = new
            {
                topic,
                type = 2,
                start_time = startUtcStr,
                duration = duration,
                password = passcode,
                settings = new
                {
                    waiting_room = false,
                    join_before_host = true,
                    audio = "voip",
                    auto_recording = "none",
                    registrants_email_notification = false,
                    enforce_login = false

                }
            };

            var http = await ClientAsync(ct);
            using var res = await http.PostAsync(
                $"users/{Uri.EscapeDataString(hostEmail)}/meetings",
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
                ct);

            if (!res.IsSuccessStatusCode)
            {
                await StampZoomErrorAsync(appt, res, "Create", ct);
                return false;
            }

            ZoomCreateResponse? created = null;
            var responseBody = await res.Content.ReadAsStringAsync(ct);

            if (!string.IsNullOrWhiteSpace(responseBody))
            {
                try
                {

                    using var doc = JsonDocument.Parse(responseBody);
                    var root = doc.RootElement;

                    created = new ZoomCreateResponse
                    {

                        Id = root.TryGetProperty("id", out var idEl)
                            ? (idEl.ValueKind == JsonValueKind.Number
                                ? idEl.GetInt64().ToString()
                                : idEl.GetString())
                            : null,
                        Uuid = root.TryGetProperty("uuid", out var uuidEl) ? uuidEl.GetString() : null,
                        JoinUrl = root.TryGetProperty("join_url", out var joinEl) ? joinEl.GetString() : null,
                        StartUrl = root.TryGetProperty("start_url", out var startEl) ? startEl.GetString() : null,
                        Password = root.TryGetProperty("password", out var pwdEl) ? pwdEl.GetString() : null
                    };
                }
                catch {  }
            }

            string? meetingIdToFetch = null;
            if (created != null && !string.IsNullOrWhiteSpace(created.Id))
            {
                meetingIdToFetch = created.Id;
            }
            else if (res.Headers.Location is Uri loc && TryExtractMeetingId(loc, out var idFromHeader))
            {
                meetingIdToFetch = idFromHeader;
            }

            if (!string.IsNullOrWhiteSpace(meetingIdToFetch) &&
                (created == null || string.IsNullOrWhiteSpace(created.JoinUrl)))
            {
                var fetched = await GetMeetingByIdAsync(meetingIdToFetch, http, ct);
                if (fetched != null)
                {

                    if (created == null) created = fetched;
                    else
                    {
                        created.JoinUrl = fetched.JoinUrl ?? created.JoinUrl;
                        created.Password = fetched.Password ?? created.Password;
                        created.Uuid = fetched.Uuid ?? created.Uuid;
                    }
                }
            }

            if (created == null || string.IsNullOrWhiteSpace(created.JoinUrl))
            {
                var found = await FindByListingThenGetAsync(http, hostEmail, topic, startUtc, duration, ct);
                if (found != null)
                {
                    if (created == null) created = found;
                    else
                    {
                        created.JoinUrl = found.JoinUrl ?? created.JoinUrl;
                        created.Password = found.Password ?? created.Password;
                        if (string.IsNullOrWhiteSpace(created.Id)) created.Id = found.Id;
                        if (string.IsNullOrWhiteSpace(created.Uuid)) created.Uuid = found.Uuid;
                    }
                }
            }

            if (created != null && !string.IsNullOrWhiteSpace(created.Id) && string.IsNullOrWhiteSpace(created.JoinUrl))
            {
                await Task.Delay(500, ct);
                var retryFetched = await GetMeetingByIdAsync(created.Id, http, ct);
                if (retryFetched != null && !string.IsNullOrWhiteSpace(retryFetched.JoinUrl))
                {
                    created.JoinUrl = retryFetched.JoinUrl;
                    created.Password = retryFetched.Password ?? created.Password;
                }
            }

            if (created == null || string.IsNullOrWhiteSpace(created.JoinUrl))
            {
                appt.ZoomHostEmail = hostEmail;
                appt.ZoomStatus = created != null && !string.IsNullOrWhiteSpace(created.Id)
                    ? "CreatedButNoJoinUrl"
                    : "CreatedButNotFetched";
                await _db.SaveChangesAsync(ct);
                return false;
            }

            appt.ZoomMeetingId = created.Id;
            appt.ZoomUUID = created.Uuid;
            appt.ZoomJoinUrl = created.JoinUrl;
            appt.ZoomStartUrl = created.StartUrl;
            appt.ZoomPassword = created.Password ?? passcode;
            appt.ZoomHostEmail = hostEmail;
            appt.ZoomCreatedAt = DateTime.UtcNow;
            appt.ZoomStatus = "Created";
            await _db.SaveChangesAsync(ct);
            return true;
        }

        public async Task<bool> UpdateZoomMeetingForAppointmentAsync(long appointmentSlotId, CancellationToken ct = default)
        {
            var appt = await _db.PT_PatientAppointmentSlots
                .FirstOrDefaultAsync(a => a.PatientAppointmentSlotId == appointmentSlotId, ct);

            if (appt == null || appt.IsActive != true) return false;
            if (!_opts.Enabled) return true;

            if (string.IsNullOrWhiteSpace(appt.ZoomMeetingId))
                return await EnsureZoomMeetingForAppointmentAsync(appointmentSlotId, ct);

            var tz = string.IsNullOrWhiteSpace(_opts.DefaultTimeZone) ? "Asia/Karachi" : _opts.DefaultTimeZone!;
            var desiredLocal = Combine(appt.StartDate ?? DateTime.UtcNow.Date, appt.StartTime);
            var safeLocal = EnsureFuture(desiredLocal, TimeSpan.FromMinutes(2), tz);
            var utc = ToUtc(safeLocal, tz);

            var patch = new
            {
                topic = string.IsNullOrWhiteSpace(appt.Title) ? $"Appointment #{appointmentSlotId}" : appt.Title!.Trim(),
                start_time = utc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
                duration = Math.Max(1, appt.Duration ?? 30)

            };

            var http = await ClientAsync(ct);
            using var req = new HttpRequestMessage(new HttpMethod("PATCH"),
                $"meetings/{Uri.EscapeDataString(appt.ZoomMeetingId!)}")
            {
                Content = new StringContent(JsonSerializer.Serialize(patch), Encoding.UTF8, "application/json")
            };

            using var res = await http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode)
            {
                await StampZoomErrorAsync(appt, res, "Update", ct);
                return false;
            }

            appt.ZoomStatus = "Updated";
            await _db.SaveChangesAsync(ct);
            return true;
        }

        public async Task<bool> RehostZoomMeetingForAppointmentAsync(long appointmentSlotId, long newProviderUserId, CancellationToken ct = default)
        {

            var appt = await _db.PT_PatientAppointmentSlots
                .FirstOrDefaultAsync(a => a.PatientAppointmentSlotId == appointmentSlotId, ct);

            if (appt == null || appt.IsActive != true) return false;
            if (!_opts.Enabled) return true;

            if (!string.IsNullOrWhiteSpace(appt.ZoomMeetingId))
            {
                var http = await ClientAsync(ct);
                try { using var _ = await http.DeleteAsync($"meetings/{Uri.EscapeDataString(appt.ZoomMeetingId!)}", ct); }
                catch {  }
            }

            appt.ZoomMeetingId = null; appt.ZoomUUID = null; appt.ZoomJoinUrl = null; appt.ZoomStartUrl = null;
            appt.ZoomPassword = null; appt.ZoomHostEmail = null; appt.ZoomStatus = null;
            await _db.SaveChangesAsync(ct);

            return await EnsureZoomMeetingForAppointmentAsync(appointmentSlotId, ct);
        }

        public async Task<bool> CancelZoomMeetingForAppointmentAsync(long appointmentSlotId, CancellationToken ct = default)
        {
            var appt = await _db.PT_PatientAppointmentSlots
                .FirstOrDefaultAsync(a => a.PatientAppointmentSlotId == appointmentSlotId, ct);

            if (appt == null || appt.IsActive != true) return false;
            if (!_opts.Enabled) return true;

            if (!string.IsNullOrWhiteSpace(appt.ZoomMeetingId))
            {
                var http = await ClientAsync(ct);
                try { using var _ = await http.DeleteAsync($"meetings/{Uri.EscapeDataString(appt.ZoomMeetingId!)}", ct); }
                catch {  }
            }

            appt.ZoomStatus = "Cancelled";
            await _db.SaveChangesAsync(ct);
            return true;
        }

        private async Task<HttpClient> ClientAsync(CancellationToken ct)
        {
            var token = await _tokenProvider.GetAccessTokenAsync(ct);
            var http = _httpFactory.CreateClient("ZoomV2");
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            http.DefaultRequestHeaders.Accept.Clear();
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return http;
        }

        private static DateTime Combine(DateTime date, TimeSpan? time) =>
            new DateTime(date.Year, date.Month, date.Day,
                (time ?? TimeSpan.Zero).Hours,
                (time ?? TimeSpan.Zero).Minutes,
                (time ?? TimeSpan.Zero).Seconds,
                DateTimeKind.Unspecified);

        private static DateTime EnsureFuture(DateTime local, TimeSpan minAhead, string tz)
        {
            try
            {
                var tzi = TimeZoneInfo.FindSystemTimeZoneById(tz);
                var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tzi);
                if (local < nowLocal.Add(minAhead)) return nowLocal.Add(minAhead);
            }
            catch {  }
            return local;
        }

        private static DateTime ToUtc(DateTime local, string tz)
        {
            try
            {
                var tzi = TimeZoneInfo.FindSystemTimeZoneById(tz);
                return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), tzi);
            }
            catch
            {
                return DateTime.SpecifyKind(local, DateTimeKind.Utc);
            }
        }

        private static string GeneratePasscode()
        {

            const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789@-_*!";
            var rng = new Random();
            var chars = new char[8];
            for (int i = 0; i < chars.Length; i++) chars[i] = alphabet[rng.Next(alphabet.Length)];
            return new string(chars);
        }

        private static bool TryExtractMeetingId(Uri location, out string? meetingId)
        {

            var segments = location.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            meetingId = (segments.Length >= 2 && segments[^2] == "meetings") ? segments[^1] : null;
            return !string.IsNullOrWhiteSpace(meetingId);
        }

        private async Task<ZoomCreateResponse?> GetMeetingByIdAsync(string meetingId, HttpClient http, CancellationToken ct)
        {
            using var res = await http.GetAsync($"meetings/{Uri.EscapeDataString(meetingId)}", ct);
            if (!res.IsSuccessStatusCode) return null;

            var json = await res.Content.ReadAsStringAsync(ct);
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string? id = root.TryGetProperty("id", out var idEl)
                    ? (idEl.ValueKind == JsonValueKind.Number
                        ? idEl.GetInt64().ToString()
                        : idEl.GetString())
                    : meetingId;

                string? uuid = root.TryGetProperty("uuid", out var uuidEl) ? uuidEl.GetString() : null;
                string? join = root.TryGetProperty("join_url", out var j) ? j.GetString() : null;
                string? pwd = root.TryGetProperty("password", out var p) ? p.GetString() : null;

                return new ZoomCreateResponse
                {
                    Id = id,
                    Uuid = uuid,
                    JoinUrl = join,
                    StartUrl = null,
                    Password = pwd
                };
            }
            catch { return null; }
        }

        private async Task<ZoomCreateResponse?> FindByListingThenGetAsync(HttpClient http, string hostEmail, string topic, DateTime utcStart, int duration, CancellationToken ct)
        {
            foreach (var type in new[] { "scheduled", "upcoming" })
            {
                string? nextPage = null;
                int guard = 0;

                do
                {
                    var url = $"users/{Uri.EscapeDataString(hostEmail)}/meetings?type={type}&page_size=100" +
                              (string.IsNullOrWhiteSpace(nextPage) ? "" : $"&next_page_token={Uri.EscapeDataString(nextPage)}");

                    using var res = await http.GetAsync(url, ct);
                    if (!res.IsSuccessStatusCode) break;

                    var json = await res.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(json);
                    nextPage = doc.RootElement.TryGetProperty("next_page_token", out var npt) ? npt.GetString() : null;

                    if (!doc.RootElement.TryGetProperty("meetings", out var arr) || arr.ValueKind != JsonValueKind.Array)
                        break;

                    foreach (var m in arr.EnumerateArray())
                    {
                        var mTopic = m.TryGetProperty("topic", out var t) ? (t.GetString() ?? "").Trim() : "";
                        if (!string.Equals(mTopic, topic, StringComparison.Ordinal)) continue;

                        var mDuration = m.TryGetProperty("duration", out var d) ? d.GetInt32() : -1;
                        if (mDuration != duration) continue;

                        var mStartStr = m.TryGetProperty("start_time", out var st) ? st.GetString() : null;
                        if (!DateTime.TryParse(mStartStr, out var mStartUtc)) continue;

                        if (Math.Abs((mStartUtc.ToUniversalTime() - utcStart).TotalMinutes) > 2.0) continue;

                        string? id = null;
                        if (m.TryGetProperty("id", out var idEl))
                        {
                            id = idEl.ValueKind == JsonValueKind.Number
                                ? idEl.GetInt64().ToString()
                                : idEl.GetString();
                        }

                        if (!string.IsNullOrWhiteSpace(id))
                        {
                            var fetched = await GetMeetingByIdAsync(id!, http, ct);
                            if (fetched != null && !string.IsNullOrWhiteSpace(fetched.JoinUrl)) return fetched;
                        }
                    }

                } while (!string.IsNullOrWhiteSpace(nextPage) && ++guard < 10);
            }

            return null;
        }

        private async Task StampZoomErrorAsync(PT_PatientAppointmentSlot appt, HttpResponseMessage res, string op, CancellationToken ct)
        {
            var code = (int)res.StatusCode;
            string tag = $"Error{code}:{op}";
            try
            {
                var body = await res.Content.ReadAsStringAsync(ct);
                var err = string.IsNullOrWhiteSpace(body) ? null : JsonSerializer.Deserialize<ZoomErrorResponse>(body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (err?.Code != null || !string.IsNullOrWhiteSpace(err?.Message))
                    tag = $"Err{code}/{err?.Code}:{Short(err?.Message, 40)}";
                else if (!string.IsNullOrWhiteSpace(body))
                    tag = $"Err{code}:{Short(body, 40)}";
            }
            catch {  }

            appt.ZoomStatus = tag;
            await _db.SaveChangesAsync(ct);

            static string Short(string? s, int max) => string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : s.Substring(0, max));
        }

        private sealed class ZoomErrorResponse
        {
            [JsonPropertyName("code")] public int? Code { get; set; }
            [JsonPropertyName("message")] public string? Message { get; set; }
        }
    }
}
