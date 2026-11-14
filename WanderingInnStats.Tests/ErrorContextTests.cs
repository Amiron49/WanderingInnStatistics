using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WanderingInnStats.Core;
using WanderingInnStats.Parsing;
using WanderingInnStats.Pipeline;
using Xunit;

namespace WanderingInnStats.Tests
{
	/// <summary>Failures must say which chapter and what went wrong, not just "lost too much".</summary>
	public class ErrorContextTests
	{
		private static Chapter Chapter(string text, string name = "7.28") =>
			new() { Name = name, Volume = "Volume 7 ", Url = "https://example.invalid/7-28", Text = text };

		[Fact]
		public void Fixup_DuplicateNavigation_NamesChapterAndOccurrences()
		{
			var chapter = Chapter("Previous Chapter Next Chapter Erin smiled. Previous Chapter Next Chapter");

			var act = () => ChapterFixing.GetFixedText(chapter);

			act.Should().Throw<ChapterFixupException>()
				.Where(e => e.ChapterIssues.Single().Url == "https://example.invalid/7-28" && e.ChapterIssues.Single().Tab == "Fixed")
				.WithMessage("*'7.28' (Volume 7)*'Previous Chapter Next Chapter'*2 times*Erin smiled*");
		}

		[Fact]
		public void Fixup_LongBlurb_NamesChapterAndLimit()
		{
			var chapter = Chapter("(" + new string('a', 400) + ") Erin smiled.");

			var act = () => ChapterFixing.GetFixedText(chapter);

			act.Should().Throw<ChapterFixupException>().WithMessage("*'7.28'*402 chars, only 300 are allowed*");
		}

		[Fact]
		public void DirtyReason_ShortText_DoesNotThrow()
		{
			ChapterFixing.DirtyReason(Chapter("(short)")).Should().StartWith("starts with a bracketed blurb");
			ChapterFixing.DirtyReason(Chapter("Erin smiled.")).Should().BeNull();
			ChapterFixing.DirtyReason(Chapter("Erin smiled. Patreon")).Should().Contain("'Patreon'");
		}

		[Fact]
		public async Task Parse_Failure_NamesChapterAndParser()
		{
			var chapter = Chapter("[" + new string('a', 1200) + "]");
			var parser = new ChapterStatisticsParser(NullLogger.Instance, new WanderingInnDefinitions());

			var act = () => parser.Create(chapter);

			(await act.Should().ThrowAsync<ChapterParseException>())
				.Where(e => e.ChapterIssues.Single().Url == "https://example.invalid/7-28" && e.ChapterIssues.Single().Tab == "Stats preview")
				.WithMessage("*'7.28'*failed in BracketsParser*matched 1?202 chars*");
		}

		[Fact]
		public void Fixup_RefusedStep_IsNamed()
		{
			var act = () => ChapterFixing.GetFixedText(Chapter("(" + new string('a', 400) + ") Erin smiled."));

			act.Should().Throw<ChapterFixupException>().Where(e => e.Fix == ChapterFix.LeadingBlurb);
		}

		[Fact]
		public void Fixup_DisabledLeadingBlurb_KeepsTextAndSkipsBracketCheck()
		{
			var chapter = Chapter("(" + new string('a', 400) + ") Erin smiled.");

			var (text, applied) = ChapterFixing.GetFixed(chapter, ChapterFix.LeadingBlurb);

			text.Should().Be(chapter.Text);
			applied.Should().Be(ChapterFix.None);
			ChapterFixing.DirtyReason(chapter, ChapterFix.LeadingBlurb).Should().BeNull();
		}

		[Fact]
		public void Fixup_ReportsAppliedSteps()
		{
			var chapter = Chapter("(note) Previous Chapter Next Chapter Erin smiled. After Chapter Thoughts bye");

			var (text, applied) = ChapterFixing.GetFixed(chapter);

			text.Should().Be("  Erin smiled. ");
			applied.Should().Be(ChapterFix.NavigationLine | ChapterFix.AfterChapterThoughts | ChapterFix.LeadingBlurb);
		}

		[Fact]
		public void Fixup_DisabledStep_IsSkipped()
		{
			var chapter = Chapter("Erin smiled. After Chapter Thoughts bye");

			ChapterFixing.GetFixedText(chapter, ChapterFix.AfterChapterThoughts).Should().Be(chapter.Text);
			ChapterFixing.DirtyReason(chapter, ChapterFix.AfterChapterThoughts).Should().Contain("After Chapter Thoughts");
			ChapterFixing.DirtyReason(chapter, ChapterFix.AfterChapterThoughts | ChapterFix.SuspiciousPhraseCheck).Should().BeNull();
		}

		[Fact]
		public void Fixup_PerChapterSettings_ApplyOnlyToThatChapter()
		{
			var a = Chapter("Erin smiled. After Chapter Thoughts bye", "a");
			var b = Chapter("Erin smiled. After Chapter Thoughts bye", "b");

			ChapterFixing.Fixup(new() { a, b }, x => x.Name == "a" ? ChapterFix.AfterChapterThoughts : ChapterFix.None);

			a.Text.Should().Contain("After Chapter Thoughts");
			b.Text.Should().Be("Erin smiled. ");
		}

		[Fact]
		public void Fixup_AntiniumWars_KeepsLeadingBracket()
		{
			var chapter = Chapter("(The Antinium Wars) The Hive marched.", "S02 – The Antinium Wars (Pt.1)");

			ChapterFixing.GetFixedText(chapter).Should().Be(chapter.Text);
			ChapterFixing.IsDirty(chapter).Should().BeFalse();
		}

		[Fact]
		public void Context_MarksOnlyTheMatchedOccurrence()
		{
			var text = "Erin and Erin";
			var match = new Regex("Erin").Matches(text)[1];

			match.Context(text).Should().Be("Erin and -->Erin<--");
		}
	}
}
