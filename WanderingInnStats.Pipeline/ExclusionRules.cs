namespace WanderingInnStats.Pipeline;

/// <summary>The hard-coded filters the old CLI applied; now only used as defaults for the Excluded flags.</summary>
public static class ExclusionRules
{
    public static bool IsChapterExcludedByDefault(string name) => name == "Glossary";

    public static bool IsCharacterExcludedByDefault(string name) =>
        name.Contains("Chapter 8.17 H") || name.Contains("Gnoll Geneva");
}
