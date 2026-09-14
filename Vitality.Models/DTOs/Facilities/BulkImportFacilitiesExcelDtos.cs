using System.Collections.Generic;

namespace Vitality.Models.DTOs.Facilities
{
    public class BulkImportIssueRowDto
    {
        public string Sheet { get; set; } = string.Empty;
        public int Row { get; set; }
        public string? FacilityImportKey { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class FacilityImportSuccessDto
    {
        public string FacilityImportKey { get; set; } = string.Empty;
        public long FacilityId { get; set; }
        public string? TitleLong { get; set; }
        public int AdminsCreated { get; set; }
    }

    public class AdminImportSuccessDto
    {
        public string FacilityImportKey { get; set; } = string.Empty;
        public long FacilityId { get; set; }
        public long UserId { get; set; }
        public string Email { get; set; } = string.Empty;
    }

    public class FacilityImportFailureDto
    {
        public string FacilityImportKey { get; set; } = string.Empty;
        public string? TitleLong { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class AdminImportFailureDto
    {
        public string FacilityImportKey { get; set; } = string.Empty;
        public long? FacilityId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public class BulkImportFacilitiesExcelResponseDto
    {

        public bool ImportSucceeded { get; set; }

        public bool ProcessingCompleted { get; set; }

        public int FacilityRowsRead { get; set; }
        public int AdminRowsRead { get; set; }
        public int FacilitiesCreated { get; set; }
        public int AdminsCreated { get; set; }

        public List<BulkImportIssueRowDto> ParseAndValidationErrors { get; set; } = new();
        public List<FacilityImportSuccessDto> FacilitiesSucceeded { get; set; } = new();
        public List<AdminImportSuccessDto> AdminsSucceeded { get; set; } = new();
        public List<FacilityImportFailureDto> FacilitiesFailed { get; set; } = new();
        public List<AdminImportFailureDto> AdminsFailed { get; set; } = new();
    }
}
