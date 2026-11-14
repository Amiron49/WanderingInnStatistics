using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WanderingInnStats.Core;
using WanderingInnStats.Parsing;
using WanderingInnStats.Parsing.IndividualStatistic.Brackets;
using Xunit;

namespace WanderingInnStats.Tests
{
	public class GroundTruthTests
	{
		private static WikiEntityRaw Entity(WikiEntityKind kind, string name, string? parent = null, params string[] aliases) =>
			new() { Kind = kind, Name = name, Parent = parent, Aliases = aliases };

		private static async Task<IWanderingInnStatistics> Parse(string text, WanderingInnDefinitions definitions)
		{
			var parser = new ChapterStatisticsParser(NullLogger.Instance, definitions);
			return await parser.Create(new Chapter { Text = text, Name = "test" });
		}

		[Fact]
		public async Task Mentions_CountsLongestMatchOnce_AndBothNumbers()
		{
			var definitions = new WanderingInnDefinitions();
			definitions.Add(new List<WikiEntityRaw>
			{
				Entity(WikiEntityKind.Monster, "Dire Wolves"),
				Entity(WikiEntityKind.Monster, "Wolves"),
				Entity(WikiEntityKind.Species, "Drakes"),
				Entity(WikiEntityKind.Location, "Liscor")
			});

			var statistics = await Parse("A Drake saw two Drakes in Liscor. Dire Wolves howled; one wolf? No, wolves. Liscor's walls.", definitions);

			statistics.SpeciesMentions.Should().Contain("Drakes", 2);
			statistics.LocationMentions.Should().Contain("Liscor", 2);
			statistics.MonsterMentions.Should().Contain("Dire Wolves", 1);
			statistics.MonsterMentions.Should().Contain("Wolves", 2);
		}

		[Fact]
		public async Task Mentions_IgnoreBrackets_AmbiguousSubEntries_AndCharacterNames()
		{
			var definitions = new WanderingInnDefinitions();
			definitions.Add(new List<WikiEntityRaw>
			{
				Entity(WikiEntityKind.Species, "Antinium"),
				Entity(WikiEntityKind.Species, "Soldier", parent: "Antinium"),
				Entity(WikiEntityKind.Location, "Erin")
			});
			definitions.Add(new List<CharacterRaw>
			{
				new() { Name = "Erin", Aliases = System.Array.Empty<string>(), WikiUrl = "x" }
			});

			var statistics = await Parse("The Soldier, an Antinium [Antinium Warrior], met Erin.", definitions);

			statistics.SpeciesMentions.Should().Contain("Antinium", 1);
			statistics.SpeciesMentions.Should().NotContainKey("Soldier");
			statistics.LocationMentions.Should().BeEmpty();
		}

		[Fact]
		public async Task Mentions_GenericAliasesIgnored_LocationsNotInflected_AliasesCaseSensitive()
		{
			var definitions = new WanderingInnDefinitions();
			definitions.Add(new List<WikiEntityRaw>
			{
				Entity(WikiEntityKind.Monster, "Scalelings", null, "Children"),
				Entity(WikiEntityKind.Monster, "Sewer Slimes", null, "Poo Slimes"),
				Entity(WikiEntityKind.Monster, "Nightmares"),
				Entity(WikiEntityKind.Location, "Manus")
			});

			var statistics = await Parse("Children of Manus had nightmares about poo slimes, Poo Slimes and a scaleling. Manu? Nightmares!", definitions);

			statistics.MonsterMentions.Should().BeEquivalentTo(new Dictionary<string, int> { ["Scalelings"] = 1, ["Sewer Slimes"] = 1, ["Nightmares"] = 1 });
			statistics.LocationMentions.Should().BeEquivalentTo(new Dictionary<string, int> { ["Manus"] = 1 });
		}

		[Fact]
		public async Task WikiSpells_WinOverContextInference()
		{
			// "hire a [X]" makes ClassInference call X a class; the wiki says it's a spell.
			var chapters = new List<Chapter> { new() { Text = "They wanted to hire a [Fireball]. Also a [Fireball] and an [Abacus Counter].", Name = "test" } };

			var firstPass = new WanderingInnDefinitions();
			firstPass.Add((await Parse(chapters[0].Text, new WanderingInnDefinitions())));
			firstPass.Classes.Should().Contain("Fireball");

			firstPass.Add(new List<WikiEntityRaw>
			{
				Entity(WikiEntityKind.Spell, "Fireball"),
				Entity(WikiEntityKind.Class, "Abacus Counter")
			});

			firstPass.Classes.Should().NotContain("Fireball").And.Contain("Abacus Counter");
			firstPass.Spells.Should().Contain("Fireball");
			firstPass.Unknown.Should().NotContain("Abacus Counter");

			var second = await Parse(chapters[0].Text, firstPass);
			second.SkillsSpells.Should().ContainKey("Fireball");
			second.Classes.Should().ContainKey("Abacus Counter");
			second.UnknownBrackets.Should().BeEmpty();
		}

		[Fact]
		public void RaceHelper_Configure_AddsSingularSpecies_AndResets()
		{
			try
			{
				RaceHelper.Configure(new[] { "Agelum", "Centaurs", "Drowned People", "lowercase" });
				RaceHelper.Races.Should().Contain(new[] { "Agelum", "Centaur", "Drake" });
				RaceHelper.Races.Should().NotContain(new[] { "Drowned People", "lowercase", "Centaurs" });
			}
			finally
			{
				RaceHelper.Configure(System.Array.Empty<string>());
			}

			RaceHelper.Races.Should().BeEquivalentTo(RaceHelper.BaseRaces);
		}
	}
}
