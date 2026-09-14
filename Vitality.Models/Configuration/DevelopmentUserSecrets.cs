using Microsoft.Extensions.Configuration;

namespace Vitality.Models.Configuration
{
    /// <summary>
    /// Lets code that builds its own <see cref="IConfiguration"/> read the same
    /// local user-secrets store the API project uses.
    /// <para>
    /// This exists because of an architectural quirk rather than by choice.
    /// Around 21 repositories derive from <c>BaseRepo</c>, whose parameterless
    /// constructor does <c>new MainContext()</c> instead of taking the
    /// DI-registered <c>DbContext</c>. Those instances never see
    /// <c>WebApplicationBuilder.Configuration</c>, so
    /// <c>MainContext.OnConfiguring</c> and <c>BaseRepo.GetConnectionString()</c>
    /// each assemble a fresh <see cref="ConfigurationBuilder"/> from
    /// appsettings.json plus environment variables.
    /// </para>
    /// <para>
    /// Once the connection string moved out of appsettings.json, those two
    /// fallback paths would have gone blind in local development unless the
    /// developer exported an environment variable in every shell. Adding the
    /// user-secrets provider here keeps <c>dotnet user-secrets</c> working for
    /// the whole application, including the repositories.
    /// </para>
    /// <para>
    /// The right long-term fix is to inject <c>MainContext</c> into the
    /// repositories and delete both fallback paths. This is the small,
    /// low-risk step that makes secret externalisation possible without that
    /// refactor.
    /// </para>
    /// </summary>
    public static class DevelopmentUserSecrets
    {
        /// <summary>
        /// The user-secrets store shared with the Vitality API project.
        /// Must match <c>TeleHealthUserSecretsId</c> in Directory.Build.props
        /// and <c>UserSecretsId</c> in Vitality.csproj. Changing it orphans
        /// every developer's existing local secrets.
        /// </summary>
        public const string UserSecretsId = "b7f4c2e1-9a63-4d18-8c5f-2e1a7d940c36";

        /// <summary>
        /// Adds the local user-secrets store, but only when running in the
        /// Development environment.
        /// <para>
        /// User secrets are a development-time convenience stored in the
        /// developer's own profile directory. They are intentionally ignored
        /// outside Development, where secrets must come from environment
        /// variables instead, so this method is a no-op on a server.
        /// </para>
        /// </summary>
        /// <param name="builder">The configuration builder to extend.</param>
        /// <returns>The same builder, for chaining.</returns>
        public static IConfigurationBuilder AddDevelopmentUserSecrets(this IConfigurationBuilder builder)
        {
            if (builder is null)
            {
                return builder!;
            }

            if (!IsDevelopment())
            {
                return builder;
            }

            try
            {
                // The userSecretsId overload registers the file as optional, so
                // a developer who supplies everything through environment
                // variables and has no secrets.json is unaffected.
                builder.AddUserSecrets(UserSecretsId, reloadOnChange: false);
            }
            catch
            {
                // Never let an unreadable local secrets file stop the process
                // from starting. If the connection string really is missing,
                // the callers of this method already throw a descriptive error.
            }

            return builder;
        }

        /// <summary>
        /// Reads the hosting environment straight from the process environment.
        /// <c>IWebHostEnvironment</c> is not reachable from the code paths this
        /// helper serves, since they run outside the DI container.
        /// </summary>
        private static bool IsDevelopment()
        {
            var environmentName =
                Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");

            return string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase);
        }
    }
}
