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
			var chapter = Chapter("(" + new string('a', 1100) + ")\nErin smiled.");

			var act = () => ChapterFixing.GetFixedText(chapter);

			act.Should().Throw<ChapterFixupException>().WithMessage("*'7.28'*is 1?102 chars, only 1?000 are allowed*");
		}

		[Fact]
		public void DirtyReason_ShortText_DoesNotThrow()
		{
			// The samples are short on purpose; the length check is covered separately.
			const ChapterFix noLength = ChapterFix.LengthCheck;
			ChapterFixing.DirtyReason(Chapter("(short)"), noLength).Should().StartWith("starts with an author note");
			ChapterFixing.DirtyReason(Chapter("Erin smiled."), noLength).Should().BeNull();
			ChapterFixing.DirtyReason(Chapter("Erin smiled. Patreon"), noLength).Should().Contain("'Patreon'");
		}

		[Fact]
		public void DirtyReason_CutOffDownload_IsFlagged()
		{
			// 10.25 MG was stored as just its opening announcement.
			var cutOff = Chapter("(Announcing a fashion collection! Check it out now!)", "10.25 MG");

			ChapterFixing.DirtyReason(cutOff).Should().Contain("cut-off download").And.Contain("Announcing");
			ChapterFixing.DirtyReason(cutOff, ChapterFix.LengthCheck).Should().NotContain("cut-off");
			ChapterFixing.DirtyReason(Chapter("Erin smiled. " + new string('a', ChapterFixing.MinimumLength))).Should().BeNull();
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
			var act = () => ChapterFixing.GetFixedText(Chapter("(" + new string('a', 1100) + ")\nErin smiled."));

			act.Should().Throw<ChapterFixupException>().Where(e => e.Fix == ChapterFix.LeadingBlurb);
		}

		[Fact]
		public void Fixup_DisabledLeadingBlurb_KeepsTextAndSkipsBracketCheck()
		{
			var chapter = Chapter("(" + new string('a', 1100) + ")\nErin smiled.");

			var (text, applied) = ChapterFixing.GetFixed(chapter, ChapterFix.LeadingBlurb);

			text.Should().Be(chapter.Text);
			applied.Should().Be(ChapterFix.None);
			ChapterFixing.DirtyReason(chapter, ChapterFix.LeadingBlurb | ChapterFix.LengthCheck).Should().BeNull();
		}

		[Fact]
		public void Fixup_ReportsAppliedSteps()
		{
			var chapter = Chapter("(note)\nPrevious Chapter Next Chapter Erin smiled. After Chapter Thoughts bye");

			var (text, applied) = ChapterFixing.GetFixed(chapter);

			text.Should().Be("\n Erin smiled. ");
			applied.Should().Be(ChapterFix.NavigationLine | ChapterFix.AfterChapterThoughts | ChapterFix.LeadingBlurb);
		}

		[Fact]
		public void Fixup_DisabledStep_IsSkipped()
		{
			var chapter = Chapter("Erin smiled. After Chapter Thoughts bye");

			ChapterFixing.GetFixedText(chapter, ChapterFix.AfterChapterThoughts).Should().Be(chapter.Text);
			ChapterFixing.DirtyReason(chapter, ChapterFix.AfterChapterThoughts | ChapterFix.LengthCheck).Should().Contain("After Chapter Thoughts");
			ChapterFixing.DirtyReason(chapter, ChapterFix.AfterChapterThoughts | ChapterFix.SuspiciousPhraseCheck | ChapterFix.LengthCheck).Should().BeNull();
		}

		[Theory]
		[InlineData("Authors’ Note:")]
		[InlineData("Author’s Note:")]
		[InlineData("Author's Notes\r")]
		[InlineData("After Chapter Notes: There were some parts I didn’t get to write!")]
		[InlineData("Post-Chapter Note: You may see some more of my ramblings.")]
		public void Fixup_ClosingAuthorNoteHeading_CutsToTheEnd(string heading)
		{
			var story = string.Join("\n", Enumerable.Repeat("Erin smiled at the Goblin.", 20));
			var chapter = Chapter(story + "\n \n" + heading + "\nThanks for reading!\nKo-Fi: https://ko-fi.com/someone");

			var (text, applied) = ChapterFixing.GetFixed(chapter);

			text.Should().Be(story + "\n \n");
			applied.Should().Be(ChapterFix.AfterChapterThoughts);
		}

		[Fact]
		public void Fixup_AuthorNoteHeadingInFirstHalf_IsKept()
		{
			var story = string.Join("\n", Enumerable.Repeat("Erin smiled at the Goblin.", 20));
			var chapter = Chapter("Author’s Note:\n" + story);

			ChapterFixing.GetFixedText(chapter).Should().Be(chapter.Text);
		}

		[Fact]
		public void Fixup_NoteHeadingBetweenStories_IsKept()
		{
			var story = string.Join("\n", Enumerable.Repeat("Erin smiled at the Goblin.", 400));
			var chapter = Chapter(story + "\nPost-Chapter Note: on to the next mini-chapter!\n" + story);

			ChapterFixing.GetFixedText(chapter).Should().Be(chapter.Text);
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
			var chapter = Chapter("(The Antinium Wars) The Hive marched.", "Interlude – The Antinium Wars (Pt.1)");

			ChapterFixing.GetFixedText(chapter).Should().Be(chapter.Text);
			ChapterFixing.IsDirty(chapter, ChapterFix.LengthCheck).Should().BeFalse();
		}

		[Fact]
		public void OpeningNotes_StackedInAnyBracket_AreRemovedWithTheirLinks()
		{
			// Shapes from 9.65, 10.43 and 10.50: stacked notes, a note spanning lines, links and their labels after one.
			var text = string.Join("\n",
				"<Book 11, The Titan of Baleros is out now.>",
				"[The Wandering Inn is running the annual survey. Consider filling it out here.]",
				" ",
				"(We are selling a poster. Link is here.)",
				"<Hey everyone, there’s a livestream coming! Fill in the form",
				"https://forms.gle/abc",
				"See you there!>",
				"https://www.youtube.com/@TheWanderingInn",
				"Poll:",
				"https://www.patreon.com/posts/123",
				"Q&A Link: https://docs.google.com/forms/xyz",
				"Password (if you get a prompt):",
				"MrshaTheSoftAndHuggable",
				" ",
				"Erin woke up.",
				"Poll:");

			ChapterFixing.GetFixed(Chapter(text)).Text.TrimStart().Should().Be("Erin woke up.\nPoll:");
		}

		[Theory]
		[InlineData("[Innkeeper Level 12!]\nErin woke up.")]
		[InlineData("[Skill – Basic Footwork obtained!]\n[Princess Level 6!]\nShe ran.")]
		[InlineData("[User Rose has joined the conversation.]\n[15 Users Connected]\nLol.")]
		[InlineData("[Lieutenant] Gershal of Vaunt was an unhappy man.\nHe sighed.")]
		[InlineData("[Conditions Met: Human → Lesser Lich! Class obtained, with a very long message indeed!]\nHe rose.")]
		[InlineData("<Quest>\nThe quest began.")]
		[InlineData("(An aside that continues) on the same line.\nMore story.")]
		public void OpeningNotes_StoryText_IsKept(string text)
		{
			var chapter = Chapter(text);

			ChapterFixing.GetFixed(chapter).Text.Should().Be(text);
			ChapterFixing.FindLeadingNote(text).Should().BeNull();
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
