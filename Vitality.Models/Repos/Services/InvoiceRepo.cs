using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Square.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Vitality.Models;
using Vitality.Models.DTOs.Brands;
using Vitality.Models.DTOs.Invoices;
using Vitality.Models.DTOs.Square;
using Vitality.Models.EntityClasses;
using Vitality.Models.Enums;
using Vitality.Models.Repos.Interfaces;
using DudeMeds.Models.DTOs.PatientOrders;
using Vitality.Models.Repos.Services.Audit;
using Vitality.Models.CommonMethods;

namespace Vitality.Models.Repos.Services
{
    public class InvoiceRepo : BaseRepo, IInvoiceRepo
    {
        private readonly IMapper _mapper;
        private readonly ISquarePaymentRepo _squarPaymentRepo;
        private readonly IAuditService _auditService;
        private readonly INotificationService _notificationService;
        private readonly FacilityStatusService _facilityStatusService;
        private readonly IStripePlatformCharge? _stripePlatformCharge;
        private readonly IStripeAccountResolver? _stripeAccountResolver;
        private readonly IStripeRecurringCharge? _stripeRecurringCharge;

        public InvoiceRepo(IMapper mapper, ISquarePaymentRepo squarPaymentRepo, IAuditService auditService, INotificationService notificationService, IStripePlatformCharge? stripePlatformCharge = null, IStripeAccountResolver? stripeAccountResolver = null, IStripeRecurringCharge? stripeRecurringCharge = null)
        {
            _mapper = mapper;
            _squarPaymentRepo = squarPaymentRepo;
            _auditService = auditService;
            _notificationService = notificationService;
            _facilityStatusService = new FacilityStatusService(_db);
            _stripePlatformCharge = stripePlatformCharge;
            _stripeAccountResolver = stripeAccountResolver;
            _stripeRecurringCharge = stripeRecurringCharge;
        }

        private int ResolveFacilityPaymentMode(long facilityId)
        {
            var mode = _db.SYS_Facilities.AsNoTracking()
                .Where(f => f.FacilityId == facilityId)
                .Select(f => (int?)f.PaymentModeId)
                .FirstOrDefault();
            return mode ?? (int)FacilityPaymentMode.Square;
        }

        private static long? GetSquareChargeFacilityId(Sys_Invoice invoice)
        {
            if (invoice.InvoiceType == InvoiceType.ClinicToGlobal.ToString() ||
                invoice.InvoiceType == InvoiceType.GAToClinic.ToString())
                return null;
            return invoice.FacilityId;
        }

        public GetInvoiceByIdResponseDTO GetInvoiceById(long? invoiceId)
        {
            if (!invoiceId.HasValue) return new GetInvoiceByIdResponseDTO();

            var rawInvoice =
                (from inv in _db.Sys_Invoices.AsNoTracking()
                 where inv.InvoiceId == (int)invoiceId.Value
                 join c in _db.SYS_UserCards.AsNoTracking()
                     on inv.CardId equals c.CardId into cards
                 from c in cards.DefaultIfEmpty()
                 join u in _db.SYS_UserDetails.AsNoTracking()
                     on c.UserId equals u.UserId into users
                 from u in users.DefaultIfEmpty()
                 join iu in _db.SYS_UserDetails.AsNoTracking()
                    on inv.UserId equals iu.UserId into invoiceUsers
                 from iu in invoiceUsers.DefaultIfEmpty()
                 join p in _db.PT_Patients.AsNoTracking()
                    on inv.PatientId equals p.PatientId into patients
                 from p in patients.DefaultIfEmpty()
                 join f in _db.SYS_Facilities.AsNoTracking()
                    on inv.FacilityId equals f.FacilityId into facilities
                 from f in facilities.DefaultIfEmpty()
                 join city in _db.SYS_Cities.AsNoTracking()
                    on (f.BillingCityId ?? f.CityId) equals city.Id into cities
                 from city in cities.DefaultIfEmpty()
                 join state in _db.SYS_States.AsNoTracking()
                    on (f.BillingStateId ?? f.StateId) equals state.Id into states
                 from state in states.DefaultIfEmpty()
                 join country in _db.SYS_Countries.AsNoTracking()
                    on state.CountryId equals country.Id into countries
                 from country in countries.DefaultIfEmpty()
                 select new
                 {
                     InvoiceId = (long)inv.InvoiceId,
                     InvoiceNumber = inv.InvoiceNumber,
                     CustomerName = inv.CustomerName,
                     Amount = inv.Amount,
                     Status = inv.Status,
                     InvoiceType = inv.InvoiceType,
                     IsActive = inv.IsActive,
                     CreatedBy = inv.CreatedBy,
                     CreatedDate = CommonMethods.CommonMethods.ToLocalTime(inv.CreatedDate),
                     SubscriptionId = inv.SubscriptionId,
                     CardId = inv.CardId,

                     Last4 = c != null ? c.Last4 : null,
                     CardBrand = c != null ? c.CardBrand : null,
                     CardHolderName = c != null ? c.CardHolderName : null,
                     Currency = c != null ? c.Currency : null,
                     ExpirationMonth = c != null ? (c.ExpirationMonth == null ? null : c.ExpirationMonth.ToString()) : null,
                     ExpirationYear = c != null ? (c.ExpirationYear == null ? null : c.ExpirationYear.ToString()) : null,
                     DueDate = CommonMethods.CommonMethods.ToLocalTime(inv.CreatedDate.HasValue ? inv.CreatedDate.Value.AddDays(29) : (DateTime?)null),

                     Email =
                        (from admin in _db.SYS_UserDetails.AsNoTracking()
                         join login in _db.SYS_Logins.AsNoTracking() on admin.LoginId equals login.LoginId
                         where c != null
                               && c.UserId.HasValue
                               && admin.UserId == c.UserId.Value
                               && login.RoleId == 3
                         select !string.IsNullOrWhiteSpace(admin.Email) ? admin.Email : login.Email).FirstOrDefault()

                        ?? (from uf in _db.FC_UsersInFacilities.AsNoTracking()
                            join admin in _db.SYS_UserDetails.AsNoTracking() on uf.UserId equals admin.UserId
                            join login in _db.SYS_Logins.AsNoTracking() on admin.LoginId equals login.LoginId
                            where inv.FacilityId.HasValue
                                  && uf.FacilityId == inv.FacilityId.Value
                                  && uf.IsAssign == true
                                  && admin.IsActive == true
                                  && admin.Status == "Active"
                                  && login.RoleId == 3
                            select !string.IsNullOrWhiteSpace(admin.Email) ? admin.Email : login.Email).FirstOrDefault()
                        ?? (iu != null && !string.IsNullOrWhiteSpace(iu.Email)
                            ? iu.Email
                            : (p != null && !string.IsNullOrWhiteSpace(p.Email) ? p.Email : (u != null ? u.Email : null))),
                     FacilityPhone = f != null ? f.Phone : null,
                     FacilityAddressLine = f != null ? (string.IsNullOrWhiteSpace(f.BillingAddress) ? f.Address : f.BillingAddress) : null,
                     FacilityCity = city != null ? city.Name : null,
                     FacilityZipCode = f != null ? (!string.IsNullOrWhiteSpace(f.BillingZipCode) ? f.BillingZipCode : f.ZipCode) : null,
                     FacilityCountry = country != null ? country.Name : null,

                     PdfS3Url = inv.PdfS3Url
                 })
                .FirstOrDefault();

            if (rawInvoice == null)
                return new GetInvoiceByIdResponseDTO();

            var dto = new GetInvoiceByIdResponseDTO
            {
                InvoiceId = rawInvoice.InvoiceId,
                InvoiceNumber = rawInvoice.InvoiceNumber,
                CustomerName = rawInvoice.CustomerName,
                Amount = rawInvoice.Amount,
                Status = rawInvoice.Status,
                InvoiceType = rawInvoice.InvoiceType,
                IsActive = rawInvoice.IsActive,
                CreatedBy = rawInvoice.CreatedBy,
                CreatedDate = rawInvoice.CreatedDate,
                SubscriptionId = rawInvoice.SubscriptionId,
                CardId = rawInvoice.CardId,
                Last4 = rawInvoice.Last4,
                CardBrand = rawInvoice.CardBrand,
                CardHolderName = rawInvoice.CardHolderName,
                Currency = rawInvoice.Currency,
                ExpirationMonth = rawInvoice.ExpirationMonth,
                ExpirationYear = rawInvoice.ExpirationYear,
                Email = rawInvoice.Email,
                DueDate = rawInvoice.DueDate,
                FacilityPhone = rawInvoice.FacilityPhone,
                FacilityAddress = string.Join(", ", new[]
                {
                    rawInvoice.FacilityAddressLine,
                    rawInvoice.FacilityCity,
                    rawInvoice.FacilityZipCode,
                    rawInvoice.FacilityCountry
                }.Where(part => !string.IsNullOrWhiteSpace(part))),
                PdfS3Url = rawInvoice.PdfS3Url
            };

            var lineItems = _db.Sys_InvoiceLineItems
                .AsNoTracking()
                .Where(li => li.InvoiceId == dto.InvoiceId && (li.IsActive == true || li.IsActive == null))
                .Select(li => new GetInvoiceByIdLineItemDTO
                {
                    InvoiceLineItemId = li.InvoiceLineItemId,
                    ProductLineItemName = li.ProductLineItemName,
                    Qty = li.Qty,
                    UnitPrice = li.UnitPrice,
                    LineTotal = li.LineTotal,
                    DrugId = li.DrugId,
                    ProductId = li.ProductId,
                    BundleId = li.BundleId,
                    CouponCode = li.CouponCode,
                    OriginalPrice = li.OriginalPrice,
                    DiscountedPrice = li.DiscountedPrice,
                    DiscountAmount = li.DiscountAmount
                })
                .OrderBy(li => li.InvoiceLineItemId)
                .ToList();

            dto.LineItems = lineItems;
            return dto;
        }

        public bool SaveInvoice(SaveInvoiceRequestDTO request, long UserId)
        {
            try
            {
                Sys_Invoice Invoice = new Sys_Invoice();
                if (request.InvoiceId == 0)
                {
                    Invoice = _mapper.Map<Sys_Invoice>(request);
                    Invoice.CreatedBy = UserId;

                    Invoice.CreatedDate = DateTime.UtcNow;
                    Invoice.IsActive = true;
                    _db.Sys_Invoices.Add(Invoice);
                    _db.SaveChanges();

                    _auditService.LogEntityChange(
                        action: "Create",
                        entityType: "Sys_Invoice",
                        entityId: Invoice.InvoiceId,
                        newValues: Invoice,
                        userId: UserId,
                        patientId: Invoice.PatientId,
                        facilityId: Invoice.FacilityId,
                        description: $"Invoice {Invoice.InvoiceNumber ?? Invoice.InvoiceId.ToString()} created",
                        module: "Invoice");
                }
                else
                {
                    Invoice = _db.Sys_Invoices.Where(x => x.InvoiceId == request.InvoiceId).FirstOrDefault();
                    var oldInvoice = _db.Sys_Invoices.AsNoTracking().FirstOrDefault(x => x.InvoiceId == request.InvoiceId);
                    _mapper.Map(request, Invoice);

                    _db.SaveChanges();

                    _auditService.LogEntityChange(
                        action: "Update",
                        entityType: "Sys_Invoice",
                        entityId: Invoice.InvoiceId,
                        oldValues: oldInvoice,
                        newValues: Invoice,
                        userId: UserId,
                        patientId: Invoice.PatientId,
                        facilityId: Invoice.FacilityId,
                        description: $"Invoice {Invoice.InvoiceNumber ?? Invoice.InvoiceId.ToString()} updated",
                        module: "Invoice");

                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public Sys_Invoice SaveInvoiceReturnInoice(SaveInvoiceRequestDTO request, long UserId)
        {
            try
            {
                Sys_Invoice Invoice = new Sys_Invoice();

                Invoice = _mapper.Map<Sys_Invoice>(request);
                Invoice.CreatedBy = UserId;

                Invoice.CreatedDate = DateTime.UtcNow;
                Invoice.IsActive = true;
                Invoice.PatientId = request.PatientId;
                _db.Sys_Invoices.Add(Invoice);
                _db.SaveChanges();

                _auditService.LogEntityChange(
                    action: "Create",
                    entityType: "Sys_Invoice",
                    entityId: Invoice.InvoiceId,
                    newValues: Invoice,
                    userId: UserId,
                    patientId: Invoice.PatientId,
                    facilityId: Invoice.FacilityId,
                    description: $"Invoice {Invoice.InvoiceNumber ?? Invoice.InvoiceId.ToString()} created",
                    module: "Invoice");

                return Invoice;
            }
            catch
            {
                return null;
            }
        }

        public async Task<bool> CreateInvoiceLineItemForBundle(
            int invoiceId,
            long bundleId,
            string? couponCode,
            decimal originalPrice,
            decimal discountedPrice,
            long userId)
        {
            try
            {

                var bundle = _db.PD_Bundles
                    .AsNoTracking()
                    .FirstOrDefault(b => b.BundleId == bundleId);

                var bundleName = bundle?.Name ?? "Bundle";
                var discountAmount = originalPrice - discountedPrice;

                var lineItem = new Sys_InvoiceLineItem
                {
                    InvoiceId = invoiceId,
                    BundleId = bundleId,
                    ProductId = bundleId,
                    ProductLineItemName = bundleName,
                    Qty = 1,
                    UnitPrice = discountedPrice,
                    LineTotal = discountedPrice,
                    CouponCode = couponCode,
                    OriginalPrice = originalPrice,
                    DiscountedPrice = discountedPrice,
                    DiscountAmount = discountAmount > 0 ? discountAmount : null,
                    IsActive = true,
                    CreatedBy = userId,
                    CreatedDate = DateTime.UtcNow
                };

                _db.Sys_InvoiceLineItems.Add(lineItem);
                await _db.SaveChangesAsync();

                return true;
            }
            catch
            {
                return false;
            }
        }

        public List<GetAllInvoicesResponseDTO> GetInvoicesByPatientId(GetInvoicesByPatientRequestDTO request)
        {
            var patientId = request?.PatientId ?? 0;
            if (patientId <= 0) return new List<GetAllInvoicesResponseDTO>();

            var statusFilter = request?.Status?.Trim()?.ToLowerInvariant();
            var invoiceIdFilter = request?.InvoiceId;
            var patientNameFilter = request?.PatientName?.Trim();

            var invoices = _db.Sys_Invoices
                .AsNoTracking()
                .Include(inv => inv.Patient)
                .Where(x => (x.IsActive == true || x.IsActive == null) && x.PatientId == patientId)
                .Where(x =>
                    !invoiceIdFilter.HasValue || invoiceIdFilter.Value <= 0 || x.InvoiceId == invoiceIdFilter.Value)
                .Where(x =>
                    string.IsNullOrWhiteSpace(statusFilter) ||
                    ((statusFilter == "pending" || statusFilter == "paid" || statusFilter == "cancelled")
                        ? (x.Status != null && x.Status.Trim().ToLower() == statusFilter)
                        : true))
                .Where(x =>
                    string.IsNullOrWhiteSpace(patientNameFilter) ||
                    (x.Patient != null && EF.Functions.Like((((x.Patient.FirstName ?? "") + " " + (x.Patient.LastName ?? "")).Trim()), $"%{patientNameFilter}%")))
                .OrderByDescending(x => x.CreatedDate)
                .Select(inv => new GetAllInvoicesResponseDTO
                {
                    InvoiceId = (long)inv.InvoiceId,
                    InvoiceNumber = inv.InvoiceNumber,
                    CustomerName = !string.IsNullOrWhiteSpace(inv.CustomerName)
                        ? inv.CustomerName
                        : (inv.Patient != null
                            ? inv.Patient.FirstName + " " + inv.Patient.LastName
                            : null),
                    Amount = inv.Amount.HasValue ? inv.Amount.Value.ToString("F2") : null,
                    Status = inv.Status,
                    InvoiceType = inv.InvoiceType,
                    IsActive = inv.IsActive,
                    CreatedBy = inv.CreatedBy,
                    CreatedDate = CommonMethods.CommonMethods.ToLocalTime(inv.CreatedDate),
                    SubscriptionId = inv.SubscriptionId,
                    CardId = inv.CardId
                })
                .ToList();

            return invoices;
        }

        public async Task<bool> PayInvoice(long UserId, long InvoiceId)
        {
            try
            {
                var invoice = _db.Sys_Invoices.Where(x => x.InvoiceId == InvoiceId).FirstOrDefault();
                var user = _db.SYS_UserDetails.Include(x => x.SYS_UserCards).Where(x => x.UserId == UserId).FirstOrDefault();

                if (user == null || invoice == null)
                {
                    return false;
                }

                if (invoice.FacilityId.HasValue && !_facilityStatusService.IsFacilityActive(invoice.FacilityId.Value))
                {
                    throw new InvalidOperationException("Cannot process payment: Facility has been disabled.");
                }

                if (invoice.Amount == null)
                {
                    return false;
                }

                var amount = invoice.Amount.Value;
                if (amount <= 0)
                {
                    return false;
                }

                long? facilityId = invoice.FacilityId;
                int amountInCents = (int)Math.Round(amount * 100, MidpointRounding.AwayFromZero);

                if ((invoice.InvoiceType == InvoiceType.ClinicToGlobal.ToString() || invoice.InvoiceType == InvoiceType.GAToClinic.ToString()) && facilityId.HasValue)
                {
                    var gaMode = ResolveFacilityPaymentMode(facilityId.Value);
                    if (gaMode == (int)FacilityPaymentMode.Stripe)
                    {
                        if (_stripeAccountResolver == null || _stripePlatformCharge == null)
                            throw new InvalidOperationException("Stripe billing is not configured for this application.");
                        var facilityStripeAccountId = await _stripeAccountResolver.GetStripeAccountIdForFacilityAsync(facilityId.Value).ConfigureAwait(false);
                        if (string.IsNullOrWhiteSpace(facilityStripeAccountId))
                            throw new InvalidOperationException("This clinic is set to Stripe payments but Stripe Connect is not linked. Contact your administrator.");
                        if (string.IsNullOrWhiteSpace(user.StripePlatformCustomerId))
                        {
                            throw new InvalidOperationException(
                                "Please set up your clinic billing payment method before paying this invoice. Go to Stripe Connect or billing settings and add a payment method for clinic-to-global billing.");
                        }
                        var piId = await _stripePlatformCharge.ChargeCustomerAsync(
                            user.StripePlatformCustomerId,
                            amountInCents,
                            "usd",
                            default,
                            new StripePlatformGaChargeContext
                            {
                                ConnectedAccountId = facilityStripeAccountId,
                                FacilityId = facilityId.Value
                            }).ConfigureAwait(false);
                        if (!string.IsNullOrEmpty(piId))
                        {
                            await RecordPayment(
                                invoiceId: InvoiceId,
                                amount: amount,
                                transactionId: piId,
                                squarePaymentId: null,
                                userId: UserId,
                                patientId: null,
                                facilityId: invoice.FacilityId,
                                cardId: null,
                                paymentMethod: "Stripe",
                                notes: $"GA payment via Stripe - PaymentIntent {piId}").ConfigureAwait(false);
                            _db.SaveChanges();
                            return true;
                        }
                        throw new InvalidOperationException(
                            "Stripe payment failed. Please ensure your clinic billing payment method is set up and valid in Stripe.");
                    }

                }

                if ((invoice.InvoiceType == InvoiceType.PatientToClinic.ToString() ||
                     invoice.InvoiceType == InvoiceType.ClinicToPatient.ToString()) &&
                    facilityId.HasValue &&
                    user.SYS_UserCards != null)
                {
                    var patientMode = ResolveFacilityPaymentMode(facilityId.Value);
                    if (patientMode == (int)FacilityPaymentMode.Stripe)
                    {
                        var stripeNotes = invoice.InvoiceType == InvoiceType.ClinicToPatient.ToString()
                            ? "Clinic-to-patient invoice"
                            : "Patient-to-clinic invoice";
                        var stripeCardId = await TryCompletePatientInvoicePaymentViaStripeConnectAsync(
                            invoiceId: InvoiceId,
                            amount: amount,
                            payingUserId: UserId,
                            patientId: invoice.PatientId,
                            facilityId: facilityId,
                            userCards: user.SYS_UserCards,
                            notesContext: stripeNotes).ConfigureAwait(false);
                        if (stripeCardId.HasValue)
                        {
                            invoice.CardId = stripeCardId;
                            _db.SaveChanges();
                            return true;
                        }
                        return false;
                    }

                }

                if (user.SYS_UserCards == null || !user.SYS_UserCards.Any())
                {
                    return false;
                }

                var squareEligible = user.SYS_UserCards
                    .Where(c => (c.IsActive == true || c.IsActive == null) && !string.IsNullOrEmpty(c.SquareCardId))
                    .ToList();
                var userCard = squareEligible.FirstOrDefault(c => c.IsDefault) ?? squareEligible.FirstOrDefault();
                if (userCard == null)
                {
                    return false;
                }

                long? facilityIdForSquareClient = GetSquareChargeFacilityId(invoice);

                CreatePaymentResponse response = await _squarPaymentRepo.ChargeCustomerWithSavedCard(
                    userCard.SquareClientId,
                    userCard.SquareCardId.ToString(),
                    amountInCents,
                    "USD",
                    facilityIdForSquareClient);

                if (response.Payment != null && response.Payment.Status == "COMPLETED")
                {

                    var transactionId = response.Payment.Id ?? Guid.NewGuid().ToString();
                    var squarePaymentId = response.Payment.Id;

                    long? patientId = invoice.PatientId;

                    if (invoice.InvoiceType == InvoiceType.PatientToClinic.ToString())
                    {
                        patientId = invoice.PatientId;
                    }
                    else if (invoice.InvoiceType == InvoiceType.ClinicToGlobal.ToString() ||
                             invoice.InvoiceType == InvoiceType.GAToClinic.ToString())
                    {
                        patientId = null;
                        facilityId = invoice.FacilityId;
                    }

                    await RecordPayment(
                        invoiceId: InvoiceId,
                        amount: amount,
                        transactionId: transactionId,
                        squarePaymentId: squarePaymentId,
                        userId: UserId,
                        patientId: patientId,
                        facilityId: facilityId,
                        cardId: userCard.CardId,
                        paymentMethod: "Card",
                        notes: $"Payment via Square - Card ending in {userCard.Last4}"
                    );

                    invoice.CardId = userCard.CardId;
                    _db.SaveChanges();
                    return true;
                }
                else
                {

                    try
                    {
                        var failedPayment = new Sys_InvoicePayment
                        {
                            InvoiceId = (int)InvoiceId,
                            PaymentAmount = amount,
                            PaymentMethod = "Card",
                            TransactionId = response?.Payment?.Id ?? Guid.NewGuid().ToString(),
                            SquarePaymentId = response?.Payment?.Id,
                            PaymentStatus = "Failed",
                            PaidByUserId = UserId,
                            PaidByPatientId = invoice.PatientId,
                            PaidByFacilityId = invoice.FacilityId,
                            CardId = userCard.CardId,
                            PaymentNotes = $"Payment failed: {response?.Payment?.Status ?? "Unknown error"}",
                            PaymentDate = DateTime.UtcNow,
                            IsActive = true,
                            CreatedBy = UserId,
                            CreatedDate = DateTime.UtcNow
                        };
                        _db.Sys_InvoicePayments.Add(failedPayment);
                        _db.SaveChanges();
                    }
                    catch {  }

                    return false;
                }
            }
            catch (Exception ex)
            {

                throw;
            }
        }

        public async Task<bool> PayManualInvoice(long userId, long invoiceId)
        {
            try
            {
                var invoice = _db.Sys_Invoices.Where(x => x.InvoiceId == invoiceId).FirstOrDefault();

                if (invoice == null)
                {
                    return false;
                }

                if (invoice.FacilityId.HasValue && !_facilityStatusService.IsFacilityActive(invoice.FacilityId.Value))
                {
                    throw new InvalidOperationException("Your facility has been disabled. Payment processing is not available. Please contact your administrator to reactivate your facility.");
                }

                if (invoice.InvoiceType != InvoiceType.ClinicToPatient.ToString()
                    && invoice.InvoiceType != InvoiceType.GAToClinic.ToString())
                {
                    return false;
                }

                if (invoice.InvoiceType == InvoiceType.ClinicToPatient.ToString())
                {

                    if (!invoice.PatientId.HasValue)
                    {
                        return false;
                    }

                    var patient = _db.PT_Patients.Where(p => p.PatientId == invoice.PatientId.Value).FirstOrDefault();
                    if (patient == null)
                    {
                        return false;
                    }

                    SYS_UserDetail? patientUser = null;
                    if (patient.LoginId.HasValue)
                    {
                        patientUser = _db.SYS_UserDetails
                            .Include(x => x.SYS_UserCards)
                            .Where(x => x.UserId == userId)
                            .FirstOrDefault();
                    }

                    if (patientUser == null || patientUser.SYS_UserCards == null || !patientUser.SYS_UserCards.Any())
                    {
                        return false;
                    }

                    long payingUserId = patientUser.UserId;

                    if (invoice.Amount == null)
                    {
                        return false;
                    }

                    var amount = invoice.Amount.Value;
                    if (amount <= 0)
                    {
                        return false;
                    }

                    long? facilityId = invoice.FacilityId;

                if (!facilityId.HasValue)
                {
                    return false;
                }

                var manualMode = ResolveFacilityPaymentMode(facilityId.Value);
                var facilityCreds = _squarPaymentRepo.GetFacilitySquareCredentialsByFacilityId(facilityId.Value);
                var hasSquare = !string.IsNullOrWhiteSpace(facilityCreds.AccessToken);
                var hasStripe = false;
                if (_stripeAccountResolver != null)
                {
                    var acct = await _stripeAccountResolver.GetStripeAccountIdForFacilityAsync(facilityId.Value).ConfigureAwait(false);
                    hasStripe = !string.IsNullOrWhiteSpace(acct);
                }

                if (manualMode == (int)FacilityPaymentMode.Stripe)
                {
                    if (!hasStripe)
                        throw new InvalidOperationException("This clinic is set to Stripe payments but Stripe Connect is not linked. Please contact your administrator.");
                }
                else
                {
                    if (!hasSquare)
                        throw new InvalidOperationException("This clinic is set to Square payments but Square is not connected. Please contact your administrator.");
                }

                int amountInCents = (int)Math.Round(amount * 100, MidpointRounding.AwayFromZero);

                if (manualMode == (int)FacilityPaymentMode.Stripe)
                {
                    var stripeCardId = await TryCompletePatientInvoicePaymentViaStripeConnectAsync(
                        invoiceId: invoiceId,
                        amount: amount,
                        payingUserId: payingUserId,
                        patientId: invoice.PatientId,
                        facilityId: facilityId,
                        userCards: patientUser.SYS_UserCards,
                        notesContext: "Manual clinic-to-patient invoice").ConfigureAwait(false);
                    if (stripeCardId.HasValue)
                    {
                        invoice.CardId = stripeCardId;
                        _db.SaveChanges();
                        return true;
                    }
                    throw new InvalidOperationException(
                        "Stripe payment could not be completed. Add or update a card saved for this clinic (Stripe), then try again.");
                }

                var squareEligible = patientUser.SYS_UserCards
                    .Where(c => (c.IsActive == true || c.IsActive == null) && !string.IsNullOrEmpty(c.SquareCardId))
                    .ToList();
                var cardToUse = squareEligible.FirstOrDefault(c => c.IsDefault) ?? squareEligible.FirstOrDefault();
                if (cardToUse == null)
                {
                    throw new InvalidOperationException("This clinic uses Square. Add a Square-saved payment card to pay this invoice.");
                }

                CreatePaymentResponse response = await _squarPaymentRepo.ChargeCustomerWithSavedCard(
                    cardToUse.SquareClientId,
                    cardToUse.SquareCardId.ToString(),
                    amountInCents,
                    "USD",
                    facilityId);

                    if (response.Payment != null && response.Payment.Status == "COMPLETED")
                    {

                        var transactionId = response.Payment.Id ?? Guid.NewGuid().ToString();
                        var squarePaymentId = response.Payment.Id;

                        invoice.CardId = cardToUse.CardId;
                        _db.SaveChanges();

                        await RecordPayment(
                            invoiceId: invoiceId,
                            amount: amount,
                            transactionId: transactionId,
                            squarePaymentId: squarePaymentId,
                            userId: payingUserId,
                            patientId: invoice.PatientId,
                            facilityId: invoice.FacilityId,
                            cardId: cardToUse.CardId,
                            paymentMethod: "Card",
                            notes: $"Payment for manual clinic-to-patient invoice - Card ending in {cardToUse.Last4}"
                        );

                        return true;
                    }

                    try
                    {
                        var failedPayment = new Sys_InvoicePayment
                        {
                            InvoiceId = (int)invoiceId,
                            PaymentAmount = amount,
                            PaymentMethod = "Card",
                            TransactionId = null,
                            SquarePaymentId = null,
                            PaymentStatus = "Failed",
                            PaidByUserId = payingUserId,
                            PaidByPatientId = invoice.PatientId,
                            PaidByFacilityId = invoice.FacilityId,
                            CardId = cardToUse.CardId,
                            PaymentNotes = "Payment failed for manual ClinicToPatient invoice",
                            PaymentDate = DateTime.UtcNow,
                            IsActive = true,
                            CreatedBy = payingUserId,
                            CreatedDate = DateTime.UtcNow
                        };
                        _db.Sys_InvoicePayments.Add(failedPayment);
                        _db.SaveChanges();
                    }
                    catch {  }

                    return false;
                }
                else
                {

                    return await PayInvoice(userId, invoiceId);
                }
            }
            catch (Exception ex)
            {

                throw;
            }
        }

        private async Task<long?> TryCompletePatientInvoicePaymentViaStripeConnectAsync(
            long invoiceId,
            decimal amount,
            long payingUserId,
            long? patientId,
            long? facilityId,
            IEnumerable<SYS_UserCard> userCards,
            string notesContext,
            CancellationToken cancellationToken = default)
        {
            if (!facilityId.HasValue || _stripeAccountResolver == null)
                return null;

            var stripeAccountId = await _stripeAccountResolver.GetStripeAccountIdForFacilityAsync(facilityId.Value, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(stripeAccountId))
                return null;

            if (_stripeRecurringCharge == null)
                return null;

            var stripeCard = userCards
                .Where(c => (c.IsActive == true || c.IsActive == null)
                    && c.StripeAccountId == stripeAccountId
                    && !string.IsNullOrWhiteSpace(c.StripePaymentMethodId))
                .OrderByDescending(c => c.IsDefault)
                .ThenByDescending(c => string.IsNullOrEmpty(c.StripeCustomerId) ? 0 : 1)
                .ThenBy(c => c.CardId)
                .FirstOrDefault();

            if (stripeCard == null)
                return null;

            var amountCents = (long)Math.Round(amount * 100, MidpointRounding.AwayFromZero);
            var applicationFeeCents = 0;

            var invoiceIdempotencyKey = $"INVOICE-{invoiceId}-{amountCents}";

            var chargeResult = await _stripeRecurringCharge.ChargeAsync(
                stripeAccountId,
                stripeCard.StripePaymentMethodId!,
                amountCents,
                "usd",
                stripeCard.StripeCustomerId,
                applicationFeeCents,
                cancellationToken,
                invoiceIdempotencyKey).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(chargeResult.CustomerId) && string.IsNullOrWhiteSpace(stripeCard.StripeCustomerId))
            {
                var trackedCard = await _db.SYS_UserCards.FirstOrDefaultAsync(c => c.CardId == stripeCard.CardId, cancellationToken).ConfigureAwait(false);
                if (trackedCard != null)
                {
                    trackedCard.StripeCustomerId = chargeResult.CustomerId;
                    await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }
            }

            if (string.IsNullOrEmpty(chargeResult.PaymentIntentId))
                return null;

            await RecordPayment(
                invoiceId: invoiceId,
                amount: amount,
                transactionId: chargeResult.PaymentIntentId,
                squarePaymentId: null,
                userId: payingUserId,
                patientId: patientId,
                facilityId: facilityId,
                cardId: stripeCard.CardId,
                paymentMethod: "Stripe",
                notes: $"{notesContext} via Stripe Connect - PaymentIntent {chargeResult.PaymentIntentId}").ConfigureAwait(false);

            return stripeCard.CardId;
        }

        public List<GetAllInvoicesResponseDTO> GetAllInvoices()
        {
            var invoices = _db.Sys_Invoices.Where(x => x.IsActive == true).ToList();
            var response = _mapper.Map<List<GetAllInvoicesResponseDTO>>(invoices);
            return response;
        }
        public PagedInvoicesResponseDTO GetInvoicesByFacilityId(GetInvoicesByFacilityRequestDTO request)
        {
            var facilityId = request?.FacilityId ?? 0;
            var pageNumber = request?.PageNumber ?? 1;
            var pageSize = request?.PageSize ?? 10;

            if (pageNumber <= 0)
                pageNumber = 1;

            if (pageSize <= 0)
                pageSize = 10;

            var statusFilter = request?.Status?.Trim();
            var invoiceIdFilter = request?.InvoiceId;
            var patientNameFilter = request?.PatientName?.Trim();
            var query = _db.Sys_Invoices
                .AsNoTracking()
                .Include(inv => inv.Patient)
                .Where(x => (x.IsActive == true || x.IsActive == null) &&
                            x.FacilityId == (long)facilityId &&
                            (x.InvoiceType == InvoiceType.PatientToClinic.ToString() || x.InvoiceType == InvoiceType.ClinicToPatient.ToString()));

            if (!string.IsNullOrEmpty(statusFilter))
            {
                var normalizedStatus = statusFilter.ToLowerInvariant();
                if (normalizedStatus == "pending" || normalizedStatus == "paid" || normalizedStatus == "cancelled")
                {
                    query = query.Where(x => x.Status != null && x.Status.Trim().ToLower() == normalizedStatus);
                }
            }

            if (invoiceIdFilter.HasValue && invoiceIdFilter.Value > 0)
            {
                query = query.Where(x => x.InvoiceId == invoiceIdFilter.Value);
            }

            if (!string.IsNullOrWhiteSpace(patientNameFilter))
            {
                var nameLike = $"%{patientNameFilter}%";
                query = query.Where(x =>
                    x.Patient != null &&
                    EF.Functions.Like((((x.Patient.FirstName ?? "") + " " + (x.Patient.LastName ?? "")).Trim()), nameLike));
            }

            var totalCount = query.Count();

            var invoices = query
                .OrderByDescending(x => x.CreatedDate)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(inv => new GetAllInvoicesResponseDTO
                {
                    InvoiceId = (long)inv.InvoiceId,
                    InvoiceNumber = inv.InvoiceNumber,
                    CustomerName = inv.CustomerName ?? (inv.Patient != null
                        ? inv.Patient.FirstName + " " + inv.Patient.LastName
                        : null),
                    Amount = inv.Amount.HasValue ? inv.Amount.Value.ToString("F2") : null,
                    Status = inv.Status,
                    InvoiceType = inv.InvoiceType,
                    IsActive = inv.IsActive,
                    CreatedBy = inv.CreatedBy,
                    CreatedDate = CommonMethods.CommonMethods.ToLocalTime(inv.CreatedDate),
                    SubscriptionId = inv.SubscriptionId,
                    CardId = inv.CardId
                })
                .ToList();

            return new PagedInvoicesResponseDTO
            {
                Invoices = invoices,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            };
        }

        public async Task<bool> PayInvoicePatient(long UserId, long InvoiceId)
        {
            try
            {
                var invoice = _db.Sys_Invoices.Where(x => x.InvoiceId == InvoiceId).FirstOrDefault();
                var user = _db.SYS_UserDetails.Include(x => x.SYS_UserCards).Where(x => x.UserId == UserId).FirstOrDefault();

                if (user == null || invoice == null || user.SYS_UserCards == null || !user.SYS_UserCards.Any())
                {
                    return false;
                }

                if (invoice.FacilityId.HasValue && !_facilityStatusService.IsFacilityActive(invoice.FacilityId.Value))
                {
                    throw new InvalidOperationException("Your facility has been disabled. Payment processing is not available. Please contact your administrator to reactivate your facility.");
                }

                var userCard = user.SYS_UserCards.Where(x => x.IsDefault).FirstOrDefault();
                if (userCard == null)
                {
                    return false;
                }

                decimal amount;
                if (invoice.Amount == null)
                {
                    return false;
                }

                amount = invoice.Amount.Value;

                if (amount <= 0)
                {
                    return false;
                }

                long? facilityId = invoice.FacilityId;
                long? squareChargeFacilityId = GetSquareChargeFacilityId(invoice);

                int amountInCents = (int)Math.Round(amount * 100, MidpointRounding.AwayFromZero);

                CreatePaymentResponse response = await _squarPaymentRepo.ChargeCustomerWithSavedCard(
                    userCard.SquareClientId,
                    userCard.SquareCardId.ToString(),
                    amountInCents,
                    "USD",
                    squareChargeFacilityId);

                if (response.Payment != null && response.Payment.Status == "COMPLETED")
                {

                    var transactionId = response.Payment.Id ?? Guid.NewGuid().ToString();
                    var squarePaymentId = response.Payment.Id;

                    invoice.CardId = userCard.CardId;
                    _db.SaveChanges();

                    await RecordPayment(
                        invoiceId: InvoiceId,
                        amount: amount,
                        transactionId: transactionId,
                        squarePaymentId: squarePaymentId,
                        userId: UserId,
                        patientId: invoice.PatientId,
                        facilityId: facilityId,
                        cardId: userCard.CardId,
                        paymentMethod: "Card",
                        notes: $"Payment via Square - Card ending in {userCard.Last4}"
                    );

                    return true;
                }
                else
                {

                    try
                    {
                        var failedPayment = new Sys_InvoicePayment
                        {
                            InvoiceId = (int)InvoiceId,
                            PaymentAmount = amount,
                            PaymentMethod = "Card",
                            TransactionId = response?.Payment?.Id ?? Guid.NewGuid().ToString(),
                            SquarePaymentId = response?.Payment?.Id,
                            PaymentStatus = "Failed",
                            PaidByUserId = UserId,
                            PaidByPatientId = invoice.PatientId,
                            PaidByFacilityId = facilityId,
                            CardId = userCard.CardId,
                            PaymentNotes = $"Payment failed: {response?.Payment?.Status ?? "Unknown error"}",
                            PaymentDate = DateTime.UtcNow,
                            IsActive = true,
                            CreatedBy = UserId,
                            CreatedDate = DateTime.UtcNow
                        };
                        _db.Sys_InvoicePayments.Add(failedPayment);
                        _db.SaveChanges();
                    }
                    catch {  }

                    return false;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        public GetDetailedFacilityInvoiceResponseDTO GetDetailedFacilityInvoice(long invoiceId)
        {
            var invoiceData = (from inv in _db.Sys_Invoices.AsNoTracking()
                               where inv.InvoiceId == invoiceId
                               join facility in _db.SYS_Facilities.AsNoTracking() on inv.FacilityId equals facility.FacilityId into facilities
                               from facility in facilities.DefaultIfEmpty()
                               select new
                               {
                                   Invoice = inv,
                                   Facility = facility
                               }).FirstOrDefault();

            if (invoiceData?.Invoice == null || invoiceData.Invoice.FacilityId == null)
                return new GetDetailedFacilityInvoiceResponseDTO();

            var invoice = invoiceData.Invoice;
            var invoiceFacility = invoiceData.Facility;
            var facilityId = invoice.FacilityId.Value;

            var response = new GetDetailedFacilityInvoiceResponseDTO
            {
                InvoiceId = invoice.InvoiceId,
                InvoiceNumber = invoice.InvoiceNumber,
                FacilityId = invoice.FacilityId,
                FacilityName = invoiceFacility != null ? (invoiceFacility.TitleLong ?? invoiceFacility.TitleShort) : null,
                FacilityPhone = invoiceFacility?.Phone,
                FacilityEmail = invoiceFacility?.Email,
                FacilityAddress = invoiceFacility?.Address,
                Status = invoice.Status,
                InvoiceDate = CommonMethods.CommonMethods.ToLocalTime(invoice.CreatedDate),
                DueDate = CommonMethods.CommonMethods.ToLocalTime(invoice.CreatedDate.HasValue ? invoice.CreatedDate.Value.AddDays(29) : (DateTime?)null),
                InvoiceType = invoice.InvoiceType
            };

            var isLocked = string.Equals(invoice.Status, InvoiceStatus.Paid.ToString(), StringComparison.OrdinalIgnoreCase);
            response.IsLocked = isLocked;

            var providerBillStored = invoice.ProviderBillTotal.HasValue ? Convert.ToDecimal(invoice.ProviderBillTotal.Value) : 0m;
            response.ProviderBillTotal = providerBillStored;

            var invoiceWindow = ResolveInvoiceWindow(invoice);
            var startDate = invoiceWindow.Start;
            var endDate = invoiceWindow.End;

            var orderRecords = (from order in _db.PT_PatientOrders.AsNoTracking()
                                where order.FacilityId == facilityId &&
                                      order.IsActive == true &&
                                      order.OrderStatus == "Completed" &&
                                      order.CreatedDate >= startDate &&
                                      order.CreatedDate <= endDate
                                join patient in _db.PT_Patients.AsNoTracking() on order.PatientId equals patient.PatientId into patientGroup
                                from patient in patientGroup.DefaultIfEmpty()
                                join patientState in _db.SYS_States.AsNoTracking() on patient.StateId equals (int?)patientState.Id into patientStateGroup
                                from patientState in patientStateGroup.DefaultIfEmpty()
                                join patientCity in _db.SYS_Cities.AsNoTracking() on patient.CityId equals (int?)patientCity.Id into patientCityGroup
                                from patientCity in patientCityGroup.DefaultIfEmpty()
                                join provider in _db.SYS_UserDetails.AsNoTracking() on order.ProviderId equals (long?)provider.UserId into providerGroup
                                from provider in providerGroup.DefaultIfEmpty()
                                join providerState in _db.SYS_States.AsNoTracking() on provider.StateId equals (int?)providerState.Id into providerStateGroup
                                from providerState in providerStateGroup.DefaultIfEmpty()
                                join providerCity in _db.SYS_Cities.AsNoTracking() on provider.CityId equals (int?)providerCity.Id into providerCityGroup
                                from providerCity in providerCityGroup.DefaultIfEmpty()
                                select new
                                {
                                    Order = order,
                                    Patient = patient,
                                    PatientState = patientState,
                                    PatientCity = patientCity,
                                    Provider = provider,
                                    ProviderState = providerState,
                                    ProviderCity = providerCity
                                }).ToList();

            if (isLocked && invoice.CreatedDate.HasValue)
            {
                var invoiceCreated = invoice.CreatedDate.Value;
                orderRecords = orderRecords
                    .Where(record => !record.Order.ModifiedDate.HasValue || record.Order.ModifiedDate <= invoiceCreated)
                    .ToList();
            }

            var orderIds = orderRecords.Select(r => r.Order.PatientOrderId).ToList();
            if (!orderIds.Any())
            {
                response.OrderDetails = new List<FacilityInvoiceOrderDTO>();
                response.MedicationInvoiceDetails = new List<MedicationInvoiceDetailDTO>();
                response.WholesaleTotal = 0m;
                response.RetailTotal = 0m;
                response.PharmacyBillTotal = 0m;
                response.TotalAmount = providerBillStored;
                response.ProviderInvoiceDetails = new List<ProviderInvoiceDetailDTO>();
                response.Summary = new InvoiceSummaryDTO
                {
                    TotalAppointments = 0,
                    TotalOrders = 0,
                    TotalProviders = 0,
                    TotalProducts = 0,
                    SubTotal = response.TotalAmount,
                    Tax = 0m,
                    Total = response.TotalAmount
                };
                return response;
            }

            var prescriptions = _db.PT_PatientPrescriptions.AsNoTracking()
                .Where(pr => pr.IsActive == true &&
                             pr.PatientOrderId.HasValue &&
                             orderIds.Contains(pr.PatientOrderId.Value))
                .Select(pr => new
                {
                    pr.PatientPrescriptionId,
                    PatientOrderId = pr.PatientOrderId!.Value
                })
                .ToList();

            var prescriptionIds = prescriptions.Select(p => p.PatientPrescriptionId).ToList();

            var medicineData = new List<(PT_PrescriptionMedicine Medicine, PD_Drug? Drug, SYS_Pharmacy? Pharmacy)>();
            if (prescriptionIds.Any())
            {
                medicineData = (from med in _db.PT_PrescriptionMedicines.AsNoTracking()
                                where prescriptionIds.Contains(med.PatientPrescriptionId)
                                join drug in _db.PD_Drugs.AsNoTracking() on med.DrugId equals drug.DrugId into drugGroup
                                from drug in drugGroup.DefaultIfEmpty()
                                join pharmacy in _db.SYS_Pharmacies.AsNoTracking() on drug.PharmacyId equals pharmacy.PharmacyId into pharmacyGroup
                                from pharmacy in pharmacyGroup.DefaultIfEmpty()
                                select new
                                {
                                    Medicine = med,
                                    Drug = drug,
                                    Pharmacy = pharmacy
                                })
                                .AsEnumerable()
                                .Select(x => (x.Medicine, x.Drug, x.Pharmacy))
                                .ToList();
            }

            var medicineIds = medicineData
                .Select(md => md.Medicine.PrescriptionMedicineId)
                .Distinct()
                .ToList();

            var suppliesByMedId = new Dictionary<long, List<OrderMedicineSupplyDTO>>();
            if (medicineIds.Any())
            {
                var supplies = _db.Set<PT_MedicineSupply>().AsNoTracking()
                    .Where(supply => medicineIds.Contains(supply.PrescriptionMedicineId))
                    .ToList();

                suppliesByMedId = supplies
                    .GroupBy(supply => supply.PrescriptionMedicineId)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Select(supply => new OrderMedicineSupplyDTO
                        {
                            MedicineSupplyId = supply.MedicineSupplyId,
                            PrescriptionMedicineId = supply.PrescriptionMedicineId,
                            SupplyDesc = supply.SupplyDesc,
                        SupplyQuantity = supply.SupplyQuantity,
                        SupplyItemDesignatorID = supply.SupplyItemDesignatorID,
                        Name = supply.Name,
                        WholesalePrice = supply.WholesalePrice,
                        TotalAmount = supply.TotalAmount
                        }).ToList());
            }

            var orderDetails = new List<FacilityInvoiceOrderDTO>();
            decimal wholesaleTotal = 0m;
            decimal retailTotal = 0m;

            foreach (var record in orderRecords)
            {
                var order = record.Order;
                var orderId = order.PatientOrderId;

                var orderPrescriptionIds = prescriptions
                    .Where(p => p.PatientOrderId == orderId)
                    .Select(p => p.PatientPrescriptionId)
                    .ToList();

                var orderMedicineEntries = medicineData
                    .Where(md => orderPrescriptionIds.Contains(md.Medicine.PatientPrescriptionId))
                    .ToList();

                var orderDrugItems = orderMedicineEntries.Select(md => new OrderDrugItemDTO
                {
                    PrescriptionMedicineId = md.Medicine.PrescriptionMedicineId,
                    PatientPrescriptionId = md.Medicine.PatientPrescriptionId,
                    MedicineName = md.Medicine.MedicineName ?? string.Empty,
                    DrugId = md.Medicine.DrugId,
                    DaysSupplies = md.Medicine.DaysSupplies,
                    Injection = md.Medicine.Injection,
                    InjectionQuantity = md.Medicine.InjectionQuantity,
                    Needle = md.Medicine.Needle,
                    NeedleQuantity = md.Medicine.NeedleQuantity,
                    Direction = md.Medicine.Direction,
                    Instruction = md.Medicine.Instruction,
                    Quantity = md.Medicine.Quantity,
                    DrugName = md.Drug != null ? md.Drug.Name : null,
                    GenericName = md.Drug != null ? md.Drug.GenericName : null,
                    DosageForm = md.Drug != null ? md.Drug.DosageForm : null,
                    Strength = md.Drug != null ? md.Drug.Strenght : null,
                    Price = md.Drug != null ? md.Drug.Price : null,
                    PackageSize = md.Drug != null ? md.Drug.PackageSize : null,
                    PharmacyName = md.Pharmacy != null ? md.Pharmacy.PharmacyName : null
                }).ToList();

                var orderMedicineDtos = orderMedicineEntries.Select(md =>
                {
                    var supplies = suppliesByMedId.TryGetValue(md.Medicine.PrescriptionMedicineId, out var supplyList)
                        ? supplyList
                        : new List<OrderMedicineSupplyDTO>();

                    return new OrderMedicineDTO
                    {
                        PrescriptionMedicineId = md.Medicine.PrescriptionMedicineId,
                        PatientPrescriptionId = md.Medicine.PatientPrescriptionId,
                        MedicineName = md.Medicine.MedicineName ?? string.Empty,
                        DrugId = md.Medicine.DrugId,
                        DaysSupplies = md.Medicine.DaysSupplies,
                        Injection = md.Medicine.Injection,
                        InjectionQuantity = md.Medicine.InjectionQuantity,
                        Needle = md.Medicine.Needle,
                        NeedleQuantity = md.Medicine.NeedleQuantity,
                        Direction = md.Medicine.Direction,
                        Instruction = md.Medicine.Instruction,
                        Quantity = md.Medicine.Quantity,
                        ItemDesignatorID = md.Medicine.ItemDesignatorID,
                        strenght = md.Medicine.strenght,
                        DosageForm = md.Medicine.DosageForm,
                        PackageSize = md.Medicine.PackageSize,
                        ControlSubstance = md.Medicine.ControlSubstance,
                        CourierMethod = md.Medicine.CourierMethod,
                        WholesalePrice = md.Medicine.WholesalePrice,
                        TotalAmount = md.Medicine.TotalAmount,
                        Supplies = supplies
                    };
                }).ToList();

                decimal orderWholesale = 0m;
                foreach (var medicineEntry in orderMedicineEntries)
                {

                    if (medicineEntry.Medicine.TotalAmount.HasValue)
                    {
                        orderWholesale += medicineEntry.Medicine.TotalAmount.Value;
                    }

                    if (suppliesByMedId.TryGetValue(medicineEntry.Medicine.PrescriptionMedicineId, out var supplyList))
                    {
                        foreach (var supply in supplyList)
                        {
                            if (supply.TotalAmount.HasValue)
                            {
                                orderWholesale += supply.TotalAmount.Value;
                            }
                        }
                    }
                }

                if (orderWholesale == 0m)
                {
                    orderWholesale = order.OrderTotal ?? 0m;
                }

                var orderRetail = order.OrderPayableAmount ?? order.OrderTotal ?? 0m;

                wholesaleTotal += orderWholesale;
                retailTotal += orderRetail;

                var providerDto = record.Provider != null
                    ? new OrderProviderDTO
                    {
                        ProviderId = record.Provider.UserId,
                        ProviderName = FormatName(record.Provider.FirstName, record.Provider.LastName),
                        Email = record.Provider.Email,
                        Phone = record.Provider.Phone,
                        ProviderType = record.Provider.ProviderType,
                        NPI = record.Provider.NPI,
                        City = record.ProviderCity?.Name,
                        State = record.ProviderState?.Name
                    }
                    : null;

                var patientName = record.Patient != null
                    ? FormatName(record.Patient.FirstName, record.Patient.LastName)
                    : null;

                var detail = new FacilityInvoiceOrderDTO
                {
                    OrderId = orderId,
                    PatientId = order.PatientId,
                    PatientName = patientName,
                    OrderDate = CommonMethods.CommonMethods.ToLocalTime(order.CreatedDate),
                    OrderStatus = order.OrderStatus,
                    CouponCode = order.CouponCode,
                    RetailAmount = order.OrderTotal,
                    Discount = order.OrderDiscount,
                    PayableAmount = orderRetail,
                    WholesaleAmount = orderWholesale,
                    SuppliesWholesaleAmount = 0m,
                    Provider = providerDto,
                    Drugs = orderDrugItems,
                    Medicines = orderMedicineDtos
                };

                orderDetails.Add(detail);
            }

            response.OrderDetails = orderDetails
                .OrderBy(detail => detail.OrderDate ?? DateTime.MinValue)
                .ThenBy(detail => detail.OrderId)
                .ToList();

            response.MedicationInvoiceDetails = new List<MedicationInvoiceDetailDTO>();
            response.WholesaleTotal = wholesaleTotal;
            response.RetailTotal = retailTotal;

            var storedPharmacyTotal = invoice.PharmacyBillToltal.HasValue ? Convert.ToDecimal(invoice.PharmacyBillToltal.Value) : 0m;
            if (isLocked)
            {
                response.PharmacyBillTotal = storedPharmacyTotal > 0 ? storedPharmacyTotal : wholesaleTotal;
                response.TotalAmount = invoice.Amount ?? (providerBillStored + response.PharmacyBillTotal.GetValueOrDefault());
            }
            else
            {
                response.PharmacyBillTotal = wholesaleTotal;
                response.TotalAmount = providerBillStored + wholesaleTotal;
            }

            response.ProviderInvoiceDetails = new List<ProviderInvoiceDetailDTO>();
            response.Summary = new InvoiceSummaryDTO
            {
                TotalAppointments = 0,
                TotalOrders = response.OrderDetails.Count,
                TotalProviders = response.OrderDetails
                    .Where(detail => detail.Provider?.ProviderId.HasValue == true)
                    .Select(detail => detail.Provider!.ProviderId!.Value)
                    .Distinct()
                    .Count(),
                TotalProducts = response.OrderDetails.Sum(detail => detail.Medicines?.Count ?? 0),
                SubTotal = response.TotalAmount,
                Tax = 0m,
                Total = response.TotalAmount
            };

            return response;
        }

        public GetPatientBillResponseDTO GetPatientBill(long invoiceId)
        {

            var invoiceData = (from inv in _db.Sys_Invoices.AsNoTracking()
                              where inv.InvoiceId == invoiceId
                              join patient in _db.PT_Patients.AsNoTracking() on inv.PatientId equals patient.PatientId into patients
                              from patient in patients.DefaultIfEmpty()
                              join facility in _db.SYS_Facilities.AsNoTracking() on inv.FacilityId equals facility.FacilityId into facilities
                              from facility in facilities.DefaultIfEmpty()
                              select new
                              {
                                  Invoice = inv,
                                  Patient = patient,
                                  Facility = facility
                              }).FirstOrDefault();

            if (invoiceData?.Invoice == null || invoiceData.Invoice.PatientId == null)
                return new GetPatientBillResponseDTO();

            var invoice = invoiceData.Invoice;
            var invoicePatient = invoiceData.Patient;
            var invoiceFacility = invoiceData.Facility;

            var response = new GetPatientBillResponseDTO
            {
                InvoiceId = invoice.InvoiceId,
                InvoiceNumber = invoice.InvoiceNumber,
                PatientId = invoice.PatientId,
                PatientName = invoicePatient != null ? $"{invoicePatient.FirstName} {invoicePatient.LastName}" : "Unknown",
                PatientEmail = invoicePatient?.Email,
                PatientAddress = invoicePatient?.Address,
                FacilityId = invoice.FacilityId,
                FacilityName = invoiceFacility != null ? (invoiceFacility.TitleLong ?? invoiceFacility.TitleShort) : null,
                Status = invoice.Status,
                InvoiceDate = CommonMethods.CommonMethods.ToLocalTime(invoice.CreatedDate),
                DueDate = CommonMethods.CommonMethods.ToLocalTime(invoice.CreatedDate?.AddDays(29)),
                InvoiceType = invoice.InvoiceType
            };

            var lineItems = _db.Sys_InvoiceLineItems
                .AsNoTracking()
                .Where(li => li.InvoiceId == invoiceId && (li.IsActive == true || li.IsActive == null))
                .OrderBy(li => li.InvoiceLineItemId)
                .ToList();

            var items = new List<PatientBillItemDTO>();

            if (lineItems.Any())
            {

                foreach (var lineItem in lineItems)
                {
                    items.Add(new PatientBillItemDTO
                    {
                        OrderId = null,
                        ProductId = lineItem.BundleId ?? lineItem.ProductId,
                        ProductName = lineItem.ProductLineItemName,
                        ProductType = lineItem.BundleId.HasValue ? "Bundle" : (lineItem.DrugId.HasValue ? "Drug" : "Product"),
                        Quantity = lineItem.Qty,
                        UnitPrice = lineItem.UnitPrice,
                        TotalPrice = lineItem.LineTotal,
                        CouponCode = lineItem.CouponCode,
                        Discount = lineItem.DiscountAmount,
                        OrderDate = CommonMethods.CommonMethods.ToLocalTime(lineItem.CreatedDate ?? invoice.CreatedDate)
                    });
                }

                var totalOriginalAmount = lineItems.Sum(li => li.OriginalPrice ?? li.UnitPrice);
                var totalDiscountAmount = lineItems.Sum(li => li.DiscountAmount ?? 0m);
                var totalPayableAmount = lineItems.Sum(li => li.LineTotal);

                response.TotalAmount = totalOriginalAmount;
                response.DiscountAmount = totalDiscountAmount;
                response.PayableAmount = totalPayableAmount;
                response.CouponCode = lineItems.FirstOrDefault(li => !string.IsNullOrWhiteSpace(li.CouponCode))?.CouponCode;
            }
            else
            {

                items.Add(new PatientBillItemDTO
                {
                    OrderId = null,
                    ProductId = null,
                    ProductName = "Invoice Item",
                    ProductType = "Bundle",
                    Quantity = 1,
                    UnitPrice = invoice.Amount,
                    TotalPrice = invoice.Amount,
                    CouponCode = null,
                    Discount = null,
                    OrderDate = CommonMethods.CommonMethods.ToLocalTime(invoice.CreatedDate)
                });

                response.TotalAmount = invoice.Amount ?? 0;
                response.DiscountAmount = 0;
                response.PayableAmount = invoice.Amount ?? 0;
                response.CouponCode = null;
            }

            response.Items = items;

            var paymentData = (from p in _db.Sys_InvoicePayments.AsNoTracking()
                              where p.InvoiceId == invoiceId && p.IsActive == true
                              orderby p.PaymentDate descending
                              select new PaymentDetailDTO
                              {
                                  PaymentId = p.InvoicePaymentId,
                                  PaymentDate = CommonMethods.CommonMethods.ToLocalTime(p.PaymentDate),
                                  PaymentStatus = p.PaymentStatus,
                                  PaymentMethod = p.PaymentMethod,
                                  TransactionId = p.TransactionId
                              }).FirstOrDefault();

            response.Payment = paymentData;

            return response;
        }

        public GetPatientBillResponseDTO GetPatientInvoiceDetail(long invoiceId)
        {
            return GetPatientBill(invoiceId);
        }

        public async Task<bool> UpdateAppointmentStatus(long appointmentId, string status)
        {
            if (appointmentId <= 0 || string.IsNullOrWhiteSpace(status))
            {
                return false;
            }

            var normalizedStatus = status.Trim();
            if (normalizedStatus.Equals("Pending", StringComparison.OrdinalIgnoreCase))
                normalizedStatus = "Scheduled";
            else if (normalizedStatus.Equals("Cancelled", StringComparison.OrdinalIgnoreCase) ||
                     normalizedStatus.Equals("Canceled", StringComparison.OrdinalIgnoreCase))
                normalizedStatus = "Missed";

            if (!new[] { "Scheduled", "Missed", "Completed" }.Contains(normalizedStatus))
                return false;

            var appointment = await _db.PT_PatientAppointmentSlots
                .FirstOrDefaultAsync(a => a.PatientAppointmentSlotId == appointmentId && (a.IsActive == true || a.IsActive == null));

            if (appointment == null)
                return false;

            var oldStatus = appointment.Status;
            appointment.Status = normalizedStatus;
            appointment.ModifiedDate = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            await _auditService.LogEntityChangeAsync(
                action: "Update",
                entityType: "PT_PatientAppointmentSlot",
                entityId: appointment.PatientAppointmentSlotId,
                oldValues: new { Status = oldStatus },
                newValues: new { Status = appointment.Status },
                userId: appointment.ModifiedBy,
                patientId: appointment.PatientId,
                facilityId: appointment.FacilityId,
                description: $"Appointment status changed from '{oldStatus}' to '{appointment.Status}'",
                module: "Appointment");
            return true;
        }

        public async Task<GetDetailedFacilityInvoiceResponseDTO> GenerateMonthlyFacilityInvoice(long facilityId, DateTime startDate, DateTime endDate)
        {

            var facilityData = await (from f in _db.SYS_Facilities.AsNoTracking()
                                     where f.FacilityId == facilityId
                                     select f).FirstOrDefaultAsync();

            if (facilityData == null)
                return new GetDetailedFacilityInvoiceResponseDTO();

            var response = new GetDetailedFacilityInvoiceResponseDTO
            {
                FacilityId = facilityId,
                FacilityName = facilityData.TitleLong ?? facilityData.TitleShort,
                FacilityPhone = facilityData.Phone,
                FacilityEmail = facilityData.Email,
                FacilityAddress = facilityData.Address,
                InvoiceDate = DateTime.UtcNow,
                DueDate = DateTime.UtcNow.AddDays(29),
                InvoiceType = InvoiceType.ClinicToGlobal.ToString(),
                Status = InvoiceStatus.Pending.ToString()
            };

            var appointmentData = await (from apt in _db.PT_PatientAppointmentSlots.AsNoTracking()
                                        where apt.FacilityId == facilityId &&
                                              apt.StartDate >= startDate &&
                                              apt.StartDate <= endDate &&
                                              (apt.IsActive == true || apt.IsActive == null) &&
                                              apt.ProviderId.HasValue &&
                                              apt.Status != null &&
                                              apt.Status.ToLower() == "completed"
                                        join provider in _db.SYS_UserDetails.AsNoTracking() on apt.ProviderId equals provider.UserId into providers
                                        from provider in providers.DefaultIfEmpty()
                                        join patient in _db.PT_Patients.AsNoTracking() on apt.PatientId equals patient.PatientId into patients
                                        from patient in patients.DefaultIfEmpty()
                                        join product in _db.PD_Drugs.AsNoTracking() on apt.ProductId equals product.DrugId into products
                                        from product in products.DefaultIfEmpty()
                                        join treatment in _db.PT_PatientTreatments.AsNoTracking() on apt.PatientTreatmentId equals treatment.PatientTreatmentId into treatments
                                        from treatment in treatments.DefaultIfEmpty()
                                        join bundle in _db.PD_Bundles.AsNoTracking() on treatment.ProductId equals bundle.BundleId into bundles
                                        from bundle in bundles.DefaultIfEmpty()
                                        select new AppointmentDetailDTO
                                        {
                                            AppointmentId = apt.PatientAppointmentSlotId,
                                            PatientId = apt.PatientId,
                                            PatientName = patient != null ? $"{patient.FirstName} {patient.LastName}" : "Unknown",
                                            AppointmentDate = CommonMethods.CommonMethods.ToLocalTime(apt.StartDate),
                                            AppointmentStatus = apt.Status,
                                            AppointmentAmount = 60m,
                                            ProductName = product != null ? product.Name : null,
                                            TreatmentName = bundle != null ? bundle.Name : null
                                        }).ToListAsync();

            var appointmentProviderData = await (from apt in _db.PT_PatientAppointmentSlots.AsNoTracking()
                                                where apt.FacilityId == facilityId &&
                                                      apt.CreatedDate >= startDate &&
                                                      apt.CreatedDate <= endDate &&
                                                      apt.IsActive == true &&
                                                      apt.ProviderId.HasValue &&
                                                      apt.Status == "Completed"
                                                join provider in _db.SYS_UserDetails.AsNoTracking() on apt.ProviderId equals provider.UserId
                                                select new
                                                {
                                                    Appointment = apt,
                                                    Provider = provider
                                                }).ToListAsync();

            var providerGroups = appointmentProviderData
                .GroupBy(x => new { ProviderId = x.Appointment.ProviderId.Value, Provider = x.Provider })
                .Select(g => new
                {
                    ProviderId = g.Key.ProviderId,
                    Provider = g.Key.Provider,
                    Appointments = g.Select(x => x.Appointment).ToList()
                }).ToList();

            decimal totalProviderBill = 0;
            foreach (var providerGroup in providerGroups)
            {
                var providerAppointments = appointmentData
                    .Where(a => providerGroup.Appointments.Any(apt => apt.PatientAppointmentSlotId == a.AppointmentId))
                    .ToList();

                var appointmentFee = 60m;
                var providerTotal = providerAppointments.Count * appointmentFee;
                totalProviderBill += providerTotal;

                var providerDetail = new ProviderInvoiceDetailDTO
                {
                    ProviderId = providerGroup.ProviderId,
                    ProviderName = providerGroup.Provider != null ? $"{providerGroup.Provider.FirstName} {providerGroup.Provider.LastName}" : "Unknown",
                    ProviderEmail = providerGroup.Provider?.Email,
                    AppointmentCount = providerAppointments.Count,
                    TotalAppointmentAmount = providerTotal,
                    Appointments = providerAppointments
                };

                response.ProviderInvoiceDetails.Add(providerDetail);
            }

            var orderRecords = await (from order in _db.PT_PatientOrders.AsNoTracking()
                                where order.FacilityId == facilityId &&
                                      order.IsActive == true &&
                                      order.OrderStatus == "Completed" &&
                                      order.CreatedDate >= startDate &&
                                      order.CreatedDate <= endDate
                                join patient in _db.PT_Patients.AsNoTracking() on order.PatientId equals patient.PatientId into patientGroup
                                from patient in patientGroup.DefaultIfEmpty()
                                join treatment in _db.PT_PatientTreatments.AsNoTracking() on order.PatientTreamentId equals treatment.PatientTreatmentId into treatmentGroup
                                from treatment in treatmentGroup.DefaultIfEmpty()
                                join bundle in _db.PD_Bundles.AsNoTracking() on treatment.ProductId equals bundle.BundleId into bundleGroup
                                from bundle in bundleGroup.DefaultIfEmpty()
                                select new
                                {
                                    Order = order,
                                    Patient = patient,
                                    Treatment = treatment,
                                    TreatmentName = bundle != null ? bundle.Name : null
                                }).ToListAsync();

            var orderDetails = new List<FacilityInvoiceOrderDTO>();
            decimal wholesaleTotal = 0m;
            decimal retailTotal = 0m;

            foreach (var record in orderRecords)
            {
                var order = record.Order;
                var orderId = order.PatientOrderId;

                var orderWholesale = order.OrderTotal ?? 0m;
                var orderRetail = order.OrderPayableAmount ?? order.OrderTotal ?? 0m;

                wholesaleTotal += orderWholesale;
                retailTotal += orderRetail;

                var patientName = record.Patient != null
                    ? FormatName(record.Patient.FirstName, record.Patient.LastName)
                    : null;

                var detail = new FacilityInvoiceOrderDTO
                {
                    OrderId = orderId,
                    PatientId = order.PatientId,
                    PatientName = patientName,
                    OrderDate = CommonMethods.CommonMethods.ToLocalTime(order.CreatedDate),
                    OrderStatus = order.OrderStatus,
                    CouponCode = order.CouponCode,
                    RetailAmount = order.OrderTotal,
                    Discount = order.OrderDiscount,
                    PayableAmount = orderRetail,
                    WholesaleAmount = orderWholesale,
                    SuppliesWholesaleAmount = 0m,
                    TreatmentName = record.TreatmentName,
                    Provider = null,
                    Drugs = new List<OrderDrugItemDTO>(),
                    Medicines = new List<OrderMedicineDTO>()
                };

                orderDetails.Add(detail);
            }

            response.OrderDetails = orderDetails
                .OrderBy(detail => detail.OrderDate ?? DateTime.MinValue)
                .ThenBy(detail => detail.OrderId)
                .ToList();

            response.MedicationInvoiceDetails = new List<MedicationInvoiceDetailDTO>();
            response.WholesaleTotal = wholesaleTotal;
            response.RetailTotal = retailTotal;

            response.ProviderBillTotal = totalProviderBill;
            response.PharmacyBillTotal = wholesaleTotal;
            response.TotalAmount = totalProviderBill + wholesaleTotal;

            response.Summary = new InvoiceSummaryDTO
            {
                TotalAppointments = appointmentData.Count,
                TotalOrders = orderDetails.Count,
                TotalProviders = providerGroups.Count,
                TotalProducts = 0,
                SubTotal = response.TotalAmount,
                Total = response.TotalAmount
            };

            return response;
        }

        public async Task<bool> CreatePatientBillInvoice(long patientOrderId, long userId)
        {
            try
            {
                var order = _db.PT_PatientOrders
                    .FirstOrDefault(o => o.PatientOrderId == patientOrderId);

                if (order == null || order.PatientId == null || order.FacilityId == null)
                    return false;

                var existingInvoice = _db.Sys_Invoices
                    .FirstOrDefault(i => i.PatientId == order.PatientId &&
                                       i.FacilityId == order.FacilityId &&
                                       i.InvoiceType == InvoiceType.PatientToClinic.ToString() &&
                                       Math.Abs((i.CreatedDate.Value - order.CreatedDate.Value).TotalHours) < 1);

                if (existingInvoice != null)
                    return true;

                var patient = _db.PT_Patients.FirstOrDefault(p => p.PatientId == order.PatientId);
                var facility = _db.SYS_Facilities.FirstOrDefault(f => f.FacilityId == order.FacilityId);

                var invoice = new Sys_Invoice
                {
                    InvoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{patientOrderId}",
                    CustomerName = patient != null ? $"{patient.FirstName} {patient.LastName}" : "Unknown",
                    Amount = order.OrderPayableAmount ?? order.OrderTotal ?? 0,
                    Status = InvoiceStatus.Pending.ToString(),
                    InvoiceType = InvoiceType.PatientToClinic.ToString(),
                    PatientId = order.PatientId,
                    FacilityId = order.FacilityId,
                    CreatedBy = userId,
                    CreatedDate = DateTime.UtcNow,
                    IsActive = true
                };

                _db.Sys_Invoices.Add(invoice);
                await _db.SaveChangesAsync();

                if (order.ProductId.HasValue)
                {

                    var bundle = _db.PD_Bundles
                        .AsNoTracking()
                        .FirstOrDefault(b => b.BundleId == order.ProductId.Value);

                    var originalPrice = bundle?.Price ?? order.OrderTotal ?? 0m;
                    var discountedPrice = order.OrderPayableAmount ?? order.OrderTotal ?? 0m;
                    var discountAmount = order.OrderDiscount ?? (originalPrice - discountedPrice);
                    var bundleName = bundle?.Name ?? "Bundle";

                    var lineItem = new Sys_InvoiceLineItem
                    {
                        InvoiceId = invoice.InvoiceId,
                        BundleId = order.ProductId.Value,
                        ProductId = order.ProductId.Value,
                        ProductLineItemName = bundleName,
                        Qty = 1,
                        UnitPrice = discountedPrice,
                        LineTotal = discountedPrice,
                        CouponCode = order.CouponCode,
                        OriginalPrice = originalPrice,
                        DiscountedPrice = discountedPrice,
                        DiscountAmount = discountAmount > 0 ? discountAmount : null,
                        IsActive = true,
                        CreatedBy = userId,
                        CreatedDate = DateTime.UtcNow
                    };

                    _db.Sys_InvoiceLineItems.Add(lineItem);
                    await _db.SaveChangesAsync();
                }

                await _auditService.LogEntityChangeAsync(
                    action: "Create",
                    entityType: "Sys_Invoice",
                    entityId: invoice.InvoiceId,
                    newValues: invoice,
                    userId: userId,
                    patientId: order.PatientId,
                    description: $"Invoice generated for Order ID {patientOrderId}, Amount: ${invoice.Amount}",
                    module: "Invoice"
                );

                return true;
            }
            catch (Exception ex)
            {

                return false;
            }
        }

        public PagedFacilityInvoicesResponseDTO GetFacilityInvoicesForGlobalAdmin(GetFacilityInvoiceSummaryRequestDTO request)
        {
            var pageNumber = request.PageNumber <= 0 ? 1 : request.PageNumber;
            var pageSize = request.PageSize <= 0 ? 10 : request.PageSize;

            var startDateUtc = CommonMethods.CommonMethods.LocalToUtc(request.StartDate, request.ClientTimezoneOffsetMinutes);
            var endDateUtc = CommonMethods.CommonMethods.LocalToUtc(request.EndDate, request.ClientTimezoneOffsetMinutes);

            var baseQuery = from inv in _db.Sys_Invoices.AsNoTracking()
                            where (inv.IsActive == true || inv.IsActive == null) &&
                                  (inv.InvoiceType == InvoiceType.ClinicToGlobal.ToString()
                                   || inv.InvoiceType == InvoiceType.GAToClinic.ToString())
                            join facility in _db.SYS_Facilities.AsNoTracking()
                                on inv.FacilityId equals facility.FacilityId into facilityGroup
                            from facility in facilityGroup.DefaultIfEmpty()
                            select new { Invoice = inv, Facility = facility };

            if (request.FacilityId.HasValue)
                baseQuery = baseQuery.Where(x => x.Invoice.FacilityId == request.FacilityId.Value);

            if (startDateUtc.HasValue)
                baseQuery = baseQuery.Where(x => x.Invoice.CreatedDate >= startDateUtc.Value);

            if (endDateUtc.HasValue)
                baseQuery = baseQuery.Where(x => x.Invoice.CreatedDate <= endDateUtc.Value);

            if (!request.IncludePaid)
                baseQuery = baseQuery.Where(x => x.Invoice.Status != InvoiceStatus.Paid.ToString());

            if (!request.IncludePending)
                baseQuery = baseQuery.Where(x => x.Invoice.Status != InvoiceStatus.Pending.ToString());

            if (!string.IsNullOrWhiteSpace(request.InvoiceType))
                baseQuery = baseQuery.Where(x => x.Invoice.InvoiceType == request.InvoiceType);

            var totalCount = baseQuery.Count();

            var pagedInvoices = baseQuery
                .OrderByDescending(x => x.Invoice.CreatedDate)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new FacilityInvoiceListItemDTO
                {
                    InvoiceId = x.Invoice.InvoiceId,
                    InvoiceNumber = x.Invoice.InvoiceNumber,
                    FacilityId = x.Invoice.FacilityId,
                    FacilityName = x.Facility != null ? (x.Facility.TitleLong ?? x.Facility.TitleShort) : null,
                    Amount = x.Invoice.Amount,
                    ProviderBillTotal = x.Invoice.ProviderBillTotal,
                    PharmacyBillTotal = x.Invoice.PharmacyBillToltal,
                    Status = x.Invoice.Status,
                    InvoiceType = x.Invoice.InvoiceType,
                    InvoiceDate = CommonMethods.CommonMethods.ToLocalTime(x.Invoice.CreatedDate),
                    DueDate = CommonMethods.CommonMethods.ToLocalTime(x.Invoice.CreatedDate.HasValue ? x.Invoice.CreatedDate.Value.AddDays(29) : (DateTime?)null),
                    IsMonthlyInvoiceGenerated = x.Invoice.IsMonthlyInvoiceGenerated
                })
                .ToList();

            return new PagedFacilityInvoicesResponseDTO
            {
                Invoices = pagedInvoices,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = pageSize == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize)
            };
        }

        public async Task<GetPaymentDetailResponseDTO> RecordPayment(long invoiceId, decimal amount, string transactionId, string? squarePaymentId, long? userId, long? patientId, long? facilityId, long? cardId, string paymentMethod, string? notes)
        {
            try
            {
                var invoice = _db.Sys_Invoices.FirstOrDefault(i => i.InvoiceId == invoiceId);
                if (invoice == null)
                    throw new Exception("Invoice not found");

                var payment = new Sys_InvoicePayment
                {
                    InvoiceId = (int)invoiceId,
                    PaymentAmount = amount,
                    PaymentMethod = paymentMethod,
                    TransactionId = transactionId,
                    SquarePaymentId = squarePaymentId,
                    PaymentStatus = "Completed",
                    PaidByUserId = userId,
                    PaidByPatientId = patientId,
                    PaidByFacilityId = facilityId,
                    CardId = cardId,
                    PaymentNotes = notes,
                    PaymentDate = DateTime.UtcNow,
                    IsActive = true,
                    CreatedBy = userId,
                    CreatedDate = DateTime.UtcNow
                };

                _db.Sys_InvoicePayments.Add(payment);
                await _db.SaveChangesAsync();

                await _auditService.LogEntityChangeAsync(
                    action: "Create",
                    entityType: "Sys_InvoicePayment",
                    entityId: payment.InvoicePaymentId,
                    newValues: payment,
                    userId: userId,
                    patientId: patientId,
                    description: $"Payment processed for Invoice ID {invoiceId}, Amount: ${amount}, Method: {paymentMethod}",
                    module: "Payment"
                );

                var totalPaid = _db.Sys_InvoicePayments
                    .Where(p => p.InvoiceId == invoiceId && p.IsActive == true && p.PaymentStatus == "Completed")
                    .Sum(p => p.PaymentAmount);

                var oldStatus = invoice.Status;
                if (invoice.Amount.HasValue)
                {
                    if (totalPaid >= invoice.Amount.Value)
                    {
                        invoice.Status = InvoiceStatus.Paid.ToString();
                    }
                    else if (totalPaid > 0)
                    {
                        invoice.Status = "PartiallyPaid";
                    }
                }

                _db.SaveChanges();

                if (oldStatus != invoice.Status)
                {
                    _auditService.LogEntityChange(
                        action: "Update",
                        entityType: "Sys_Invoice",
                        entityId: invoice.InvoiceId,
                        oldValues: new { Status = oldStatus },
                        newValues: new { Status = invoice.Status },
                        userId: userId,
                        patientId: patientId,
                        description: $"Invoice {invoice.InvoiceNumber} status changed from '{oldStatus}' to '{invoice.Status}' - Amount: ${invoice.Amount}, Total Paid: ${totalPaid}",
                        module: "Invoice"
                    );
                }

                return await GetPaymentDetailById(payment.InvoicePaymentId);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to record payment: {ex.Message}", ex);
            }
        }

        private async Task<GetPaymentDetailResponseDTO> GetPaymentDetailById(long paymentId)
        {
            var paymentDetail = await (from p in _db.Sys_InvoicePayments.AsNoTracking()
                                      where p.InvoicePaymentId == paymentId
                                      join inv in _db.Sys_Invoices.AsNoTracking() on p.InvoiceId equals inv.InvoiceId into invoices
                                      from inv in invoices.DefaultIfEmpty()
                                      join user in _db.SYS_UserDetails.AsNoTracking() on p.PaidByUserId equals user.UserId into users
                                      from user in users.DefaultIfEmpty()
                                      join patient in _db.PT_Patients.AsNoTracking() on p.PaidByPatientId equals patient.PatientId into patients
                                      from patient in patients.DefaultIfEmpty()
                                      join facility in _db.SYS_Facilities.AsNoTracking() on p.PaidByFacilityId equals facility.FacilityId into facilities
                                      from facility in facilities.DefaultIfEmpty()
                                      join card in _db.SYS_UserCards.AsNoTracking() on p.CardId equals card.CardId into cards
                                      from card in cards.DefaultIfEmpty()
                                      select new GetPaymentDetailResponseDTO
                                      {
                                          PaymentId = p.InvoicePaymentId,
                                          InvoiceId = p.InvoiceId,
                                          InvoiceNumber = inv.InvoiceNumber,
                                          PaymentAmount = p.PaymentAmount,
                                          PaymentMethod = p.PaymentMethod,
                                          TransactionId = p.TransactionId,
                                          SquarePaymentId = p.SquarePaymentId,
                                          PaymentStatus = p.PaymentStatus,
                                          PaymentDate = CommonMethods.CommonMethods.ToLocalTime(p.PaymentDate),
                                          PaidByUserId = p.PaidByUserId,
                                          PaidByName = user != null ? $"{user.FirstName} {user.LastName}" : null,
                                          PaidByEmail = user != null ? user.Email : null,
                                          PaidByPatientId = p.PaidByPatientId,
                                          PatientName = patient != null ? $"{patient.FirstName} {patient.LastName}" : null,
                                          PaidByFacilityId = p.PaidByFacilityId,
                                          FacilityName = facility != null ? (facility.TitleLong ?? facility.TitleShort) : null,
                                          CardId = p.CardId,
                                          CardLast4 = card != null ? card.Last4 : null,
                                          CardBrand = card != null ? card.CardBrand : null,
                                          CardHolderName = card != null ? card.CardHolderName : null,
                                          PaymentNotes = p.PaymentNotes,
                                          IsRefunded = p.RefundAmount.HasValue && p.RefundAmount > 0,
                                          RefundAmount = p.RefundAmount,
                                          RefundDate = p.RefundDate,
                                          RefundReason = p.RefundReason,
                                          RefundTransactionId = p.RefundTransactionId
                                      }).FirstOrDefaultAsync();

            return paymentDetail ?? new GetPaymentDetailResponseDTO();
        }

        public async Task<GetInvoicePaymentSummaryResponseDTO> GetInvoicePaymentSummary(long invoiceId)
        {

            var invoiceData = await (from inv in _db.Sys_Invoices.AsNoTracking()
                                     where inv.InvoiceId == invoiceId
                                     join facility in _db.SYS_Facilities.AsNoTracking() on inv.FacilityId equals facility.FacilityId into facilities
                                     from facility in facilities.DefaultIfEmpty()
                                     join patient in _db.PT_Patients.AsNoTracking() on inv.PatientId equals patient.PatientId into patients
                                     from patient in patients.DefaultIfEmpty()
                                     select new
                                     {
                                         Invoice = inv,
                                         Facility = facility,
                                         Patient = patient
                                     }).FirstOrDefaultAsync();

            if (invoiceData?.Invoice == null)
                return new GetInvoicePaymentSummaryResponseDTO();

            var payments = await (from p in _db.Sys_InvoicePayments.AsNoTracking()
                                 where p.InvoiceId == invoiceId && p.IsActive == true
                                 join inv in _db.Sys_Invoices.AsNoTracking() on p.InvoiceId equals inv.InvoiceId into invoices
                                 from inv in invoices.DefaultIfEmpty()
                                 join user in _db.SYS_UserDetails.AsNoTracking() on p.PaidByUserId equals user.UserId into users
                                 from user in users.DefaultIfEmpty()
                                 join pat in _db.PT_Patients.AsNoTracking() on p.PaidByPatientId equals pat.PatientId into patients
                                 from pat in patients.DefaultIfEmpty()
                                 join fac in _db.SYS_Facilities.AsNoTracking() on p.PaidByFacilityId equals fac.FacilityId into facilities
                                 from fac in facilities.DefaultIfEmpty()
                                 join card in _db.SYS_UserCards.AsNoTracking() on p.CardId equals card.CardId into cards
                                 from card in cards.DefaultIfEmpty()
                                 orderby p.PaymentDate descending
                                 select new GetPaymentDetailResponseDTO
                                 {
                                     PaymentId = p.InvoicePaymentId,
                                     InvoiceId = p.InvoiceId,
                                     InvoiceNumber = inv.InvoiceNumber,
                                     PaymentAmount = p.PaymentAmount,
                                     PaymentMethod = p.PaymentMethod,
                                     TransactionId = p.TransactionId,
                                     SquarePaymentId = p.SquarePaymentId,
                                     PaymentStatus = p.PaymentStatus,
                                     PaymentDate = CommonMethods.CommonMethods.ToLocalTime(p.PaymentDate),
                                     PaidByUserId = p.PaidByUserId,
                                     PaidByName = user != null ? $"{user.FirstName} {user.LastName}" : null,
                                     PaidByEmail = user != null ? user.Email : null,
                                     PaidByPatientId = p.PaidByPatientId,
                                     PatientName = pat != null ? $"{pat.FirstName} {pat.LastName}" : null,
                                     PaidByFacilityId = p.PaidByFacilityId,
                                     FacilityName = fac != null ? (fac.TitleLong ?? fac.TitleShort) : null,
                                     CardId = p.CardId,
                                     CardLast4 = card != null ? card.Last4 : null,
                                     CardBrand = card != null ? card.CardBrand : null,
                                     CardHolderName = card != null ? card.CardHolderName : null,
                                     PaymentNotes = p.PaymentNotes,
                                     IsRefunded = p.RefundAmount.HasValue && p.RefundAmount > 0,
                                     RefundAmount = p.RefundAmount,
                                     RefundDate = p.RefundDate,
                                     RefundReason = p.RefundReason,
                                     RefundTransactionId = p.RefundTransactionId
                                 }).ToListAsync();

            var totalPaid = payments
                .Where(p => p.PaymentStatus == "Completed" && !p.IsRefunded)
                .Sum(p => p.PaymentAmount);

            var invoiceAmount = invoiceData.Invoice.Amount ?? 0;
            var remaining = invoiceAmount - totalPaid;

            return new GetInvoicePaymentSummaryResponseDTO
            {
                InvoiceId = invoiceData.Invoice.InvoiceId,
                InvoiceNumber = invoiceData.Invoice.InvoiceNumber,
                InvoiceAmount = invoiceData.Invoice.Amount,
                TotalPaidAmount = totalPaid,
                RemainingAmount = remaining,
                InvoiceStatus = invoiceData.Invoice.Status,
                InvoiceType = invoiceData.Invoice.InvoiceType,
                InvoiceDate = CommonMethods.CommonMethods.ToLocalTime(invoiceData.Invoice.CreatedDate),
                DueDate = CommonMethods.CommonMethods.ToLocalTime(invoiceData.Invoice.CreatedDate?.AddDays(29)),
                IsFullyPaid = remaining <= 0 && totalPaid > 0,
                IsPartiallyPaid = totalPaid > 0 && remaining > 0,
                IsOverpaid = totalPaid > invoiceAmount,
                PaymentCount = payments.Count,
                Payments = payments
            };
        }

        public async Task<GetPaymentsResponseDTO> GetPayments(GetPaymentsRequestDTO request)
        {

            var startDateUtc = CommonMethods.CommonMethods.LocalToUtc(request.StartDate, request.ClientTimezoneOffsetMinutes);
            var endDateUtc = CommonMethods.CommonMethods.LocalToUtc(request.EndDate, request.ClientTimezoneOffsetMinutes);

            var baseQuery = from p in _db.Sys_InvoicePayments.AsNoTracking()
                           where p.IsActive == true
                           join inv in _db.Sys_Invoices.AsNoTracking()
                               on p.InvoiceId equals inv.InvoiceId
                           where (inv.InvoiceType == InvoiceType.ClinicToGlobal.ToString() || inv.InvoiceType == InvoiceType.GAToClinic.ToString()) && inv.IsActive == true
                           join user in _db.SYS_UserDetails.AsNoTracking() on p.PaidByUserId equals user.UserId into users
                           from user in users.DefaultIfEmpty()
                           join patient in _db.PT_Patients.AsNoTracking() on p.PaidByPatientId equals patient.PatientId into patients
                           from patient in patients.DefaultIfEmpty()
                           join facility in _db.SYS_Facilities.AsNoTracking() on p.PaidByFacilityId equals facility.FacilityId into facilities
                           from facility in facilities.DefaultIfEmpty()
                           join card in _db.SYS_UserCards.AsNoTracking() on p.CardId equals card.CardId into cards
                           from card in cards.DefaultIfEmpty()
                           select new
                           {
                               Payment = p,
                               Invoice = inv,
                               User = user,
                               Patient = patient,
                               Facility = facility,
                               Card = card
                           };

            if (request.InvoiceId.HasValue)
                baseQuery = baseQuery.Where(x => x.Payment.InvoiceId == request.InvoiceId.Value);

            if (request.FacilityId.HasValue)
                baseQuery = baseQuery.Where(x => x.Payment.PaidByFacilityId == request.FacilityId.Value ||
                                                  x.Invoice.FacilityId == request.FacilityId.Value);

            if (request.PatientId.HasValue)
                baseQuery = baseQuery.Where(x => x.Payment.PaidByPatientId == request.PatientId.Value ||
                                                 x.Invoice.PatientId == request.PatientId.Value);

            if (request.UserId.HasValue)
                baseQuery = baseQuery.Where(x => x.Payment.PaidByUserId == request.UserId.Value);

            if (startDateUtc.HasValue)
                baseQuery = baseQuery.Where(x => x.Payment.PaymentDate >= startDateUtc.Value);

            if (endDateUtc.HasValue)
                baseQuery = baseQuery.Where(x => x.Payment.PaymentDate <= endDateUtc.Value);

            if (!string.IsNullOrEmpty(request.PaymentStatus))
                baseQuery = baseQuery.Where(x => x.Payment.PaymentStatus == request.PaymentStatus);

            if (!string.IsNullOrEmpty(request.InvoiceType) && !request.InvoiceId.HasValue)
                baseQuery = baseQuery.Where(x => x.Invoice.InvoiceType == request.InvoiceType);

            var totalCount = await baseQuery.CountAsync();

            var payments = await baseQuery
                .OrderByDescending(x => x.Payment.PaymentDate)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(x => new GetPaymentDetailResponseDTO
                {
                    PaymentId = x.Payment.InvoicePaymentId,
                    InvoiceId = x.Payment.InvoiceId,
                    InvoiceNumber = x.Invoice.InvoiceNumber,
                    PaymentAmount = x.Payment.PaymentAmount,
                    PaymentMethod = x.Payment.PaymentMethod,
                    TransactionId = x.Payment.TransactionId,
                    SquarePaymentId = x.Payment.SquarePaymentId,
                    PaymentStatus = x.Payment.PaymentStatus,
                    PaymentDate = CommonMethods.CommonMethods.ToLocalTime(x.Payment.PaymentDate),
                    PaidByUserId = x.Payment.PaidByUserId,
                    PaidByName = x.User != null ? $"{x.User.FirstName} {x.User.LastName}" : null,
                    PaidByEmail = x.User != null ? x.User.Email : null,
                    PaidByPatientId = x.Payment.PaidByPatientId,
                    PatientName = x.Patient != null ? $"{x.Patient.FirstName} {x.Patient.LastName}" : null,
                    PaidByFacilityId = x.Payment.PaidByFacilityId,
                    FacilityName = x.Facility != null ? (x.Facility.TitleLong ?? x.Facility.TitleShort) : null,
                    CardId = x.Payment.CardId,
                    CardLast4 = x.Card != null ? x.Card.Last4 : null,
                    CardBrand = x.Card != null ? x.Card.CardBrand : null,
                    CardHolderName = x.Card != null ? x.Card.CardHolderName : null,
                    PaymentNotes = x.Payment.PaymentNotes,
                    IsRefunded = x.Payment.RefundAmount.HasValue && x.Payment.RefundAmount > 0,
                    RefundAmount = x.Payment.RefundAmount,
                    RefundDate = x.Payment.RefundDate,
                    RefundReason = x.Payment.RefundReason,
                    RefundTransactionId = x.Payment.RefundTransactionId
                }).ToListAsync();

            return new GetPaymentsResponseDTO
            {
                Payments = payments,
                TotalCount = totalCount,
                PageNumber = request.PageNumber,
                PageSize = request.PageSize,
                TotalPages = (int)Math.Ceiling((double)totalCount / request.PageSize)
            };
        }

        public async Task<GetFacilityPaymentSummaryResponseDTO> GetFacilityPaymentSummary(long facilityId, DateTime? startDate, DateTime? endDate)
        {

            var facility = await (from f in _db.SYS_Facilities.AsNoTracking()
                                  where f.FacilityId == facilityId
                                  select f).FirstOrDefaultAsync();

            if (facility == null)
                return new GetFacilityPaymentSummaryResponseDTO();

            var invoiceQuery = from inv in _db.Sys_Invoices.AsNoTracking()
                              where inv.FacilityId == facilityId && inv.InvoiceType == InvoiceType.ClinicToGlobal.ToString() && inv.IsActive == true
                              select inv;

            if (startDate.HasValue)
                invoiceQuery = invoiceQuery.Where(i => i.CreatedDate >= startDate.Value);

            if (endDate.HasValue)
                invoiceQuery = invoiceQuery.Where(i => i.CreatedDate <= endDate.Value);

            var invoices = await invoiceQuery.ToListAsync();
            var invoiceIds = invoices.Select(i => (long)i.InvoiceId).ToList();

            var payments = await (from p in _db.Sys_InvoicePayments.AsNoTracking()
                                 where invoiceIds.Contains(p.InvoiceId) &&
                                       p.IsActive == true &&
                                       p.PaymentStatus == "Completed"
                                 select p).ToListAsync();

            var totalInvoiceAmount = invoices.Sum(i => i.Amount ?? 0);
            var totalPaid = payments
                .Where(p => !p.RefundAmount.HasValue)
                .Sum(p => p.PaymentAmount);
            var totalPending = totalInvoiceAmount - totalPaid;

            var invoiceSummaries = new List<GetInvoicePaymentSummaryResponseDTO>();
            foreach (var invoice in invoices)
            {
                invoiceSummaries.Add(await GetInvoicePaymentSummary(invoice.InvoiceId));
            }

            return new GetFacilityPaymentSummaryResponseDTO
            {
                FacilityId = facilityId,
                FacilityName = facility.TitleLong ?? facility.TitleShort,
                TotalInvoiceAmount = totalInvoiceAmount,
                TotalPaidAmount = totalPaid,
                TotalPendingAmount = totalPending,
                TotalInvoices = invoices.Count,
                PaidInvoices = invoices.Count(i => i.Status == InvoiceStatus.Paid.ToString()),
                PendingInvoices = invoices.Count(i => i.Status == InvoiceStatus.Pending.ToString() || i.Status == "PartiallyPaid"),
                Invoices = invoiceSummaries
            };
        }

        public async Task<GetAdminPaymentDashboardResponseDTO> GetAdminPaymentDashboard(DateTime? startDate, DateTime? endDate)
        {
            var paymentQuery = _db.Sys_InvoicePayments
                .AsNoTracking()
                .Where(p => p.IsActive == true);

            if (startDate.HasValue)
                paymentQuery = paymentQuery.Where(p => p.PaymentDate >= startDate.Value);

            if (endDate.HasValue)
                paymentQuery = paymentQuery.Where(p => p.PaymentDate <= endDate.Value);

            var paymentStats = await paymentQuery
                .GroupBy(p => 1)
                .Select(g => new
                {
                    TotalPayments = g.Count(),
                    TotalRevenue = g.Sum(p => p.PaymentStatus == "Completed" ? (decimal?)(p.PaymentAmount - (p.RefundAmount ?? 0m)) : 0m) ?? 0m,
                    TotalRefunded = g.Sum(p => p.RefundAmount ?? 0m),
                    SuccessfulPayments = g.Count(p => p.PaymentStatus == "Completed"),
                    FailedPayments = g.Count(p => p.PaymentStatus == "Failed"),
                    PendingPayments = g.Count(p => p.PaymentStatus == "Pending")
                })
                .FirstOrDefaultAsync();

            var pendingInvoicesQuery = _db.Sys_Invoices.AsNoTracking()
                .Where(inv => (inv.IsActive == true || inv.IsActive == null) &&
                              (inv.Status == InvoiceStatus.Pending.ToString() || inv.Status == "PartiallyPaid"));

            if (startDate.HasValue)
                pendingInvoicesQuery = pendingInvoicesQuery.Where(inv => inv.CreatedDate >= startDate.Value);

            if (endDate.HasValue)
                pendingInvoicesQuery = pendingInvoicesQuery.Where(inv => inv.CreatedDate <= endDate.Value);

            var totalPendingAmount = await pendingInvoicesQuery.SumAsync(inv => inv.Amount ?? 0m);

            var recentPayments = await paymentQuery
                .OrderByDescending(p => p.PaymentDate)
                .Take(10)
                .Select(p => new
                {
                    Payment = p,
                    Invoice = p.Invoice,
                    PaidByUser = p.PaidByUser,
                    PaidByPatient = p.PaidByPatient,
                    PaidByFacility = p.PaidByFacility,
                    Card = p.Card
                })
                .Select(x => new GetPaymentDetailResponseDTO
                {
                    PaymentId = x.Payment.InvoicePaymentId,
                    InvoiceId = x.Payment.InvoiceId,
                    InvoiceNumber = x.Invoice != null ? x.Invoice.InvoiceNumber : null,
                    PaymentAmount = x.Payment.PaymentAmount,
                    PaymentMethod = x.Payment.PaymentMethod,
                    TransactionId = x.Payment.TransactionId,
                    SquarePaymentId = x.Payment.SquarePaymentId,
                    PaymentStatus = x.Payment.PaymentStatus,
                    PaymentDate = CommonMethods.CommonMethods.ToLocalTime(x.Payment.PaymentDate),
                    PaidByUserId = x.Payment.PaidByUserId,
                    PaidByName = x.PaidByUser != null ? $"{x.PaidByUser.FirstName} {x.PaidByUser.LastName}" : null,
                    PaidByEmail = x.PaidByUser != null ? x.PaidByUser.Email : null,
                    PaidByPatientId = x.Payment.PaidByPatientId,
                    PatientName = x.PaidByPatient != null ? $"{x.PaidByPatient.FirstName} {x.PaidByPatient.LastName}" : null,
                    PaidByFacilityId = x.Payment.PaidByFacilityId,
                    FacilityName = x.PaidByFacility != null ? (x.PaidByFacility.TitleLong ?? x.PaidByFacility.TitleShort) : null,
                    CardId = x.Payment.CardId,
                    CardLast4 = x.Card != null ? x.Card.Last4 : null,
                    CardBrand = x.Card != null ? x.Card.CardBrand : null,
                    CardHolderName = x.Card != null ? x.Card.CardHolderName : null,
                    PaymentNotes = x.Payment.PaymentNotes,
                    IsRefunded = x.Payment.RefundAmount.HasValue && x.Payment.RefundAmount > 0,
                    RefundAmount = x.Payment.RefundAmount,
                    RefundDate = x.Payment.RefundDate,
                    RefundReason = x.Payment.RefundReason,
                    RefundTransactionId = x.Payment.RefundTransactionId
                })
                .ToListAsync();

            var facilityInvoicesQuery = _db.Sys_Invoices.AsNoTracking()
                .Where(inv => (inv.IsActive == true || inv.IsActive == null) &&
                              inv.InvoiceType == InvoiceType.ClinicToGlobal.ToString());

            if (startDate.HasValue)
                facilityInvoicesQuery = facilityInvoicesQuery.Where(inv => inv.CreatedDate >= startDate.Value);

            if (endDate.HasValue)
                facilityInvoicesQuery = facilityInvoicesQuery.Where(inv => inv.CreatedDate <= endDate.Value);

            var facilityAggregates = await facilityInvoicesQuery
                .GroupBy(inv => inv.FacilityId)
                .Select(g => new
                {
                    FacilityId = g.Key,
                    TotalInvoiceAmount = g.Sum(inv => inv.Amount ?? 0m),
                    TotalInvoices = g.Count(),
                    PaidInvoices = g.Count(inv => inv.Status == InvoiceStatus.Paid.ToString()),
                    PendingInvoices = g.Count(inv => inv.Status == InvoiceStatus.Pending.ToString() || inv.Status == "PartiallyPaid"),
                    TotalPaidAmount = g.Sum(inv => inv.Status == InvoiceStatus.Paid.ToString() ? inv.Amount ?? 0m : 0m)
                })
                .ToListAsync();

            var facilityIds = facilityAggregates
                .Where(f => f.FacilityId.HasValue)
                .Select(f => f.FacilityId.Value)
                .Distinct()
                .ToList();

            var facilityDictionary = await _db.SYS_Facilities.AsNoTracking()
                .Where(f => facilityIds.Contains(f.FacilityId))
                .ToDictionaryAsync(f => f.FacilityId, f => f);

            var facilitySummaries = facilityAggregates
                .Select(fa =>
                {
                    var facilitySummary = new GetFacilityPaymentSummaryResponseDTO
                    {
                        FacilityId = fa.FacilityId ?? 0,
                        FacilityName = fa.FacilityId.HasValue && facilityDictionary.ContainsKey(fa.FacilityId.Value)
                            ? (facilityDictionary[fa.FacilityId.Value].TitleLong ?? facilityDictionary[fa.FacilityId.Value].TitleShort)
                            : null,
                        TotalInvoiceAmount = fa.TotalInvoiceAmount,
                        TotalPaidAmount = fa.TotalPaidAmount,
                        TotalPendingAmount = fa.TotalInvoiceAmount - fa.TotalPaidAmount,
                        TotalInvoices = fa.TotalInvoices,
                        PaidInvoices = fa.PaidInvoices,
                        PendingInvoices = fa.PendingInvoices,
                        Invoices = new List<GetInvoicePaymentSummaryResponseDTO>()
                    };

                    if (facilitySummary.TotalPendingAmount < 0)
                        facilitySummary.TotalPendingAmount = 0;

                    return facilitySummary;
                })
                .ToList();

            var stats = paymentStats ?? new
            {
                TotalPayments = 0,
                TotalRevenue = 0m,
                TotalRefunded = 0m,
                SuccessfulPayments = 0,
                FailedPayments = 0,
                PendingPayments = 0
            };

            return new GetAdminPaymentDashboardResponseDTO
            {
                TotalRevenue = stats.TotalRevenue,
                TotalPendingPayments = totalPendingAmount,
                TotalRefundedAmount = stats.TotalRefunded,
                TotalPayments = stats.TotalPayments,
                SuccessfulPayments = stats.SuccessfulPayments,
                FailedPayments = stats.FailedPayments,
                PendingPayments = stats.PendingPayments,
                RecentPayments = recentPayments,
                FacilityPayments = facilitySummaries
            };
        }

        public async Task<bool> RefundPayment(long paymentId, decimal refundAmount, string reason, long userId)
        {
            try
            {
                var payment = _db.Sys_InvoicePayments
                    .Include(p => p.Invoice)
                    .FirstOrDefault(p => p.InvoicePaymentId == paymentId);

                if (payment == null || payment.PaymentStatus != "Completed")
                    return false;

                if (refundAmount > payment.PaymentAmount)
                    throw new Exception("Refund amount cannot exceed payment amount");

                payment.RefundAmount = refundAmount;
                payment.RefundDate = DateTime.UtcNow;
                payment.RefundReason = reason;
                payment.RefundTransactionId = $"REF-{DateTime.UtcNow:yyyyMMddHHmmss}-{paymentId}";
                payment.ModifiedBy = userId;
                payment.ModifiedDate = DateTime.UtcNow;

                var invoice = payment.Invoice;
                if (invoice != null)
                {
                    var oldStatus = invoice.Status;
                    var totalPaid = _db.Sys_InvoicePayments
                        .Where(p => p.InvoiceId == invoice.InvoiceId &&
                                   p.IsActive == true &&
                                   p.PaymentStatus == "Completed")
                        .Sum(p => p.PaymentAmount - (p.RefundAmount ?? 0));

                    if (totalPaid >= (invoice.Amount ?? 0))
                    {
                        invoice.Status = InvoiceStatus.Paid.ToString();
                    }
                    else if (totalPaid > 0)
                    {
                        invoice.Status = "PartiallyPaid";
                    }
                    else
                    {
                        invoice.Status = InvoiceStatus.Pending.ToString();
                    }

                    await _db.SaveChangesAsync();

                    if (oldStatus != invoice.Status)
                    {
                        await _auditService.LogEntityChangeAsync(
                            action: "Update",
                            entityType: "Sys_Invoice",
                            entityId: invoice.InvoiceId,
                            oldValues: new { Status = oldStatus },
                            newValues: new { Status = invoice.Status },
                            userId: userId,
                            patientId: invoice.PatientId,
                            facilityId: invoice.FacilityId,
                            description: $"Invoice {invoice.InvoiceNumber} status changed from '{oldStatus}' to '{invoice.Status}' after refund of ${refundAmount} on payment ID {paymentId}",
                            module: "Invoice"
                        );
                    }
                }
                else
                {
                    await _db.SaveChangesAsync();
                }

                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public async Task<List<GetInvoicePaymentSummaryResponseDTO>> GetPatientPaymentHistory(long patientId)
        {

            var invoices = await (from inv in _db.Sys_Invoices.AsNoTracking()
                                 where inv.PatientId == patientId && inv.IsActive == true
                                 join patient in _db.PT_Patients.AsNoTracking() on inv.PatientId equals patient.PatientId into patients
                                 from patient in patients.DefaultIfEmpty()
                                 orderby inv.CreatedDate descending
                                 select inv).ToListAsync();

            var result = new List<GetInvoicePaymentSummaryResponseDTO>();
            foreach (var invoice in invoices)
            {
                result.Add(await GetInvoicePaymentSummary(invoice.InvoiceId));
            }

            return result;
        }

        private static (DateTime Start, DateTime End) ResolveInvoiceWindow(Sys_Invoice invoice)
        {
            var endDate = invoice.CreatedDate ?? DateTime.UtcNow;

            if (invoice.IsMonthlyInvoiceGenerated == true)
            {
                var firstOfCurrentMonth = new DateTime(endDate.Year, endDate.Month, 1);
                var inferredStart = firstOfCurrentMonth.AddMonths(-1);
                return (inferredStart, endDate);
            }

            return (endDate.AddDays(-30), endDate);
        }

        private static OrderDrugItemDTO CloneOrderDrug(OrderDrugItemDTO source)
        {
            return new OrderDrugItemDTO
            {
                PrescriptionMedicineId = source.PrescriptionMedicineId,
                PatientPrescriptionId = source.PatientPrescriptionId,
                MedicineName = source.MedicineName,
                DrugId = source.DrugId,
                DaysSupplies = source.DaysSupplies,
                Injection = source.Injection,
                InjectionQuantity = source.InjectionQuantity,
                Needle = source.Needle,
                NeedleQuantity = source.NeedleQuantity,
                Direction = source.Direction,
                Instruction = source.Instruction,
                Quantity = source.Quantity,
                DrugName = source.DrugName,
                GenericName = source.GenericName,
                DosageForm = source.DosageForm,
                Strength = source.Strength,
                Price = source.Price,
                PackageSize = source.PackageSize,
                PharmacyName = source.PharmacyName
            };
        }

        private static OrderMedicineDTO CloneOrderMedicine(OrderMedicineDTO source)
        {
            return new OrderMedicineDTO
            {
                PrescriptionMedicineId = source.PrescriptionMedicineId,
                PatientPrescriptionId = source.PatientPrescriptionId,
                MedicineName = source.MedicineName,
                DrugId = source.DrugId,
                DaysSupplies = source.DaysSupplies,
                Injection = source.Injection,
                InjectionQuantity = source.InjectionQuantity,
                Needle = source.Needle,
                NeedleQuantity = source.NeedleQuantity,
                Direction = source.Direction,
                Instruction = source.Instruction,
                Quantity = source.Quantity,
                ItemDesignatorID = source.ItemDesignatorID,
                strenght = source.strenght,
                DosageForm = source.DosageForm,
                PackageSize = source.PackageSize,
                ControlSubstance = source.ControlSubstance,
                CourierMethod = source.CourierMethod,
                Supplies = source.Supplies != null
                    ? source.Supplies.Select(CloneOrderMedicineSupply).ToList()
                    : new List<OrderMedicineSupplyDTO>()
            };
        }

        private static OrderMedicineSupplyDTO CloneOrderMedicineSupply(OrderMedicineSupplyDTO source)
        {
            return new OrderMedicineSupplyDTO
            {
                MedicineSupplyId = source.MedicineSupplyId,
                PrescriptionMedicineId = source.PrescriptionMedicineId,
                SupplyDesc = source.SupplyDesc,
                SupplyQuantity = source.SupplyQuantity,
                SupplyItemDesignatorID = source.SupplyItemDesignatorID,
                Name = source.Name
            };
        }

        private static decimal CalculateWholesale(IEnumerable<OrderDrugItemDTO> drugs, IDictionary<long, decimal?> wholesaleLookup)
        {
            decimal total = 0m;

            foreach (var drug in drugs)
            {
                if (!drug.DrugId.HasValue)
                    continue;

                var quantity = ParseQuantity(drug.Quantity);
                var price = 0m;

                if (wholesaleLookup.TryGetValue(drug.DrugId.Value, out var wholesale) && wholesale.HasValue)
                {
                    price = wholesale.Value;
                }
                else if (drug.Price.HasValue)
                {
                    price = drug.Price.Value;
                }

                if (price > 0m && quantity > 0m)
                    total += price * quantity;
            }

            return total;
        }

        private static decimal ParseQuantity(string? quantity)
        {
            if (string.IsNullOrWhiteSpace(quantity))
                return 1m;

            var normalized = new string(quantity.Trim()
                .Where(c => char.IsDigit(c) || c == '.' || c == ',')
                .ToArray());

            if (string.IsNullOrWhiteSpace(normalized))
                return 1m;

            if (decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) && value > 0m)
                return value;

            if (decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.CurrentCulture, out value) && value > 0m)
                return value;

            return 1m;
        }

        private static string? FormatName(string? firstName, string? lastName)
        {
            var first = (firstName ?? string.Empty).Trim();
            var last = (lastName ?? string.Empty).Trim();
            var full = string.Join(" ", new[] { first, last }.Where(part => !string.IsNullOrWhiteSpace(part)));
            return string.IsNullOrWhiteSpace(full) ? null : full;
        }

        public async Task<bool> AutoPayFacilityInvoice(long invoiceId)
        {
            try
            {
                var invoice = _db.Sys_Invoices.Where(x => x.InvoiceId == invoiceId).FirstOrDefault();

                if (invoice == null)
                {
                    return false;
                }

                if (invoice.InvoiceType != InvoiceType.ClinicToGlobal.ToString())
                {
                    return false;
                }

                if (invoice.Status != InvoiceStatus.Pending.ToString())
                {
                    return false;
                }

                if (!invoice.Amount.HasValue || invoice.Amount.Value <= 0)
                {
                    return false;
                }

                if (!invoice.FacilityId.HasValue)
                {
                    return false;
                }

                var facilityAdminUser = (from uif in _db.FC_UsersInFacilities
                                        join ud in _db.SYS_UserDetails on uif.UserId equals ud.UserId
                                        join l in _db.SYS_Logins on ud.LoginId equals l.LoginId
                                        where uif.FacilityId == invoice.FacilityId.Value
                                            && l.RoleId == 3
                                            && ud.IsActive == true
                                            && ud.Status == "Active"
                                            && uif.IsAssign == true
                                        select ud)
                                        .Include(x => x.SYS_UserCards)
                                        .FirstOrDefault();

                if (facilityAdminUser == null)
                {
                    return false;
                }

                var amount = invoice.Amount.Value;
                var facilityId = invoice.FacilityId.Value;
                int amountInCents = (int)Math.Round(amount * 100, MidpointRounding.AwayFromZero);
                var autoPayMode = ResolveFacilityPaymentMode(facilityId);

                if (autoPayMode == (int)FacilityPaymentMode.Stripe)
                {
                    if (_stripeAccountResolver == null || _stripePlatformCharge == null)
                        return false;
                    if (string.IsNullOrWhiteSpace(facilityAdminUser.StripePlatformCustomerId))
                        return false;
                    var facilityStripeAccountId = await _stripeAccountResolver.GetStripeAccountIdForFacilityAsync(facilityId).ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(facilityStripeAccountId))
                        return false;
                    var piId = await _stripePlatformCharge.ChargeCustomerAsync(
                        facilityAdminUser.StripePlatformCustomerId,
                        amountInCents,
                        "usd",
                        default,
                        new StripePlatformGaChargeContext
                        {
                            ConnectedAccountId = facilityStripeAccountId,
                            FacilityId = facilityId
                        }).ConfigureAwait(false);
                    if (!string.IsNullOrEmpty(piId))
                    {
                        await RecordPayment(
                            invoiceId: invoiceId,
                            amount: amount,
                            transactionId: piId,
                            squarePaymentId: null,
                            userId: facilityAdminUser.UserId,
                            patientId: null,
                            facilityId: facilityId,
                            cardId: null,
                            paymentMethod: "Stripe",
                            notes: $"Auto-payment after 7 days - Stripe PaymentIntent {piId}").ConfigureAwait(false);
                        _db.SaveChanges();
                        return true;
                    }
                    return false;
                }

                if (facilityAdminUser.SYS_UserCards == null || !facilityAdminUser.SYS_UserCards.Any())
                {
                    return false;
                }

                var squareEligibleAdmin = facilityAdminUser.SYS_UserCards
                    .Where(c => (c.IsActive == true || c.IsActive == null) && !string.IsNullOrEmpty(c.SquareCardId))
                    .ToList();
                var userCard = squareEligibleAdmin.FirstOrDefault(c => c.IsDefault) ?? squareEligibleAdmin.FirstOrDefault();
                if (userCard == null)
                {
                    return false;
                }

                long? squareChargeFacilityId = GetSquareChargeFacilityId(invoice);

                CreatePaymentResponse response = await _squarPaymentRepo.ChargeCustomerWithSavedCard(
                    userCard.SquareClientId,
                    userCard.SquareCardId.ToString(),
                    amountInCents,
                    "USD",
                    squareChargeFacilityId);

                if (response.Payment != null && response.Payment.Status == "COMPLETED")
                {

                    var transactionId = response.Payment.Id ?? Guid.NewGuid().ToString();
                    var squarePaymentId = response.Payment.Id;

                    await RecordPayment(
                        invoiceId: invoiceId,
                        amount: amount,
                        transactionId: transactionId,
                        squarePaymentId: squarePaymentId,
                        userId: facilityAdminUser.UserId,
                        patientId: null,
                        facilityId: facilityId,
                        cardId: userCard.CardId,
                        paymentMethod: "Card",
                        notes: $"Auto-payment after 7 days - Card ending in {userCard.Last4}"
                    );

                    invoice.CardId = userCard.CardId;
                    _db.SaveChanges();
                    return true;
                }
                else
                {

                    try
                    {
                        var failedPayment = new Sys_InvoicePayment
                        {
                            InvoiceId = (int)invoiceId,
                            PaymentAmount = amount,
                            PaymentMethod = "Card",
                            TransactionId = response?.Payment?.Id ?? Guid.NewGuid().ToString(),
                            SquarePaymentId = response?.Payment?.Id,
                            PaymentStatus = "Failed",
                            PaidByUserId = facilityAdminUser.UserId,
                            PaidByFacilityId = facilityId,
                            CardId = userCard.CardId,
                            PaymentNotes = $"Auto-payment failed: {response?.Payment?.Status ?? "Unknown error"}",
                            PaymentDate = DateTime.UtcNow,
                            IsActive = true,
                            CreatedBy = facilityAdminUser.UserId,
                            CreatedDate = DateTime.UtcNow
                        };
                        _db.Sys_InvoicePayments.Add(failedPayment);
                        _db.SaveChanges();
                    }
                    catch {  }

                    return false;
                }
            }
            catch (Exception ex)
            {

                throw new Exception($"Failed to auto-pay facility invoice {invoiceId}: {ex.Message}", ex);
            }
        }

        public async Task<CreateManualClinicToPatientInvoiceResponseDTO> CreateManualClinicToPatientInvoice(
            CreateManualClinicToPatientInvoiceRequestDTO request,
            long userId)
        {
            try
            {

                var invoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}";

                var patient = await _db.PT_Patients
                    .AsNoTracking()
                    .Where(p => p.PatientId == request.PatientId)
                    .Select(p => new { p.FirstName, p.LastName })
                    .FirstOrDefaultAsync();

                var customerName = patient != null
                    ? $"{patient.FirstName} {patient.LastName}".Trim()
                    : "Patient";

                var invoice = new Sys_Invoice
                {
                    InvoiceNumber = invoiceNumber,
                    CustomerName = customerName,
                    Amount = request.AmountDue,
                    Status = InvoiceStatus.Pending.ToString(),
                    InvoiceType = InvoiceType.ClinicToPatient.ToString(),
                    IsActive = true,
                    CreatedBy = userId,
                    CreatedDate = request.InvoiceDate ?? DateTime.UtcNow,
                    FacilityId = request.FacilityId,
                    PatientId = request.PatientId
                };

                _db.Sys_Invoices.Add(invoice);
                await _db.SaveChangesAsync();

                var lineItems = request.Items.Select(drug => new Sys_InvoiceLineItem
                {
                    InvoiceId = invoice.InvoiceId,
                    DrugId = drug.DrugId,
                    ProductId = drug.ProductId,
                    ProductLineItemName = drug.ProductLineItemName,
                    Qty = drug.Qty,
                    UnitPrice = drug.UnitPrice,
                    LineTotal = drug.LineTotal,
                    IsActive = true,
                    CreatedBy = userId,
                    CreatedDate = DateTime.UtcNow
                }).ToList();

                _db.Sys_InvoiceLineItems.AddRange(lineItems);
                await _db.SaveChangesAsync();

                return new CreateManualClinicToPatientInvoiceResponseDTO
                {
                    InvoiceId = invoice.InvoiceId,
                    InvoiceNumber = invoice.InvoiceNumber,
                    InvoiceDate = CommonMethods.CommonMethods.ToLocalTime(invoice.CreatedDate),
                    DueDate = invoice.CreatedDate.HasValue
                        ? CommonMethods.CommonMethods.ToLocalTime(invoice.CreatedDate.Value.AddDays(29))
                        : (DateTime?)null,
                    AmountDue = invoice.Amount,
                    Status = invoice.Status,
                    Success = true,
                    Message = "Manual invoice created successfully"
                };
            }
            catch (Exception ex)
            {
                return new CreateManualClinicToPatientInvoiceResponseDTO
                {
                    Success = false,
                    Message = $"Failed to create manual invoice: {ex.Message}"
                };
            }
        }

        public async Task<CreateManualClinicToPatientInvoiceResponseDTO> CreateManualGAToClinicInvoice(
            CreateManualGAToClinicInvoiceRequestDTO request,
            long userId)
        {
            try
            {

                var invoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}";

                var facility = await _db.SYS_Facilities
                    .AsNoTracking()
                    .Where(f => f.FacilityId == request.FacilityId)
                    .Select(f => new { f.TitleShort, f.TitleLong })
                    .FirstOrDefaultAsync();

                var customerName = facility != null
                    ? (facility.TitleLong ?? facility.TitleShort ?? "Clinic")
                    : "Clinic";

                var invoice = new Sys_Invoice
                {
                    InvoiceNumber = invoiceNumber,
                    CustomerName = customerName,
                    Amount = request.AmountDue,
                    Status = InvoiceStatus.Pending.ToString(),
                    InvoiceType = InvoiceType.GAToClinic.ToString(),
                    IsActive = true,
                    CreatedBy = userId,
                    CreatedDate = request.InvoiceDate ?? DateTime.UtcNow,
                    FacilityId = request.FacilityId,
                    PatientId = null
                };

                _db.Sys_Invoices.Add(invoice);
                await _db.SaveChangesAsync();

                var lineItems = request.Items.Select(item => new Sys_InvoiceLineItem
                {
                    InvoiceId = invoice.InvoiceId,
                    DrugId = null,
                    ProductId = null,
                    ProductLineItemName = item.ProductLineItemName,
                    Qty = item.Qty,
                    UnitPrice = item.UnitPrice,
                    LineTotal = item.LineTotal,
                    IsActive = true,
                    CreatedBy = userId,
                    CreatedDate = DateTime.UtcNow
                }).ToList();

                _db.Sys_InvoiceLineItems.AddRange(lineItems);
                await _db.SaveChangesAsync();

                return new CreateManualClinicToPatientInvoiceResponseDTO
                {
                    InvoiceId = invoice.InvoiceId,
                    InvoiceNumber = invoice.InvoiceNumber,
                    InvoiceDate = CommonMethods.CommonMethods.ToLocalTime(invoice.CreatedDate),
                    DueDate = invoice.CreatedDate.HasValue
                        ? CommonMethods.CommonMethods.ToLocalTime(invoice.CreatedDate.Value.AddDays(29))
                        : (DateTime?)null,
                    AmountDue = invoice.Amount,
                    Status = invoice.Status,
                    Success = true,
                    Message = "Manual GA-to-Clinic invoice created successfully"
                };
            }
            catch (Exception ex)
            {
                return new CreateManualClinicToPatientInvoiceResponseDTO
                {
                    Success = false,
                    Message = $"Failed to create GA-to-Clinic manual invoice: {ex.Message}"
                };
            }
        }

        public async Task<bool> CancelInvoice(long invoiceId, string? cancellationReason = null)
        {
            var invoice = await _db.Sys_Invoices.FirstOrDefaultAsync(x => x.InvoiceId == invoiceId);
            if (invoice == null)
            {
                return false;
            }

            var isSupportedType =
                invoice.InvoiceType == InvoiceType.PatientToClinic.ToString() ||
                invoice.InvoiceType == InvoiceType.ClinicToPatient.ToString();

            if (!isSupportedType)
            {
                return false;
            }

            var oldStatus = invoice.Status;
            invoice.Status = "Cancelled";
            await _db.SaveChangesAsync();

            await _notificationService.SendInvoiceCancelledNotificationAsync(invoice.InvoiceId, cancellationReason);

            await _auditService.LogEntityChangeAsync(
                action: "Update",
                entityType: "Sys_Invoice",
                entityId: invoice.InvoiceId,
                oldValues: new { Status = oldStatus },
                newValues: new { Status = invoice.Status, CancellationReason = cancellationReason },
                userId: invoice.CreatedBy,
                patientId: invoice.PatientId,
                facilityId: invoice.FacilityId,
                description: $"Invoice {invoice.InvoiceNumber ?? invoice.InvoiceId.ToString()} cancelled",
                module: "Invoice");
            return true;
        }
    }
}
