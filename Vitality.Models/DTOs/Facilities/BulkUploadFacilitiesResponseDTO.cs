using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitality.Models.DTOs.Facilities
{
    public class BulkUploadFacilitiesRequestDTO
    {

        public IFormFile File { get; set; } = default!;
    }

    public class BulkUploadFacilitiesResponseDTO
    {
        public int Processed { get; set; }
        public int Inserted { get; set; }
        public int Updated { get; set; }
        public int Skipped { get; set; }
        public int SkippedExisting { get; set; }
        public int SkippedDuplicateInFile { get; set; }
        public int InvalidRows { get; set; }
        public List<string> Errors { get; set; } = new();
    }
    public class BulkFacilitiesResult
    {
        public int Processed { get; set; }
        public int Inserted { get; set; }
        public int Updated { get; set; }
        public int Skipped { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    public class FacilityCsvRowDTO
    {
        public string? FacilityName { get; set; }
        public string? Address { get; set; }
        public int? CityId { get; set; }
        public int? StateId { get; set; }
        public string? ZipCode { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? BillingAddressType { get; set; }
        public string? BillingAddress { get; set; }
        public int? BillingCityId { get; set; }
        public int? BillingStateId { get; set; }
        public string? BillingZipCode { get; set; }

    }

    public class FacilityCsvRow
    {
        public string? TitleLong { get; set; }
        public string? TitleShort { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Fax { get; set; }
        public string? Address { get; set; }
        public int? CityId { get; set; }
        public int? StateId { get; set; }
        public string? ZipCode { get; set; }

        public string? BillingAddressType { get; set; }
        public string? BillingAddress { get; set; }
        public int? BillingCityId { get; set; }
        public int? BillingStateId { get; set; }
        public string? BillingZipCode { get; set; }

        public string? FedearlTaxId { get; set; }
        public string? NPI { get; set; }

        public string? FacilityContactName { get; set; }
        public string? FacilityContactEmail { get; set; }
        public string? FacilityContactPhone { get; set; }

        public string? Status { get; set; }
    }

    public class BulkImportFacilitiesResultDTO
    {
        public int Inserted { get; set; }
        public int SkippedExisting { get; set; }
        public int SkippedDuplicateInFile { get; set; }
        public int InvalidRows { get; set; }
        public List<string> Errors { get; set; } = new();

    }

}
