using Microsoft.Extensions.Configuration;

namespace Vitality.Configuration
{
    /// <summary>
    /// Startup validation for configuration that used to be hardcoded in
    /// appsettings.json.
    /// <para>
    /// The point of this class is to turn silent misconfiguration into a loud,
    /// actionable message. Before secrets were externalised, a missing value
    /// produced things like a Stripe client built from the literal
    /// "sk_placeholder_configure_Stripe_SecretKey_in_appsettings" or a Square
    /// client built from "GLOBAL_ADMIN_STATIC_TOKEN_REQUIRED" - the app booted
    /// happily and then failed much later, at the payment provider, with an
    /// error that pointed nowhere near the real cause.
    /// </para>
    /// <para>
    /// Two tiers: <see cref="ValidateRequiredSecrets"/> refuses to start when a
    /// secret the whole API depends on is absent, and
    /// <see cref="LogSecretConfigurationWarnings"/> reports everything else as a
    /// warning so a single missing integration credential degrades one feature
    /// instead of taking the service down.
    /// </para>
    /// </summary>
    internal static class SecretsValidation
    {
        /// <summary>A secret this application reads, and where it comes from.</summary>
        /// <param name="Key">Configuration key, colon-separated.</param>
        /// <param name="Purpose">What stops working when it is missing.</param>
        private sealed record SecretDescriptor(string Key, string Purpose);

        /// <summary>
        /// Secrets without which the API cannot serve a single authenticated
        /// request. Absence is a startup failure.
        /// </summary>
        private static readonly SecretDescriptor[] RequiredSecrets =
        {
            new("ConnectionStrings:dbConnection", "database access - every endpoint"),
            new("Jwt:Key", "signing and validating access tokens - login and all authenticated endpoints")
        };

        /// <summary>
        /// Secrets that gate one integration each. Absence is a warning: the
        /// rest of the API still works.
        /// </summary>
        private static readonly SecretDescriptor[] OptionalSecrets =
        {
            new("Smtp:Password", "outbound email - notifications, password resets, invoices"),
            new("Twilio:AuthToken", "outbound SMS"),
            new("S3:AccessKeyId", "S3 uploads - images, documents, invoice PDFs"),
            new("S3:SecretAccessKey", "S3 uploads - images, documents, invoice PDFs"),
            new("Square:AccessToken", "Square platform payments (facility to global admin)"),
            new("Square:OAuth:ClientSecret", "Square per-facility OAuth connect flow"),
            new("Stripe:SecretKey", "all Stripe payments, Connect onboarding and subscriptions"),
            new("Stripe:WebhookSecret", "verifying inbound Stripe webhook signatures"),
            new("Stripe:WebhookSecretThin", "verifying inbound Stripe thin-event webhook signatures"),
            new("Zoom:ClientSecret", "creating Zoom meetings for appointments"),
            new("OpenTok:ApiSecret", "in-browser telehealth video sessions"),
            new("Fullscript:ClientSecret", "Fullscript supplement catalog and OAuth"),
            new("EmpowerPharmacy:ApiKey", "submitting prescriptions to Empower Pharmacy"),
            new("EmpowerPharmacy:ApiSecret", "submitting prescriptions to Empower Pharmacy")
        };

        /// <summary>Shortest acceptable HS256 signing key: 256 bits.</summary>
        private const int MinimumJwtKeyLength = 32;

        /// <summary>
        /// Throws if a required secret is missing or obviously invalid. Call
        /// this before <c>builder.Build()</c> so the process exits with a clear
        /// message instead of starting in a broken state.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Aggregates every problem found, so one restart shows the full list
        /// rather than one error at a time.
        /// </exception>
        public static void ValidateRequiredSecrets(this WebApplicationBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            var problems = new List<string>();

            foreach (var secret in RequiredSecrets)
            {
                if (string.IsNullOrWhiteSpace(builder.Configuration[secret.Key]))
                {
                    problems.Add($"  MISSING  {secret.Key}\n           needed for: {secret.Purpose}");
                }
            }

            // A short signing key makes HS256 token creation throw deep inside
            // the token handler on the first login attempt, which is a much
            // worse place to discover the problem than here.
            var jwtKey = builder.Configuration["Jwt:Key"];
            if (!string.IsNullOrWhiteSpace(jwtKey) && jwtKey.Length < MinimumJwtKeyLength)
            {
                problems.Add(
                    $"  INVALID  Jwt:Key\n" +
                    $"           is {jwtKey.Length} characters; HS256 requires at least {MinimumJwtKeyLength}.");
            }

            if (problems.Count == 0)
            {
                return;
            }

            var environmentName = builder.Environment.EnvironmentName;

            throw new InvalidOperationException(
                "\n" +
                "======================================================================\n" +
                " STARTUP ABORTED - required configuration is missing\n" +
                $" Environment: {environmentName}\n" +
                "======================================================================\n" +
                string.Join("\n", problems) + "\n" +
                "----------------------------------------------------------------------\n" +
                " These values are deliberately NOT stored in appsettings.json.\n" +
                "\n" +
                " Local development - user secrets:\n" +
                "   dotnet user-secrets set \"Jwt:Key\" \"<value>\" --project .\\Vitality\\Vitality.csproj\n" +
                "   dotnet user-secrets list --project .\\Vitality\\Vitality.csproj\n" +
                "\n" +
                " Staging / production - environment variables (\":\" becomes \"__\"):\n" +
                "   ConnectionStrings__dbConnection, Jwt__Key, ...\n" +
                "\n" +
                " Full list and setup instructions: SECRETS.md\n" +
                " Key inventory:                    Vitality/appsettings.SECURE.json\n" +
                "======================================================================\n");
        }

        /// <summary>
        /// Logs a warning for every optional secret that is absent, plus a set
        /// of sanity checks for values that are present but look wrong. Call
        /// after <c>builder.Build()</c>, once the logging pipeline exists.
        /// </summary>
        public static void LogSecretConfigurationWarnings(this WebApplication app)
        {
            ArgumentNullException.ThrowIfNull(app);

            var logger = app.Services.GetRequiredService<ILoggerFactory>()
                                     .CreateLogger("Vitality.Configuration.Secrets");
            var configuration = app.Configuration;
            var isProductionLike = !app.Environment.IsDevelopment();

            foreach (var secret in OptionalSecrets)
            {
                if (string.IsNullOrWhiteSpace(configuration[secret.Key]))
                {
                    logger.LogWarning(
                        "Configuration {Key} is not set. Disabled feature: {Purpose}. " +
                        "Set it via user-secrets (development) or the {EnvVar} environment variable.",
                        secret.Key,
                        secret.Purpose,
                        secret.Key.Replace(":", "__"));
                }
            }

            // --- Value-shape checks ------------------------------------------
            // Each of these was an actual defect found in the committed
            // configuration, so they are worth asserting on every boot.

            var stripeSecretKey = configuration["Stripe:SecretKey"]?.Trim();
            if (!string.IsNullOrWhiteSpace(stripeSecretKey))
            {
                if (!stripeSecretKey.StartsWith("sk_", StringComparison.OrdinalIgnoreCase) &&
                    !stripeSecretKey.StartsWith("rk_", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogError(
                        "Stripe:SecretKey does not look like a Stripe secret key (expected a 'sk_' or 'rk_' prefix). " +
                        "All Stripe calls will fail.");
                }
                else if (isProductionLike && stripeSecretKey.StartsWith("sk_test_", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogWarning(
                        "Stripe:SecretKey is a TEST key but the environment is {Environment}. " +
                        "Live payments will not be processed.",
                        app.Environment.EnvironmentName);
                }
            }

            // The committed configuration had WebhookSecret set to the API
            // secret key and WebhookSecretThin set to "etrdegtrh", which means
            // signature verification in StripeWebhookController could never
            // have succeeded against a real Stripe request.
            foreach (var webhookKey in new[] { "Stripe:WebhookSecret", "Stripe:WebhookSecretThin" })
            {
                var value = configuration[webhookKey]?.Trim();
                if (!string.IsNullOrWhiteSpace(value) &&
                    !value.StartsWith("whsec_", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogError(
                        "{Key} does not look like a Stripe webhook signing secret (expected a 'whsec_' prefix). " +
                        "Inbound webhook signature verification will reject every request. " +
                        "Copy the value from Stripe Dashboard > Developers > Webhooks > your endpoint > Signing secret.",
                        webhookKey);
                }
            }

            // EF Core 8 brings Microsoft.Data.SqlClient 5.x, where Encrypt
            // defaults to True. A connection string that does not say so
            // explicitly can start failing with a certificate-chain error.
            var connectionString = configuration.GetConnectionString("dbConnection");
            if (!string.IsNullOrWhiteSpace(connectionString) &&
                !connectionString.Contains("Encrypt", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning(
                    "The database connection string does not specify 'Encrypt'. " +
                    "Microsoft.Data.SqlClient 5.x (shipped with EF Core 8) defaults Encrypt to True, " +
                    "so this connection may fail certificate validation. " +
                    "Add 'Encrypt=True;TrustServerCertificate=True' for hosted SQL Server, or 'Encrypt=False' for a local instance.");
            }

            // A localhost redirect target in a deployed environment sends real
            // users back to their own machine after Stripe/Square onboarding.
            if (isProductionLike)
            {
                foreach (var urlKey in new[] { "Frontend:BaseUrl", "Stripe:DashboardReturnUrl" })
                {
                    var value = configuration[urlKey];
                    if (!string.IsNullOrWhiteSpace(value) &&
                        value.Contains("localhost", StringComparison.OrdinalIgnoreCase))
                    {
                        logger.LogWarning(
                            "{Key} is set to {Value} in the {Environment} environment. " +
                            "Payment and onboarding redirects will send users to localhost. " +
                            "Override it with the {EnvVar} environment variable.",
                            urlKey,
                            value,
                            app.Environment.EnvironmentName,
                            urlKey.Replace(":", "__"));
                    }
                }
            }
        }
    }
}
