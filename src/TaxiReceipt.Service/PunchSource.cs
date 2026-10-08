using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace TaxiReceipt.Service;

public sealed record Punch(long Id, int PersonId, string FullName, DateTime PunchTime);

public interface IPunchSource
{
    /// <summary>False until the clock system creates this month's table.</summary>
    Task<bool> TableExistsAsync(string table, CancellationToken ct);
    Task<long> MaxIdAsync(string table, CancellationToken ct);
    Task<IReadOnlyList<Punch>> PunchesAfterAsync(string table, long afterId, CancellationToken ct);
}

/// <summary>
/// Reads the attendance-clock database. The table name changes every month,
/// so it can't be a parameter: it is checked against a strict pattern and
/// quoted with QUOTENAME before it goes into the SQL.
/// </summary>
public sealed partial class SqlPunchSource(string connectionString) : IPunchSource
{
    [GeneratedRegex(@"^[A-Za-z_]+[0-9]{6}[A-Za-z_]*$")]
    private static partial Regex SafeTableName();

    private static string Quote(string table) =>
        SafeTableName().IsMatch(table)
            ? $"[dbo].[{table}]"
            : throw new ArgumentException($"Unexpected punch table name: {table}", nameof(table));

    public async Task<bool> TableExistsAsync(string table, CancellationToken ct)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand("SELECT OBJECT_ID(@name, 'U')", conn);
        cmd.Parameters.AddWithValue("@name", $"dbo.{table}");
        return await cmd.ExecuteScalarAsync(ct) is not DBNull and not null;
    }

    public async Task<long> MaxIdAsync(string table, CancellationToken ct)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand($"SELECT ISNULL(MAX(Id), 0) FROM {Quote(table)}", conn);
        return Convert.ToInt64(await cmd.ExecuteScalarAsync(ct));
    }

    public async Task<IReadOnlyList<Punch>> PunchesAfterAsync(string table, long afterId, CancellationToken ct)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(
            $"""
            SELECT p.Id, p.PersonId,
                   LTRIM(RTRIM(ISNULL(per.FirstName, N'') + N' ' + ISNULL(per.LastName, N''))) AS FullName,
                   p.PunchTime
            FROM {Quote(table)} AS p
            LEFT JOIN dbo.Persons AS per ON per.PersonId = p.PersonId
            WHERE p.Id > @afterId
            ORDER BY p.Id
            """, conn);
        cmd.Parameters.AddWithValue("@afterId", afterId);
        var list = new List<Punch>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            // int or bigint, whichever the clock system used.
            list.Add(new Punch(
                Convert.ToInt64(reader.GetValue(0)),
                Convert.ToInt32(reader.GetValue(1)),
                reader.GetString(2),
                reader.GetDateTime(3)));
        }
        return list;
    }
}
