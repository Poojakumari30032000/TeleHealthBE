using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading.Tasks;
using Vitality.Helper;
using Vitality.Models.EntityClasses;
using Vitality.Models.Repos.Interfaces;
using Vitality.Models.Enums;
using Vitality.Filters;

namespace Vitality.Controllers
{
    [Route("api/integrations")]
    [ApiController]
    [Authorize]
    public class IntegrationsController : ControllerBase
    {
        private readonly IFullscriptService _fullscriptService;
        private readonly ISquareOAuthService _squareOAuthService;
        private readonly IConfiguration _configuration;
        private readonly MainContext _db;

        public IntegrationsController(IFullscriptService fullscriptService, ISquareOAuthService squareOAuthService, IConfiguration configuration, MainContext db)
        {
            _fullscriptService = fullscriptService;
            _squareOAuthService = squareOAuthService;
            _configuration = configuration;
            _db = db;
        }

        [HttpGet("fullscript/initiate")]
        [AuthorizeRoles(UserRole.GlobalAdmin)]
        public async Task<ApiResponse<FullscriptInitiateResponse>> InitiateFullscriptOAuth()
        {
            var response = new ApiResponse<FullscriptInitiateResponse>();

            try
            {

                var stateParam = "globaladmin";
                var redirectUri = GetRedirectUri();
                var authUrl = await _fullscriptService.GetAuthorizationUrlAsync(redirectUri, stateParam);

                response.Status = 1;
                response.Message = "OAuth URL generated successfully";
                response.Data = new FullscriptInitiateResponse
                {
                    AuthUrl = authUrl
                };

                return response;
            }
            catch (Exception ex)
            {
                response.Status = 0;
                response.Message = $"Failed to initiate OAuth: {ex.Message}";
                response.Data = null;
                return response;
            }
        }

        [HttpGet("fullscript/connect")]
        [AllowAnonymous]
        public async Task<IActionResult> ConnectFullscript([FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error)
        {
            try
            {
                var frontendUrl = _configuration["Frontend:BaseUrl"] ?? "http://localhost:4200";

                if (!string.IsNullOrWhiteSpace(error))
                {
                    return Redirect($"{frontendUrl}/integrations?fullscript=error&message={Uri.EscapeDataString(error)}");
                }

                if (!string.IsNullOrWhiteSpace(code))
                {

                    long? facilityId = null;
                    if (!string.IsNullOrWhiteSpace(state))
                    {

                        if (state.Equals("globaladmin", StringComparison.OrdinalIgnoreCase))
                        {

                            facilityId = null;
                        }
                        else if (long.TryParse(state, out var stateFacilityId))
                        {

                            facilityId = stateFacilityId;
                        }
                        else
                        {

                            return Redirect($"{frontendUrl}/integrations?fullscript=error&message=Invalid OAuth state parameter");
                        }
                    }
                    else
                    {

                        facilityId = null;
                    }

                    var redirectUri = GetRedirectUri();

                    var success = await _fullscriptService.ExchangeAuthCodeForTokenAsync(code, redirectUri, facilityId);

                    if (success)
                    {
                        return Redirect($"{frontendUrl}/integrations?fullscript=connected");
                    }
                    else
                    {
                        return Redirect($"{frontendUrl}/integrations?fullscript=error&message=Failed to exchange authorization code");
                    }
                }

                return Redirect($"{frontendUrl}/login?returnUrl=/integrations");
            }
            catch (Exception ex)
            {
                var frontendUrl = _configuration["Frontend:BaseUrl"] ?? "http://localhost:4200";
                return Redirect($"{frontendUrl}/integrations?fullscript=error&message={Uri.EscapeDataString(ex.Message)}");
            }
        }

        [HttpGet("fullscript/status")]
        [AuthorizeRoles(UserRole.GlobalAdmin)]
        public async Task<ApiResponse<FullscriptStatusResponse>> GetFullscriptStatus()
        {
            var response = new ApiResponse<FullscriptStatusResponse>();

            try
            {

                var isConnected = await _fullscriptService.IsConnectedAsync(null);

                response.Status = 1;
                response.Message = "Status retrieved successfully";
                response.Data = new FullscriptStatusResponse
                {
                    Connected = isConnected
                };
            }
            catch (Exception ex)
            {
                response.Status = 0;
                response.Message = $"Failed to get status: {ex.Message}";
                response.Data = new FullscriptStatusResponse
                {
                    Connected = false
                };
            }

            return response;
        }

        [HttpGet("fullscript/session-grant")]
        [AuthorizeRoles(UserRole.GlobalAdmin)]
        public async Task<ApiResponse<FullscriptSessionGrantResponse>> GetSessionGrant()
        {
            var response = new ApiResponse<FullscriptSessionGrantResponse>();

            try
            {

                var secretToken = await _fullscriptService.GenerateSessionGrantTokenAsync(null);

                response.Status = 1;
                response.Message = "Session grant token generated successfully";
                response.Data = new FullscriptSessionGrantResponse
                {
                    SecretToken = secretToken
                };
            }
            catch (UnauthorizedAccessException ex)
            {
                response.Status = 0;
                response.Message = ex.Message;
                response.Data = null;
            }
            catch (Exception ex)
            {
                response.Status = 0;
                response.Message = $"Failed to generate session grant: {ex.Message}";
                response.Data = null;
            }

            return response;
        }

        [HttpPost("fullscript/disconnect")]
        [AuthorizeRoles(UserRole.GlobalAdmin)]
        public async Task<ApiResponse<object>> DisconnectFullscript()
        {
            var response = new ApiResponse<object>();

            try
            {

                await _fullscriptService.RevokeTokenAsync(null);

                response.Status = 1;
                response.Message = "Fullscript disconnected successfully";
                response.Data = null;
            }
            catch (Exception ex)
            {
                response.Status = 0;
                response.Message = $"Failed to disconnect: {ex.Message}";
                response.Data = null;
            }

            return response;
        }

        private async Task<long?> GetFacilityIdAsync()
        {

            var facilityIdClaim = User.FindFirst("FacilityId");
            if (facilityIdClaim != null && long.TryParse(facilityIdClaim.Value, out var facilityIdFromClaim))
            {
                return facilityIdFromClaim;
            }

            var userIdClaim = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)
                ?? User.FindFirst("UserId");

            if (userIdClaim == null || !long.TryParse(userIdClaim.Value, out var userId))
            {
                return null;
            }

            var userFacility = await _db.FC_UsersInFacilities
                .Where(uf => uf.UserId == userId && uf.IsAssign == true)
                .Select(uf => uf.FacilityId)
                .FirstOrDefaultAsync();

            return userFacility;
        }

        private long? GetFacilityId()
        {

            var facilityIdClaim = User.FindFirst("FacilityId");
            if (facilityIdClaim != null && long.TryParse(facilityIdClaim.Value, out var facilityIdFromClaim))
            {
                return facilityIdFromClaim;
            }

            var userIdClaim = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)
                ?? User.FindFirst("UserId");

            if (userIdClaim == null || !long.TryParse(userIdClaim.Value, out var userId))
            {
                return null;
            }

            var userFacility = _db.FC_UsersInFacilities
                .Where(uf => uf.UserId == userId && uf.IsAssign == true)
                .Select(uf => uf.FacilityId)
                .FirstOrDefault();

            return userFacility;
        }

        private string GetRedirectUri()
        {
            var request = HttpContext.Request;
            var baseUrl = $"{request.Scheme}://{request.Host}";
            return $"{baseUrl}/api/integrations/fullscript/connect";
        }

        private string GetSquareRedirectUri()
        {
            var over = _configuration["Square:OAuth:RedirectUri"];
            if (!string.IsNullOrWhiteSpace(over))
                return over.Trim().TrimEnd('/');
            var baseUrl = GetApiBase();
            return $"{baseUrl}/api/integrations/square/connect".TrimEnd('/');
        }

        private string GetApiBase()
        {
            var over = _configuration["Square:OAuth:ApiBaseUrl"] ?? _configuration["App:BaseUrl"];
            if (!string.IsNullOrWhiteSpace(over))
                return over.Trim().TrimEnd('/');
            var request = HttpContext.Request;
            return $"{request.Scheme}://{request.Host}";
        }

        [HttpGet("square/initiate")]
        [AuthorizeRoles(UserRole.ClinicAdmin)]
        public async Task<ApiResponse<SquareInitiateResponse>> InitiateSquareOAuth([FromQuery] long? facilityId)
        {
            var response = new ApiResponse<SquareInitiateResponse>();
            try
            {
                var fid = facilityId ?? await GetFacilityIdAsync();
                if (!fid.HasValue || fid.Value <= 0)
                {
                    response.Status = 0;
                    response.Message = "Facility is required.";
                    response.Data = null;
                    return response;
                }
                var tok = _squareOAuthService.CreateGoToken(fid.Value);
                var apiBase = GetApiBase();
                var redirectUrl = $"{apiBase}/api/integrations/square/go?facilityId={fid.Value}&tok={Uri.EscapeDataString(tok)}";
                response.Status = 1;
                response.Message = "OK";
                response.Data = new SquareInitiateResponse { AuthUrl = redirectUrl };
                return await Task.FromResult(response);
            }
            catch (Exception ex)
            {
                response.Status = 0;
                response.Message = $"Failed to initiate Square OAuth: {ex.Message}";
                response.Data = null;
                return response;
            }
        }

        [HttpGet("square/go")]
        [AllowAnonymous]
        public async Task<IActionResult> SquareGo([FromQuery] long? facilityId, [FromQuery] string? tok)
        {
            var frontendUrl = _configuration["Frontend:BaseUrl"] ?? "http://localhost:4200";
            var returnPath = "/integrate-getting-started";

            if (!_squareOAuthService.TryConsumeGoToken(tok, out var fid))
            {
                return Redirect($"{frontendUrl}{returnPath}?square=error&message=Invalid+or+expired+link.+Please+try+Connect+again.");
            }
            if (!facilityId.HasValue || facilityId.Value != fid)
            {
                return Redirect($"{frontendUrl}{returnPath}?square=error&message=Facility+mismatch.");
            }
            var redirectUri = GetSquareRedirectUri();
            var authUrl = await _squareOAuthService.GetAuthorizationUrlAsync(fid, redirectUri);
            return Redirect(authUrl);
        }

        [HttpGet("square/debug-auth-url")]
        [AuthorizeRoles(UserRole.ClinicAdmin)]
        public async Task<IActionResult> SquareDebugAuthUrl([FromQuery] long? facilityId)
        {
            var fid = facilityId ?? await GetFacilityIdAsync();
            if (!fid.HasValue || fid.Value <= 0)
                return Ok(new { error = "Facility is required." });
            var redirectUri = GetSquareRedirectUri();
            var authUrl = await _squareOAuthService.GetAuthorizationUrlAsync(fid.Value, redirectUri);
            return Ok(new { authUrl, redirectUri, facilityId = fid.Value });
        }

        [HttpGet("square/connect")]
        [AllowAnonymous]
        public async Task<IActionResult> ConnectSquare([FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error)
        {
            var frontendUrl = _configuration["Frontend:BaseUrl"] ?? "https://www.telehealthus.com";
            var returnPath = "/integrate-getting-started";

            if (!string.IsNullOrWhiteSpace(error))
            {
                return Redirect($"{frontendUrl}{returnPath}?square=error&message={Uri.EscapeDataString(error)}");
            }
            if (string.IsNullOrWhiteSpace(code))
            {
                return Redirect($"{frontendUrl}{returnPath}?square=error&message=Missing authorization code");
            }
            var redirectUri = GetSquareRedirectUri();
            var success = await _squareOAuthService.ExchangeCodeForTokenAsync(code, redirectUri, state ?? "");
            if (success)
                return Redirect($"{frontendUrl}{returnPath}?square=connected");
            return Redirect($"{frontendUrl}{returnPath}?square=error&message=Failed to exchange authorization code");
        }

        [HttpGet("square/status")]
        [AuthorizeRoles(UserRole.ClinicAdmin)]
        public async Task<ApiResponse<SquareStatusResponse>> GetSquareStatus([FromQuery] long? facilityId)
        {
            var response = new ApiResponse<SquareStatusResponse>();
            var sandbox = string.Equals(_configuration["Square:Environment"] ?? "", "sandbox", StringComparison.OrdinalIgnoreCase);
            try
            {
                var fid = facilityId ?? await GetFacilityIdAsync();
                if (!fid.HasValue || fid.Value <= 0)
                {
                    response.Status = 0;
                    response.Message = "Facility is required.";
                    response.Data = new SquareStatusResponse { Connected = false, Sandbox = sandbox };
                    return response;
                }
                var connected = await _squareOAuthService.IsConnectedAsync(fid.Value);
                response.Status = 1;
                response.Message = "Status retrieved successfully";
                response.Data = new SquareStatusResponse { Connected = connected, Sandbox = sandbox };
                return response;
            }
            catch (Exception ex)
            {
                response.Status = 0;
                response.Message = $"Failed to get Square status: {ex.Message}";
                response.Data = new SquareStatusResponse { Connected = false, Sandbox = sandbox };
                return response;
            }
        }

        [HttpPost("square/disconnect")]
        [AuthorizeRoles(UserRole.ClinicAdmin)]
        public async Task<ApiResponse<object>> DisconnectSquare()
        {
            var response = new ApiResponse<object>();
            try
            {
                var fid = await GetFacilityIdAsync();
                if (!fid.HasValue || fid.Value <= 0)
                {
                    response.Status = 0;
                    response.Message = "Facility is required.";
                    response.Data = null;
                    return response;
                }
                await _squareOAuthService.DisconnectAsync(fid.Value);
                response.Status = 1;
                response.Message = "Square disconnected successfully";
                response.Data = null;
                return response;
            }
            catch (Exception ex)
            {
                response.Status = 0;
                response.Message = $"Failed to disconnect Square: {ex.Message}";
                response.Data = null;
                return response;
            }
        }
    }

    public class SquareInitiateResponse
    {
        public string AuthUrl { get; set; } = "";
    }

    public class SquareStatusResponse
    {
        public bool Connected { get; set; }

        public bool Sandbox { get; set; }
    }

    public class FullscriptStatusResponse
    {
        public bool Connected { get; set; }
    }

    public class FullscriptInitiateResponse
    {
        public string AuthUrl { get; set; } = "";
    }

    public class FullscriptSessionGrantResponse
    {
        public string SecretToken { get; set; } = "";
    }
}
