using WanderingInnStats.Core;

namespace WanderingInnStats.Pipeline.Data;

public class StoredChapter
{
    public int Id { get; set; }
    public string Url { get; set; } = "";
    public string Volume { get; set; } = "";
    public string Name { get; set; } = "";

    /// <summary>Last known position in the table of contents. Determines processing / export order.</summary>
    public int TocOrder { get; set; }

    /// <summary>False when the last TOC check no longer listed this chapter.</summary>
    public bool InToc { get; set; } = true;

    public DateTime Date { get; set; }
    public int Comments { get; set; }

    public string? RawText { get; set; }
    public string? OverrideText { get; set; }

    /// <summary>Set when a re-download changed <see cref="RawText"/> while an override exists.</summary>
    public bool OverrideStale { get; set; }

    public bool Excluded { get; set; }
    public DateTime? DownloadedAt { get; set; }
    public string? LastError { get; set; }

    /// <summary>The last download only got Patreon's unlock prompt instead of the text.</summary>
    public bool PatreonLocked { get; set; }

    public bool IsDownloaded => RawText != null;
    public string EffectiveText => OverrideText ?? RawText ?? "";

    public Chapter ToChapter() => new()
    {
        Volume = Volume,
        Url = Url,
        Name = Name,
        Date = Date,
        Comments = Comments,
        Text = EffectiveText
    };
}
