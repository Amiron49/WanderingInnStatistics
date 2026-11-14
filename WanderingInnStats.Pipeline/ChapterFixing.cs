using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using WanderingInnStats.Core;

namespace WanderingInnStats.Pipeline
{
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

        /// <summary>
        /// Runs <see cref="Fixup"/> on a copy of a single chapter and returns the fixed text.
        /// Throws whatever <see cref="Fixup"/> throws (e.g. "lost too much").
        /// </summary>
        public static string GetFixedText(Chapter chapter)
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
            Fixup(new List<Chapter> { copy });
            return copy.Text;
        }

        public static bool IsDirty(Chapter chapter) => DirtyReason(chapter) != null;

        public static List<Chapter> FindDirtyChapters(List<Chapter> chapters) => chapters.Where(IsDirty).ToList();

        /// <summary>Why <paramref name="chapter"/> still looks dirty, or null when it's clean.</summary>
        public static string? DirtyReason(Chapter chapter)
        {
            // ReSharper disable IdentifierTypo
            // ReSharper disable StringLiteralTypo

            var suspicious = SuspiciousSentences.FirstOrDefault(suss => chapter.Text.Contains(suss, StringComparison.OrdinalIgnoreCase));
            if (suspicious != null)
            {
                var index = chapter.Text.IndexOf(suspicious, StringComparison.OrdinalIgnoreCase);
                return $"contains '{suspicious}' at {index:N0}: \"{Excerpt(chapter.Text, index, suspicious.Length)}\"";
            }

            if (IsAntiniumWarsException(chapter))
                return null;

            var start = chapter.Text[..Math.Min(20, chapter.Text.Length)];
            return start.Contains("(") ? $"starts with a bracketed blurb: \"{Excerpt(chapter.Text, 0, 0)}\"" : null;

            // ReSharper restore IdentifierTypo
            // ReSharper restore StringLiteralTypo
        }

        private static bool IsAntiniumWarsException(Chapter chapter) =>
            chapter.Name.Contains("S02 – The Antinium Wars (Pt.1)") ||
            chapter.Name.Contains("S02 – The Antinium Wars (Pt.3)");

        public static void Fixup(List<Chapter> chapters)
        {
            // ReSharper disable IdentifierTypo
            // ReSharper disable StringLiteralTypo

            foreach (var marker in new[] { "Previous Chapter Next Chapter", "Previous ChapterNext Chapter" })
            {
                foreach (var target in chapters.Where(x => x.Text.Contains(marker)))
                {
                    var before = target.Text;
                    target.Text = target.Text.Replace(marker, "");

                    // Only one navigation line is expected; more means the marker appears inside the story text too.
                    var loss = before.Length - target.Text.Length;
                    if (loss > marker.Length)
                    {
                        var first = before.IndexOf(marker, StringComparison.Ordinal);
                        var second = before.IndexOf(marker, first + marker.Length, StringComparison.Ordinal);
                        throw new ChapterFixupException(target, $"Removing '{marker}'",
                            $"it occurs {loss / marker.Length} times, but only one navigation line is expected. " +
                            $"First at {first:N0}: \"{Excerpt(before, first, marker.Length)}\"; " +
                            $"second at {second:N0}: \"{Excerpt(before, second, marker.Length)}\"");
                    }
                }
            }

            var afterChapterThoughts = "After Chapter Thoughts";
            foreach (var target in chapters.Where(x => x.Text.Contains(afterChapterThoughts)))
            {
                var index = target.Text.IndexOf(afterChapterThoughts, StringComparison.OrdinalIgnoreCase);
                target.Text = target.Text.Remove(index);
            }

            foreach (var chapter in chapters)
            {
                ManualFixesBecauseICantBeBotheredToAutomateThis(chapter);
            }
            
            var beginningCommentBlurbRegex = new Regex(@"^\(.+\)");
            foreach (var target in chapters.Where(x => beginningCommentBlurbRegex.IsMatch(x.Text)))
            {
                if (IsAntiniumWarsException(target))
                    continue;

                var match = beginningCommentBlurbRegex.Match(target.Text);

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
                    throw new ChapterFixupException(target, "Removing the leading (…) author blurb",
                        $"it would remove {match.Length:N0} chars, only {acceptableLoss:N0} are allowed. " +
                        $"The match ends with \"{Excerpt(target.Text, match.Length, 0)}\". " +
                        "Shorten the blurb with an override, or raise the chapter's allowance in ChapterFixing.Fixup.");

                target.Text = beginningCommentBlurbRegex.Replace(target.Text, "");
            }


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
    public class ChapterFixupException : Exception
    {
        public ChapterFixupException(Chapter chapter, string step, string reason)
            : base($"Fixing chapter '{chapter.Name}' ({chapter.Volume.Trim()}) failed. {step} was refused: {reason}")
        {
            ChapterName = chapter.Name;
            ChapterUrl = chapter.Url;
        }

        public string ChapterName { get; }
        public string ChapterUrl { get; }
    }
}
