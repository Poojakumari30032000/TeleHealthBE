using AutoMapper;
using DudeMeds.Models.Repos.Interfaces;
using DudeMeds.Models.Repos.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Square;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Linq;
using Vitality;
using Vitality.Configuration;
using Vitality.Filters;
using Vitality.Models.AutoMapper;
using Vitality.Models.EntityClasses;
using Vitality.Models.Repos;
using Vitality.Models.Repos.Interfaces;
using Vitality.Models.Repos.Services;
using Vitality.Models.Repos.Zoom;
using Vitality.Services.Auth;
using Vitality.Services.Email;
using Vitality.Services.Sms;
using Twilio.Clients;
using Vitality.Models.Repos.Services.S3;
using Vitality.Models.Schedulers;

var MyAllowSpecificOrigins = "_myAllowSpecificOrigins";
var builder = WebApplication.CreateBuilder(args);

// -----------------------------------------------------------------------------
//  Configuration sources, in the order they are applied by CreateBuilder:
//
//    1. appsettings.json                  (non-secret, committed)
//    2. appsettings.{Environment}.json    (non-secret, committed)
//    3. user secrets                      (Development only, outside the repo)
//    4. environment variables             (staging / production)
//    5. command-line arguments
//
//  Later sources override earlier ones, so a server's environment variables
//  always win over anything in a committed file. Nothing extra needs
//  registering here: user secrets are picked up automatically in Development
//  because Vitality.csproj declares a <UserSecretsId>, and environment
//  variables are always present.
//
//  Fail fast before anything else touches configuration - jwtSettings.Key is
//  dereferenced a few lines below, and an empty signing key would otherwise
//  fail much later with an unhelpful message.
// -----------------------------------------------------------------------------
builder.ValidateRequiredSecrets();

var jwtSettingsSection = builder.Configuration.GetSection("Jwt");
builder.Services.Configure<JwtSettings>(jwtSettingsSection);
var jwtSettings = jwtSettingsSection.Get<JwtSettings>() ?? throw new InvalidOperationException("JWT configuration is missing.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    // .NET 8 note: JwtBearer now validates with JsonWebTokenHandler by default
    // instead of JwtSecurityTokenHandler. TokenValidationParameters below are
    // honoured identically by both, and MapInboundClaims keeps its default of
    // true in both, so the claims this application reads ("UserId", "RoleId",
    // ClaimTypes.Role, ...) are unaffected.
    //
    // If a token-validation problem does appear after the upgrade, this single
    // line restores the .NET 6 handler wholesale:
    //     options.UseSecurityTokenValidators = true;
    // It is deprecated in .NET 9, so treat it as a temporary diagnostic rather
    // than a fix.
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
        ValidateIssuer = !string.IsNullOrWhiteSpace(jwtSettings.Issuer),
        ValidateAudience = !string.IsNullOrWhiteSpace(jwtSettings.Audience),
        ValidIssuer = jwtSettings.Issuer,
        ValidAudience = jwtSettings.Audience,
        RequireExpirationTime = true,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(5),
        NameClaimType = JwtRegisteredClaimNames.Sub,
        RoleClaimType = ClaimTypes.Role
    };

    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var path = context.HttpContext.Request.Path;

            if (path.StartsWithSegments("/ChatHub"))
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken))
                {
                    context.Token = accessToken;
                    Console.WriteLine($"SignalR: Token extracted from query string for path: {path}");
                    return Task.CompletedTask;
                }
            }

            if (string.IsNullOrEmpty(context.Token))
            {
                var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(authHeader))
                {
                    var sanitized = authHeader.Trim().Trim('"', '\'');
                    if (sanitized.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        var tokenValue = sanitized["Bearer ".Length..].Trim().Trim('"', '\'');
                        if (!string.IsNullOrWhiteSpace(tokenValue))
                        {
                            context.Token = tokenValue;
                            Console.WriteLine($"Token extracted from Authorization header for path: {path}");
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(context.Token) && path.StartsWithSegments("/ChatHub"))
            {
                Console.WriteLine($"WARNING: No token found for SignalR connection on path: {path}");
            }

            return Task.CompletedTask;
        },
        OnTokenValidated = async context =>
        {
            var path = context.HttpContext.Request.Path;

            var userIdClaim = context.Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                ?? context.Principal?.FindFirst("UserId")?.Value;
            var roleIdClaim = context.Principal?.FindFirst("RoleId")?.Value;

            if (!string.IsNullOrEmpty(userIdClaim) && long.TryParse(userIdClaim, out var userId))
            {

                var dbContext = context.HttpContext.RequestServices.GetRequiredService<MainContext>();

                bool isUserActive = false;
                if (!string.IsNullOrEmpty(roleIdClaim) && int.TryParse(roleIdClaim, out var roleId))
                {
                    if (roleId == 6)
                    {

                        var user = await dbContext.SYS_UserDetails
                            .AsNoTracking()
                            .FirstOrDefaultAsync(u => u.UserId == userId);

                        if (user != null && user.IsActive == true && user.Status == "Active")
                        {

                            var patient = await dbContext.PT_Patients
                                .AsNoTracking()
                                .FirstOrDefaultAsync(p => p.LoginId == user.LoginId);
                            isUserActive = patient != null && patient.IsActive == true && patient.Status == "Active";
                        }
                        else
                        {
                            isUserActive = false;
                        }
                    }
                    else
                    {
                        var user = await dbContext.SYS_UserDetails
                            .AsNoTracking()
                            .FirstOrDefaultAsync(u => u.UserId == userId);
                        isUserActive = user != null && user.IsActive == true && user.Status == "Active";
                    }
                }

                if (!isUserActive)
                {

                    context.Fail("User account has been disabled. Please contact your administrator.");
                    return;
                }
            }

            if (path.StartsWithSegments("/ChatHub"))
            {
                // .NET 8 changed the default token handler from
                // JwtSecurityTokenHandler to JsonWebTokenHandler, so
                // TokenValidatedContext.SecurityToken now arrives as a
                // JsonWebToken rather than a JwtSecurityToken. The old
                // single-type pattern match still compiled but always fell
                // through, quietly logging "N/A" for every SignalR connection.
                // Handling both types keeps this diagnostic useful and works
                // whichever handler is active.
                var issuer = context.SecurityToken switch
                {
                    System.IdentityModel.Tokens.Jwt.JwtSecurityToken jwt => jwt.Issuer,
                    Microsoft.IdentityModel.JsonWebTokens.JsonWebToken jsonWebToken => jsonWebToken.Issuer,
                    _ => "N/A"
                };
                Console.WriteLine($"SignalR: Token validated successfully for user: {userIdClaim} on path: {path}");
                Console.WriteLine($"  Token Issuer: {issuer}");
                Console.WriteLine($"  Expected Issuer: {jwtSettings.Issuer}");
            }
        },
        OnAuthenticationFailed = context =>
        {

            var path = context.HttpContext.Request.Path;
            if (path.StartsWithSegments("/ChatHub"))
            {

                var token = context.Request.Query["access_token"].FirstOrDefault();
                Console.WriteLine($"Authentication failed for SignalR connection:");
                Console.WriteLine($"  Path: {path}");
                Console.WriteLine($"  Error: {context.Exception.Message}");
                Console.WriteLine($"  Exception Type: {context.Exception.GetType().Name}");
                Console.WriteLine($"  Token Present: {!string.IsNullOrEmpty(token)}");
                Console.WriteLine($"  Token Length: {token?.Length ?? 0}");
                Console.WriteLine($"  Inner Exception: {context.Exception.InnerException?.Message ?? "None"}");

                Console.WriteLine($"  Validation Settings:");
                Console.WriteLine($"    ValidateIssuer: {context.Options.TokenValidationParameters.ValidateIssuer}");
                Console.WriteLine($"    Expected Issuer: {jwtSettings.Issuer}");
                Console.WriteLine($"    ValidateAudience: {context.Options.TokenValidationParameters.ValidateAudience}");
                Console.WriteLine($"    Expected Audience: {jwtSettings.Audience}");
                Console.WriteLine($"    ClockSkew: {context.Options.TokenValidationParameters.ClockSkew}");

                context.NoResult();
                return Task.CompletedTask;
            }

            context.NoResult();
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            var payload = JsonSerializer.Serialize(new
            {
                error = "AuthenticationFailed",
                detail = context.Exception.Message
            });
            return context.Response.WriteAsync(payload);
        },
        OnChallenge = context =>
        {

            var path = context.HttpContext.Request.Path;
            if (path.StartsWithSegments("/ChatHub"))
            {

                context.HandleResponse();
                return Task.CompletedTask;
            }

            if (!context.Handled && !context.Response.HasStarted)
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                var payload = JsonSerializer.Serialize(new
                {
                    error = "Unauthorized",
                    detail = string.IsNullOrWhiteSpace(context.ErrorDescription)
                        ? "The access token is missing or invalid."
                        : context.ErrorDescription
                });
                return context.Response.WriteAsync(payload);
            }

            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Vitality.API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme."
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                          new OpenApiSecurityScheme
                            {
                                Reference = new OpenApiReference
                                {
                                    Type = ReferenceType.SecurityScheme,
                                    Id = "Bearer"
                                }
                            },
                            new string[] {}

                    }
                });
});

// EF Core 8 translates `collection.Contains(entity.Column)` as OPENJSON(@p)
// rather than IN (@p0, @p1, ...). Around 108 queries in this codebase use that
// shape, and OPENJSON requires the target DATABASE to be at compatibility level
// 130 or higher (SQL Server 2016+). Check it with:
//
//   SELECT name, compatibility_level FROM sys.databases WHERE name = '<db>';
//
// If it is below 130, set Database:SqlServerCompatibilityLevel to 120 in
// appsettings.json to restore the EF Core 6 form globally, with no query edits.
// MainContext.OnConfiguring reads the same setting for the repositories that
// build their own context.
var sqlServerCompatibilityLevel = MainContext.ReadSqlServerCompatibilityLevel(builder.Configuration);

builder.Services.AddDbContext<MainContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("dbConnection"),
        sql =>
        {
            if (sqlServerCompatibilityLevel > 0)
            {
                sql.UseCompatibilityLevel(sqlServerCompatibilityLevel);
            }
        }));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<Vitality.Models.Repos.Services.Audit.IAuditService, Vitality.Models.Repos.Services.Audit.AuditService>();

builder.Services.AddTransient<IAccountsRepo, AccountsRepo>();
builder.Services.AddTransient<IDropDownsRepo, DropDownsRepo>();
builder.Services.AddTransient<IUsersRepo, UsersRepo>();
builder.Services.AddTransient<IFacilitiesRepo, FacilitiesRepo>();
builder.Services.AddTransient<IProviderSchedulesRepo, ProviderSchedulesRepo>();
builder.Services.AddTransient<IProviderHoursRepo, ProviderHoursRepo>();
builder.Services.AddSingleton<Vitality.Models.Repos.Services.Schedules.TimezoneConverter>();
builder.Services.AddSingleton<Vitality.Models.Repos.Services.Validators.ProviderHoursValidator>();
builder.Services.AddScoped<Vitality.Models.Repos.Services.Schedules.SlotMaterializer>();
builder.Services.AddTransient<IPatientAppointmentsRepo, PatientAppointmentsRepo>();
builder.Services.AddTransient<IQuestionnairesRepo, QuestionnairesRepo>();
builder.Services.AddTransient<IProductsRepo, ProductsRepo>();
builder.Services.AddTransient<IProductCategoriesRepo, ProductCategoriesRepo>();
builder.Services.AddTransient<IProductConditionsRepo, ProductConditionsRepo>();
builder.Services.AddTransient<IPatientPaymentsRepo, PatientsPaymentsRepo>();
builder.Services.AddTransient<IPharmaciesRepo, PharmaciesRepo>();
builder.Services.AddTransient<ITicketsRepo, TicketsRepo>();
builder.Services.AddTransient<IBrandsRepo, BrandsRepo>();
builder.Services.AddTransient<INotificationsRepo, NotificationsRepo>();
builder.Services.AddTransient<IDashboardsRepo, DashboardsRepo>();
builder.Services.AddTransient<ISubscriptionsRepo, SubscriptionsRepo>();
builder.Services.AddTransient<IInvoiceRepo, InvoiceRepo>();
builder.Services.AddTransient<IPrescriptionMedicinesRepo, PrescriptionMedicinesRepo>();
builder.Services.AddScoped<ISquarePaymentRepo, SquarePaymentRepo>();
builder.Services.AddScoped<IEmpowerPharmacyService, EmpowerPharmacyService>();
builder.Services.AddScoped<IEmpowerWebhookService, EmpowerWebhookService>();
builder.Services.AddScoped<ICouponRepo, CouponRepo>();
builder.Services.AddHttpClient<Vitality.Models.Repos.Interfaces.IFullscriptService, Vitality.Models.Repos.Services.FullscriptService>();
builder.Services.AddHttpClient<Vitality.Models.Repos.Interfaces.IEhrWebhookService, Vitality.Models.Repos.Services.EhrWebhookService>();
builder.Services.AddScoped<Vitality.Models.Repos.Interfaces.IFullscriptService, Vitality.Models.Repos.Services.FullscriptService>();
builder.Services.AddScoped<IInvoicePdfService, InvoicePdfService>();
builder.Services.AddTransient<Vitality.Models.Repos.Interfaces.IAuditLogsRepo, Vitality.Models.Repos.Services.AuditLogsRepo>();
builder.Services.AddScoped<Vitality.Models.Repos.Interfaces.IFailedEmailLogsRepo, Vitality.Models.Repos.Services.FailedEmailLogsRepo>();

builder.Services.AddHttpClient<ChatsRepo>();
builder.Services.AddScoped<ChatsRepo>();
builder.Services.AddScoped<ChatValidationService>();
builder.Services.AddScoped<ChatChannelService>();
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
    options.MaximumReceiveMessageSize = 1024 * 1024;
    options.StreamBufferCapacity = 10;
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.HandshakeTimeout = TimeSpan.FromSeconds(15);
});

builder.Services.Configure<Vitality.Models.Schedulers.SchedulerSettings>(
    builder.Configuration.GetSection("SchedulerSettings"));

// The six background schedulers write to the database the moment the process
// starts - they do their work BEFORE their first Task.Delay, so lengthening an
// interval does not stop the first pass. A developer whose connection string
// points at a shared database therefore mutates it just by pressing F5:
// ProviderHoursMaterializationService deletes and recreates every Available
// slot, ReconciliationService writes an audit row per finding, and
// RecurringPaymentService charges real cards.
//
// So they are OFF by default in Development and ON everywhere else. Deployed
// environments need no config change and are unaffected.
//
// To run them locally anyway - pointing at a throwaway database, please - set:
//     "SchedulerSettings": { "Enabled": true }
// The flag is honoured in both directions, so Enabled:false also disables them
// on a server if one ever needs to be quiesced without a redeploy.
var schedulersEnabled = builder.Configuration.GetValue<bool?>("SchedulerSettings:Enabled")
                        ?? !builder.Environment.IsDevelopment();

if (schedulersEnabled)
{
    builder.Services.AddHostedService<MonthlyEventSchedulerService>();
    builder.Services.AddHostedService<InvoicePdfS3UploadService>();
    builder.Services.AddHostedService<RecurringPaymentService>();
    builder.Services.AddHostedService<IntakeReminderService>();
    builder.Services.AddHostedService<Vitality.Models.Schedulers.ReconciliationService>();
    builder.Services.AddHostedService<Vitality.Models.Schedulers.ProviderHoursMaterializationService>();
}
builder.Services.Configure<ZoomOptions>(builder.Configuration.GetSection("Zoom"));
builder.Services.AddMemoryCache();
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection(SmtpSettings.SectionName));
builder.Services.AddScoped<MailSender>();
builder.Services.AddScoped<IMailSender>(sp => new FailedEmailLoggingMailSender(
    sp.GetRequiredService<MailSender>(),
    sp.GetRequiredService<MainContext>(),
    sp.GetRequiredService<ILogger<FailedEmailLoggingMailSender>>()));
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.Configure<TwilioSettings>(builder.Configuration.GetSection(TwilioSettings.SectionName));
builder.Services.AddSingleton<ITwilioRestClient>(sp =>
{
    var settings = sp.GetRequiredService<IOptions<TwilioSettings>>().Value;
    if (string.IsNullOrWhiteSpace(settings.AccountSid) || string.IsNullOrWhiteSpace(settings.AuthToken))
    {
        throw new InvalidOperationException("Twilio credentials are not configured.");
    }

    return new TwilioRestClient(settings.AccountSid, settings.AuthToken);
});
builder.Services.AddScoped<ITwilioClientWrapper, TwilioClientWrapper>();
builder.Services.AddScoped<ISmsSender, TwilioSmsSender>();
builder.Services.AddScoped<Vitality.Models.Repos.Services.Email.BackgroundEmailService>();
builder.Services.AddScoped<Vitality.Models.Repos.Services.INotificationService, Vitality.Services.Notifications.NotificationService>();

builder.Services.AddSingleton<Vitality.Models.Repos.Services.Auth.IPasswordHasher, Vitality.Models.Repos.Services.Auth.PasswordHasher>();

// -----------------------------------------------------------------------------
//  RBAC (role-based access control)
//
//  IPermissionResolver is the single authority for permission decisions - the
//  [RequiresPermission] filter, GET api/Roles/myPermissions and any service-level
//  check all resolve through it, so they cannot disagree.
//
//  Scoped, because both take the request-scoped MainContext. Role -> permission
//  grants are cached in the IMemoryCache registered above; the user -> role lookup
//  deliberately is not, so a role change takes effect on the caller's next request
//  instead of when their 30-day token expires.
// -----------------------------------------------------------------------------
builder.Services.AddScoped<Vitality.Models.Repos.Services.Security.IPermissionResolver,
                           Vitality.Models.Repos.Services.Security.PermissionResolver>();
builder.Services.AddScoped<Vitality.Models.Repos.Interfaces.IRolesRepo,
                           Vitality.Models.Repos.Services.RolesRepo>();

builder.Services.Configure<S3Settings>(builder.Configuration.GetSection(S3Settings.SectionName));
builder.Services.AddScoped<IS3Service, S3Service>();

builder.Services.AddHttpClient("ZoomV2", c =>
{
    c.BaseAddress = new Uri("https://api.zoom.us/v2/");
});

builder.Services.AddScoped<IZoomTokenProvider, ZoomTokenProvider>();
builder.Services.AddScoped<IAppointmentZoomService, AppointmentZoomService>();
var mapperConfiguration = new MapperConfiguration(mc =>
{
    mc.AddProfile(new AutoMapperProfiles());
});
IMapper mapper = mapperConfiguration.CreateMapper();
builder.Services.AddSingleton(mapper);
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: MyAllowSpecificOrigins,
                      policy =>
                      {
                          policy.WithOrigins("http://localhost:4200",
                                             "https://localhost:4200",
                                             "http://localhost:7039",
                                             "https://localhost:7039",
                                             "https://vitality.thequantumz.com",
                                             "https://crmxofront.bitzage.com",
                                             "https://isolhealth.bitzage.com",
                                             "https://impacthealthos.com",
                                             "https://www.impacthealthos.com",
                                             "https://telehealthus.com",
                                             "https://www.telehealthus.com"
                                             ).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
                      });
});

builder.Services.AddControllers(options =>
{
    var policy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    options.Filters.Add(new AuthorizeFilter(policy));
})
.AddJsonOptions(options =>
{

    options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
});

var squareSection = builder.Configuration.GetSection("Square");
var staticAccessToken = squareSection["AccessToken"];
builder.Services.AddSingleton<SquareClient>(sp =>
{
    var environment = squareSection["Environment"]?.ToLowerInvariant() == "sandbox"
        ? Square.Environment.Sandbox
        : Square.Environment.Production;

    if (string.IsNullOrWhiteSpace(staticAccessToken))
    {
        // Deliberately still returns a client rather than throwing: Square is
        // one integration among many, and a missing token should disable
        // facility-to-global-admin payments, not stop the whole API from
        // starting. The placeholder token guarantees any real call fails
        // loudly, and this warning names the setting to fix.
        sp.GetRequiredService<ILogger<Program>>().LogWarning(
            "Square:AccessToken is not set. Facility-to-Global-Admin payments (ClinicToGlobal) will fail. " +
            "Supply it via the Square__AccessToken environment variable (staging/production) or " +
            "dotnet user-secrets set \"Square:AccessToken\" \"<value>\" (development). See SECRETS.md.");

        return new SquareClient.Builder()
            .Environment(environment)
            .AccessToken("GLOBAL_ADMIN_STATIC_TOKEN_REQUIRED")
            .Build();
    }

    return new SquareClient.Builder()
        .Environment(environment)
        .AccessToken(staticAccessToken)
        .Build();
});

builder.Services.AddMemoryCache();
builder.Services.AddScoped<IFacilitySquareClientProvider, FacilitySquareClientProvider>();
builder.Services.AddScoped<Vitality.Models.Repos.Interfaces.ISquareOAuthService, Vitality.Models.Repos.Services.SquareOAuthService>();

var stripeSecretKey = builder.Configuration["Stripe:SecretKey"]?.Trim();
var stripeSecretKeyIsUsable =
    !string.IsNullOrEmpty(stripeSecretKey) &&
    (stripeSecretKey.StartsWith("sk_", StringComparison.OrdinalIgnoreCase) ||
     stripeSecretKey.StartsWith("rk_", StringComparison.OrdinalIgnoreCase));

// Registered as a factory rather than a pre-built instance so the missing-key
// case can be logged. Previously the placeholder was substituted silently, and
// the first sign of trouble was an authentication error from Stripe with
// nothing pointing back to the configuration.
builder.Services.AddSingleton(sp =>
{
    if (!stripeSecretKeyIsUsable)
    {
        sp.GetRequiredService<ILogger<Program>>().LogWarning(
            "Stripe:SecretKey is missing or malformed (expected a 'sk_' or 'rk_' prefix). " +
            "All Stripe payments, Connect onboarding and subscriptions will fail. " +
            "Supply it via the Stripe__SecretKey environment variable (staging/production) or " +
            "dotnet user-secrets set \"Stripe:SecretKey\" \"<value>\" (development). See SECRETS.md.");

        return new Stripe.StripeClient("sk_placeholder_Stripe_SecretKey_is_not_configured");
    }

    // Null-forgiving: stripeSecretKeyIsUsable already proved it is non-empty,
    // but the compiler cannot carry that through the closure.
    return new Stripe.StripeClient(stripeSecretKey!);
});
builder.Services.AddHttpClient("StripeV2", c =>
{
    c.BaseAddress = new Uri("https://api.stripe.com");
    c.DefaultRequestHeaders.Add("Stripe-Version", "2025-04-30.basil");
});
builder.Services.AddScoped<Vitality.Services.Stripe.IStripeConnectService, Vitality.Services.Stripe.StripeConnectService>();
builder.Services.AddScoped<Vitality.Models.Repos.Interfaces.IStripeAccountResolver, Vitality.Services.Stripe.StripePaymentVerifier>();
builder.Services.AddScoped<Vitality.Models.Repos.Interfaces.IStripePaymentVerifier, Vitality.Services.Stripe.StripePaymentVerifier>();
builder.Services.AddScoped<Vitality.Models.Repos.Interfaces.IStripeRecurringCharge, Vitality.Services.Stripe.StripeRecurringChargeService>();
builder.Services.AddScoped<Vitality.Models.Repos.Interfaces.IStripeSavePaymentMethod, Vitality.Services.Stripe.StripeSavePaymentMethodService>();
builder.Services.AddScoped<Vitality.Models.Repos.Interfaces.IStripePlatformCharge, Vitality.Services.Stripe.StripePlatformChargeService>();
builder.Services.AddScoped<Vitality.Models.Repos.Interfaces.IOnboardingCompletePlatformCustomerService, Vitality.Services.Stripe.OnboardingCompletePlatformCustomerService>();

var app = builder.Build();

// Report every optional secret that is absent, plus sanity checks on values
// that are present but look wrong (a Stripe webhook secret that is not a
// whsec_, a connection string with no Encrypt setting, a localhost redirect URL
// in a deployed environment). Runs after Build() because it needs the logging
// pipeline; anything genuinely fatal was already caught by
// builder.ValidateRequiredSecrets() at the top of this file.
app.LogSecretConfigurationWarnings();

// Say plainly whether the background schedulers are running. Their absence is
// silent and has no error of its own - recurring billing simply stops - so the
// state is worth one unmissable line in the startup log.
if (schedulersEnabled)
{
    app.Logger.LogInformation(
        "Background schedulers ENABLED ({Environment}). They write to the database from startup onwards.",
        app.Environment.EnvironmentName);
}
else
{
    app.Logger.LogWarning(
        "Background schedulers DISABLED ({Environment}). No recurring payments, monthly invoices, " +
        "appointment reminders, slot materialization, reconciliation or invoice PDF archival will run. " +
        "Set SchedulerSettings:Enabled to true to override.",
        app.Environment.EnvironmentName);
}

// Hand the OpenTok/Vonage Video credentials to Vitality.Models.CommonMethods,
// which is a static class outside the DI container. These were hardcoded in
// CommonMethods.GetSessionIdAndToken() until they were moved to configuration.
Vitality.Models.CommonMethods.CommonMethods.ConfigureOpenTok(
    app.Configuration.GetValue<int>("OpenTok:ApiKey"),
    app.Configuration["OpenTok:ApiSecret"]);

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Vitality.API V1");
});

app.UseRouting();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseDefaultFiles();
app.UseWebSockets();

app.UseCors(MyAllowSpecificOrigins);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<ChatHub>("/ChatHub", options =>
{
    options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets |
                         Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
});

app.Run();
