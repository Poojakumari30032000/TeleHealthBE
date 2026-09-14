using ClosedXML.Excel;
using DudeMeds.Models.DTOs.Products;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Vitality.Models.DTOs.Products;

namespace DudeMeds.Models.Repos.Services
{
    public partial class ProductsRepo
    {
        private const string BulkDrugsSheetName = "Drugs";

        private static readonly string[] DrugBulkRequiredHeaders =
        {
            "Product Name",
            "Strength",
            "Dosage Form",
            "Package Size",
            "Controlled Substance",
            "Pharmacy Price",
            "Markup Type",
            "Markup Amount",
            "Wholesale Price",
            "Suggested Retail Price",
        };

        public byte[] GenerateDrugBulkImportTemplate(long catalogId)
        {
            if (catalogId <= 0)
                throw new ArgumentException("CatalogId must be greater than zero.", nameof(catalogId));

            var catalog = _db.PD_Catalogs.AsNoTracking()
                .FirstOrDefault(c => c.CatalogId == catalogId && c.IsActive == true);
            if (catalog == null)
                throw new InvalidOperationException("Selected catalog does not exist or is inactive.");

            using var wb = new XLWorkbook();

            var drugs = wb.AddWorksheet(BulkDrugsSheetName);
            for (var i = 0; i < DrugBulkRequiredHeaders.Length; i++)
            {
                drugs.Cell(1, i + 1).Value = DrugBulkRequiredHeaders[i];
                drugs.Cell(1, i + 1).Style.Font.Bold = true;
                drugs.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#E7EEF7");
            }

            drugs.Cell(2, 1).Value = "Sample Acetaminophen";
            drugs.Cell(2, 2).Value = "500 mg";
            drugs.Cell(2, 3).Value = "TABLET";
            drugs.Cell(2, 4).Value = "100 ct bottle";
            drugs.Cell(2, 5).Value = "No";
            drugs.Cell(2, 6).Value = 12.50m;
            drugs.Cell(2, 7).Value = "Percentage";
            drugs.Cell(2, 8).Value = 15m;
            drugs.Cell(2, 9).Value = 14.38m;
            drugs.Cell(2, 10).Value = 24.99m;

            drugs.SheetView.FreezeRows(1);
            drugs.Row(1).Height = 22;
            drugs.Column(1).Width = 26;
            drugs.Column(2).Width = 14;
            drugs.Column(3).Width = 18;
            drugs.Column(4).Width = 18;
            drugs.Column(5).Width = 22;
            drugs.Column(6).Width = 16;
            drugs.Column(7).Width = 14;
            drugs.Column(8).Width = 14;
            drugs.Column(9).Width = 16;
            drugs.Column(10).Width = 22;

            using var stream = new MemoryStream();
            wb.SaveAs(stream);
            return stream.ToArray();
        }

        public async Task<BulkImportDrugsExcelResponseDto> ImportDrugsFromExcelAsync(
            Stream excelStream,
            long catalogId,
            long userId,
            long organizationId,
            CancellationToken ct = default)
        {
            var response = new BulkImportDrugsExcelResponseDto { CatalogId = catalogId };

            if (catalogId <= 0)
            {
                response.ParseAndValidationErrors.Add(new BulkImportDrugIssueRowDto
                {
                    Sheet = "Request",
                    Row = 0,
                    Message = "CatalogId must be greater than zero."
                });
                response.ProcessingCompleted = true;
                return response;
            }

            var catalog = await _db.PD_Catalogs.AsNoTracking()
                .FirstOrDefaultAsync(c => c.CatalogId == catalogId && c.IsActive == true, ct);
            if (catalog == null)
            {
                response.ParseAndValidationErrors.Add(new BulkImportDrugIssueRowDto
                {
                    Sheet = "Request",
                    Row = 0,
                    Message = "Selected catalog does not exist or is inactive."
                });
                response.ProcessingCompleted = true;
                return response;
            }

            var sheetNameForErrors = BulkDrugsSheetName;
            try
            {
                using var ms = new MemoryStream();
                await excelStream.CopyToAsync(ms, ct);
                ms.Position = 0;
                using var wb = new XLWorkbook(ms);
                var sheet = ResolveDrugImportWorksheet(wb);
                if (sheet == null)
                {
                    response.ParseAndValidationErrors.Add(new BulkImportDrugIssueRowDto
                    {
                        Sheet = BulkDrugsSheetName,
                        Row = 0,
                        Message =
                            $"Use a single-sheet workbook, or include a sheet named \"{BulkDrugsSheetName}\"."
                    });
                    response.ProcessingCompleted = true;
                    return response;
                }

                sheetNameForErrors = sheet.Name;
                var headerToCol = ReadHeaderMap(sheet, response.ParseAndValidationErrors);

                foreach (var required in DrugBulkRequiredHeaders)
                {
                    if (!headerToCol.ContainsKey(NormalizeHeaderKey(required)))
                    {
                        response.ParseAndValidationErrors.Add(new BulkImportDrugIssueRowDto
                        {
                            Sheet = sheet.Name,
                            Row = 1,
                            Message = $"Missing required column header \"{required}\"."
                        });
                    }
                }

                if (response.ParseAndValidationErrors.Count > 0)
                {
                    response.ProcessingCompleted = true;
                    return response;
                }

                var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
                var dataRows = 0;

                for (var r = 2; r <= lastRow; r++)
                {
                    ct.ThrowIfCancellationRequested();

                    if (IsEmptyDrugRow(sheet, r, headerToCol))
                        continue;

                    dataRows++;

                    var rowNamePreview = GetCellText(sheet, r, headerToCol, "Product Name");
                    if (!TryBuildSaveDrugRequest(sheet, r, headerToCol, catalogId, out var request, out var parseErr))
                    {
                        response.ParseAndValidationErrors.Add(new BulkImportDrugIssueRowDto
                        {
                            Sheet = sheet.Name,
                            Row = r,
                            Name = rowNamePreview,
                            Message = parseErr
                        });
                        continue;
                    }

                    var saveResult = SaveDrug(request, userId, organizationId);
                    if (saveResult == "Drug Added Successfully")
                    {
                        response.DrugsCreated++;
                        response.DrugsSucceeded.Add(new DrugImportSuccessDto { Name = request.Name ?? string.Empty });
                        response.ImportSucceeded = true;
                    }
                    else
                    {
                        response.DrugsFailed.Add(new DrugImportFailureDto
                        {
                            Name = request.Name,
                            Message = saveResult
                        });
                    }
                }

                response.DrugRowsRead = dataRows;
                response.ProcessingCompleted = true;
                if (response.DrugRowsRead == 0 && response.ParseAndValidationErrors.Count == 0)
                {
                    response.ParseAndValidationErrors.Add(new BulkImportDrugIssueRowDto
                    {
                        Sheet = sheet.Name,
                        Row = 0,
                        Message = "No data rows found under the header row."
                    });
                }

                return response;
            }
            catch (Exception)
            {
                response.ParseAndValidationErrors.Add(new BulkImportDrugIssueRowDto
                {
                    Sheet = sheetNameForErrors,
                    Row = 0,
                    Message = "Could not read the Excel file. Save as .xlsx and try again."
                });
                response.ProcessingCompleted = true;
                return response;
            }
        }

        private static IXLWorksheet? ResolveDrugImportWorksheet(XLWorkbook wb)
        {
            if (wb.Worksheets.Count == 0)
                return null;
            if (wb.Worksheets.Count == 1)
                return wb.Worksheet(1);
            return wb.Worksheets.FirstOrDefault(w =>
                string.Equals(w.Name, BulkDrugsSheetName, StringComparison.OrdinalIgnoreCase));
        }

        private static Dictionary<string, int> ReadHeaderMap(IXLWorksheet sheet, List<BulkImportDrugIssueRowDto> errors)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var firstRow = sheet.FirstRowUsed();
            if (firstRow == null)
            {
                errors.Add(new BulkImportDrugIssueRowDto
                {
                    Sheet = sheet.Name,
                    Row = 1,
                    Message = "Sheet has no header row."
                });
                return map;
            }

            var headerRow = firstRow.RowNumber();
            var lastCol = sheet.LastColumnUsed()?.ColumnNumber() ?? 1;
            for (var c = 1; c <= lastCol; c++)
            {
                var raw = sheet.Cell(headerRow, c).GetString();
                if (string.IsNullOrWhiteSpace(raw))
                    continue;

                var key = NormalizeHeaderKey(raw);
                if (string.IsNullOrEmpty(key))
                    continue;

                if (!map.ContainsKey(key))
                    map[key] = c;
            }

            return map;
        }

        private static string NormalizeHeaderKey(string raw)
        {
            var t = raw.Trim();
            if (string.IsNullOrEmpty(t))
                return string.Empty;
            t = Regex.Replace(t, @"\s+", " ");

            if (string.Equals(t, "Strenght", StringComparison.OrdinalIgnoreCase))
                return "Strength";
            if (string.Equals(t, "Name", StringComparison.OrdinalIgnoreCase))
                return "Product Name";
            if (string.Equals(t, "DosageForm", StringComparison.OrdinalIgnoreCase))
                return "Dosage Form";
            if (string.Equals(t, "PackageSize", StringComparison.OrdinalIgnoreCase))
                return "Package Size";
            if (string.Equals(t, "ControlSubstance", StringComparison.OrdinalIgnoreCase))
                return "Controlled Substance";
            if (string.Equals(t, "Price", StringComparison.OrdinalIgnoreCase))
                return "Pharmacy Price";
            if (string.Equals(t, "MarkupType", StringComparison.OrdinalIgnoreCase))
                return "Markup Type";
            if (string.Equals(t, "Markup", StringComparison.OrdinalIgnoreCase))
                return "Markup Amount";
            if (string.Equals(t, "ComparePrice", StringComparison.OrdinalIgnoreCase)
                || string.Equals(t, "Wholesale", StringComparison.OrdinalIgnoreCase))
                return "Wholesale Price";
            if (string.Equals(t, "SuggestedRetail", StringComparison.OrdinalIgnoreCase))
                return "Suggested Retail Price";
            return t;
        }

        private static bool IsEmptyDrugRow(IXLWorksheet sheet, int row, Dictionary<string, int> headerToCol)
        {
            foreach (var h in DrugBulkRequiredHeaders)
            {
                var nk = NormalizeHeaderKey(h);
                if (!headerToCol.TryGetValue(nk, out var col))
                    continue;
                var t = sheet.Cell(row, col).GetString().Trim();
                if (!string.IsNullOrEmpty(t))
                    return false;
            }

            return true;
        }

        private static string? GetCellText(IXLWorksheet sheet, int row, Dictionary<string, int> headerToCol, string header)
        {
            var nk = NormalizeHeaderKey(header);
            if (!headerToCol.TryGetValue(nk, out var col))
                return null;
            var t = sheet.Cell(row, col).GetString().Trim();
            return string.IsNullOrEmpty(t) ? null : t;
        }

        private static bool TryGetDecimal(IXLWorksheet sheet, int row, Dictionary<string, int> headerToCol, string header,
            out decimal value, out string? error)
        {
            value = 0;
            error = null;
            var nk = NormalizeHeaderKey(header);
            if (!headerToCol.TryGetValue(nk, out var col))
            {
                error = $"Missing column \"{header}\".";
                return false;
            }

            var cell = sheet.Cell(row, col);
            if (cell.IsEmpty())
            {
                error = $"\"{header}\" is required.";
                return false;
            }

            if (cell.TryGetValue(out double dbl))
            {
                value = (decimal)dbl;
                return true;
            }

            var s = cell.GetString().Trim();
            if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.CurrentCulture, out value))
                return true;
            if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out value))
                return true;

            error = $"\"{header}\" must be a number.";
            return false;
        }

        private static bool TryParseBoolRequired(string text, out bool value, out string? error)
        {
            value = false;
            error = null;
            if (string.IsNullOrWhiteSpace(text))
            {
                error = "Controlled Substance is required (Yes/No).";
                return false;
            }

            var s = text.Trim();
            if (string.Equals(s, "yes", StringComparison.OrdinalIgnoreCase)
                || string.Equals(s, "y", StringComparison.OrdinalIgnoreCase)
                || string.Equals(s, "true", StringComparison.OrdinalIgnoreCase)
                || s == "1")
            {
                value = true;
                return true;
            }

            if (string.Equals(s, "no", StringComparison.OrdinalIgnoreCase)
                || string.Equals(s, "n", StringComparison.OrdinalIgnoreCase)
                || string.Equals(s, "false", StringComparison.OrdinalIgnoreCase)
                || s == "0")
            {
                value = false;
                return true;
            }

            error = "Controlled Substance must be Yes or No.";
            return false;
        }

        private static bool TryBuildSaveDrugRequest(
            IXLWorksheet sheet,
            int row,
            Dictionary<string, int> headerToCol,
            long expectedCatalogId,
            out SaveDrugRequestDTO request,
            out string error)
        {
            request = new SaveDrugRequestDTO { DrugId = 0 };
            request.CatalogId = expectedCatalogId;

            request.IsCustom = expectedCatalogId == EmpowerCatalogId ? null : true;

            var name = GetCellText(sheet, row, headerToCol, "Product Name");
            if (string.IsNullOrWhiteSpace(name))
            {
                error = "Product Name is required.";
                return false;
            }

            request.Name = name.Trim();

            var strenght = GetCellText(sheet, row, headerToCol, "Strength");
            if (string.IsNullOrWhiteSpace(strenght))
            {
                error = "Strength is required.";
                return false;
            }

            request.Strenght = strenght.Trim();

            var dosageForm = GetCellText(sheet, row, headerToCol, "Dosage Form");
            if (string.IsNullOrWhiteSpace(dosageForm))
            {
                error = "Dosage Form is required.";
                return false;
            }

            request.DosageForm = dosageForm.Trim();

            var packageSize = GetCellText(sheet, row, headerToCol, "Package Size");
            if (string.IsNullOrWhiteSpace(packageSize))
            {
                error = "Package Size is required.";
                return false;
            }

            request.PackageSize = packageSize.Trim();

            if (!headerToCol.TryGetValue(NormalizeHeaderKey("Controlled Substance"), out var csCol))
            {
                error = "Controlled Substance column missing.";
                return false;
            }

            var csText = sheet.Cell(row, csCol).GetString();
            if (!TryParseBoolRequired(csText, out var csBool, out var csErr))
            {
                error = csErr ?? "Invalid Controlled Substance.";
                return false;
            }

            request.ControlSubstance = csBool;

            var markupTypeRaw = GetCellText(sheet, row, headerToCol, "Markup Type");
            if (string.IsNullOrWhiteSpace(markupTypeRaw))
            {
                error = "Markup Type is required.";
                return false;
            }

            var mt = markupTypeRaw.Trim();
            if (!string.Equals(mt, "Percentage", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(mt, "Amount", StringComparison.OrdinalIgnoreCase))
            {
                error = "Markup Type must be Percentage or Amount.";
                return false;
            }

            request.MarkupType = string.Equals(mt, "Amount", StringComparison.OrdinalIgnoreCase) ? "Amount" : "Percentage";

            if (!TryGetDecimal(sheet, row, headerToCol, "Pharmacy Price", out var price, out var priceErr))
            {
                error = priceErr ?? "Invalid Pharmacy Price.";
                return false;
            }

            request.Price = price;

            if (!TryGetDecimal(sheet, row, headerToCol, "Markup Amount", out var markup, out var markupErr))
            {
                error = markupErr ?? "Invalid Markup Amount.";
                return false;
            }

            request.Markup = markup;

            if (!TryGetDecimal(sheet, row, headerToCol, "Wholesale Price", out var wholesale, out var wholesaleErr))
            {
                error = wholesaleErr ?? "Invalid Wholesale Price.";
                return false;
            }

            request.ComparePrice = wholesale;

            if (!TryGetDecimal(sheet, row, headerToCol, "Suggested Retail Price", out var sr, out var srErr))
            {
                error = srErr ?? "Invalid Suggested Retail Price.";
                return false;
            }

            request.SuggestedRetail = sr;

            error = string.Empty;
            return true;
        }
    }
}
