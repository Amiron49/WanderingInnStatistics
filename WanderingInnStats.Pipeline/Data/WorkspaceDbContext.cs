using Microsoft.EntityFrameworkCore;

namespace WanderingInnStats.Pipeline.Data;

public class WorkspaceDbContext : DbContext
{
    public WorkspaceDbContext(DbContextOptions<WorkspaceDbContext> options) : base(options)
    {
    }

    public DbSet<StoredChapter> Chapters => Set<StoredChapter>();
    public DbSet<StoredCharacter> Characters => Set<StoredCharacter>();
    public DbSet<StatisticsRun> Runs => Set<StatisticsRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StoredChapter>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Url).IsUnique();
            e.Ignore(x => x.IsDownloaded);
            e.Ignore(x => x.EffectiveText);
        });

        modelBuilder.Entity<StoredCharacter>(e =>
        {
            e.HasKey(x => x.WikiUrl);
            e.HasIndex(x => x.Name);
        });

        modelBuilder.Entity<StatisticsRun>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasConversion<string>();
        });
    }
}

/// <summary>
/// Creates short-lived contexts against the current <see cref="WorkspaceOptions.DatabaseFile"/>.
/// Reads the options on every call, so a changed workspace root takes effect immediately.
/// </summary>
public class WorkspaceDbFactory : IDbContextFactory<WorkspaceDbContext>
{
    private readonly WorkspaceOptions _options;
    private readonly Lock _lock = new();
    private string? _ensuredFor;

    public WorkspaceDbFactory(WorkspaceOptions options)
    {
        _options = options;
    }

    public WorkspaceDbContext CreateDbContext()
    {
        var dbOptions = new DbContextOptionsBuilder<WorkspaceDbContext>()
            .UseSqlite($"Data Source={_options.DatabaseFile}")
            .Options;
        var context = new WorkspaceDbContext(dbOptions);

        lock (_lock)
        {
            if (_ensuredFor != _options.DatabaseFile)
            {
                _options.EnsureDirectories();
                context.Database.EnsureCreated();
                UpgradeSchema(context);
                _ensuredFor = _options.DatabaseFile;
            }
        }

        return context;
    }

    /// <summary>
    /// <c>EnsureCreated</c> only creates missing databases, so columns added after a workspace was
    /// created are added here.
    /// </summary>
    private static void UpgradeSchema(WorkspaceDbContext context)
    {
        AddColumnIfMissing(context, "Chapters", "PatreonLocked", "INTEGER NOT NULL DEFAULT 0");
    }

    private static void AddColumnIfMissing(WorkspaceDbContext context, string table, string column, string definition)
    {
        var connection = context.Database.GetDbConnection();
        var wasClosed = connection.State != System.Data.ConnectionState.Open;
        if (wasClosed)
            connection.Open();

        try
        {
            using (var check = connection.CreateCommand())
            {
                check.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = '{column}'";
                if (Convert.ToInt64(check.ExecuteScalar()) > 0)
                    return;
            }

            using var alter = connection.CreateCommand();
            alter.CommandText = $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {definition}";
            alter.ExecuteNonQuery();
        }
        finally
        {
            if (wasClosed)
                connection.Close();
        }
    }
}
