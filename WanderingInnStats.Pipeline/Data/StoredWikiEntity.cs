using System.Text.Json;
using WanderingInnStats.Core;

namespace WanderingInnStats.Pipeline.Data;

/// <summary>A spell, class, species, monster or location from the wiki, stored as ground truth.</summary>
public class StoredWikiEntity
{
    /// <summary><c>"{Kind}:{Name}"</c>.</summary>
    public string Id { get; set; } = "";
    public WikiEntityKind Kind { get; set; }
    public string Name { get; set; } = "";

    /// <summary>Position in the wiki list.</summary>
    public int SortOrder { get; set; }
    public string Json { get; set; } = "";
    public DateTime DownloadedAt { get; set; }
    public bool Excluded { get; set; }

    public static string IdFor(WikiEntityKind kind, string name) => $"{kind}:{name}";

    public WikiEntityRaw ToEntity() => JsonSerializer.Deserialize<WikiEntityRaw>(Json)!;

    public static StoredWikiEntity From(WikiEntityRaw raw, int sortOrder, DateTime downloadedAt) => new()
    {
        Id = IdFor(raw.Kind, raw.Name),
        Kind = raw.Kind,
        Name = raw.Name,
        SortOrder = sortOrder,
        Json = JsonSerializer.Serialize(raw),
        DownloadedAt = downloadedAt
    };
}
