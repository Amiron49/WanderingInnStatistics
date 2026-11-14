using WanderingInnStats.Scrapping;

namespace WanderingInnStats.App.Services;

/// <summary>Human readable durations for <see cref="ScrapeGate.Estimate"/>.</summary>
public static class ScrapeEstimate
{
    /// <summary>"~45 s", "~12 min", "~1 h 05 min" for <paramref name="requests"/> gated requests, empty for none.</summary>
    public static string For(int requests) => requests <= 0 ? "" : "~" + Format(ScrapeGate.Estimate(requests));

    public static string Format(TimeSpan duration)
    {
        if (duration < TimeSpan.FromMinutes(1))
            return $"{Math.Max(1, (int)Math.Ceiling(duration.TotalSeconds))} s";
        if (duration < TimeSpan.FromHours(1))
            return $"{(int)Math.Ceiling(duration.TotalMinutes)} min";
        return $"{(int)duration.TotalHours} h {duration.Minutes:00} min";
    }
}
