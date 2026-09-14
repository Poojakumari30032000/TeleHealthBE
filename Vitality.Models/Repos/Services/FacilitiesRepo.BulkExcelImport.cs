using ClosedXML.Excel;
using DudeMeds.Models.DTOs.Facilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Vitality.Models.DTOs.Facilities;
using Vitality.Models.Enums;
using Vitality.Models.Helpers;

namespace DudeMeds.Models.Repos.Services
{
    public partial class FacilitiesRepo
    {

        private const string BulkClinicsSheetName = "Clinics";
        private const string BulkInstructionsSheetName = "Instructions";

        private static readonly string[] BulkClinicHeaders =
        {
            "Clinic Admin Email",
            "Clinic Admin First Name",
            "Clinic Admin Last Name",
            "Clinic Name (Long)",
            "Clinic Name (Short)",
            "Clinic Admin Phone",
            "Billing",
            "Address",
            "Payment Mode"
        };

        public byte[] GenerateFacilityBulkImportTemplate()
        {
            using var wb = new XLWorkbook();

            var ws = wb.AddWorksheet(BulkClinicsSheetName);
            for (var i = 0; i < BulkClinicHeaders.Length; i++)
            {
                var cell = ws.Cell(1, i + 1);
                cell.Value = BulkClinicHeaders[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#E7EEF7");
            }

            ws.Columns(1, BulkClinicHeaders.Length).Style.NumberFormat.Format = "@";

            ws.Cell(2, 1).Value = "sam.smith@sunrise.com";
            ws.Cell(2, 2).Value = "Sam";
            ws.Cell(2, 3).Value = "Smith";
            ws.Cell(2, 4).Value = "Sunrise Family Clinic";
            ws.Cell(2, 5).Value = "Sunrise";
            ws.Cell(2, 6).Value = "5552002000";
            ws.Cell(2, 7).Value = "Billable";
            ws.Cell(2, 8).Value = "123 Main St, Miami";
            ws.Cell(2, 9).Value = "Square";

            ws.SheetView.FreezeRows(1);
            ws.Row(1).Height = 22;
            ws.Column(1).Width = 30;
            ws.Column(2).Width = 22;
            ws.Column(3).Width = 22;
            ws.Column(4).Width = 30;
            ws.Column(5).Width = 20;
            ws.Column(6).Width = 18;
            ws.Column(7).Width = 14;
            ws.Column(8).Width = 36;
            ws.Column(9).Width = 16;

            using var stream = new MemoryStream();
            wb.SaveAs(stream);
            return stream.ToArray();
        }

        public async Task<BulkImportFacilitiesExcelResponseDto> ImportFacilitiesFromExcelAsync(
            Stream excelStream,
            long userId,
            long organizationId,
            CancellationToken ct = default)
        {
            var response = new BulkImportFacilitiesExcelResponseDto();

            if (excelStream == null)
            {
                response.ParseAndValidationErrors.Add(new BulkImportIssueRowDto
                {
                    Sheet = "Workbook",
                    Row = 0,
                    Message = "No file was received. Please upload a .xlsx file."
                });
                return response;
            }

            List<ParsedClinicImportRow> rows;
            try
            {
                using var ms = new MemoryStream();
                await excelStream.CopyToAsync(ms, ct);
                ms.Position = 0;

                if (ms.Length == 0)
                {
                    response.ParseAndValidationErrors.Add(new BulkImportIssueRowDto
                    {
                        Sheet = "Workbook",
                        Row = 0,
                        Message = "The uploaded file is empty."
                    });
                    return response;
                }

                using var wb = new XLWorkbook(ms);

                if (wb.Worksheets == null || !wb.Worksheets.Any())
                {
                    response.ParseAndValidationErrors.Add(new BulkImportIssueRowDto
                    {
                        Sheet = BulkClinicsSheetName,
                        Row = 0,
                        Message = "The workbook does not contain any worksheets."
                    });
                    return response;
                }

                var sheet = wb.Worksheets.FirstOrDefault(w =>
                                string.Equals(w.Name, BulkClinicsSheetName, StringComparison.OrdinalIgnoreCase))
                            ?? wb.Worksheets.FirstOrDefault(w =>
                                !string.Equals(w.Name, BulkInstructionsSheetName, StringComparison.OrdinalIgnoreCase))
                            ?? wb.Worksheets.First();

                rows = ParseClinicsSheet(sheet, response.ParseAndValidationErrors);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Bulk clinic Excel import failed while reading workbook.");
                response.ParseAndValidationErrors.Add(new BulkImportIssueRowDto
                {
                    Sheet = "Workbook",
                    Row = 0,
                    Message = "Could not read the Excel file. Make sure it is a valid .xlsx file and try again."
                });
                return response;
            }

            response.FacilityRowsRead = rows.Count;
            if (rows.Count == 0)
            {
                if (response.ParseAndValidationErrors.Count == 0)
                {
                    response.ParseAndValidationErrors.Add(new BulkImportIssueRowDto
                    {
                        Sheet = BulkClinicsSheetName,
                        Row = 0,
                        Message = "No data rows found under the header row."
                    });
                }
                return response;
            }

            var duplicateEmails = rows
                .GroupBy(r => r.Email.ToLowerInvariant())
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToHashSet(StringComparer.Ordinal);

            HashSet<string> titleAddressSeen;
            try
            {
                var existingTitleAddress = await _db.SYS_Facilities
                    .AsNoTracking()
                    .Where(f => f.OrganizationId == organizationId)
                    .Select(f => new { f.TitleLong, f.Address })
                    .ToListAsync(ct);

                titleAddressSeen = new HashSet<string>(
                    existingTitleAddress
                        .Where(x => !string.IsNullOrWhiteSpace(x.TitleLong) && !string.IsNullOrWhiteSpace(x.Address))
                        .Select(x => FacilityBulkImportValidation.BuildTitleAddressKey(x.TitleLong!, x.Address!)),
                    StringComparer.Ordinal);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Bulk clinic import: failed to load existing clinics for dedup.");
                titleAddressSeen = new HashSet<string>(StringComparer.Ordinal);
            }

            foreach (var row in rows)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    if (duplicateEmails.Contains(row.Email.ToLowerInvariant()))
                    {
                        response.FacilitiesFailed.Add(new FacilityImportFailureDto
                        {
                            FacilityImportKey = row.Email,
                            TitleLong = row.TitleLong,
                            Message = $"Duplicate Clinic Admin Email \"{row.Email}\" appears more than once in the uploaded file."
                        });
                        continue;
                    }

                    var titleAddrKey = FacilityBulkImportValidation.BuildTitleAddressKey(row.TitleLong, row.Address);
                    if (titleAddressSeen.Contains(titleAddrKey))
                    {
                        response.FacilitiesFailed.Add(new FacilityImportFailureDto
                        {
                            FacilityImportKey = row.Email,
                            TitleLong = row.TitleLong,
                            Message =
                                "A clinic with the same name and address already exists, or this file repeats the same clinic."
                        });
                        continue;
                    }

                    var saveDto = new SaveFacilityRequestDTO
                    {
                        FacilityId = 0,
                        TitleLong = row.TitleLong,
                        TitleShort = row.TitleShort,
                        Email = row.Email,
                        Phone = row.Phone,
                        Address = row.Address,
                        ClinicAdminFirstName = row.FirstName,
                        ClinicAdminLastName = row.LastName,
                        IsBillable = row.IsBillable,
                        PaymentModeId = row.PaymentModeId,
                        BillingAddressType = "Same as Facility"
                    };

                    SaveFacilityResult saveResult;
                    try
                    {
                        saveResult = await SaveFacilityAsync(saveDto, userId, organizationId);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Bulk import: SaveFacilityAsync failed for clinic {Title} ({Email}).",
                            row.TitleLong, row.Email);
                        response.FacilitiesFailed.Add(new FacilityImportFailureDto
                        {
                            FacilityImportKey = row.Email,
                            TitleLong = row.TitleLong,
                            Message = "Clinic could not be created due to an unexpected error."
                        });
                        continue;
                    }

                    if (saveResult == null || !saveResult.IsSuccess || !saveResult.FacilityId.HasValue)
                    {
                        response.FacilitiesFailed.Add(new FacilityImportFailureDto
                        {
                            FacilityImportKey = row.Email,
                            TitleLong = row.TitleLong,
                            Message = string.IsNullOrWhiteSpace(saveResult?.Message)
                                ? "Clinic could not be created."
                                : saveResult!.Message
                        });
                        continue;
                    }

                    titleAddressSeen.Add(titleAddrKey);

                    var facilityId = saveResult.FacilityId.Value;
                    response.FacilitiesCreated++;

                    response.AdminsCreated++;
                    response.FacilitiesSucceeded.Add(new FacilityImportSuccessDto
                    {
                        FacilityImportKey = row.Email,
                        FacilityId = facilityId,
                        TitleLong = row.TitleLong,
                        AdminsCreated = 1
                    });
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Bulk import: unexpected error processing row {Row} ({Email}).",
                        row.ExcelRowNumber, row.Email);
                    response.FacilitiesFailed.Add(new FacilityImportFailureDto
                    {
                        FacilityImportKey = row.Email,
                        TitleLong = row.TitleLong,
                        Message = $"Row {row.ExcelRowNumber}: clinic could not be processed due to an unexpected error."
                    });
                }
            }

            response.ProcessingCompleted = true;
            response.ImportSucceeded = response.FacilitiesCreated > 0;
            return response;
        }

        private List<ParsedClinicImportRow> ParseClinicsSheet(
            IXLWorksheet ws,
            List<BulkImportIssueRowDto> errors)
        {
            var rows = new List<ParsedClinicImportRow>();
            var sheetName = ws?.Name ?? BulkClinicsSheetName;

            IXLRow? first;
            try
            {
                first = ws?.FirstRowUsed();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Bulk import: failed to locate the header row.");
                errors.Add(new BulkImportIssueRowDto
                {
                    Sheet = sheetName,
                    Row = 0,
                    Message = "The sheet appears to be empty or unreadable."
                });
                return rows;
            }

            if (first == null || ws == null)
            {
                errors.Add(new BulkImportIssueRowDto
                {
                    Sheet = sheetName,
                    Row = 0,
                    Message = "The sheet is empty - no header row was found."
                });
                return rows;
            }

            var headerRow = first.RowNumber();
            var headers = BuildNormalizedHeaderMap(ws, headerRow);

            var colEmail = ResolveColumn(headers, "clinicadminemail", "adminemail", "email");
            var colFirst = ResolveColumn(headers, "clinicadminfirstname", "adminfirstname", "firstname");
            var colLast = ResolveColumn(headers, "clinicadminlastname", "adminlastname", "lastname");
            var colTitleLong = ResolveColumn(headers, "clinicnamelong", "titlelong", "clinicname");
            var colTitleShort = ResolveColumn(headers, "clinicnameshort", "titleshort");
            var colPhone = ResolveColumn(headers, "clinicadminphone", "adminphone", "phone");
            var colBilling = ResolveColumn(headers, "billing", "billingtype", "billable");
            var colAddress = ResolveColumn(headers, "address");
            var colPayment = ResolveColumn(headers, "paymentmode", "paymentgateway", "payment");

            var missing = new List<string>();
            if (colEmail == null) missing.Add("Clinic Admin Email");
            if (colFirst == null) missing.Add("Clinic Admin First Name");
            if (colLast == null) missing.Add("Clinic Admin Last Name");
            if (colTitleLong == null) missing.Add("Clinic Name (Long)");
            if (colAddress == null) missing.Add("Address");

            if (missing.Count > 0)
            {
                foreach (var m in missing)
                {
                    errors.Add(new BulkImportIssueRowDto
                    {
                        Sheet = sheetName,
                        Row = headerRow,
                        Message = $"Missing required column \"{m}\" in the header row."
                    });
                }
                return rows;
            }

            int lastRow;
            try
            {
                lastRow = ws.LastRowUsed()?.RowNumber() ?? headerRow;
            }
            catch
            {
                lastRow = headerRow;
            }

            for (var r = headerRow + 1; r <= lastRow; r++)
            {
                try
                {
                    string Read(int? col) =>
                        col.HasValue ? ReadCellText(ws.Cell(r, col.Value)) : string.Empty;

                    var email = Read(colEmail);
                    var firstName = Read(colFirst);
                    var lastName = Read(colLast);
                    var titleLong = Read(colTitleLong);
                    var titleShort = Read(colTitleShort);
                    var phone = Read(colPhone);
                    var billing = Read(colBilling);
                    var address = Read(colAddress);
                    var payment = Read(colPayment);

                    if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(firstName) &&
                        string.IsNullOrWhiteSpace(lastName) && string.IsNullOrWhiteSpace(titleLong) &&
                        string.IsNullOrWhiteSpace(titleShort) && string.IsNullOrWhiteSpace(phone) &&
                        string.IsNullOrWhiteSpace(billing) && string.IsNullOrWhiteSpace(address) &&
                        string.IsNullOrWhiteSpace(payment))
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(email))
                    {
                        errors.Add(new BulkImportIssueRowDto { Sheet = sheetName, Row = r, Message = "Clinic Admin Email is required." });
                        continue;
                    }

                    if (!FacilityBulkImportValidation.IsValidEmail(email))
                    {
                        errors.Add(new BulkImportIssueRowDto { Sheet = sheetName, Row = r, Message = $"Clinic Admin Email \"{email}\" is not valid." });
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(firstName))
                    {
                        errors.Add(new BulkImportIssueRowDto { Sheet = sheetName, Row = r, Message = "Clinic Admin First Name is required." });
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(lastName))
                    {
                        errors.Add(new BulkImportIssueRowDto { Sheet = sheetName, Row = r, Message = "Clinic Admin Last Name is required." });
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(titleLong))
                    {
                        errors.Add(new BulkImportIssueRowDto { Sheet = sheetName, Row = r, Message = "Clinic Name (Long) is required." });
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(address))
                    {
                        errors.Add(new BulkImportIssueRowDto { Sheet = sheetName, Row = r, Message = "Address is required." });
                        continue;
                    }

                    if (!TryParseBilling(billing, out var isBillable, out var billingError))
                    {
                        errors.Add(new BulkImportIssueRowDto { Sheet = sheetName, Row = r, Message = billingError });
                        continue;
                    }

                    if (!TryParsePaymentMode(payment, out var paymentModeId, out var paymentError))
                    {
                        errors.Add(new BulkImportIssueRowDto { Sheet = sheetName, Row = r, Message = paymentError });
                        continue;
                    }

                    rows.Add(new ParsedClinicImportRow
                    {
                        ExcelRowNumber = r,
                        Email = email.Trim(),
                        FirstName = firstName.Trim(),
                        LastName = lastName.Trim(),
                        TitleLong = titleLong.Trim(),
                        TitleShort = string.IsNullOrWhiteSpace(titleShort) ? null : titleShort.Trim(),
                        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
                        Address = address.Trim(),
                        IsBillable = isBillable,
                        PaymentModeId = paymentModeId
                    });
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Bulk import: failed to parse row {Row}.", r);
                    errors.Add(new BulkImportIssueRowDto
                    {
                        Sheet = sheetName,
                        Row = r,
                        Message = $"Row {r} could not be read. Check for unusual cell formatting and try again."
                    });
                }
            }

            return rows;
        }

        private static string ReadCellText(IXLCell? cell)
        {
            if (cell == null)
            {
                return string.Empty;
            }

            try
            {
                var value = cell.Value;

                if (value.IsBlank)
                {
                    return string.Empty;
                }

                if (value.IsText)
                {
                    return (value.GetText() ?? string.Empty).Trim();
                }

                if (value.IsNumber)
                {
                    var number = value.GetNumber();

                    if (!double.IsNaN(number) && !double.IsInfinity(number)
                        && number >= long.MinValue && number <= long.MaxValue
                        && Math.Abs(number - Math.Floor(number)) < double.Epsilon)
                    {
                        return ((long)number).ToString(CultureInfo.InvariantCulture);
                    }

                    return number.ToString(CultureInfo.InvariantCulture);
                }

                if (value.IsDateTime)
                {
                    return value.GetDateTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                }

                if (value.IsTimeSpan)
                {
                    return value.GetTimeSpan().ToString();
                }

                if (value.IsBoolean)
                {
                    return value.GetBoolean() ? "TRUE" : "FALSE";
                }

                if (value.IsError)
                {
                    return string.Empty;
                }

                return (value.ToString() ?? string.Empty).Trim();
            }
            catch
            {

                try
                {
                    return (cell.GetString() ?? string.Empty).Trim();
                }
                catch
                {
                    try
                    {
                        return (cell.GetFormattedString() ?? string.Empty).Trim();
                    }
                    catch
                    {
                        return string.Empty;
                    }
                }
            }
        }

        private static bool TryParseBilling(string? raw, out bool isBillable, out string error)
        {
            isBillable = false;
            error = string.Empty;

            var t = (raw ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(t))
            {
                return true;
            }

            if (t.Equals("Billable", StringComparison.OrdinalIgnoreCase)
                || t.Equals("Billed", StringComparison.OrdinalIgnoreCase)
                || t.Equals("Paid", StringComparison.OrdinalIgnoreCase)
                || t.Equals("True", StringComparison.OrdinalIgnoreCase)
                || t.Equals("Yes", StringComparison.OrdinalIgnoreCase)
                || t == "1")
            {
                isBillable = true;
                return true;
            }

            if (t.Equals("Free", StringComparison.OrdinalIgnoreCase)
                || t.Equals("False", StringComparison.OrdinalIgnoreCase)
                || t.Equals("No", StringComparison.OrdinalIgnoreCase)
                || t == "0")
            {
                isBillable = false;
                return true;
            }

            error = "Billing must be \"Billable\" or \"Free\".";
            return false;
        }

        private static bool TryParsePaymentMode(string? raw, out int paymentModeId, out string error)
        {
            paymentModeId = (int)FacilityPaymentMode.Square;
            error = string.Empty;

            var t = (raw ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(t))
            {
                return true;
            }

            if (t.Equals("Square", StringComparison.OrdinalIgnoreCase) || t == "1")
            {
                paymentModeId = (int)FacilityPaymentMode.Square;
                return true;
            }

            if (t.Equals("Stripe", StringComparison.OrdinalIgnoreCase) || t == "2")
            {
                paymentModeId = (int)FacilityPaymentMode.Stripe;
                return true;
            }

            error = "Payment Mode must be \"Square\" or \"Stripe\".";
            return false;
        }

        private static string NormalizeHeader(string? raw) =>
            new string((raw ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

        private static Dictionary<string, int> BuildNormalizedHeaderMap(IXLWorksheet ws, int headerRow)
        {
            var map = new Dictionary<string, int>(StringComparer.Ordinal);

            try
            {
                foreach (var cell in ws.Row(headerRow).CellsUsed())
                {
                    var norm = NormalizeHeader(ReadCellText(cell));
                    if (!string.IsNullOrEmpty(norm) && !map.ContainsKey(norm))
                    {
                        map[norm] = cell.Address.ColumnNumber;
                    }
                }
            }
            catch
            {

            }

            return map;
        }

        private static int? ResolveColumn(Dictionary<string, int> headers, params string[] normalizedAliases)
        {
            foreach (var alias in normalizedAliases)
            {
                if (headers.TryGetValue(alias, out var col))
                {
                    return col;
                }
            }

            return null;
        }

        private sealed class ParsedClinicImportRow
        {
            public int ExcelRowNumber { get; init; }
            public string Email { get; init; } = string.Empty;
            public string FirstName { get; init; } = string.Empty;
            public string LastName { get; init; } = string.Empty;
            public string TitleLong { get; init; } = string.Empty;
            public string? TitleShort { get; init; }
            public string? Phone { get; init; }
            public string Address { get; init; } = string.Empty;
            public bool IsBillable { get; init; }
            public int PaymentModeId { get; init; }
        }
    }
}
