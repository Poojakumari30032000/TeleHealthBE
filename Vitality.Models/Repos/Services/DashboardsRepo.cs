using AutoMapper;
using Dapper;
using Vitality.Models.DTOs.DashBooards;
using System.Data;
using Vitality.Models.Repos.Interfaces;
using Vitality.Models.EntityClasses;
using Microsoft.EntityFrameworkCore;
using Vitality.Models.Enums;

namespace Vitality.Models.Repos.Services
{
    public class DashboardsRepo : BaseRepo , IDashboardsRepo
    {
        private readonly IMapper _mapper;
        public DashboardsRepo(IMapper mapper)
        {
            _mapper = mapper;
        }
        public async Task<List<GetAllDashboardTilesResponseDTO>> GetAllDashboardTilesAsync(GetAllDashboardTilesRequestDTO request)
        {
            var tiles = new List<GetAllDashboardTilesResponseDTO>();

            if (request.RoleId == 2)
            {
                return await GetGlobalAdminTilesAsync();
            }
            else if (request.RoleId == 6 && request.UserId.HasValue)
            {
                return await GetPatientTilesAsync(request.UserId.Value, request.FacilityId);
            }
            else if (request.FacilityId.HasValue)
            {
                return await GetFacilityAdminTilesAsync(request.FacilityId.Value);
            }

            return tiles;
        }

        private async Task<List<GetAllDashboardTilesResponseDTO>> GetGlobalAdminTilesAsync()
        {
            var tiles = new List<GetAllDashboardTilesResponseDTO>();
            var currentMonth = DateTime.UtcNow;
            var now = DateTime.UtcNow;

            var clinics = await _db.SYS_Facilities
                .CountAsync(f => f.IsActive == true && (f.Status == "Active" || f.Status == null));

            var monthStart = new DateTime(currentMonth.Year, currentMonth.Month, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);
            var monthlyRevenue = await _db.Sys_Invoices
                .Where(i => i.IsActive == true
                    && i.InvoiceType == Vitality.Models.Enums.InvoiceType.PatientToClinic.ToString()
                    && i.CreatedDate.HasValue
                    && i.CreatedDate.Value >= monthStart
                    && i.CreatedDate.Value <= monthEnd)
                .SumAsync(i => i.Amount ?? 0m);

            var activeTreatments = await _db.PT_PatientTreatments
                .CountAsync(t => t.IsActive == true);

            var monthlyProfits = await _db.Sys_Invoices
                .Where(i => i.IsActive == true
                    && i.InvoiceType == Vitality.Models.Enums.InvoiceType.ClinicToGlobal.ToString()
                    && i.CreatedDate.HasValue
                    && i.CreatedDate.Value >= monthStart
                    && i.CreatedDate.Value <= monthEnd)
                .SumAsync(i => i.Amount ?? 0m);

            var refillRequests = await _db.PT_PatientTreatments
                .CountAsync(t => t.IsActive == true && t.Refill == true);

            var pendingIntakes = await _db.PT_PatientTreatments
                .CountAsync(t => t.IsActive == true &&
                    !_db.PT_PatientTreatmentInTakeForms
                        .Any(f => f.PatientTreatmentId == t.PatientTreatmentId &&
                                 !string.IsNullOrWhiteSpace(f.Answer)));

            var candidateAppointments = await _db.PT_PatientAppointmentSlots
                .AsNoTracking()
                .Where(a => a.IsActive == true &&
                            a.StartDate.HasValue &&
                            a.StartDate.Value.Date >= now.Date)
                .Select(a => new
                {
                    a.StartDate,
                    a.StartTime
                })
                .ToListAsync();

            var upcomingAppointments = candidateAppointments
                .Count(a => a.StartDate.HasValue &&
                            a.StartDate.Value.Date.Add(a.StartTime) > now);

            tiles.Add(new GetAllDashboardTilesResponseDTO { Index = 1, TileName = "Clinics", Value = clinics.ToString() });
            tiles.Add(new GetAllDashboardTilesResponseDTO { Index = 2, TileName = "Monthly Revenue (Clinics)", Value = monthlyRevenue.ToString("F2") });
            tiles.Add(new GetAllDashboardTilesResponseDTO { Index = 3, TileName = "Total Active Treatments", Value = activeTreatments.ToString() });
            tiles.Add(new GetAllDashboardTilesResponseDTO { Index = 4, TileName = "Monthly Profits", Value = monthlyProfits.ToString("F2") });
            tiles.Add(new GetAllDashboardTilesResponseDTO { Index = 5, TileName = "Refill Requests", Value = refillRequests.ToString() });
            tiles.Add(new GetAllDashboardTilesResponseDTO { Index = 6, TileName = "Pending Intakes", Value = pendingIntakes.ToString() });
            tiles.Add(new GetAllDashboardTilesResponseDTO { Index = 7, TileName = "Upcoming Appointments", Value = upcomingAppointments.ToString() });

            return tiles;
        }

        private async Task<List<GetAllDashboardTilesResponseDTO>> GetFacilityAdminTilesAsync(long facilityId)
        {
            var tiles = new List<GetAllDashboardTilesResponseDTO>();

            var totalProviders = await (from uif in _db.FC_UsersInFacilities
                                 join ud in _db.SYS_UserDetails on uif.UserId equals ud.UserId
                                 join login in _db.SYS_Logins on ud.LoginId equals login.LoginId
                                 where uif.FacilityId == facilityId
                                    && uif.IsAssign == true
                                    && ud.IsActive == true
                                    && login.RoleId == 4
                                 select uif.UserId).Distinct().CountAsync();

            var totalPatients = await _db.PT_Patients
                .CountAsync(p => p.FacilityId == facilityId && p.IsActive == true);

            var drugsSold = await (from prescription in _db.PT_PatientPrescriptions
                                   join prescriptionMedicine in _db.PT_PrescriptionMedicines on prescription.PatientPrescriptionId equals prescriptionMedicine.PatientPrescriptionId
                                   where prescription.IsActive == true
                                      && prescription.FacilityId == facilityId
                                      && prescriptionMedicine.DrugId.HasValue
                                   select prescriptionMedicine.DrugId.Value).CountAsync();

            var packagesSold = await (from invoice in _db.Sys_Invoices
                                     where invoice.IsActive == true
                                        && invoice.FacilityId == facilityId
                                        && invoice.InvoiceType == Vitality.Models.Enums.InvoiceType.PatientToClinic.ToString()
                                        && invoice.PatientId.HasValue
                                        && invoice.CreatedDate.HasValue
                                     join treatment in _db.PT_PatientTreatments on invoice.PatientId equals treatment.PatientId
                                     join bundle in _db.PD_Bundles on treatment.ProductId equals bundle.BundleId
                                     where treatment.IsActive == true
                                        && treatment.FacilityId == facilityId
                                        && treatment.ProductId.HasValue
                                        && bundle.IsActive == true
                                        && treatment.CreatedDate.HasValue

                                        && treatment.CreatedDate.Value >= invoice.CreatedDate.Value.AddDays(-1)
                                        && treatment.CreatedDate.Value <= invoice.CreatedDate.Value.AddDays(1)
                                     select bundle.BundleId).CountAsync();

            tiles.Add(new GetAllDashboardTilesResponseDTO { Index = 1, TileName = "Total Providers", Value = totalProviders.ToString() });
            tiles.Add(new GetAllDashboardTilesResponseDTO { Index = 2, TileName = "Total Patients", Value = totalPatients.ToString() });
            tiles.Add(new GetAllDashboardTilesResponseDTO { Index = 3, TileName = "Drugs Sold", Value = drugsSold.ToString() });
            tiles.Add(new GetAllDashboardTilesResponseDTO { Index = 4, TileName = "Packages Sold", Value = packagesSold.ToString() });

            return tiles;
        }

        private async Task<List<GetAllDashboardTilesResponseDTO>> GetPatientTilesAsync(long userId, long? facilityId)
        {
            var tiles = new List<GetAllDashboardTilesResponseDTO>();

            var patient = new PT_Patient();

                var userDetail = await _db.SYS_UserDetails.FirstOrDefaultAsync(u => u.UserId == userId);
                if (userDetail != null && userDetail.LoginId.HasValue)
                {
                    patient = await _db.PT_Patients.FirstOrDefaultAsync(p => p.LoginId == userDetail.LoginId.Value);
                }

            if (patient == null) return tiles;

            var patientId = patient.PatientId;

            var activeTreatments = await _db.PT_PatientTreatments
                .CountAsync(t => t.PatientId == patientId && t.IsActive == true);

            var currentUtcTime = DateTime.UtcNow;
            var currentUtcDate = currentUtcTime.Date;

            var candidateAppointments = await _db.PT_PatientAppointmentSlots
                .Where(a => a.PatientId == patientId
                    && a.IsActive == true
                    && a.StartDate.HasValue
                    && a.StartDate.Value.Date >= currentUtcDate)
                .ToListAsync();

            var upcomingAppointment = candidateAppointments
                .Where(a => {
                    if (a.StartDate == null) return false;

                    var appointmentDateTime = a.StartDate.Value.Date.Add(a.StartTime);
                    return appointmentDateTime > currentUtcTime;
                })
                .OrderBy(a => {

                    return a.StartDate!.Value.Date.Add(a.StartTime);
                })
                .FirstOrDefault();

            var nextShipping = await _db.PT_PatientOrders
                .Where(o => o.PatientId == patientId
                    && o.IsActive == true
                    && o.ShippedDate.HasValue
                    && o.ShippedDate.Value >= DateTime.UtcNow.Date)
                .OrderBy(o => o.ShippedDate)
                .FirstOrDefaultAsync();

            var nextRefill = await _db.PT_PatientTreatments
                .Where(t => t.PatientId == patientId
                    && t.IsActive == true
                    && t.ExpiryDate.HasValue
                    && t.ExpiryDate.Value >= DateTime.UtcNow.Date)
                .OrderBy(t => t.ExpiryDate)
                .FirstOrDefaultAsync();

            tiles.Add(new GetAllDashboardTilesResponseDTO
            {
                Index = 1,
                TileName = "Active Treatments",
                Value = activeTreatments.ToString()
            });

            tiles.Add(new GetAllDashboardTilesResponseDTO
            {
                Index = 2,
                TileName = "Upcoming Appointment",
                Value = upcomingAppointment?.StartDate?.ToString("yyyy-MM-dd") ?? "N/A"
            });

            tiles.Add(new GetAllDashboardTilesResponseDTO
            {
                Index = 3,
                TileName = "Next Shipping Date",
                Value = nextShipping?.ShippedDate?.ToString("yyyy-MM-dd") ?? "N/A"
            });

            var refillName = "N/A";
            if (nextRefill != null && nextRefill.ProductId.HasValue)
            {
                var bundle = await _db.PD_Bundles.FirstOrDefaultAsync(b => b.BundleId == nextRefill.ProductId.Value);
                refillName = bundle?.Name ?? "N/A";
            }

            tiles.Add(new GetAllDashboardTilesResponseDTO
            {
                Index = 4,
                TileName = "Next Refill",
                Value = refillName
            });

            return tiles;
        }

        public async Task<(string[] Months, decimal[] RevenueData, decimal[] SubscriptionData)> GetRevenueAndNewSubscriptionsGrowthAsync()
        {
            List<string> monthsList = new List<string>();
            List<decimal> revenueList = new List<decimal>();
            List<decimal> subscriptionList = new List<decimal>();

            var currentDate = DateTime.Now;
            for (int i = 0; i < 6; i++)
            {
                var targetDate = currentDate.AddMonths(-i);
                var monthNameWithYear = targetDate.ToString("MMMM yyyy");
                monthsList.Insert(0, monthNameWithYear);

                var month = targetDate.Month;
                var year = targetDate.Year;
                var subscriptions = await _db.SYS_Subscriptions
                    .Where(x => x.CreatedDate.Month == month && x.CreatedDate.Year == year && x.IsActive == true && x.Status == "Active")
                    .ToListAsync();

                decimal revenue = subscriptions.Sum(s => s.MonthlyPrice ?? 0m);
                decimal newSubscriptions = subscriptions.Count;

                revenueList.Insert(0, revenue);
                subscriptionList.Insert(0, newSubscriptions);
            }

            string[] months = monthsList.ToArray();
            decimal[] revenueData = revenueList.ToArray();
            decimal[] subscriptionData = subscriptionList.ToArray();

            return (months, revenueData, subscriptionData);
        }

        public async Task<List<object>> GetPaymentStatusPieChartAsync(DateTime? startDate, DateTime? endDate)
        {
            List<object> data = new List<object>();

            var end = endDate ?? DateTime.UtcNow;
            var start = startDate ?? end.AddMonths(-1);

            var invoices = await _db.Sys_Invoices
                .Where(x => x.IsActive == true
                    && x.InvoiceType == Vitality.Models.Enums.InvoiceType.ClinicToGlobal.ToString()
                    && x.CreatedDate.HasValue
                    && x.CreatedDate.Value >= start
                    && x.CreatedDate.Value <= end)
                .ToListAsync();

            var paidAmount = invoices
                .Where(x => x.Status == Vitality.Models.Enums.InvoiceStatus.Paid.ToString())
                .Sum(x => x.Amount ?? 0m);

            var pendingAmount = invoices
                .Where(x => x.Status == Vitality.Models.Enums.InvoiceStatus.Pending.ToString())
                .Sum(x => x.Amount ?? 0m);

            data.Add(new { value = paidAmount, name = "Paid" });
            data.Add(new { value = pendingAmount, name = "Pending" });

            return data;
        }

        public async Task<List<object>> GetClinicStatusPieChartAsync()
        {
            List<object> data = new List<object>();

            var currentDate = DateTime.Now;
            var sixMonthsAgo = currentDate.AddMonths(-6);

            var clinic = await _db.SYS_Facilities
                .Where(x => x.CreatedDate >= sixMonthsAgo && x.CreatedDate <= currentDate)
                .GroupBy(p => p.Status)
                .Select(g => new
                {
                    Value = g.Count(),
                    Name = g.Key == "Active" ? "Active Clinics" :
                    g.Key == "InActive" ? "Inactive Clinics" :
                    "Unknown"
                })
                .ToListAsync();

            data.Add(new { value = clinic.FirstOrDefault(p => p.Name == "Active Clinics")?.Value ?? 0, name = "Active Clinics" });
            data.Add(new { value = clinic.FirstOrDefault(p => p.Name == "Inactive Clinics")?.Value ?? 0, name = "Inactive Clinics" });

            return (data);
        }

        public async Task<List<object>> GetTreatmentTypePieChartAsync()
        {
            List<object> data = new List<object>();

            var patientTreatments = await _db.PT_PatientTreatments
                .Where(x => x.IsActive == true)
                .Join(_db.PD_Drugs,
                    pt => pt.ProductId,
                    pd => pd.ProductId,
                    (pt, pd) => new { pt, pd.CategoryId })
                .GroupBy(x => x.CategoryId)
                .Select(g => new
                {
                    Value = g.Count(),
                    Name = g.Key == 1 ? "Hair Loss" :
                    g.Key == 2 ? "Weight Loss" :
                    g.Key == 2 ? "Anti Aging" :
                    g.Key == 2 ? "Testosterone" :
                    "Unknown"
                })
                .ToListAsync();

            data.Add(new { value = patientTreatments.FirstOrDefault(p => p.Name == "Hair Loss")?.Value ?? 0, name = "Hair Loss" });
            data.Add(new { value = patientTreatments.FirstOrDefault(p => p.Name == "Weight Loss")?.Value ?? 0, name = "Weight Loss" });
            data.Add(new { value = patientTreatments.FirstOrDefault(p => p.Name == "Anti Aging")?.Value ?? 0, name = "Anti Aging" });
            data.Add(new { value = patientTreatments.FirstOrDefault(p => p.Name == "Testosterone")?.Value ?? 0, name = "Testosterone" });

            return data;
        }

        public async Task<(List<object> Series, List<string> Days)> GetEarningsAsync(long? facilityId, DateTime? startDate, DateTime? endDate)
        {

            var end = endDate.HasValue
                ? endDate.Value.Date.AddDays(1).AddMilliseconds(-1)
                : DateTime.UtcNow.Date.AddDays(1).AddMilliseconds(-1);

            var start = startDate.HasValue
                ? startDate.Value.Date
                : end.AddMonths(-6).Date;

            var invoicesQuery = _db.Sys_Invoices
                .AsNoTracking()
                .Where(i => i.IsActive == true
                    && i.InvoiceType == Vitality.Models.Enums.InvoiceType.PatientToClinic.ToString()
                    && i.CreatedDate.HasValue
                    && i.CreatedDate.Value >= start
                    && i.CreatedDate.Value <= end);

            if (facilityId.HasValue)
            {
                invoicesQuery = invoicesQuery.Where(i => i.FacilityId == facilityId.Value);
            }

            var invoices = await invoicesQuery
                .Select(i => new
                {
                    InvoiceDate = i.CreatedDate.Value,
                    Amount = i.Amount ?? 0m
                })
                .ToListAsync();

            var currentDate = end;
            var daysList = new List<string>();
            var earningsData = new List<decimal>();

            for (int i = 5; i >= 0; i--)
            {
                var targetDate = currentDate.AddMonths(-i);
                var monthName = targetDate.ToString("MMM");
                daysList.Add(monthName);

                var monthStart = new DateTime(targetDate.Year, targetDate.Month, 1);
                var monthEnd = monthStart.AddMonths(1).AddDays(-1);

                var monthEarnings = invoices
                    .Where(inv => inv.InvoiceDate.Date >= monthStart && inv.InvoiceDate.Date <= monthEnd)
                    .Sum(inv => inv.Amount);

                earningsData.Add(monthEarnings);
            }

            var series = new List<object>
            {
                new
                {
                    name = "Total Earning",
                    data = earningsData.Select(d => (object)d).ToArray()
                }
            };

            return (series, daysList);
        }

        public async Task<(List<object> Series, List<string> Months)> GetAppointmentsAsync(long? facilityId, DateTime? startDate, DateTime? endDate)
        {

            var end = endDate ?? DateTime.UtcNow.Date;
            var start = startDate ?? end.AddMonths(-6).Date;

            var appointmentsQuery = _db.PT_PatientAppointmentSlots
                .Where(a => a.IsActive == true
                    && a.StartDate.HasValue
                    && a.StartDate.Value.Date >= start
                    && a.StartDate.Value.Date <= end);

            if (facilityId.HasValue)
            {
                appointmentsQuery = appointmentsQuery.Where(a => a.FacilityId == facilityId.Value);
            }

            var appointments = await appointmentsQuery
                .Select(a => new
                {
                    Year = a.StartDate.Value.Year,
                    Month = a.StartDate.Value.Month,
                    Status = a.Status ?? "Unknown"
                })
                .ToListAsync();

            var currentDate = end;
            var monthsList = new List<string>();
            var pendingData = new List<int>();
            var completedData = new List<int>();
            var canceledData = new List<int>();

            for (int i = 5; i >= 0; i--)
            {
                var targetDate = currentDate.AddMonths(-i);
                var monthName = targetDate.ToString("MMM");
                monthsList.Add(monthName);

                var year = targetDate.Year;
                var month = targetDate.Month;

                var monthAppointments = appointments
                    .Where(a => a.Year == year && a.Month == month)
                    .ToList();

                var monthPending = monthAppointments
                    .Count(a => a.Status == "Scheduled" || a.Status == "Confirmed");

                var monthCompleted = monthAppointments
                    .Count(a => NormalizeAppointmentStatus(a.Status) == "Completed");

                var monthCanceled = monthAppointments
                    .Count(a => a.Status == "Missed" || a.Status == "Canceled" || a.Status == "Cancelled");

                pendingData.Add(monthPending);
                completedData.Add(monthCompleted);
                canceledData.Add(monthCanceled);
            }

            var series = new List<object>
            {
                new
                {
                    name = "Scheduled",
                    data = pendingData.Select(d => (object)d).ToArray()
                },
                new
                {
                    name = "Completed",
                    data = completedData.Select(d => (object)d).ToArray()
                },
                new
                {
                    name = "Missed",
                    data = canceledData.Select(d => (object)d).ToArray()
                }
            };

            return (series, monthsList);
        }

        public async Task<List<object>> GetPendingPrescriptionsAsync(long? facilityId)
        {
            var query = _db.PT_PatientPrescriptions
                .Where(p => p.IsActive == true
                    && (p.PrescriptionStatus == "Pending" || p.PrescriptionStatus == "pending"));

            if (facilityId.HasValue)
            {
                query = query.Where(p => p.FacilityId == facilityId.Value);
            }

            var prescriptions = await query
                .Join(_db.PT_Patients,
                    pres => pres.PatientId,
                    pat => pat.PatientId,
                    (pres, pat) => new { pres, pat })
                .Join(_db.PD_Bundles,
                    pp => pp.pres.ProductId,
                    bundle => bundle.BundleId,
                    (pp, bundle) => new
                    {
                        Id = pp.pres.PatientPrescriptionId,
                        Status = pp.pres.PrescriptionStatus,
                        Name = bundle.Name ?? "Unknown",
                        Patient = (pp.pat.FirstName ?? "") + " " + (pp.pat.LastName ?? ""),
                        Date = pp.pres.CreatedDate ?? pp.pres.PrescritionDate ?? DateTime.UtcNow
                    })
                .OrderByDescending(x => x.Date)
                .ToListAsync();

            return prescriptions.Select(p => new
            {
                id  = p.Id,
                status = p.Status,
                name = p.Name,
                patient = p.Patient,
                date = p.Date.ToString("yyyy-MM-dd")
            } as object).ToList();
        }

        public async Task<(List<string> Months, List<decimal> RevenueData)> GetMonthlyRevenueByMonthsAsync(DateTime? startDate, DateTime? endDate)
        {
            var end = endDate ?? DateTime.UtcNow;
            var start = startDate ?? end.AddMonths(-6);

            var invoices = await _db.Sys_Invoices
                .Where(x => x.IsActive == true
                    && x.InvoiceType == Vitality.Models.Enums.InvoiceType.PatientToClinic.ToString()
                    && x.CreatedDate.HasValue
                    && x.CreatedDate.Value >= start
                    && x.CreatedDate.Value <= end)
                .ToListAsync();

            var monthsList = new List<string>();
            var revenueList = new List<decimal>();

            var currentDate = start;
            while (currentDate <= end)
            {
                var monthStart = new DateTime(currentDate.Year, currentDate.Month, 1);
                var monthEnd = monthStart.AddMonths(1).AddDays(-1);
                if (monthEnd > end) monthEnd = end;

                var monthRevenue = invoices
                    .Where(x => x.CreatedDate.HasValue
                        && x.CreatedDate.Value >= monthStart
                        && x.CreatedDate.Value <= monthEnd)
                    .Sum(x => x.Amount ?? 0m);

                monthsList.Add(currentDate.ToString("MMM"));
                revenueList.Add(monthRevenue);

                currentDate = currentDate.AddMonths(1);
            }

            return (monthsList, revenueList);
        }

        public async Task<List<object>> GetMonthlyTreatmentCategoryPieChartAsync(DateTime? startDate, DateTime? endDate)
        {

            var end = endDate.HasValue
                ? endDate.Value.Date.AddDays(1).AddMilliseconds(-1)
                : DateTime.UtcNow.Date.AddDays(1).AddMilliseconds(-1);

            var start = startDate.HasValue
                ? startDate.Value.Date
                : new DateTime(end.Year, end.Month, 1);

            var treatments = await _db.PT_PatientTreatments
                .Where(t => t.IsActive == true
                    && t.CreatedDate.HasValue
                    && t.CreatedDate.Value >= start
                    && t.CreatedDate.Value <= end)
                .Join(_db.PD_Bundles,
                    t => t.ProductId,
                    b => b.BundleId,
                    (t, b) => new { t, b.CategoryId })
                .Join(_db.PD_Categories,
                    tb => tb.CategoryId,
                    c => c.CategoryId,
                    (tb, c) => new { CategoryName = c.CategoryName ?? "Unknown" })
                .GroupBy(x => x.CategoryName)
                .Select(g => new
                {
                    value = g.Count(),
                    name = g.Key
                })
                .ToListAsync();

            return treatments.Select(t => new { value = t.value, name = t.name } as object).ToList();
        }

        public async Task<int> GetActiveProvidersCountAsync()
        {
            return await _db.SYS_UserDetails
                .AsNoTracking()
                .Where(ud => ud.IsActive == true
                    && _db.SYS_Logins
                        .AsNoTracking()
                        .Any(ln => ln.LoginId == ud.LoginId && ln.RoleId == 4))
                .CountAsync();
        }

        public async Task<List<object>> GetMedicationHistoryAsync(long userId)
        {

            var patient = new PT_Patient();
            var userDetail = await _db.SYS_UserDetails.FirstOrDefaultAsync(u => u.UserId == userId);
            if (userDetail != null && userDetail.LoginId.HasValue)
            {
                patient = await _db.PT_Patients.FirstOrDefaultAsync(p => p.LoginId == userDetail.LoginId.Value);
            }

            if (patient == null) return new List<object>();

            var patientId = patient.PatientId;
            var facilityId = patient.FacilityId;

            var ordersQuery = from o in _db.PT_PatientOrders
                             where o.PatientId == patientId && o.IsActive == true
                             join t in _db.PT_PatientTreatments on o.PatientTreamentId equals t.PatientTreatmentId into tj
                             from t in tj.DefaultIfEmpty()
                             join b in _db.PD_Bundles on o.ProductId equals b.BundleId into bj
                             from b in bj.DefaultIfEmpty()
                             select new
                             {
                                 Order = o,
                                 Treatment = t,
                                 Bundle = b
                             };

            var ordersData = await ordersQuery
                .OrderByDescending(x => x.Order.CreatedDate)
                .Take(20)
                .ToListAsync();

            var treatmentLookupByProduct = new Dictionary<long, PT_PatientTreatment>();
            var ordersNeedingTreatment = ordersData
                .Where(x => x.Treatment == null && x.Order.ProductId.HasValue)
                .Select(x => x.Order.ProductId!.Value)
                .Distinct()
                .ToList();

            if (ordersNeedingTreatment.Any())
            {
                var treatments = await _db.PT_PatientTreatments
                    .Where(t => t.PatientId == patientId &&
                               t.ProductId.HasValue &&
                               ordersNeedingTreatment.Contains(t.ProductId.Value) &&
                               t.IsActive == true)
                    .OrderByDescending(t => t.CreatedDate)
                    .ThenByDescending(t => t.PatientTreatmentId)
                    .ToListAsync();

                foreach (var treatment in treatments)
                {
                    if (treatment.ProductId.HasValue && !treatmentLookupByProduct.ContainsKey(treatment.ProductId.Value))
                    {
                        treatmentLookupByProduct[treatment.ProductId.Value] = treatment;
                    }
                }
            }

            var bundleIds = ordersData
                .Where(x => x.Bundle != null)
                .Select(x => x.Bundle!.BundleId)
                .Distinct()
                .ToList();

            var facilityBundlePrices = new Dictionary<long, decimal>();
            if (facilityId.HasValue && bundleIds.Any())
            {
                var prices = await _db.PD_FacilityBundlePrices
                    .Where(fbp => fbp.FacilityId == facilityId.Value && bundleIds.Contains(fbp.BundleId))
                    .ToListAsync();

                foreach (var price in prices)
                {
                    facilityBundlePrices[price.BundleId] = price.ClinicPrice;
                }
            }

            var groupedData = ordersData
                .Select(o =>
                {
                    var bundle = o.Bundle;
                    var treatment = o.Treatment;
                    var order = o.Order;

                    if (treatment == null && order.ProductId.HasValue &&
                        treatmentLookupByProduct.TryGetValue(order.ProductId.Value, out var foundTreatment))
                    {
                        treatment = foundTreatment;
                    }

                    decimal price = 0m;
                    if (bundle != null && facilityId.HasValue && facilityBundlePrices.TryGetValue(bundle.BundleId, out var facilityPrice))
                    {
                        price = facilityPrice;
                    }
                    else if (bundle != null && bundle.Price.HasValue)
                    {
                        price = bundle.Price.Value;
                    }
                    else if (order.OrderTotal.HasValue)
                    {
                        price = order.OrderTotal.Value;
                    }

                    return new
                    {
                        patientTreatmentId = treatment != null ? treatment.PatientTreatmentId : (long?)null,
                        packageName = bundle != null ? bundle.Name : "Unknown",
                        duration =
                            bundle != null && bundle.visits.HasValue && bundle.visits.Value > 0
                                ? $"{bundle.visits.Value} Months"
                                : (treatment != null && treatment.RecurringDurationMonths.HasValue && treatment.RecurringDurationMonths.Value > 0
                                    ? $"{treatment.RecurringDurationMonths.Value} Months"
                                    : "N/A"),
                        price = price,
                        orderDate = order.CreatedDate,
                        status = !string.IsNullOrWhiteSpace(treatment?.TreatmentStatus)
                            ? treatment!.TreatmentStatus
                            : order.OrderStatus
                    };
                })
                .Where(x => x.patientTreatmentId.HasValue)
                .GroupBy(x => x.patientTreatmentId.Value)
                .Select(g => g.OrderByDescending(x => x.orderDate).First())
                .Select(x => new
                {
                    patientTreatmentId = x.patientTreatmentId,
                    packageName = x.packageName,
                    duration = x.duration,
                    price = x.price,
                    status = x.status
                } as object)
                .ToList();

            return groupedData;
        }

        public async Task<List<object>> GetPaymentHistoryAsync(long userId)
        {

            var patient = new PT_Patient();

            var userDetail = await _db.SYS_UserDetails.FirstOrDefaultAsync(u => u.UserId == userId);
            if (userDetail != null && userDetail.LoginId.HasValue)
            {
                patient = await _db.PT_Patients.FirstOrDefaultAsync(p => p.LoginId == userDetail.LoginId.Value);
            }

            if (patient == null) return new List<object>();

            var patientId = patient.PatientId;
            var payments = await _db.Sys_Invoices
                .Where(p => p.PatientId == patientId && p.InvoiceType == InvoiceType.PatientToClinic.ToString() && p.IsActive == true)
                .OrderByDescending(p => p.CreatedDate)
                .Take(20)
                .Select(p => new
                {
                    InvoiceNo = p.InvoiceId.ToString(),
                    Date = p.CreatedDate ?? DateTime.UtcNow,
                    Amount = p.Amount ?? 0m,
                    Status = p.Status
                })
                .ToListAsync();

            return payments.Select(p => new
            {
                invoiceNo = p.InvoiceNo,
                date = p.Date,
                amount = p.Amount,
                status = p.Status
            } as object).ToList();
        }

        public async Task<List<GetAllDashboardTilesResponseDTO>> GetProviderSummaryAsync(long userId, long roleId)
        {
            var tiles = new List<GetAllDashboardTilesResponseDTO>();

            var pid = userId;

            var activePatients = await _db.PT_Patients
                .AsNoTracking()
                .Where(pt => pt.IsActive == true
                    && _db.PT_PatientAppointmentSlots
                        .AsNoTracking()
                        .Any(a => a.IsActive == true
                            && a.PatientId == pt.PatientId
                            && a.ProviderId == pid)
                   )
                .CountAsync();

            var activeTreatments = await _db.PT_PatientTreatments
                .AsNoTracking()
                .CountAsync(t => t.IsActive == true
                    && t.TreatmentStatus == "Active"
                    && _db.PT_PatientAppointmentSlots
                        .AsNoTracking()
                        .Any(a => a.IsActive == true
                            && a.PatientTreatmentId == t.PatientTreatmentId
                            && a.ProviderId == pid));

            var pendingPrescriptions = await _db.PT_PatientPrescriptions
                .AsNoTracking()
                .CountAsync(p => p.IsActive == true
                    && (p.PrescriptionStatus == "Pending" || p.PrescriptionStatus == "pending")
                    && p.ProviderId == pid);

            var pendingAppointments = await _db.PT_PatientAppointmentSlots
                .AsNoTracking()
                .CountAsync(a => a.IsActive == true
                    && (a.Status == "Scheduled" || a.Status == "Pending")
                    && a.ProviderScheduledSlotId != null);

            tiles.Add(new GetAllDashboardTilesResponseDTO
            {
                Index = 1,
                TileName = "Active Patients",
                Value = activePatients.ToString()
            });

            tiles.Add(new GetAllDashboardTilesResponseDTO
            {
                Index = 2,
                TileName = "Active Treatments",
                Value = activeTreatments.ToString()
            });

            tiles.Add(new GetAllDashboardTilesResponseDTO
            {
                Index = 3,
                TileName = "Pending Prescription Upload",
                Value = pendingPrescriptions.ToString()
            });

            tiles.Add(new GetAllDashboardTilesResponseDTO
            {
                Index = 4,
                TileName = "Pending Appointments",
                Value = pendingAppointments.ToString()
            });

            return tiles;
        }

        public async Task<List<object>> GetDailyAppointmentsAsync(long userId, DateTime date, int? clientTimezoneOffsetMinutes)
        {
            var localDate = date.Date;
            var utcWindowStart = clientTimezoneOffsetMinutes.HasValue
                ? localDate.AddMinutes(-clientTimezoneOffsetMinutes.Value)
                : localDate;
            var utcWindowEnd = utcWindowStart.AddDays(1);

            var candidates = await _db.PT_PatientAppointmentSlots
                .Where(a => a.IsActive == true
                    && a.ProviderId == userId
                    && a.StartDate.HasValue
                    && a.StartDate.Value.Date >= utcWindowStart.Date.AddDays(-1)
                    && a.StartDate.Value.Date <= utcWindowEnd.Date.AddDays(1))
                .Join(_db.PT_Patients,
                    a => a.PatientId,
                    p => p.PatientId,
                    (a, p) => new { a, p })
                .Join(_db.SYS_UserDetails,
                    ap => ap.a.ProviderId,
                    ud => ud.UserId,
                    (ap, ud) => new
                    {
                        PatientName = (ap.p.FirstName ?? "") + " " + (ap.p.LastName ?? ""),
                        DoctorName = (ud.FirstName ?? "") + " " + (ud.LastName ?? ""),
                        Category = "General",
                        StartDate = ap.a.StartDate,
                        StartTime = ap.a.StartTime,
                        Status = NormalizeAppointmentStatus(ap.a.Status)
                    })
                .ToListAsync();

            var appointments = candidates
                .Select(a =>
                {
                    var startUtc = a.StartDate!.Value.Date.Add(a.StartTime);
                    var startLocal = clientTimezoneOffsetMinutes.HasValue
                        ? startUtc.AddMinutes(clientTimezoneOffsetMinutes.Value)
                        : CommonMethods.CommonMethods.ToLocalTime(startUtc);

                    return new
                    {
                        a.PatientName,
                        a.DoctorName,
                        a.Category,
                        LocalStart = startLocal,
                        a.Status
                    };
                })
                .Where(a => a.LocalStart.Date == localDate)
                .OrderBy(a => a.LocalStart)
                .ToList();

            return appointments.Select(a => new
            {
                patientName = a.PatientName,
                doctorName = a.DoctorName,
                category = a.Category,
                date = a.LocalStart.ToString("yyyy-MM-dd"),
                time = a.LocalStart.ToString("hh:mm tt"),
                status = a.Status
            } as object).ToList();
        }

        private static string NormalizeAppointmentStatus(string? status)
        {
            var s = (status ?? string.Empty).Trim();
            if (s.Equals("Completed", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("Done", StringComparison.OrdinalIgnoreCase))
                return "Completed";

            if (s.Equals("Missed", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("Cancelled", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("Canceled", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("Declined", StringComparison.OrdinalIgnoreCase))
                return "Missed";

            return "Scheduled";
        }

        public async Task<int> GetTotalPatientsAsync(long userId, DateTime? startDate, DateTime? endDate)
        {

            var pid = userId;

            var baseQuery = _db.PT_Patients
                .AsNoTracking()
                .Where(pt => pt.IsActive == true
                    && _db.PT_PatientAppointmentSlots
                        .AsNoTracking()
                        .Any(a => a.IsActive == true
                            && a.PatientId == pt.PatientId
                            && a.ProviderId == pid));

            if (startDate.HasValue)
            {
                baseQuery = baseQuery.Where(pt => pt.CreatedDate.HasValue
                    && pt.CreatedDate.Value.Date >= startDate.Value.Date);
            }

            if (endDate.HasValue)
            {
                var endDateInclusive = endDate.Value.Date.AddDays(1);
                baseQuery = baseQuery.Where(pt => pt.CreatedDate.HasValue
                    && pt.CreatedDate.Value.Date < endDateInclusive);
            }

            return await baseQuery.CountAsync();
        }

        public async Task<(List<string> Months, List<int> PatientCounts)> GetPatientCountsAsync(long userId, DateTime? startDate, DateTime? endDate)
        {

            var end = endDate ?? DateTime.UtcNow;
            var start = startDate ?? end.AddMonths(-6);
            var pid = userId;

            var patients = await _db.PT_Patients
                .AsNoTracking()
                .Where(pt => pt.IsActive == true
                    && pt.CreatedDate.HasValue
                    && pt.CreatedDate.Value >= start
                    && pt.CreatedDate.Value <= end
                    && _db.PT_PatientAppointmentSlots
                        .AsNoTracking()
                        .Any(a => a.IsActive == true
                            && a.PatientId == pt.PatientId
                            && a.ProviderId == pid))
                .Select(pt => new
                {
                    Year = pt.CreatedDate.Value.Year,
                    Month = pt.CreatedDate.Value.Month,
                    PatientId = pt.PatientId
                })
                .ToListAsync();

            var monthsList = new List<string>();
            var patientCounts = new List<int>();

            var currentDate = start;
            while (currentDate <= end)
            {
                var year = currentDate.Year;
                var month = currentDate.Month;

                var uniquePatients = patients
                    .Where(p => p.Year == year && p.Month == month)
                    .Select(p => p.PatientId)
                    .Distinct()
                    .Count();

                monthsList.Add(currentDate.ToString("MMM"));
                patientCounts.Add(uniquePatients);

                currentDate = currentDate.AddMonths(1);
            }

            return (monthsList, patientCounts);
        }

        public async Task<List<object>> GetTreatmentDistributionAsync(long userId)
        {

            var pid = userId;

            var treatments = await _db.PT_PatientTreatments
                .AsNoTracking()
                .Where(t => t.IsActive == true
                    && _db.PT_PatientAppointmentSlots
                        .AsNoTracking()
                        .Any(a => a.IsActive == true
                            && a.PatientTreatmentId == t.PatientTreatmentId
                            && a.ProviderId == pid))
                .Join(_db.PD_Bundles,
                    t => t.ProductId,
                    b => b.BundleId,
                    (t, b) => new { b.CategoryId })
                .Join(_db.PD_Categories,
                    tb => tb.CategoryId,
                    c => c.CategoryId,
                    (tb, c) => new { CategoryName = c.CategoryName ?? "Unknown" })
                .GroupBy(x => x.CategoryName)
                .Select(g => new
                {
                    value = g.Count(),
                    name = g.Key
                })
                .ToListAsync();

            return treatments.Select(t => new { value = t.value, name = t.name } as object).ToList();
        }
    }
}
