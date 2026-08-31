using EbedrendeloApp.Data;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Tests.TestSupport;

/// <summary>
/// File-backed Sqlite <see cref="IDbContextFactory{TContext}"/> for tests that need two independent
/// connections racing against the same database. Unlike <see cref="SqliteDbContextFactory"/>, which
/// shares one open connection across every context it creates (fine for a single handler's sequential
/// awaits, not safe for two commands executing concurrently on it), each instance of this factory opens
/// its own connection(s) to the same temp file, so two factory instances genuinely race for the SQLite
/// write lock instead of racing for one shared ADO.NET connection object.
/// </summary>
public sealed class FileSqliteDbContextFactory : IDbContextFactory<EbedrendeloDbContext>, IDisposable
{
    private readonly string dbPath;
    private readonly bool deleteFileOnDispose;
    private readonly DbContextOptions<EbedrendeloDbContext> options;

    /// <param name="dbPath">Shared temp-file path — construct a second factory with the same path to
    /// race against this one.</param>
    /// <param name="ensureCreated">Only the first factory pointed at a given path should create the
    /// schema; a second instance racing against it should pass false.</param>
    public FileSqliteDbContextFactory(string dbPath, bool ensureCreated)
    {
        this.dbPath = dbPath;
        deleteFileOnDispose = ensureCreated;

        // "Default Timeout" (seconds) sets sqlite3_busy_timeout, so a writer blocked behind the other
        // connection's write lock waits instead of failing immediately with SQLITE_BUSY.
        options = new DbContextOptionsBuilder<EbedrendeloDbContext>()
            .UseSqlite($"Data Source={dbPath};Default Timeout=5")
            .Options;

        if (ensureCreated)
        {
            using var db = CreateDbContext();
            db.Database.EnsureCreated();
        }
    }

    public EbedrendeloDbContext CreateDbContext() => new(options);

    public Task<EbedrendeloDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(CreateDbContext());

    public void Dispose()
    {
        if (!deleteFileOnDispose)
        {
            return;
        }

        // Best-effort cleanup only: on Windows, the OS can briefly hold the file handle open just
        // after a Sqlite connection closes, so a failed delete here must not fail the test — a
        // leftover file under %TEMP% is harmless.
        try
        {
            File.Delete(dbPath);
        }
        catch (IOException)
        {
        }
    }
}
