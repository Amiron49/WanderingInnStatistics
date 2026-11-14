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
    public DbSet<StoredWikiEntity> WikiEntities => Set<StoredWikiEntity>();

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

        modelBuilder.Entity<StoredWikiEntity>(e =>
        {
            e.ToTable("WikiEntities");
            e.HasKey(x => x.Id);
            e.Property(x => x.Kind).HasConversion<string>();
            e.HasIndex(x => x.Kind);
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
    /// <c>EnsureCreated</c> only creates missing databases, so tables and columns added after a workspace was
    /// created are added here.
    /// </summary>
    private static void UpgradeSchema(WorkspaceDbContext context)
    {
        AddColumnIfMissing(context, "Chapters", "PatreonLocked", "INTEGER NOT NULL DEFAULT 0");
        Execute(context, """
            CREATE TABLE IF NOT EXISTS "WikiEntities" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_WikiEntities" PRIMARY KEY,
                "Kind" TEXT NOT NULL,
                "Name" TEXT NOT NULL,
                "SortOrder" INTEGER NOT NULL,
                "Json" TEXT NOT NULL,
                "DownloadedAt" TEXT NOT NULL,
                "Excluded" INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS "IX_WikiEntities_Kind" ON "WikiEntities" ("Kind");
            """);
    }

    private static void Execute(WorkspaceDbContext context, string sql)
    {
        var connection = context.Database.GetDbConnection();
        var wasClosed = connection.State != System.Data.ConnectionState.Open;
        if (wasClosed)
            connection.Open();

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }
        finally
        {
            if (wasClosed)
                connection.Close();
        }
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
