using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace BinanceUsdtTicker.Data
{
    /// <summary>
    /// Repository for reading and writing encrypted secrets stored in the database.
    /// </summary>
    public class SecretRepository
    {
        private readonly string _cs;
        public SecretRepository(string connectionString) => _cs = connectionString;

        public virtual async Task UpsertAsync(string name, byte[] valueEnc, CancellationToken ct = default)
        {
            await using var conn = new SqlConnection(_cs);
            await conn.OpenAsync(ct);

            await EnsureTableAsync(conn, ct);

            var cmd = conn.CreateCommand();
            cmd.CommandText = @"MERGE dbo.Secrets WITH (HOLDLOCK) AS t
USING (VALUES (@n, @v)) AS s(Name, ValueEnc)
   ON t.Name = s.Name
WHEN MATCHED THEN UPDATE SET ValueEnc = s.ValueEnc, UpdatedAt = SYSUTCDATETIME(), Version = Version + 1
WHEN NOT MATCHED THEN INSERT(Name, ValueEnc) VALUES(s.Name, s.ValueEnc);";
            cmd.Parameters.AddWithValue("@n", name);
            cmd.Parameters.AddWithValue("@v", valueEnc);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        public virtual async Task<Dictionary<string, byte[]>> GetAllAsync(CancellationToken ct = default)
        {
            var result = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            await using var conn = new SqlConnection(_cs);
            await conn.OpenAsync(ct);

            await EnsureTableAsync(conn, ct);

            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Name, ValueEnc FROM dbo.Secrets";
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                result[r.GetString(0)] = (byte[])r.GetValue(1);
            return result;
        }

        private static async Task EnsureTableAsync(SqlConnection conn, CancellationToken ct)
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"IF OBJECT_ID(N'dbo.Secrets', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Secrets (
        Name NVARCHAR(450) NOT NULL PRIMARY KEY,
        ValueEnc VARBINARY(MAX) NOT NULL,
        UpdatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        Version INT NOT NULL DEFAULT 1
    );
END";
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }
}
