using Colors.Infrastructure.Persistence;
using Colors.Infrastructure.Persistence.Auditing;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Colors.IntegrationTests.Common;

/// <summary>
/// A real PostgreSQL database for the tests, built from the migrations and thrown
/// away afterwards.
///
/// Not an in-memory provider on purpose. What these tests exist to prove — a sequence
/// that never repeats, a unique index that stops two rows, a check constraint that
/// refuses negative stock — are all things only the real database does. An in-memory
/// provider would pass every one of them while proving nothing.
///
/// The connection comes from <c>COLORS_TEST_DB</c> if it is set, so a build server can
/// point somewhere else; otherwise it is the local development server with a database
/// of its own, never the one being developed against.
/// </summary>
public sealed class DatabaseFixture : IAsyncLifetime
{
    private const string DefaultServer =
        "Host=localhost;Port=5432;Username=postgres;Password=password";

    private readonly string _databaseName =
        $"colors_erp_test_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N")[..6]}";

    private string _connectionString = string.Empty;

    public async Task InitializeAsync()
    {
        var server = Environment.GetEnvironmentVariable("COLORS_TEST_DB") ?? DefaultServer;

        try
        {
            await using (var admin = new NpgsqlConnection($"{server};Database=postgres"))
            {
                await admin.OpenAsync();
                await using var create = admin.CreateCommand();
                create.CommandText = $"CREATE DATABASE \"{_databaseName}\"";
                await create.ExecuteNonQueryAsync();
            }

            _connectionString = $"{server};Database={_databaseName}";

            await using var db = CreateContext();
            await db.Database.MigrateAsync();
        }
        catch (NpgsqlException ex)
        {
            // Loudly, with a message that says what to do. Skipping quietly is how a
            // test suite stops meaning anything: it would go green on a machine that
            // never ran a single one of these.
            throw new InvalidOperationException(
                "These tests need a PostgreSQL server. Start the local one, or point "
                + "COLORS_TEST_DB at another. They create their own database and drop "
                + $"it afterwards, and never touch the development one. ({ex.Message})",
                ex);
        }
    }

    public ColorsDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ColorsDbContext>()
            .UseNpgsql(_connectionString)
            // The same interceptor the running application uses, so a test sees the
            // audit log the factory will see. Without it the suite would go green on
            // auditing that never happened (specification section 15).
            .AddInterceptors(new AuditInterceptor(new NoActor()))
            .Options;

        return new ColorsDbContext(options);
    }

    public async Task DisposeAsync()
    {
        if (_connectionString.Length == 0)
        {
            return;
        }

        NpgsqlConnection.ClearAllPools();

        var server = Environment.GetEnvironmentVariable("COLORS_TEST_DB") ?? DefaultServer;
        await using var admin = new NpgsqlConnection($"{server};Database=postgres");
        await admin.OpenAsync();

        await using var drop = admin.CreateCommand();
        drop.CommandText = $"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)";
        await drop.ExecuteNonQueryAsync();
    }
}

/// <summary>One database for the whole run, rather than one per test class.</summary>
[CollectionDefinition(Name)]
public sealed class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
    public const string Name = "database";
}
