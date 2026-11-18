using CityFix_backend.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CityFix_backend.Tests.Infrastructure;

/// <summary>
/// A throw-away PostgreSQL + PostGIS database created through the real migrations and dropped afterwards.
/// The server comes from CITYFIX_TEST_POSTGRES (a connection string without a database name).
/// </summary>
public sealed class PostgresDatabase : IAsyncLifetime
{
    private const string DefaultServer = "Host=localhost;Username=postgres;Password=123456789";

    private static string Server => Environment.GetEnvironmentVariable("CITYFIX_TEST_POSTGRES") ?? DefaultServer;

    public string Name { get; } = "cityfix_test_" + Guid.NewGuid().ToString("N")[..12];

    public string ConnectionString => new NpgsqlConnectionStringBuilder(Server) { Database = Name, IncludeErrorDetail = true }.ToString();

    public ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(ConnectionString, o => o.UseNetTopologySuite())
            .Options);

    public async Task InitializeAsync()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await using var connection = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(Server) { Database = "postgres" }.ToString());
        await connection.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{Name}\" WITH (FORCE)", connection);
        await drop.ExecuteNonQueryAsync();
    }
}
