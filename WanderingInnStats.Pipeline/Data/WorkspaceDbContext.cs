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
                _ensuredFor = _options.DatabaseFile;
            }
        }

        return context;
    }
}
