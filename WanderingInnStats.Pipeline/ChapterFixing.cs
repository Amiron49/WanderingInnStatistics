using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using WanderingInnStats.Core;

namespace WanderingInnStats.Pipeline
{
    /// <summary>A step of <see cref="ChapterFixing.Fixup"/> (or its dirty check) that can be turned off per chapter.</summary>
    [Flags]
    public enum ChapterFix
    {
        None = 0,
        NavigationLine = 1,
        AfterChapterThoughts = 2,
        ManualRemovals = 4,
        LeadingBlurb = 8,
        SuspiciousPhraseCheck = 16
    }

    public static class ChapterFixing
    {
        // ReSharper disable StringLiteralTypo
        public static readonly IReadOnlyList<string> SuspiciousSentences = new[]
        {
            "After Chapter Thoughts",
            "Author Note",
            "Patreon",
            "DeviantArt",
            "Deviant-Art",
            "Deviant Art",
            "ko-fi",
            "Next Chapter"
        };
        // ReSharper restore StringLiteralTypo

        /// <summary>The steps <see cref="Fixup"/> can take, with a label and description for the UI.</summary>
        public static readonly IReadOnlyList<(ChapterFix Fix, string Label, string Description)> AllFixes = new[]
        {
            (ChapterFix.NavigationLine, "Remove navigation line",
                "Removes the 'Previous Chapter Next Chapter' link line. Refuses when it occurs more than once."),
            (ChapterFix.AfterChapterThoughts, "Cut 'After Chapter Thoughts'",
                "Removes everything from 'After Chapter Thoughts' to the end."),
            (ChapterFix.ManualRemovals, "Hard-coded removals",
                "Removes author notes hard-coded for a few chapters (6.44 E, 7.28, 7.37, Solstice (Pt. 3), Mating Rituals)."),
            (ChapterFix.LeadingBlurb, "Remove leading (…) note",
                "Removes a bracketed author note at the very start. Turning it off also skips the 'starts with a bracket' dirty check."),
            (ChapterFix.SuspiciousPhraseCheck, "Check for suspicious phrases",
                "Not a fix: fails the statistics run if a phrase like 'Patreon' or 'Next Chapter' is left. Turn off when the story text itself uses one.")
        };

        /// <summary>Steps that are always off for a chapter, regardless of its stored settings.</summary>
        public static ChapterFix BuiltInDisabled(Chapter chapter) =>
            chapter.Name.Contains("S02 – The Antinium Wars (Pt.1)") || chapter.Name.Contains("S02 – The Antinium Wars (Pt.3)")
                ? ChapterFix.LeadingBlurb // these chapters really start with a bracket
                : ChapterFix.None;

        private static ChapterFix Effective(Chapter chapter, ChapterFix disabled) => disabled | BuiltInDisabled(chapter);

        /// <summary>
        /// Runs <see cref="Fixup"/> on a copy of a single chapter and returns the fixed text.
        /// Throws <see cref="ChapterFixupException"/> when a safety check refuses a step.
        /// </summary>
        public static string GetFixedText(Chapter chapter, ChapterFix disabled = ChapterFix.None) => GetFixed(chapter, disabled).Text;

        /// <summary>Like <see cref="GetFixedText"/>, plus which steps actually changed the text.</summary>
        public static (string Text, ChapterFix Applied) GetFixed(Chapter chapter, ChapterFix disabled = ChapterFix.None)
        {
            var copy = new Chapter
            {
                Volume = chapter.Volume,
                Url = chapter.Url,
                Name = chapter.Name,
                Date = chapter.Date,
                Text = chapter.Text,
                Comments = chapter.Comments
            };
            var applied = FixChapter(copy, disabled);
            return (copy.Text, applied);
        }

        public static bool IsDirty(Chapter chapter, ChapterFix disabled = ChapterFix.None) => DirtyReason(chapter, disabled) != null;

        public static List<Chapter> FindDirtyChapters(List<Chapter> chapters, Func<Chapter, ChapterFix>? disabledFor = null) =>
            chapters.Where(x => IsDirty(x, disabledFor?.Invoke(x) ?? ChapterFix.None)).ToList();

        /// <summary>Why <paramref name="chapter"/> still looks dirty, or null when it's clean.</summary>
        public static string? DirtyReason(Chapter chapter, ChapterFix disabled = ChapterFix.None)
        {
            // ReSharper disable IdentifierTypo
            // ReSharper disable StringLiteralTypo
            disabled = Effective(chapter, disabled);

            if (!disabled.HasFlag(ChapterFix.SuspiciousPhraseCheck))
            {
                var suspicious = SuspiciousSentences.FirstOrDefault(suss => chapter.Text.Contains(suss, StringComparison.OrdinalIgnoreCase));
                if (suspicious != null)
                {
                    var index = chapter.Text.IndexOf(suspicious, StringComparison.OrdinalIgnoreCase);
                    return $"contains '{suspicious}' at {index:N0}: \"{Excerpt(chapter.Text, index, suspicious.Length)}\"";
                }
            }

            if (disabled.HasFlag(ChapterFix.LeadingBlurb))
                return null;

            var start = chapter.Text[..Math.Min(20, chapter.Text.Length)];
            return start.Contains("(") ? $"starts with a bracketed blurb: \"{Excerpt(chapter.Text, 0, 0)}\"" : null;

            // ReSharper restore IdentifierTypo
            // ReSharper restore StringLiteralTypo
        }

        /// <param name="disabledFor">The steps turned off per chapter; null runs every step on every chapter.</param>
        public static void Fixup(List<Chapter> chapters, Func<Chapter, ChapterFix>? disabledFor = null)
        {
            foreach (var chapter in chapters)
                FixChapter(chapter, disabledFor?.Invoke(chapter) ?? ChapterFix.None);
        }

        /// <returns>The steps that changed the text.</returns>
        private static ChapterFix FixChapter(Chapter target, ChapterFix disabled)
        {
            // ReSharper disable IdentifierTypo
            // ReSharper disable StringLiteralTypo
            disabled = Effective(target, disabled);
            var applied = ChapterFix.None;

            if (!disabled.HasFlag(ChapterFix.NavigationLine))
            {
                foreach (var marker in new[] { "Previous Chapter Next Chapter", "Previous ChapterNext Chapter" })
                {
                    if (!target.Text.Contains(marker))
                        continue;

                    var before = target.Text;
                    target.Text = target.Text.Replace(marker, "");
                    applied |= ChapterFix.NavigationLine;

                    // Only one navigation line is expected; more means the marker appears inside the story text too.
                    var loss = before.Length - target.Text.Length;
                    if (loss > marker.Length)
                    {
                        var first = before.IndexOf(marker, StringComparison.Ordinal);
                        var second = before.IndexOf(marker, first + marker.Length, StringComparison.Ordinal);
                        throw new ChapterFixupException(target, ChapterFix.NavigationLine, $"Removing '{marker}'",
                            $"it occurs {loss / marker.Length} times, but only one navigation line is expected. " +
                            $"First at {first:N0}: \"{Excerpt(before, first, marker.Length)}\"; " +
                            $"second at {second:N0}: \"{Excerpt(before, second, marker.Length)}\"");
                    }
                }
            }

            var afterChapterThoughts = "After Chapter Thoughts";
            if (!disabled.HasFlag(ChapterFix.AfterChapterThoughts) && target.Text.Contains(afterChapterThoughts))
            {
                var index = target.Text.IndexOf(afterChapterThoughts, StringComparison.OrdinalIgnoreCase);
                target.Text = target.Text.Remove(index);
                applied |= ChapterFix.AfterChapterThoughts;
            }

            if (!disabled.HasFlag(ChapterFix.ManualRemovals))
            {
                var before = target.Text;
                ManualFixesBecauseICantBeBotheredToAutomateThis(target);
                if (target.Text != before)
                    applied |= ChapterFix.ManualRemovals;
            }

            var beginningCommentBlurbRegex = new Regex(@"^\(.+\)");
            if (!disabled.HasFlag(ChapterFix.LeadingBlurb) && beginningCommentBlurbRegex.Match(target.Text) is { Success: true } match)
            {
                var acceptableLoss = 300;
                if (target.Name.Contains("5.54 (Non-Canon)"))
                {
                    acceptableLoss = 380;
                }

                if (target.Name.Contains("6.14 K") || target.Name.Contains("6.15 K"))
                {
                    acceptableLoss = 315;
                }

                if (match.Length > acceptableLoss)
                    throw new ChapterFixupException(target, ChapterFix.LeadingBlurb, "Removing the leading (…) author blurb",
                        $"it would remove {match.Length:N0} chars, only {acceptableLoss:N0} are allowed. " +
                        $"The match ends with \"{Excerpt(target.Text, match.Length, 0)}\". " +
                        "If the bracket is story text, turn this fix off for the chapter; otherwise shorten the note with an override.");

                target.Text = beginningCommentBlurbRegex.Replace(target.Text, "");
                applied |= ChapterFix.LeadingBlurb;
            }

            return applied;

            // ReSharper restore IdentifierTypo
            // ReSharper restore StringLiteralTypo
        }

        /// <summary>The text around [<paramref name="index"/>, +<paramref name="length"/>), on one line, for error messages.</summary>
        private static string Excerpt(string text, int index, int length, int radius = 60)
        {
            if (index < 0)
                return "";
            var start = Math.Max(0, index - radius);
            var end = Math.Min(text.Length, index + length + radius);
            var excerpt = (start > 0 ? "…" : "") + text[start..end] + (end < text.Length ? "…" : "");
            return Regex.Replace(excerpt, @"\s+", " ");
        }

        private static void ManualFixesBecauseICantBeBotheredToAutomateThis(Chapter chapter)
        {
            chapter.Text = chapter.Name switch
            {
                "S01 – Mating Rituals " or "S01 – Mating Rituals" => chapter.Text.Replace(
                    "*Warning. If you don’t like descriptions of genitalia or sex or anything of that nature, please don’t read this story. This is a Patreon side story which isn’t chronologically linked to the same part in the plot as current chapters.*",
                    ""),
                "6.44 E" => chapter.Text.Replace(
                    "(Volume 2 of The Wandering Inn is available on Amazon! If you can, please leave a review or tell people it’s out! Buying is not necessarily necessary. Thanks so much for the support!)",
                    ""),
                "7.28" => chapter.Text.Replace("(I’m planning on taking my monthly week-long break after next chapter. I think. I’ll let you know the details next chapter!)", ""),
                "7.37" => chapter.Text.Replace("(One of our subreddit mods, Akrasia, is putting on a poll for TWI-readers, like last year! Consider filling it out!)", ""),
                "Solstice (Pt. 3)" => chapter.Text.Replace(
                    "(Before going to next chapter, read Solstice Pt. 4-9. This is for users who do not see hyperlinks, such as those on mobile devices or WordPress’ Reader Mode.)",
                    ""),
                _ => chapter.Text
            };
        }
    }

    /// <summary>A <see cref="ChapterFixing.Fixup"/> safety check refused to change a chapter.</summary>
    public class ChapterFixupException : Exception, IHasChapterIssues
    {
        public ChapterFixupException(Chapter chapter, ChapterFix fix, string step, string reason)
            : base($"Fixing chapter '{chapter.Name}' ({chapter.Volume.Trim()}) failed. {step} was refused: {reason}")
        {
            Fix = fix;
            ChapterName = chapter.Name;
            ChapterUrl = chapter.Url;
            ChapterIssues = new[] { new ChapterIssue(chapter.Name, chapter.Url, $"{step} was refused", "Fixed") };
        }

        /// <summary>The step that refused; turning it off for the chapter skips it.</summary>
        public ChapterFix Fix { get; }

        public string ChapterName { get; }
        public string ChapterUrl { get; }
        public IReadOnlyList<ChapterIssue> ChapterIssues { get; }
    }

    /// <summary>Chapters that still trip the dirty check after <see cref="ChapterFixing.Fixup"/>.</summary>
    public class DirtyChaptersException : InvalidOperationException, IHasChapterIssues
    {
        public DirtyChaptersException(IReadOnlyList<ChapterIssue> issues)
            : base($"{issues.Count} chapter(s) are still dirty after fixing; write an override or turn off the check for each (chapter page → Fixed):"
                   + string.Concat(issues.Select(x => $"{Environment.NewLine}• '{x.Name}': {x.Detail}")))
        {
            ChapterIssues = issues;
        }

        public IReadOnlyList<ChapterIssue> ChapterIssues { get; }
    }
}
