using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Vitality.Models.DTOs.EmpowerPharmacy;
using Vitality.Models.EntityClasses;
using Vitality.Models.Repos;
using Vitality.Models.Repos.Interfaces;

public class EmpowerPharmacyService : BaseRepo, IEmpowerPharmacyService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _cfg;

    public EmpowerPharmacyService(HttpClient http, IConfiguration cfg)
    {
        _http = http;
        _cfg = cfg;
    }

    public async Task<EmpowerOrderResultDTO> CreateOrderForPrescriptionAsync(long patientPrescriptionId)
    {

        var pres = await _db.PT_PatientPrescriptions
            .FirstOrDefaultAsync(p => p.PatientPrescriptionId == patientPrescriptionId);

        if (pres == null)
            throw new InvalidOperationException($"Prescription {patientPrescriptionId} not found.");

        var meds = await _db.PT_PrescriptionMedicines
            .AsNoTracking()
            .Where(m => m.PatientPrescriptionId == patientPrescriptionId)
            .OrderBy(m => m.PrescriptionMedicineId)
            .ToListAsync();

        if (meds.Count == 0)
            throw new InvalidOperationException("This prescription has no medicines.");

        var medIds = meds.Select(m => m.PrescriptionMedicineId).ToList();
        var supplies = await _db.Set<PT_MedicineSupply>()
            .AsNoTracking()
            .Where(s => medIds.Contains(s.PrescriptionMedicineId))
            .OrderBy(s => s.MedicineSupplyId)
            .ToListAsync();

        var suppliesByMedId = supplies
            .GroupBy(s => s.PrescriptionMedicineId)
            .ToDictionary(
                g => g.Key,
                g => g.ToList()
            );

        var patient = await _db.PT_Patients.AsNoTracking()
            .FirstOrDefaultAsync(x => x.PatientId == pres.PatientId);
        if (patient == null)
            throw new InvalidOperationException("Patient not found.");

        if (!pres.ProviderId.HasValue)
            throw new InvalidOperationException($"Prescription {patientPrescriptionId} does not have a ProviderId. Please assign a provider to this prescription.");

        var localProvider = await (from u in _db.SYS_UserDetails.AsNoTracking()
                                  where u.UserId == pres.ProviderId.Value
                                  join st in _db.SYS_States.AsNoTracking() on u.StateId equals st.Id into stj
                                  from state in stj.DefaultIfEmpty()
                                  join ct in _db.SYS_Cities.AsNoTracking() on u.CityId equals ct.Id into ctj
                                  from city in ctj.DefaultIfEmpty()
                                  select new
                                  {
                                      u.UserId,
                                      u.FirstName,
                                      u.LastName,
                                      u.NPI,
                                      u.DEA,
                                      u.License,
                                      u.Phone,
                                      u.Address,
                                      StateName = state != null ? state.Name : null,
                                      CityName = city != null ? city.Name : null,
                                      u.ZipCode,
                                      u.StateId,
                                      u.LicenseStateId
                                  }).FirstOrDefaultAsync();

        if (localProvider == null)
            throw new InvalidOperationException($"Provider with UserId={pres.ProviderId.Value} not found in SYS_UserDetails.");

        string? stateLicenseNumber = null;
        if (localProvider.LicenseStateId.HasValue)
        {
            var license = await _db.UR_ProviderStateLicenses.AsNoTracking()
                .Where(l => l.ProviderId == localProvider.UserId && l.StateId == localProvider.LicenseStateId.Value)
                .Select(l => l.StateLicense)
                .FirstOrDefaultAsync();
            stateLicenseNumber = license;
        }

        if (string.IsNullOrWhiteSpace(stateLicenseNumber))
        {
            stateLicenseNumber = localProvider.License;
        }

        if (string.IsNullOrWhiteSpace(localProvider.NPI))
            throw new InvalidOperationException($"Provider (UserId={localProvider.UserId}) must have an NPI number configured.");

        if (string.IsNullOrWhiteSpace(localProvider.FirstName) || string.IsNullOrWhiteSpace(localProvider.LastName))
            throw new InvalidOperationException($"Provider (UserId={localProvider.UserId}) must have FirstName and LastName configured.");

        string? deaNumber = null;
        if (!string.IsNullOrWhiteSpace(localProvider.DEA))
        {

            deaNumber = localProvider.DEA.Trim();

            deaNumber = string.Join(" ", deaNumber.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries));
        }

        var prescriberBlock = new
        {
            Npi = localProvider.NPI.Trim(),
            StateLicenseNumber = stateLicenseNumber?.Trim(),
            DeaNumber = deaNumber,
            LastName = localProvider.LastName.Trim(),
            FirstName = localProvider.FirstName.Trim(),
            PhoneNumber = ToN1_15(localProvider.Phone ?? "0000000000"),
            AddressLine1 = localProvider.Address ?? "Unknown",
            AddressLine2 = (string)null,
            City = localProvider.CityName ?? "Unknown",
            StateProvince = localProvider.StateName ?? "NA",
            PostalCode = localProvider.ZipCode ?? "00000",
            CountryCode = "US"
        };

        var accessToken = await GetAccessTokenAsync();

        var deliveryService = await GetDefaultShippingNameAsync(accessToken) ?? "FEDEX 2-DAY";

        var patientPhone = ToN1_15(patient.Phone ?? "0000000000");
        var patientAddress = new
        {
            AddressLine1 = string.IsNullOrWhiteSpace(patient.Address) ? "Unknown" : patient.Address,
            AddressLine2 = (string)null,
            City = await _db.SYS_Cities.AsNoTracking().Where(c => c.Id == patient.CityId).Select(c => c.Name).FirstOrDefaultAsync() ?? "Unknown",
            StateProvince = await _db.SYS_States.AsNoTracking().Where(s => s.Id == patient.StateId).Select(s => s.Name).FirstOrDefaultAsync() ?? "NA",
            PostalCode = patient.Zipcode ?? "00000",
            CountryCode = "US"
        };
        var dob = patient.DOB ?? new DateTime(1990, 1, 1);

        var clientOrderId = GenerateClientOrderId($"rx_{patient.PatientId}_pres_{pres.PatientPrescriptionId}_");

        var writtenDate = pres.PrescritionDate ?? pres.CreatedDate ?? DateTime.UtcNow;
        var lastVisit = (pres.PrescritionDate ?? patient.ModifiedDate ?? patient.CreatedDate ?? DateTime.UtcNow);

        bool hasControlledSubstance = false;
        foreach (var med in meds)
        {
            PD_Drug? drug = null;
            if (med.DrugId.HasValue)
            {
                drug = await _db.PD_Drugs.AsNoTracking()
                        .FirstOrDefaultAsync(d => d.DrugId == med.DrugId.Value);
            }
            bool isControlled = drug?.ControlSubstance == true || drug?.Control_Substance == true;
            if (isControlled)
            {
                hasControlledSubstance = true;
                break;
            }
        }

        if (hasControlledSubstance && string.IsNullOrWhiteSpace(prescriberBlock.DeaNumber))
        {
            throw new InvalidOperationException(
                "This prescription contains controlled substances and requires a DEA number. " +
                $"Please ensure the prescriber has a DEA number configured in the local provider record " +
                $"(SYS_UserDetails.DEA for ProviderId={pres.ProviderId}). " +
                "The DEA number is mandatory for controlled substance prescriptions.");
        }

        string? prescriptionImageBase64 = null;
        if (hasControlledSubstance)
        {

            string? imageToUse = !string.IsNullOrWhiteSpace(pres.ControlledSubImage)
                ? pres.ControlledSubImage
                : pres.PresImage;

            if (!string.IsNullOrWhiteSpace(imageToUse))
            {
                prescriptionImageBase64 = await ResolvePrescriptionImageBase64Async(imageToUse);

                if (string.IsNullOrWhiteSpace(prescriptionImageBase64))
                {

                    bool isS3Url = !string.IsNullOrWhiteSpace(imageToUse) &&
                                   (imageToUse.Contains("s3.amazonaws.com") || imageToUse.Contains(".s3."));

                    var imageFieldName = !string.IsNullOrWhiteSpace(pres.ControlledSubImage)
                        ? "ControlledSubImage"
                        : "PresImage";

                    var errorMessage = isS3Url
                        ? $"Controlled substance detected but prescription image ({imageFieldName}) could not be downloaded from S3. " +
                          "Please ensure: (1) The S3 bucket policy allows public read access, (2) The file URL is correct and accessible. " +
                          $"Image URL: {imageToUse}"
                        : $"Controlled substance detected but prescription image ({imageFieldName}) could not be resolved. " +
                          "Please ensure the image file exists and is accessible.";

                    throw new InvalidOperationException(errorMessage);
                }
            }
            else
            {
                throw new InvalidOperationException(
                    "This prescription contains controlled substances and requires a prescription image. " +
                    "Please upload an image using ControlledSubImage or PresImage field.");
            }
        }

        var newRxs = new List<object>();
        var lineSummaries = new List<EmpowerOrderLineDTO>();

        foreach (var med in meds)
        {
            PD_Drug? drug = null;
            if (med.DrugId.HasValue)
            {
                drug = await _db.PD_Drugs.AsNoTracking()
                        .FirstOrDefaultAsync(d => d.DrugId == med.DrugId.Value);
            }

            var itemDesignatorId = drug?.ItemDesignatorID?.Trim();

            if (string.IsNullOrWhiteSpace(itemDesignatorId) && med.DrugId.HasValue)
            {
                var mapSection = _cfg.GetSection("EmpowerPharmacy:DrugMap");
                if (mapSection.Exists())
                {
                    itemDesignatorId = mapSection[med.DrugId.Value.ToString()]?.Trim();
                }
            }

            if (string.IsNullOrWhiteSpace(itemDesignatorId))
            {
                itemDesignatorId = med.ItemDesignatorID?.Trim();
            }

            if (string.IsNullOrWhiteSpace(itemDesignatorId))
            {
                throw new InvalidOperationException(
                    $"ItemDesignatorId not found for PrescriptionMedicineId={med.PrescriptionMedicineId} (DrugId={med.DrugId}). " +
                    $"Please set ItemDesignatorID in PD_Drug table or configure in EmpowerPharmacy:DrugMap in appsettings.json.");
            }

            var drugDesc = !string.IsNullOrWhiteSpace(med.MedicineName) ? med.MedicineName : (drug?.Name ?? "MEDICINE");
            var quantity = ParseIntSafe(med.Quantity) ?? 1;
            var refills = drug?.Refills ?? 0;
            var daysSupply = ParseIntSafe(med.DaysSupplies) ?? 0;

            string? genderValue = MapGenderToString(patient.Gender);

            string? ResolveEssentialCopy(string itemDesignatorId)
            {

                var essentialCopySection = _cfg.GetSection($"EmpowerPharmacy:EssentialCopyMap:{itemDesignatorId}");
                if (essentialCopySection.Exists() && !string.IsNullOrWhiteSpace(essentialCopySection.Value))
                {
                    return essentialCopySection.Value.Trim();
                }

                return null;
            }

            object CreateNewRxEntry(string medItemDesignatorId, string medDrugDescription, string medQuantity, string medRefills, string medDaysSupply, string medNote, string medSigText, string? clientPrescriptionId = null, bool isSupply = false)
            {

                var essentialCopy = ResolveEssentialCopy(medItemDesignatorId);

                return new
                {
                    Patient = new
                    {
                        ClientPatientId = (string)null,
                        LastName = patient.LastName ?? "Unknown",
                        FirstName = patient.FirstName ?? "Unknown",
                        Gender = genderValue,
                        DateOfBirth = dob,
                        Address = patientAddress,
                        PhoneNumber = patientPhone,
                        Email = patient.Email ?? (string)null
                    },
                    Prescriber = new
                    {
                        NPI = prescriberBlock.Npi,
                        StateLicenseNumber = prescriberBlock.StateLicenseNumber,
                        DeaNumber = prescriberBlock.DeaNumber,
                        LastName = prescriberBlock.LastName,
                        FirstName = prescriberBlock.FirstName,
                        Address = new
                        {
                            AddressLine1 = prescriberBlock.AddressLine1,
                            AddressLine2 = prescriberBlock.AddressLine2,
                            City = prescriberBlock.City,
                            StateProvince = prescriberBlock.StateProvince,
                            PostalCode = prescriberBlock.PostalCode,
                            CountryCode = prescriberBlock.CountryCode
                        },
                        PhoneNumber = prescriberBlock.PhoneNumber
                    },
                    Medication = new
                    {
                        ItemDesignatorId = medItemDesignatorId,
                        ClientPrescriptionId = clientPrescriptionId,
                        EssentialCopy = essentialCopy,
                        DrugDescription = medDrugDescription,
                        Quantity = medQuantity,
                        Refills = medRefills,
                        DaysSupply = medDaysSupply,
                        WrittenDate = writtenDate,
                        Diagnosis = new
                        {
                            ClinicalInformationQualifier = (string)null,
                            Primary = new
                            {
                                Code = "Z76.89",
                                Qualifier = (string)null,
                                Description = "Encounter for other specified special examinations",
                                DateOfLastOfficeVisit = new
                                {
                                    Date = (DateTime?)null,
                                    DateTime = lastVisit
                                }
                            }
                        },
                        Note = medNote,
                        SigText = medSigText
                    }
                };
            }

            newRxs.Add(CreateNewRxEntry(
                itemDesignatorId,
                drugDesc,
                quantity.ToString(),
                refills.ToString(),
                daysSupply.ToString(),
                TrimNote(med.Instruction, 210),
                !string.IsNullOrWhiteSpace(med.Direction) ? med.Direction : "Use as directed.",
                clientPrescriptionId: med.PrescriptionMedicineId.ToString()
            ));

            var medicineSupplies = suppliesByMedId.TryGetValue(med.PrescriptionMedicineId, out var supplyList)
                ? supplyList
                : new List<PT_MedicineSupply>();

            foreach (var supply in medicineSupplies.Where(s => !string.IsNullOrWhiteSpace(s.SupplyItemDesignatorID)))
            {
                var supplyItemDesignatorId = supply.SupplyItemDesignatorID.Trim();
                var supplyQuantity = ParseIntSafe(supply.SupplyQuantity) ?? 1;
                var supplyDescription = !string.IsNullOrWhiteSpace(supply.Name)
                    ? supply.Name.Trim()
                    : (!string.IsNullOrWhiteSpace(supply.SupplyDesc)
                        ? supply.SupplyDesc.Trim()
                        : "Supply Item");

                newRxs.Add(CreateNewRxEntry(
                    supplyItemDesignatorId,
                    supplyDescription,
                    supplyQuantity.ToString(),
                    "0",
                    daysSupply.ToString(),
                    TrimNote($"Supply for {drugDesc}", 210),
                    "Use as directed.",
                    clientPrescriptionId: supply.MedicineSupplyId.ToString()
                ));
            }

            lineSummaries.Add(new EmpowerOrderLineDTO
            {
                PrescriptionMedicineId = med.PrescriptionMedicineId,
                MedicineName = med.MedicineName,
                ItemDesignatorIdUsed = itemDesignatorId,
                Quantity = quantity,
                DaysSupply = daysSupply,
                Refills = refills
            });
        }

        var payload = new
        {
            ClientOrderId = clientOrderId,
            PONumber = $"PO_{patient.PatientId}_{pres.PatientPrescriptionId}",
            DeliveryService = deliveryService,
            AllowOverrideDeliveryService = true,
            AllowOverrideEssentialCopyGuidance = true,
            PrescriptionImageBase64 = prescriptionImageBase64,
            PrescriptionPdfBase64 = (string)null,
            LFPracticeId = (int?)null,
            NewRxs = newRxs,
            ReferenceFields = new
            {
                Reference1 = (string)null,
                Reference2 = (string)null,
                Reference3 = (string)null,
                Reference4 = (string)null,
                Reference5 = (string)null
            }
        };

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://eip-prod.azurewebsites.net/newrx/easyrx");
        req.Headers.Add("Token", accessToken);
        req.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var resp = await _http.SendAsync(req);
        var body = await resp.Content.ReadAsStringAsync();

        var result = new EmpowerOrderResultDTO
        {
            PrescriptionId = pres.PatientPrescriptionId,
            Success = resp.IsSuccessStatusCode,
            StatusCode = (int)resp.StatusCode,
            ResponseBody = body,
            ClientOrderId = clientOrderId,
            DeliveryServiceUsed = deliveryService,
            Lines = lineSummaries
        };

        if (resp.IsSuccessStatusCode)
        {
            try
            {

                var responseObj = JsonSerializer.Deserialize<EasyRxResponse>(body, JsonOptions);
                var eipOrderId = responseObj?.EipOrderId;

                var empowerOrder = new Sys_EmpowerOrder
                {
                    PatientPrescriptionId = pres.PatientPrescriptionId,
                    PatientId = patient.PatientId,
                    FacilityId = pres.FacilityId,
                    ClientOrderId = clientOrderId,
                    EipOrderId = eipOrderId,
                    OrderStatus = "Received",
                    OrderStatusLastUpdatedTime = DateTime.UtcNow,
                    CreatedDate = DateTime.UtcNow,
                    IsActive = true
                };
                _db.Sys_EmpowerOrders.Add(empowerOrder);
                await _db.SaveChangesAsync();

                pres.PrescriptionStatus = "Sent";
                pres.ModifiedDate = DateTime.UtcNow;
                await _db.SaveChangesAsync();

                PT_PatientOrder? orderToUpdate = null;
                if (pres.PatientOrderId.HasValue)
                {
                    orderToUpdate = await _db.PT_PatientOrders
                        .FirstOrDefaultAsync(o => o.PatientOrderId == pres.PatientOrderId.Value);
                }

                if (orderToUpdate == null && pres.PatientTreatmentId.HasValue)
                {
                    orderToUpdate = await _db.PT_PatientOrders
                        .Where(o => o.PatientTreamentId == pres.PatientTreatmentId.Value)
                        .OrderByDescending(o => o.CreatedDate)
                        .FirstOrDefaultAsync();
                }

                if (orderToUpdate != null)
                {
                    orderToUpdate.OrderStatus = "Sent";
                    orderToUpdate.ModifiedDate = DateTime.UtcNow;
                    await _db.SaveChangesAsync();
                    result.PatientOrderIdUpdated = orderToUpdate.PatientOrderId;
                    result.OrderStatusAfterUpdate = orderToUpdate.OrderStatus;
                }
            }
            catch (Exception ex)
            {

                System.Diagnostics.Debug.WriteLine($"Error saving Empower order to database: {ex.Message}");
            }
        }

        return result;
    }

    private async Task<string?> ResolvePrescriptionImageBase64Async(string? presImage)
    {
        if (string.IsNullOrWhiteSpace(presImage))
            return null;

        var value = presImage.Trim();

        try
        {

            var base64MarkerIndex = value.IndexOf("base64,", StringComparison.OrdinalIgnoreCase);
            if (base64MarkerIndex >= 0)
            {
                var base64Part = value[(base64MarkerIndex + "base64,".Length)..];

                Convert.FromBase64String(base64Part);
                return base64Part.Trim();
            }

            if (IsLikelyBase64(value))
            {
                Convert.FromBase64String(value);
                return value;
            }

            if (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
                (uri.Scheme == "http" || uri.Scheme == "https"))
            {

                if (uri.Host == "localhost" || uri.Host == "127.0.0.1" || uri.IsLoopback)
                {

                    var urlPath = uri.AbsolutePath.TrimStart('/');

                    System.Diagnostics.Debug.WriteLine($"Processing localhost URL. Extracted path: '{urlPath}'");

                    var candidatePaths = BuildPossibleImagePaths(urlPath);
                    foreach (var path in candidatePaths)
                    {
                        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                        {
                            System.Diagnostics.Debug.WriteLine($"Found image at: {path}");
                            var bytes = await File.ReadAllBytesAsync(path);
                            return Convert.ToBase64String(bytes);
                        }
                    }

                    var decodedPath = Uri.UnescapeDataString(urlPath);
                    if (decodedPath != urlPath)
                    {
                        System.Diagnostics.Debug.WriteLine($"Trying URL-decoded path: '{decodedPath}'");
                        var decodedPaths = BuildPossibleImagePaths(decodedPath);
                        foreach (var path in decodedPaths)
                        {
                            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                            {
                                System.Diagnostics.Debug.WriteLine($"Found image at: {path}");
                                var bytes = await File.ReadAllBytesAsync(path);
                                return Convert.ToBase64String(bytes);
                            }
                        }
                    }

                    var fileName = Path.GetFileName(decodedPath);
                    if (!string.IsNullOrWhiteSpace(fileName) && fileName != decodedPath)
                    {
                        System.Diagnostics.Debug.WriteLine($"Trying just filename: '{fileName}'");
                        var fileNamePaths = BuildPossibleImagePaths(fileName);
                        foreach (var path in fileNamePaths)
                        {
                            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                            {
                                System.Diagnostics.Debug.WriteLine($"Found image at: {path}");
                                var bytes = await File.ReadAllBytesAsync(path);
                                return Convert.ToBase64String(bytes);
                            }
                        }
                    }

                    System.Diagnostics.Debug.WriteLine($"Could not find image file for localhost URL: {value}");

                    return null;
                }

                try
                {

                    var encodedUri = new UriBuilder(uri) { Path = Uri.EscapeUriString(uri.AbsolutePath) }.Uri;

                    bool isS3Url = uri.Host.Contains("s3.amazonaws.com") || uri.Host.Contains(".s3.");

                    System.Diagnostics.Debug.WriteLine($"Attempting to download image from URL: {encodedUri}");
                    if (isS3Url)
                    {
                        System.Diagnostics.Debug.WriteLine($"Detected S3 URL. Ensure bucket policy allows public read access.");
                    }

                    var bytes = await _http.GetByteArrayAsync(encodedUri);
                    System.Diagnostics.Debug.WriteLine($"Successfully downloaded {bytes.Length} bytes from URL: {encodedUri}");
                    return Convert.ToBase64String(bytes);
                }
                catch (HttpRequestException ex)
                {

                    bool isS3Url = uri.Host.Contains("s3.amazonaws.com") || uri.Host.Contains(".s3.");
                    if (isS3Url)
                    {
                        System.Diagnostics.Debug.WriteLine($"S3 URL download failed for {value}. Error: {ex.Message}");
                        System.Diagnostics.Debug.WriteLine($"This may indicate the S3 bucket policy is not configured for public read access, or the file path is incorrect.");

                        return null;
                    }
                    else
                    {

                        System.Diagnostics.Debug.WriteLine($"HTTP download failed for {value}: {ex.Message}");
                    }
                }
                catch (Exception ex)
                {

                    bool isS3Url = uri.Host.Contains("s3.amazonaws.com") || uri.Host.Contains(".s3.");
                    if (isS3Url)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error downloading image from S3 URL {value}: {ex.Message}");
                        return null;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Error downloading image from {value}: {ex.Message}");
                    }
                }
            }

            var filePaths = BuildPossibleImagePaths(value);
            foreach (var path in filePaths)
            {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    var bytes = await File.ReadAllBytesAsync(path);
                    return Convert.ToBase64String(bytes);
                }
            }
        }
        catch (Exception ex)
        {

            System.Diagnostics.Debug.WriteLine($"Error resolving prescription image: {ex.Message}. Image value: {value}");
            System.Diagnostics.Debug.WriteLine($"Exception type: {ex.GetType().Name}, StackTrace: {ex.StackTrace}");
            return null;
        }

        System.Diagnostics.Debug.WriteLine($"Could not resolve prescription image. Tried value: {value}");
        return null;
    }

    private static bool IsLikelyBase64(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length % 4 != 0)
            return false;

        const string base64Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/=";
        foreach (var c in value)
        {
            if (!base64Chars.Contains(c))
                return false;
        }

        return true;
    }

    private static IEnumerable<string> BuildPossibleImagePaths(string value)
    {
        var paths = new List<string>();

        var cleanValue = value;
        var queryIndex = cleanValue.IndexOf('?');
        if (queryIndex >= 0)
        {
            cleanValue = cleanValue.Substring(0, queryIndex);
        }

        try
        {
            cleanValue = Uri.UnescapeDataString(cleanValue);
        }
        catch
        {

        }

        if (Path.IsPathRooted(cleanValue))
        {
            paths.Add(cleanValue);
        }
        else
        {
            var trimmed = cleanValue.TrimStart('/', '\\');

            var possibleBaseDirs = new List<string>();

            possibleBaseDirs.Add(AppContext.BaseDirectory);

            try
            {
                possibleBaseDirs.Add(Directory.GetCurrentDirectory());
            }
            catch { }

            try
            {
                var binDir = AppContext.BaseDirectory;
                var parent = Directory.GetParent(binDir);
                if (parent != null)
                {
                    possibleBaseDirs.Add(parent.FullName);

                    var grandParent = parent.Parent;
                    if (grandParent != null)
                    {
                        possibleBaseDirs.Add(grandParent.FullName);
                    }
                }
            }
            catch { }

            foreach (var baseDir in possibleBaseDirs.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(baseDir) || !Directory.Exists(baseDir))
                    continue;

                paths.Add(Path.Combine(baseDir, cleanValue));
                paths.Add(Path.Combine(baseDir, trimmed));
                paths.Add(Path.Combine(baseDir, "wwwroot", trimmed));
                paths.Add(Path.Combine(baseDir, "wwwroot", "UploadImages", trimmed));

                var fileName = Path.GetFileName(trimmed);
                if (!string.IsNullOrWhiteSpace(fileName) && fileName != trimmed)
                {
                    paths.Add(Path.Combine(baseDir, "wwwroot", "UploadImages", fileName));
                }
            }

            if (cleanValue != value)
            {
                var origTrimmed = value.TrimStart('/', '\\');
                foreach (var baseDir in possibleBaseDirs.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrWhiteSpace(baseDir) || !Directory.Exists(baseDir))
                        continue;

                    paths.Add(Path.Combine(baseDir, "wwwroot", origTrimmed));
                    paths.Add(Path.Combine(baseDir, "wwwroot", "UploadImages", origTrimmed));

                    var origFileName = Path.GetFileName(origTrimmed);
                    if (!string.IsNullOrWhiteSpace(origFileName) && origFileName != origTrimmed)
                    {
                        paths.Add(Path.Combine(baseDir, "wwwroot", "UploadImages", origFileName));
                    }
                }
            }
        }

        var distinctPaths = paths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        System.Diagnostics.Debug.WriteLine($"Trying to find image '{value}' in {distinctPaths.Count} locations:");
        foreach (var path in distinctPaths)
        {
            var exists = File.Exists(path);
            System.Diagnostics.Debug.WriteLine($"  - {path} (Exists: {exists})");
        }

        return distinctPaths;
    }

    private string? MapGenderToString(string? gender)
    {
        if (string.IsNullOrWhiteSpace(gender))
            return null;

        var genderLower = gender.Trim().ToLowerInvariant();

        if (genderLower == "male" || genderLower == "m")
            return "M";
        if (genderLower == "female" || genderLower == "f")
            return "F";

        return null;
    }

    private static int? ParseIntSafe(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        if (int.TryParse(s, out var n)) return n;
        return null;
    }

    private static string? TrimNote(string? text, int max = 210)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var collapsed = string.Join(" ", text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)).Trim();
        return collapsed.Length <= max ? collapsed : collapsed.Substring(0, max);
    }

    private static string ToN1_15(string raw)
    {
        var digits = new string((raw ?? "").Where(char.IsDigit).ToArray());
        if (string.IsNullOrEmpty(digits)) digits = "0000000000";
        return digits.Length <= 15 ? digits : digits[^15..];
    }

    private static string GenerateClientOrderId(string prefix = "test_")
    {
        var ts = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmssfff");
        var suffix = Guid.NewGuid().ToString("N")[..6];
        return $"{prefix}{ts}-{suffix}";
    }

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private async Task<string> GetAccessTokenAsync()
    {
        var apiKey = _cfg["EmpowerPharmacy:ApiKey"] ?? throw new InvalidOperationException("EmpowerPharmacy:ApiKey missing.");
        var apiSecret = _cfg["EmpowerPharmacy:ApiSecret"] ?? throw new InvalidOperationException("EmpowerPharmacy:ApiSecret missing.");
        var url = _cfg["EmpowerPharmacy:TokenUrl"] ?? "https://eip-prod.azurewebsites.net/gettoken/post";

        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Add("APIKey", apiKey);
        req.Headers.Add("APISecret", apiSecret);

        using var resp = await _http.SendAsync(req);
        var body = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
            throw new Exception($"Token failed {(int)resp.StatusCode}: {body}");

        var token = JsonSerializer.Deserialize<GetTokenResponse>(body, JsonOptions)?.Token;
        if (string.IsNullOrWhiteSpace(token))
            throw new Exception("Token not returned in response.");

        return token;
    }

    private async Task<string?> GetDefaultShippingNameAsync(string token)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "https://eip-prod.azurewebsites.net/ShippingTypes");
        req.Headers.Add("Token", token);

        using var resp = await _http.SendAsync(req);
        var body = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode) return null;

        var env = JsonSerializer.Deserialize<ShippingTypesEnvelope>(body, JsonOptions);
        return env?.ShippingTypeItems?.FirstOrDefault()?.Name?.Trim();
    }

    public async Task<List<string>> GetShippingTypeNamesAsync()
    {
        var token = await GetAccessTokenAsync();

        using var req = new HttpRequestMessage(HttpMethod.Get, "https://eip-prod.azurewebsites.net/ShippingTypes");
        req.Headers.Add("Token", token);

        using var resp = await _http.SendAsync(req);
        var body = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
            throw new Exception($"Failed to fetch shipping types: {(int)resp.StatusCode} {resp.ReasonPhrase}. Body: {body}");

        var env = JsonSerializer.Deserialize<ShippingTypesEnvelope>(body, JsonOptions);

        var names = env?.ShippingTypeItems?
            .Select(x => (x?.Name ?? string.Empty).Trim())
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n)
            .ToList() ?? new List<string>();

        return names;
    }

    private async Task<PrescriberEnvelopeItem?> GetDefaultPrescriberAsync(string token)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "https://eip-prod.azurewebsites.net/prescribers");
        req.Headers.Add("Token", token);

        using var resp = await _http.SendAsync(req);
        var body = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode) return null;

        var env = JsonSerializer.Deserialize<PrescribersEnvelope>(body, JsonOptions);
        return env?.Data?.FirstOrDefault();
    }

    private sealed class GetTokenResponse
    {
        [JsonPropertyName("token")] public string Token { get; set; } = "";
    }

    private sealed class ShippingTypesEnvelope
    {
        [JsonPropertyName("shippingTypeItems")] public List<ShippingTypeItem>? ShippingTypeItems { get; set; }
    }
    private sealed class ShippingTypeItem
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
    }

    private sealed class PrescribersEnvelope
    {
        [JsonPropertyName("data")] public List<PrescriberEnvelopeItem>? Data { get; set; }
    }
    private sealed class PrescriberEnvelopeItem
    {
        [JsonPropertyName("npi")] public string? Npi { get; set; }
        [JsonPropertyName("stateLicenseNumber")] public string? StateLicenseNumber { get; set; }
        [JsonPropertyName("deaNumber")] public string? DeaNumber { get; set; }
        [JsonPropertyName("lastName")] public string? LastName { get; set; }
        [JsonPropertyName("firstName")] public string? FirstName { get; set; }
        [JsonPropertyName("phoneNumber")] public string? PhoneNumber { get; set; }
        [JsonPropertyName("address")] public PrescriberAddress? Address { get; set; }
    }
    private sealed class PrescriberAddress
    {
        [JsonPropertyName("addressLine1")] public string? AddressLine1 { get; set; }
        [JsonPropertyName("addressLine2")] public string? AddressLine2 { get; set; }
        [JsonPropertyName("city")] public string? City { get; set; }
        [JsonPropertyName("stateProvince")] public string? StateProvince { get; set; }
        [JsonPropertyName("postalCode")] public string? PostalCode { get; set; }
        [JsonPropertyName("countryCode")] public string? CountryCode { get; set; }
    }

    private sealed class EasyRxResponse
    {
        [JsonPropertyName("clientOrderId")] public string? ClientOrderId { get; set; }
        [JsonPropertyName("eipOrderId")] public int? EipOrderId { get; set; }
        [JsonPropertyName("note")] public string? Note { get; set; }
    }
}
