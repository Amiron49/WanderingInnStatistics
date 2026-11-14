using System.Text.Json;
using WanderingInnStats.Core;

namespace WanderingInnStats.Pipeline.Data;

public class StoredCharacter
{
    public string WikiUrl { get; set; } = "";
    public string Name { get; set; } = "";

    /// <summary>Position in the wiki listing; keeps the definition order identical to the old json cache.</summary>
    public int SortOrder { get; set; }
    public string Json { get; set; } = "";
    public DateTime DownloadedAt { get; set; }
    public bool Excluded { get; set; }

    public CharacterRaw ToCharacter() => JsonSerializer.Deserialize<CharacterRaw>(Json)!;

    public static StoredCharacter From(CharacterRaw raw, int sortOrder, DateTime downloadedAt) => new()
    {
        SortOrder = sortOrder,
        WikiUrl = raw.WikiUrl,
        Name = raw.Name,
        Json = JsonSerializer.Serialize(raw),
        DownloadedAt = downloadedAt,
        Excluded = ExclusionRules.IsCharacterExcludedByDefault(raw.Name)
    };
}
