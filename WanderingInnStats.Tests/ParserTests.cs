using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WanderingInnStats.Core;
using WanderingInnStats.Parsing;
using WanderingInnStats.Parsing.IndividualStatistic;
using Xunit;

namespace WanderingInnStats.Tests
{
	public class ParserTests
	{
		[Fact]
		public async Task Parse_WithoutDefinitions_AsExpected()
		{
			var input = @"
[Innkeeper Class Obtained!]
[Innkeeper Level 1!]
[Skill – Basic Cleaning obtained!]
[Basic Cleaning]
[Skill – Basic Cooking obtained!]
[Skill – Final Run Learne—   “Nae, she will not";

			
			var parser = new ChapterStatisticsParser(NullLogger.Instance, new WanderingInnDefinitions());

			var statistics = await parser.Create(new Chapter()
			{
				Text = input,
				Name = "test"
			});

			statistics.ClassObtains.Should().ContainKey("Innkeeper");
			statistics.ClassLevelUps.Should().ContainKey(new ClassWithLevel("Innkeeper", 1));
			statistics.UnknownBrackets.Should().ContainKey("Basic Cleaning");
			statistics.SkillObtains.Should().ContainKey("Basic Cleaning");
			statistics.SkillObtains.Should().ContainKey("Basic Cooking");
		}
		
		[Fact]
		public async Task Parse_WithDefinitions_AsExpected()
		{
			var input = @"
[Innkeeper Class Obtained!]
[Innkeeper Level 1!]
[Skill – Basic Cleaning obtained!]
[Basic Cleaning]
[Skill – Basic Cooking obtained!]
[Skill – Final Run Learne—   “Nae, she will not";

			
			var parser = new ChapterStatisticsParser(NullLogger.Instance, new WanderingInnDefinitions()
			{
				Skills = new List<string>
				{
					"Basic Cleaning"
				}
			});

			var statistics = await parser.Create(new Chapter()
			{
				Text = input,
				Name = "test"
			});

			statistics.ClassObtains.Should().ContainKey("Innkeeper");
			statistics.ClassLevelUps.Should().ContainKey(new ClassWithLevel("Innkeeper", 1));
			statistics.UnknownBrackets.Should().NotContainKey("[Basic Cleaning]");
			statistics.Skills.Should().ContainKey(new Skill("Basic Cleaning", SkillType.Skill));
			statistics.SkillObtains.Should().ContainKey("Basic Cleaning");
			statistics.SkillObtains.Should().ContainKey("Basic Cooking");
		}
		
		[Fact]
		public async Task Parse_Greedy_AsExpected()
		{
			var input = @"It’s like [Mages] have to study all their lives just to use their fancy magic. Worse than [Warriors]. I couldn’t handle it.";

			
			var parser = new ChapterStatisticsParser(NullLogger.Instance, new WanderingInnDefinitions());

			var statistics = await parser.Create(new Chapter()
			{
				Text = input,
				Name = "test"
			});

			statistics.UnknownBrackets.Keys.Should().HaveCount(2);
		}
	}
}