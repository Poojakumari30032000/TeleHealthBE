using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Vitality.Models.Repos;
using Vitality.Models.Repos.Interfaces;

namespace Vitality.Models.Repos.Services
{
    public class EhrWebhookService : BaseRepo, IEhrWebhookService
    {
        private const string TargetFacilityName = "Jonathan Edward Health Care";
        private const string DefaultWebhookUrl = "https://sandybrown-termite-989191.hostingersite.com/api/webhook/ehr";

        private readonly HttpClient _http;
        private readonly IConfiguration _cfg;

        public EhrWebhookService(HttpClient http, IConfiguration cfg)
        {
            _http = http;
            _cfg = cfg;
        }

        public async Task NotifyBookingCreatedAsync(
            long? facilityId,
            long? patientId,
            string? bundleName,
            bool isRecurring,
            decimal payment,
            CancellationToken ct = default)
        {
            try
            {
                if (!facilityId.HasValue)
                    return;

                var facilityTitle = await _db.SYS_Facilities
                    .AsNoTracking()
                    .Where(f => f.FacilityId == facilityId.Value)
                    .Select(f => f.TitleLong)
                    .FirstOrDefaultAsync(ct);

                if (!string.Equals(facilityTitle?.Trim(), TargetFacilityName, StringComparison.OrdinalIgnoreCase))
                    return;

                string email = string.Empty;
                string stateCode = string.Empty;
                if (patientId.HasValue)
                {
                    var info = await (from p in _db.PT_Patients.AsNoTracking()
                                      where p.PatientId == patientId.Value
                                      join s in _db.SYS_States.AsNoTracking() on p.StateId equals s.Id into sj
                                      from state in sj.DefaultIfEmpty()
                                      select new
                                      {
                                          p.Email,
                                          StateShortName = state != null ? state.ShortName : null
                                      }).FirstOrDefaultAsync(ct);

                    if (info != null)
                    {
                        email = info.Email ?? string.Empty;
                        stateCode = info.StateShortName ?? string.Empty;
                    }
                }

                var payload = new
                {
                    patient_id = patientId.HasValue ? $"PT-{patientId.Value}" : string.Empty,
                    email = email,
                    state = stateCode,
                    product_code = bundleName ?? string.Empty,
                    payment_status = "paid",
                    payment = Math.Round(payment, 2, MidpointRounding.AwayFromZero),
                    subscription_status = isRecurring ? "active" : "inactive",
                    timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                };

                var json = JsonSerializer.Serialize(payload);
                var url = _cfg["EhrWebhook:Url"];
                if (string.IsNullOrWhiteSpace(url))
                    url = DefaultWebhookUrl;

                using var req = new HttpRequestMessage(HttpMethod.Post, url);
                req.Content = new StringContent(json, Encoding.UTF8, "application/json");

                using var resp = await _http.SendAsync(req, ct);
                if (!resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"EHR webhook non-success: {(int)resp.StatusCode} {body}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"EHR webhook error: {ex.Message}");
            }
        }
    }
}
