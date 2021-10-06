using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WanderingInnStats.Parsing;
using WanderingInnStats.Parsing.IndividualStatistic;
using WanderingInnStats.Parsing.IndividualStatistic.Brackets;
using Xunit;

namespace WanderingInnStats.Tests
{
	public class BrokenBracketsTests
	{
		[Fact]
		public void CancelledBracket_CancelledSkill_Works()
		{
			var input = "[Skill – Final Run Learne—   “Nae, she will not";

			var cancelledBracketHandler = new BrokenBrackets(NullLogger.Instance);

			var result = cancelledBracketHandler.Parse(input, new WanderingInnStatistics(), new WanderingInnDefinitions());

			result.Should().Be("   “Nae, she will not");
		}
		
		[Fact]
		public void CancelledBracket_Short_Works()
		{
			var input = @"[M— 
—test";

			var cancelledBracketHandler = new BrokenBrackets(NullLogger.Instance);

			var result = cancelledBracketHandler.Parse(input, new WanderingInnStatistics(), new WanderingInnDefinitions());

			result.Should().Be(@" 
—test");
		}
		
		[Fact]
		public void CancelledBracket_YvlonPls_Works()
		{
			var input = @"[Wounded Warrior Level 32…
[Skill Change – Crescent Cut…
[Conditions Met: Wounded Warrior…
[Skill – Armform (Duelist)…";

			var cancelledBracketHandler = new BrokenBrackets(NullLogger.Instance);

			var result = cancelledBracketHandler.Parse(input, new WanderingInnStatistics(), new WanderingInnDefinitions());

			result.Should().BeNullOrWhiteSpace();
		}
		
		[Fact]
		public void CancelledBracket_NonCancelledSkill_GetsIgnored()
		{
			var input = @"
[Skill – Basic Cooking obtained!]
[Skill – Final Run Learne—   “Nae, she will not";

			var cancelledBracketHandler = new BrokenBrackets(NullLogger.Instance);

			var result = cancelledBracketHandler.Parse(input, new WanderingInnStatistics(), new WanderingInnDefinitions());

			result.Should().Be(@"
[Skill – Basic Cooking obtained!]
   “Nae, she will not");
		}
	}
}