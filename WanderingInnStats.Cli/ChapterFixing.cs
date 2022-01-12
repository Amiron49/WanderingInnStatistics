using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using WanderingInnStats.Core;

namespace WanderingInnStats.Cli
{
    public static class ChapterFixing
    {
        public static List<Chapter> FindDirtyChapters(List<Chapter> chapters)
        {
            // ReSharper disable IdentifierTypo
            // ReSharper disable StringLiteralTypo

            var suspiciousSentences = new string[]
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

            return chapters.Where(target =>
            {
                if (suspiciousSentences.Any(suss => target.Text.Contains(suss, StringComparison.OrdinalIgnoreCase))) return true;

                if (
                    target.Name.Contains("S02 – The Antinium Wars (Pt.1)") ||
                    target.Name.Contains("S02 – The Antinium Wars (Pt.3)")
                )
                {
                    return false;
                }

                return target.Text[..20].Contains("(");
            }).ToList();

            // ReSharper restore IdentifierTypo
            // ReSharper restore StringLiteralTypo
        }

        public static void Fixup(List<Chapter> chapters)
        {
            // ReSharper disable IdentifierTypo
            // ReSharper disable StringLiteralTypo

            var previousChapterNextChapter = "Previous Chapter Next Chapter";
            foreach (var target in chapters.Where(x => x.Text.Contains(previousChapterNextChapter)))
            {
                var lengthBefore = target.Text.Length;

                target.Text = target.Text.Replace(previousChapterNextChapter, "");

                var lengthAfter = target.Text.Length;

                var loss = lengthBefore - lengthAfter;
                if (loss > previousChapterNextChapter.Length)
                    throw new Exception("lost too much");
            }

            var previousChapterNextChapter2 = "Previous ChapterNext Chapter";
            foreach (var target in chapters.Where(x => x.Text.Contains(previousChapterNextChapter2)))
            {
                var lengthBefore = target.Text.Length;

                target.Text = target.Text.Replace(previousChapterNextChapter2, "");

                var lengthAfter = target.Text.Length;

                var loss = lengthBefore - lengthAfter;
                if (loss > previousChapterNextChapter2.Length)
                    throw new Exception("lost too much");
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
                if (
                    target.Name.Contains("S02 – The Antinium Wars (Pt.1)") ||
                    target.Name.Contains("S02 – The Antinium Wars (Pt.3)")
                )
                {
                    continue;
                }

                var lengthBefore = target.Text.Length;
                target.Text = beginningCommentBlurbRegex.Replace(target.Text, "");

                var lengthAfter = target.Text.Length;


                var acceptableLoss = 300;
                if (target.Name.Contains("5.54 (Non-Canon)"))
                {
                    acceptableLoss = 380;
                }

                if (target.Name.Contains("6.14 K") || target.Name.Contains("6.15 K"))
                {
                    acceptableLoss = 315;
                }


                var loss = lengthBefore - lengthAfter;
                if (loss > acceptableLoss)
                    throw new Exception("lost too much");
            }


            // ReSharper restore IdentifierTypo
            // ReSharper restore StringLiteralTypo
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
}