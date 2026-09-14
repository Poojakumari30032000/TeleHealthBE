using DudeMeds.Models.DTOs.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using Vitality.Helper;
using Vitality.Models.DTOs.Common;
using Vitality.Models.Repos.Services.S3;

namespace DudeMeds.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CommonsController : ControllerBase
    {
        [HttpPost]
        [Route("UploadFile")]
        public async Task<IActionResult> UploadFile(
            IFormFile file,
            [FromServices] IS3Service s3Service)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return BadRequest("No file was selected for upload.");
                }

                var uploadResult = await s3Service.UploadFileAsync(file, null);

                var fileDetails = new
                {
                    FileName = uploadResult.FileName,
                    FilePath = uploadResult.Url
                };

                return Ok(new { Message = "File uploaded successfully to S3.", FileDetails = fileDetails });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [AllowAnonymous]
        [HttpPost]
        [Route("getNPIDetails")]
        public async Task<GetNPISearchDoctorResponseDTO> GetNPIDetails(GetNPISearchDoctorRequestDTO npiFinder)
        {
            try
            {
                string baseUrl = "https://npiregistry.cms.hhs.gov/api";

                var queryParams = new List<string>
        {
            $"number={npiFinder.NpiNumber}",
            $"enumeration_type={npiFinder.EnumerationType}",
            $"taxonomy_description={npiFinder.TaxonomyDescription}",
            $"name_purpose={npiFinder.name_purpose}",
            $"first_name={npiFinder.ProviderFirstName}",
            $"use_first_name_alias={npiFinder.use_first_name_alias}",
            $"last_name={npiFinder.ProviderLastName}",
            $"organization_name={npiFinder.OrganizationName}",
            $"address_purpose={npiFinder.address_purpose}",
            $"city={npiFinder.City}",
            $"state={npiFinder.State}",
            $"postal_code={npiFinder.PostalCode}",
            $"country_code={npiFinder.Country}",
            $"limit={npiFinder.limit}",
            $"skip={npiFinder.skip}",
            "pretty=on",
            "version=2.1"
        };

                string fullUrl = $"{baseUrl}/?{string.Join("&", queryParams.Where(param => !string.IsNullOrEmpty(param)))}";

                using (HttpClient client = new HttpClient())
                {
                    HttpResponseMessage response = await client.GetAsync(fullUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        string content = await response.Content.ReadAsStringAsync();

                        var npiResponse = JsonConvert.DeserializeObject<GetNPISearchDoctorResponseDTO>(content);

                        if (npiResponse?.Results != null)
                        {
                            foreach (var result in npiResponse.Results)
                            {
                                if (result.Basic != null && string.IsNullOrEmpty(result.Basic.first_name))
                                {
                                    result.Basic.first_name = result.Basic.authorized_official_first_name;
                                    result.Basic.last_name = result.Basic.authorized_official_last_name;
                                }
                            }
                        }

                        return npiResponse;
                    }
                    else
                    {
                        throw new Exception("Failed to fetch NPI details.");
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error fetching NPI details: {ex.Message}", ex);
            }
        }

        [HttpPost]
        [Route("UploadSignature")]
        [RequestSizeLimit(10_000_000)]
        public async Task<ApiResponse<UploadFileResponseDTO>> UploadSignature(
            IFormFile file,
            [FromServices] IS3Service s3Service)
        {
            var resp = new ApiResponse<UploadFileResponseDTO>();
            try
            {
                if (file == null || file.Length == 0)
                {
                    resp.Message = "No file was selected for upload.";
                    return resp;
                }

                var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                 { "image/png", "image/jpeg", "image/jpg", "image/gif", "image/webp" };
                if (!allowed.Contains(file.ContentType))
                {
                    resp.Message = $"Unsupported content type: {file.ContentType}";
                    return resp;
                }

                var uploadResult = await s3Service.UploadFileAsync(file, "signatures");

                resp.Data = uploadResult;
                resp.Message = "File uploaded successfully to S3.";
            }
            catch (Exception ex)
            {
                resp.Message = ex.Message;
            }
            return resp;
        }
    }
}
