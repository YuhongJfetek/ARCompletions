using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace ARCompletions.Services;

/// <summary>
/// Distributed lock implemented with Postgres advisory locks.
/// Uses a dedicated NpgsqlConnection per lock to hold the session lock.
/// Falls back to a no-op if connection string is not configured.
/// </summary>
public sealed class PostgresAdvisoryLock : IDistributedLock
{
    private readonly string? _connString;
    private NpgsqlConnection? _conn;
    private long _lockKey;
    private bool _locked;

    public PostgresAdvisoryLock(IConfiguration config)
    {
        _connString = config["ConnectionStrings:Default"] ?? config["DATABASE_URL"];
    }

    private static long KeyFromString(string key)
    {
        // compute 64-bit signed key from SHA256 first 8 bytes
        using var sha = SHA256.Create();
        var b = sha.ComputeHash(Encoding.UTF8.GetBytes(key));
        long v = 0;
        for (int i = 0; i < 8; i++) v = (v << 8) | b[i];
        return v;
    }

    public async Task<bool> TryAcquireAsync(string key, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_connString)) return false;
        _lockKey = KeyFromString(key);
        _conn = new NpgsqlConnection(_connString);
        await _conn.OpenAsync(cancellationToken);

        // Try immediate acquisition with pg_try_advisory_lock
        using var cmd = new NpgsqlCommand("SELECT pg_try_advisory_lock(@p)", _conn);
        cmd.Parameters.AddWithValue("p", _lockKey);
        var res = await cmd.ExecuteScalarAsync(cancellationToken);
        _locked = res is bool b && b;
        return _locked;
    }

    public async Task ReleaseAsync()
    {
        try
        {
            if (_conn != null && _locked)
            {
                using var cmd = new NpgsqlCommand("SELECT pg_advisory_unlock(@p)", _conn);
                cmd.Parameters.AddWithValue("p", _lockKey);
                await cmd.ExecuteScalarAsync();
            }
        }
        catch
        {
            // swallow
        }
        finally
        {
            _locked = false;
            if (_conn != null)
            {
                try { await _conn.CloseAsync(); } catch { }
                _conn.Dispose();
                _conn = null;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await ReleaseAsync();
    }
}
