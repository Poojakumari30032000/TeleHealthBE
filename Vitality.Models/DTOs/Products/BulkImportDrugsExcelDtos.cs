using Microsoft.AspNetCore.Http;

namespace Vitality.Models.DTOs.Products
{
    public class BulkUploadDrugsRequestDTO
    {
        public IFormFile File { get; set; } = default!;
        public long CatalogId { get; set; }
    }

    public class BulkImportDrugIssueRowDto
    {
        public string Sheet { get; set; } = string.Empty;
        public int Row { get; set; }
        public string? Name { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class DrugImportSuccessDto
    {
        public string Name { get; set; } = string.Empty;
    }

    public class DrugImportFailureDto
    {
        public string? Name { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class BulkImportDrugsExcelResponseDto
    {
        public bool ImportSucceeded { get; set; }
        public bool ProcessingCompleted { get; set; }
        public long CatalogId { get; set; }
        public int DrugRowsRead { get; set; }
        public int DrugsCreated { get; set; }

        public List<BulkImportDrugIssueRowDto> ParseAndValidationErrors { get; set; } = new();
        public List<DrugImportSuccessDto> DrugsSucceeded { get; set; } = new();
        public List<DrugImportFailureDto> DrugsFailed { get; set; } = new();
    }
}
