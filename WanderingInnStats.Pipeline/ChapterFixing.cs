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
        SuspiciousPhraseCheck = 16,
        LengthCheck = 32
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
            (ChapterFix.AfterChapterThoughts, "Cut closing author notes",
                "Removes everything from 'After Chapter Thoughts', or from a closing-note heading in the last 6,000 chars ('Author's Note:', 'Authors' Note:', 'After Chapter Notes:', …), to the end."),
            (ChapterFix.ManualRemovals, "Hard-coded removals",
                "Removes author notes hard-coded for a few chapters (6.44 E, 7.28, 7.37, Solstice (Pt. 3), Mating Rituals)."),
            (ChapterFix.LeadingBlurb, "Remove opening author notes",
                "Removes author notes at the very start, in (…), {…}, long <…> or long non-system […] brackets, plus a link line right after one. " +
                "Turning it off also skips the 'starts with a bracket' dirty check."),
            (ChapterFix.SuspiciousPhraseCheck, "Check for suspicious phrases",
                "Not a fix: fails the statistics run if a phrase like 'Patreon' or 'Next Chapter' is left. Turn off when the story text itself uses one."),
            (ChapterFix.LengthCheck, "Check for cut-off text",
                $"Not a fix: marks the chapter dirty when it has under {MinimumLength:N0} chars, which usually means the download stopped early. Turn off for posts that really are short (comics, announcements).")
        };

        /// <summary>
        /// Real chapters have at least ~3,900 chars; downloads that stopped after the opening note had under 1,600.
        /// Comics and announcements fall in between and need <see cref="ChapterFix.LengthCheck"/> turned off.
        /// </summary>
        public const int MinimumLength = 3000;

        public static bool IsSuspiciouslyShort(int length) => length < MinimumLength;

        /// <summary>Steps that are always off for a chapter, regardless of its stored settings.</summary>
        public static ChapterFix BuiltInDisabled(Chapter chapter) =>
            // Matched without the prefix: the site renamed them from "S02 – …" to "Interlude – …".
            chapter.Name.Contains("The Antinium Wars (Pt.1)") || chapter.Name.Contains("The Antinium Wars (Pt.3)")
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

            var length = chapter.Text.Trim().Length;
            if (!disabled.HasFlag(ChapterFix.LengthCheck) && IsSuspiciouslyShort(length))
                return $"only {length:N0} chars of text (under {MinimumLength:N0}), probably a cut-off download: \"{Excerpt(chapter.Text, 0, 0, 100)}\". " +
                       "Re-download it, or turn off 'Check for cut-off text' if the post really is this short.";

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

            if (FindLeadingNote(chapter.Text) is { } note)
                return $"starts with an author note: \"{Excerpt(chapter.Text, note.Start, 0, 100)}\"";

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

            // The scrapper stops at "Author’s Note", but some notes start differently: "Authors’ Note:" (10.03 Y), "After Chapter Notes:" (8.05 I), …
            if (!disabled.HasFlag(ChapterFix.AfterChapterThoughts) && FindClosingNote(target.Text) is { } closingNote)
            {
                target.Text = target.Text.Remove(closingNote);
                applied |= ChapterFix.AfterChapterThoughts;
            }

            if (!disabled.HasFlag(ChapterFix.ManualRemovals))
            {
                var before = target.Text;
                ManualFixesBecauseICantBeBotheredToAutomateThis(target);
                if (target.Text != before)
                    applied |= ChapterFix.ManualRemovals;
            }

            if (!disabled.HasFlag(ChapterFix.LeadingBlurb))
            {
                var notes = 0;
                while (notes < MaxLeadingNotes && FindLeadingNote(target.Text) is { } note)
                {
                    var noteText = target.Text[note.Start..note.End].Trim();
                    if (noteText.Length > MaxNoteLength)
                        throw new ChapterFixupException(target, ChapterFix.LeadingBlurb, "Removing the opening author note",
                            $"the {noteText[0]}…{noteText[^1]} block at the start is {noteText.Length:N0} chars, only {MaxNoteLength:N0} are allowed. " +
                            $"It ends with \"{Excerpt(target.Text, note.End, 0)}\". " +
                            "If the bracket is story text, turn this fix off for the chapter; otherwise shorten the note with an override.");

                    target.Text = target.Text[note.End..];
                    applied |= ChapterFix.LeadingBlurb;
                    notes++;
                }
            }

            return applied;

            // ReSharper restore IdentifierTypo
            // ReSharper restore StringLiteralTypo
        }

        /// <summary>
        /// A closing-note heading at the start of a line, followed by a colon or the line end: "Author’s Note:", "Authors’ Note",
        /// "After Chapter Notes: …", "Post-Chapter Note: …", "Author’s Thoughts:", "Author Call: …", "Author’s Story: …".
        /// </summary>
        private static readonly Regex ClosingNoteHeading = new(
            @"^[ \t\u00a0]*(?:(?:After|Post)[- ]Chapter[ \t]+(?:Notes?|Thoughts)|Authors?['’‘]?s?['’‘]?[ \t]+(?:Notes?|Thoughts|Story|Call))[ \t]*(?::|\r?$)",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);

        /// <summary>
        /// Finds the closing author note's heading. Only the last <see cref="MaxClosingNoteLength"/> chars (and never the
        /// first half) count: real closing notes are under 3,600 chars, while posts like "Mini-Chapters – Patreon Poll" put a
        /// "Post-Chapter Note" between stories, where cutting would lose most of the text.
        /// </summary>
        /// <returns>Where the note starts, or null.</returns>
        public static int? FindClosingNote(string text)
        {
            var match = ClosingNoteHeading.Match(text, Math.Max(text.Length / 2, text.Length - MaxClosingNoteLength));
            return match.Success ? match.Index : null;
        }

        private const int MaxClosingNoteLength = 6000;

        private const int MaxLeadingNotes = 6;
        private const int MaxNoteLines = 6;

        /// <summary>A note longer than this is more likely story text the bracket rule caught by mistake.</summary>
        private const int MaxNoteLength = 1000;

        private static readonly Regex BareUrlLine = new(@"^[ \t ]*https?://\S+[ \t ]*$");
        private static readonly Regex LabeledUrlLine = new(@"^[ \t ]*[^:\n]{1,40}:[ \t ]*https?://\S+[ \t ]*$");
        private static readonly Regex LinkLabelLine = new(@"^[ \t ]*[^:\n]{1,40}:[ \t ]*$");
        private static readonly Regex SingleTokenLine = new(@"^[ \t ]*\S{1,60}[ \t ]*$");

        /// <summary>Level-ups and Skill/Spell/Class messages: story text that opens many chapters in [brackets].</summary>
        private static readonly Regex SystemMessage = new(@"Level \d+|obtained|\b(Skill|Spell|Class|Conditions)\b", RegexOptions.IgnoreCase);

        /// <summary>
        /// Finds an author note at the start of <paramref name="text"/> (after whitespace): a block that opens with
        /// (, [, &lt; or { and closes with the matching bracket at the end of one of its first <see cref="MaxNoteLines"/> lines.
        /// <c>[…]</c> and <c>&lt;…&gt;</c> also open story text ("[Innkeeper Level 12!]", "&lt;Quest&gt;"), so they only count
        /// when long enough and not a system message. The returned range also covers a bare link line right after the note.
        /// </summary>
        /// <returns>The note's range, or null when the text doesn't start with one.</returns>
        public static (int Start, int End)? FindLeadingNote(string text)
        {
            var start = 0;
            while (start < text.Length && char.IsWhiteSpace(text[start]))
                start++;
            if (start >= text.Length)
                return null;

            var close = text[start] switch { '(' => ')', '[' => ']', '<' => '>', '{' => '}', _ => '\0' };
            if (close == '\0')
                return null;

            // The note ends at the first line (within the limit) whose last visible char is the closing bracket.
            var lineStart = start;
            int? end = null;
            for (var line = 0; line < MaxNoteLines && lineStart < text.Length; line++)
            {
                var lineEnd = text.IndexOf('\n', lineStart);
                if (lineEnd < 0)
                    lineEnd = text.Length;
                var visible = text[lineStart..lineEnd].TrimEnd();
                if (visible.EndsWith(close))
                {
                    end = lineEnd;
                    break;
                }

                lineStart = lineEnd + 1;
            }

            if (end == null)
                return null;

            var note = text[start..end.Value].Trim();
            var isNote = text[start] switch
            {
                '[' => note.Length >= 50 && note.IndexOf('[', 1) < 0 && !SystemMessage.IsMatch(note),
                '<' => note.Length >= 30,
                _ => true
            };
            if (!isNote)
                return null;

            // A link the note points to sometimes sits on its own line right after it.
            // So do labels for them ("Poll:", "Q&A Link: https://…", "Password (if you get a prompt):" + the password).
            // Blank lines in between are skipped; anything else ends the note.
            var position = end.Value; // at the '\n' ending the note's last line, or the end of the text
            var afterLabel = false;
            while (position < text.Length)
            {
                var lineBegin = position + 1;
                var lineEnd = text.IndexOf('\n', lineBegin);
                if (lineEnd < 0)
                    lineEnd = text.Length;
                var line = text[lineBegin..lineEnd];

                if (string.IsNullOrWhiteSpace(line))
                {
                    position = lineEnd;
                    continue;
                }

                var isLabel = LinkLabelLine.IsMatch(line);
                if (!BareUrlLine.IsMatch(line) && !LabeledUrlLine.IsMatch(line) && !isLabel && !(afterLabel && SingleTokenLine.IsMatch(line)))
                    break;

                end = lineEnd;
                afterLabel = isLabel;
                position = lineEnd;
            }

            return (start, end.Value);
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
                "S01 – Mating Rituals " or "S01 – Mating Rituals" or "Interlude – Mating Rituals (Pt. 1)" => chapter.Text.Replace(
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
