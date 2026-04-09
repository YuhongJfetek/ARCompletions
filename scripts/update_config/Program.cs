using System;
using Npgsql;

int exitCode = 1;
try
{
    var key = args.Length > 0 ? args[0] : "bot.embedding.directLow";
    var value = args.Length > 1 ? args[1] : "0.012";

    var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
    if (string.IsNullOrWhiteSpace(databaseUrl))
    {
        Console.Error.WriteLine("Set DATABASE_URL env var to connect to Postgres (postgresql://...)");
        return 2;
    }

    string connString = databaseUrl;
    if (databaseUrl.StartsWith("postgres://") || databaseUrl.StartsWith("postgresql://"))
    {
        var uri = new Uri(databaseUrl);
        var userInfo = uri.UserInfo.Split(':');
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port,
            Username = userInfo.Length > 0 ? userInfo[0] : string.Empty,
            Password = userInfo.Length > 1 ? userInfo[1] : string.Empty,
            Database = uri.AbsolutePath.TrimStart('/'),
            SslMode = SslMode.Require,
            TrustServerCertificate = true
        };
        connString = builder.ToString();
    }

    using var conn = new NpgsqlConnection(connString);
    conn.Open();

    // Ensure table name and columns match ARCompletionsContext mapping
    var sql = @"INSERT INTO bot_constants_config (""ConfigKey"", ""ConfigValue"", ""ValueType"", ""UpdatedAt"", ""UpdatedBy"")
VALUES (@key, @value, 'float', now(), 'auto-update')
ON CONFLICT (""ConfigKey"") DO UPDATE SET ""ConfigValue"" = EXCLUDED.""ConfigValue"", ""ValueType"" = EXCLUDED.""ValueType"", ""UpdatedAt"" = now(), ""UpdatedBy"" = EXCLUDED.""UpdatedBy"";";

    using var cmd = new NpgsqlCommand(sql, conn);
    cmd.Parameters.AddWithValue("key", key);
    cmd.Parameters.AddWithValue("value", value);
    var r = cmd.ExecuteNonQuery();
    Console.WriteLine($"Updated config {key} -> {value}. Rows affected: {r}");
    exitCode = 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine("Update failed: " + ex.Message);
    exitCode = 3;
}
return exitCode;