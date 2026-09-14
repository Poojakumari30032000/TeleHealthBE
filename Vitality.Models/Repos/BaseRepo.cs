using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vitality.Models.Configuration;
using Vitality.Models.EntityClasses;

namespace Vitality.Models.Repos
{
    public abstract class BaseRepo
    {
        internal MainContext _db;
        private static string? _connectionString;
        private static readonly object _lock = new object();

        public BaseRepo()
        {
            if (_db is null)
            {
                _db = new MainContext();
            }
        }

        private static string GetConnectionString()
        {
            if (_connectionString == null)
            {
                lock (_lock)
                {
                    if (_connectionString == null)
                    {

                        try
                        {
                            using (var tempContext = new MainContext())
                            {

                                var connection = tempContext.Database.GetDbConnection();
                                if (connection != null && !string.IsNullOrEmpty(connection.ConnectionString))
                                {
                                    _connectionString = connection.ConnectionString;
                                }
                                else
                                {
                                    throw new InvalidOperationException("MainContext connection string is null or empty.");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            // The connection string is no longer stored in appsettings.json.
                            // Check the environment variable first, so a deployed server never
                            // depends on probing the filesystem for a config file.
                            _connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__dbConnection");

                            IConfiguration? configuration = null;

                            var searchPaths = new[]
                            {
                                Directory.GetCurrentDirectory(),
                                AppDomain.CurrentDomain.BaseDirectory,
                                Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ""
                            };

                            if (string.IsNullOrWhiteSpace(_connectionString))
                            {
                                foreach (var searchPath in searchPaths)
                                {
                                    try
                                    {
                                        var appsettingsPath = Path.Combine(searchPath, "appsettings.json");
                                        if (File.Exists(appsettingsPath))
                                        {
                                            configuration = new ConfigurationBuilder()
                                                .SetBasePath(searchPath)
                                                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                                                .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
                                                // Development-only, no-op on a server. Lets the Dapper
                                                // stored-procedure path resolve the connection string from
                                                // the same user-secrets store the API project reads.
                                                .AddDevelopmentUserSecrets()
                                                .AddEnvironmentVariables()
                                                .Build();
                                            break;
                                        }
                                    }
                                    catch
                                    {
                                        continue;
                                    }
                                }

                                if (configuration == null)
                                {
                                    try
                                    {
                                        configuration = new ConfigurationBuilder()
                                            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                                            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
                                            .AddDevelopmentUserSecrets()
                                            .AddEnvironmentVariables()
                                            .Build();
                                    }
                                    catch { }
                                }

                                if (configuration != null)
                                {
                                    _connectionString = configuration.GetConnectionString("dbConnection");
                                }
                            }

                            if (string.IsNullOrWhiteSpace(_connectionString))
                            {
                                throw new InvalidOperationException(
                                    $"Failed to resolve the database connection string. MainContext error: {ex.Message}. " +
                                    $"Searched paths: {string.Join(", ", searchPaths)}. " +
                                    "The connection string is intentionally not stored in appsettings.json: set the " +
                                    "'ConnectionStrings__dbConnection' environment variable (staging/production), or run " +
                                    "dotnet user-secrets set \"ConnectionStrings:dbConnection\" \"<value>\" " +
                                    "--project .\\Vitality\\Vitality.csproj (development). See SECRETS.md.");
                            }
                        }
                    }
                }
            }
            return _connectionString;
        }

        protected static SqlConnection CreateSqlConnection() => new SqlConnection(GetConnectionString());

        public static void ExecuteWithoutReturn(string procedureName, DynamicParameters param)
        {
            using (SqlConnection con = new SqlConnection(GetConnectionString()))
            {
                con.Open();
                con.Execute(procedureName, param, commandType: CommandType.StoredProcedure);
            }

        }
        public static T ExecuteReturnScalar<T>(string procedureName, DynamicParameters param)
        {
            using (SqlConnection con = new SqlConnection(GetConnectionString()))
            {
                con.Open();
                return (T)Convert.ChangeType(con.ExecuteScalar(procedureName, param, commandType: CommandType.StoredProcedure), typeof(T));
            }

        }
        public static IEnumerable<T> ReturnList<T>(string procedureName, DynamicParameters param)
        {
            using (SqlConnection con = new SqlConnection(GetConnectionString()))
            {
                con.Open();
                return con.Query<T>(procedureName, param, commandTimeout: 0, commandType: CommandType.StoredProcedure);
            }

        }
        public string? GeneratePasswordResetCode()
        {
            Random rnd = new Random();
            int myRandomNo = rnd.Next(10000000, 99999999);
            return Convert.ToString(myRandomNo);
        }

        public static List<T> ReturnJson<T>(string procedureName, DynamicParameters param)
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                StringBuilder stringBuilder = new StringBuilder();
                var results = connection.Query<string>(procedureName, param, commandType: System.Data.CommandType.StoredProcedure);
                foreach (var result in results)
                {
                    stringBuilder.Append(result);
                }
                List<T> response = new List<T>();
                if (stringBuilder.Length > 0)
                {
                    response = JsonConvert.DeserializeObject<List<T>>(stringBuilder.ToString());
                }
                return response;
            }

        }
    }
}
