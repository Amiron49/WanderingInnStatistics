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
				.Where(e => e.ChapterName == "7.28")
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
				.Where(e => e.ChapterName == "7.28")
				.WithMessage("*'7.28'*failed in BracketsParser*matched 1?202 chars*");
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
