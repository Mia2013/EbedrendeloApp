using EbedrendeloApp.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Tests.TestSupport;

/// <summary>
/// In-memory Sqlite-backed <see cref="IDbContextFactory{TContext}"/> for handler tests
/// (01-szerver-architektura.md 9. fejezet). The connection is kept open for the lifetime of the
/// factory so the in-memory database survives across the multiple contexts a handler creates.
/// </summary>
public sealed class SqliteDbContextFactory : IDbContextFactory<EbedrendeloDbContext>, IDisposable
{
    private readonly SqliteConnection connection;
    private readonly DbContextOptions<EbedrendeloDbContext> options;

    public SqliteDbContextFactory()
    {
        connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        options = new DbContextOptionsBuilder<EbedrendeloDbContext>()
            .UseSqlite(connection)
            .Options;

        using var db = CreateDbContext();
        db.Database.EnsureCreated();
    }

    public EbedrendeloDbContext CreateDbContext() => new(options);

    public Task<EbedrendeloDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(CreateDbContext());

    public void Dispose() => connection.Dispose();
}
