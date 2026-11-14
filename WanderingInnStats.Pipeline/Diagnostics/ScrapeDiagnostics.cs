using System.Text;
using AngleSharp;

namespace WanderingInnStats.Pipeline.Diagnostics;

public record SelectorCheck(string Selector, int Matches);

/// <summary>What the server returned for a url that the scrapper failed on.</summary>
public record PageSnapshot(
    string Url,
    DateTime CapturedAt,
    int? StatusCode,
    string? ReasonPhrase,
    string? ContentType,
    long? Length,
    string? Title,
    bool LooksLikeCloudflare,
    IReadOnlyList<SelectorCheck> Selectors,
    string? SavedTo,
    string? FetchError)
{
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Snapshot of {Url} at {CapturedAt:u}");
        if (FetchError != null)
        {
            sb.AppendLine($"  Fetch failed: {FetchError}");
            return sb.ToString();
        }

        sb.AppendLine($"  HTTP {StatusCode} {ReasonPhrase}, {ContentType}, {Length:N0} bytes");
        sb.AppendLine($"  Title: {Title}");
        if (LooksLikeCloudflare)
            sb.AppendLine("  Looks like a Cloudflare challenge/block page");
        foreach (var check in Selectors)
            sb.AppendLine($"  {check.Selector}: {check.Matches} match(es)");
        if (SavedTo != null)
            sb.AppendLine($"  Saved to: {SavedTo}");
        return sb.ToString();
    }
}

/// <summary>A scrape failed. Carries the original exception plus the evidence needed to work out why.</summary>
public class ScrapeException : Exception
{
    public ScrapeException(string message, Exception? inner, PageSnapshot? snapshot, string? consoleOutput)
        : base(message, inner)
    {
        Snapshot = snapshot;
        ConsoleOutput = string.IsNullOrWhiteSpace(consoleOutput) ? null : consoleOutput;
    }

    public PageSnapshot? Snapshot { get; }

    /// <summary>What the scrapping assembly wrote to the console while this happened (it logs swallowed exceptions there).</summary>
    public string? ConsoleOutput { get; }

    public override string ToString()
    {
        var sb = new StringBuilder(base.ToString());
        if (Snapshot != null)
            sb.AppendLine().AppendLine().Append(Snapshot);
        if (ConsoleOutput != null)
            sb.AppendLine().AppendLine("Scrapper console output:").Append(ConsoleOutput);
        return sb.ToString();
    }
}

public static class ScrapeDiagnostics
{
    /// <summary>The selectors <c>Scrapper.GetToc</c> depends on.</summary>
    public static readonly string[] TocSelectors = [".entry-content", ".entry-content p", ".entry-content p a"];

    /// <summary>The selectors <c>Scrapper.GetChapter</c> depends on.</summary>
    public static readonly string[] ChapterSelectors = ["#content", "#content time", "#content .entry-content", "#comments-title"];

    /// <summary>
    /// Re-fetches <paramref name="url"/> the same way the scrapper does (a default HttpClient, no extra headers), saves the
    /// body under <paramref name="directory"/>, and reports which of the scrapper's selectors still match.
    /// Never throws.
    /// </summary>
    public static async Task<PageSnapshot> CaptureAsync(string url, IEnumerable<string> selectors, string directory, CancellationToken ct = default)
    {
        var now = DateTime.Now;
        try
        {
            using var client = new HttpClient();
            using var response = await client.GetAsync(url, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            Directory.CreateDirectory(directory);
            var fileName = $"{now:yyyyMMdd-HHmmss}-{Sanitize(url)}.html";
            var path = Path.Combine(directory, fileName);
            await File.WriteAllTextAsync(path, body, ct);

            var document = await BrowsingContext.New().OpenAsync(r => r.Content(body), ct);
            var checks = selectors.Select(s => new SelectorCheck(s, document.QuerySelectorAll(s).Length)).ToList();
            var title = document.Title?.Trim();
            // Normal pages also embed Cloudflare's challenge-platform script, so only look for actual block/challenge pages.
            var cloudflare = body.Contains("cf-error-details", StringComparison.OrdinalIgnoreCase)
                             || (title?.Contains("Just a moment", StringComparison.OrdinalIgnoreCase) ?? false)
                             || (title?.Contains("Attention Required", StringComparison.OrdinalIgnoreCase) ?? false);

            return new PageSnapshot(url, now, (int)response.StatusCode, response.ReasonPhrase,
                response.Content.Headers.ContentType?.ToString(), body.Length, title, cloudflare, checks, path, null);
        }
        catch (Exception e)
        {
            return new PageSnapshot(url, now, null, null, null, null, null, false, [], null, $"{e.GetType().Name}: {e.Message}");
        }
    }

    private static string Sanitize(string url)
    {
        var trimmed = url.Replace("https://", "").Replace("http://", "").TrimEnd('/');
        var safe = new string(trimmed.Select(c => char.IsLetterOrDigit(c) || c is '-' or '.' ? c : '_').ToArray());
        return safe.Length > 80 ? safe[..80] : safe;
    }
}

/// <summary>
/// Tees <see cref="Console.Out"/> into a buffer for the duration of the scope. The scrapping assembly reports swallowed
/// exceptions only via Console.WriteLine, which is invisible in a desktop app. Scopes are serialised, so use them only
/// around scrapper calls.
/// </summary>
public sealed class ConsoleCapture : IDisposable
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private readonly TextWriter _original;
    private readonly StringWriter _buffer = new();

    private ConsoleCapture()
    {
        _original = Console.Out;
        Console.SetOut(new TeeWriter(_original, _buffer));
    }

    public static async Task<ConsoleCapture> StartAsync(CancellationToken ct = default)
    {
        await Gate.WaitAsync(ct);
        try
        {
            return new ConsoleCapture();
        }
        catch
        {
            Gate.Release();
            throw;
        }
    }

    public string Text
    {
        get
        {
            lock (_buffer)
                return _buffer.ToString();
        }
    }

    /// <summary>Returns the captured text and empties the buffer.</summary>
    public string Take()
    {
        lock (_buffer)
        {
            var text = _buffer.ToString();
            _buffer.GetStringBuilder().Clear();
            return text;
        }
    }

    public void Dispose()
    {
        Console.SetOut(_original);
        Gate.Release();
    }

    private sealed class TeeWriter(TextWriter first, StringWriter second) : TextWriter
    {
        public override Encoding Encoding => first.Encoding;

        public override void Write(char value)
        {
            first.Write(value);
            lock (second) second.Write(value);
        }

        public override void Write(string? value)
        {
            first.Write(value);
            lock (second) second.Write(value);
        }

        public override void WriteLine(string? value)
        {
            first.WriteLine(value);
            lock (second) second.WriteLine(value);
        }
    }
}
