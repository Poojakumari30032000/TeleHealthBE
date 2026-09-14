using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading.Tasks;
using Vitality.Helper;
using Vitality.Models.EntityClasses;
using Vitality.Models.Repos.Interfaces;

namespace Vitality.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class FullscriptController : ControllerBase
    {
        private readonly IFullscriptService _fullscriptService;
        private readonly MainContext _db;

        public FullscriptController(IFullscriptService fullscriptService, MainContext db)
        {
            _fullscriptService = fullscriptService;
            _db = db;
        }

        [HttpGet("session-grant")]
        public async Task<ApiResponse<FullscriptSessionGrantResponse>> GetSessionGrant()
        {
            var response = new ApiResponse<FullscriptSessionGrantResponse>();

            try
            {
                var facilityId = await GetFacilityIdAsync();
                var secretToken = await _fullscriptService.GenerateSessionGrantTokenAsync(facilityId);

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
                response.Message = $"Failed to generate session grant token: {ex.Message}";
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
    }

}
