using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Vitality.Models.Schedulers
{

    public sealed class SchedulerDistributedLock : IAsyncDisposable, IDisposable
    {
        private readonly SqlConnection _connection;
        private readonly string _lockName;
        private bool _disposed;

        private SchedulerDistributedLock(SqlConnection connection, string lockName)
        {
            _connection = connection;
            _lockName = lockName;
        }

        public string LockName => _lockName;

        public static async Task<SchedulerDistributedLock?> TryAcquireAsync(
            string connectionString,
            string lockName,
            CancellationToken ct = default,
            int timeoutMilliseconds = 0)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Connection string is required.", nameof(connectionString));
            if (string.IsNullOrWhiteSpace(lockName))
                throw new ArgumentException("Lock name is required.", nameof(lockName));

            var connection = new SqlConnection(connectionString);
            try
            {
                await connection.OpenAsync(ct).ConfigureAwait(false);

                using var cmd = connection.CreateCommand();
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandText = "sp_getapplock";
                cmd.CommandTimeout = Math.Max(30, (timeoutMilliseconds / 1000) + 30);

                cmd.Parameters.Add(new SqlParameter("@Resource", SqlDbType.NVarChar, 255) { Value = lockName });
                cmd.Parameters.Add(new SqlParameter("@LockMode", SqlDbType.NVarChar, 32) { Value = "Exclusive" });
                cmd.Parameters.Add(new SqlParameter("@LockOwner", SqlDbType.NVarChar, 32) { Value = "Session" });
                cmd.Parameters.Add(new SqlParameter("@LockTimeout", SqlDbType.Int) { Value = timeoutMilliseconds });

                var ret = new SqlParameter
                {
                    ParameterName = "@RETURN_VALUE",
                    SqlDbType = SqlDbType.Int,
                    Direction = ParameterDirection.ReturnValue
                };
                cmd.Parameters.Add(ret);

                await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

                var code = ret.Value is int i ? i : -999;

                if (code >= 0)
                {
                    return new SchedulerDistributedLock(connection, lockName);
                }

                await connection.DisposeAsync().ConfigureAwait(false);
                return null;
            }
            catch
            {
                try { await connection.DisposeAsync().ConfigureAwait(false); } catch { }
                throw;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try { _connection.Dispose(); } catch { }
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;
            try { await _connection.DisposeAsync().ConfigureAwait(false); } catch { }
        }
    }
}
