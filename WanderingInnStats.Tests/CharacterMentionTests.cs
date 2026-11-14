using System.Linq;
using FluentAssertions;
using WanderingInnStats.Parsing;
using Xunit;

namespace WanderingInnStats.Tests
{
	public class CharacterMentionTests
	{
		private static CharacterDefinition Character(string name, params string[] aliases) =>
			new($"https://example.invalid/{name}", name) { Aliases = aliases };

		private static WanderingInnStatistics Parse(string text, params CharacterDefinition[] characters)
		{
			var statistics = new WanderingInnStatistics();
			new CharacterStats().Parse(text, statistics, new WanderingInnDefinitions { Characters = characters.ToList() });
			return statistics;
		}

		private static int Mentions(WanderingInnStatistics statistics, string name) =>
			statistics.CharacterMentions.Where(x => x.Key.Name == name).Sum(x => x.Value);

		[Fact]
		public void SameText_GoesToTheCharacterWithThatName_NotToOneSharingTheFirstName()
		{
			var mrsha = Character("Mrsha", "Mrsha du Marquin");
			var root = Character("Mrsha du Marquin", "Roots Mrsha");

			var statistics = Parse("Mrsha waved. Mrsha ran.", mrsha, root);

			Mentions(statistics, "Mrsha").Should().Be(2);
			Mentions(statistics, "Mrsha du Marquin").Should().Be(0);
		}

		[Fact]
		public void SameText_AliasBeatsPartOfAName()
		{
			var rags = Character("Rags");
			var ragathsi = Character("Ragathsi", "Rags");
			var dreams = Character("Rags of Dreams", "Chieftain Rags");

			var statistics = Parse("Rags nodded.", dreams, ragathsi, rags);

			Mentions(statistics, "Rags").Should().Be(1);
			Mentions(statistics, "Ragathsi").Should().Be(0);
			Mentions(statistics, "Rags of Dreams").Should().Be(0);
		}

		[Fact]
		public void SharedFamilyName_StillCountsForEach()
		{
			var erin = Character("Erin Solstice");
			var gregori = Character("Gregori Solstice");

			var statistics = Parse("Solstice smiled.", erin, gregori);

			Mentions(statistics, "Erin Solstice").Should().Be(1);
			Mentions(statistics, "Gregori Solstice").Should().Be(1);
		}

		[Fact]
		public void LongestMatch_StillWins()
		{
			var statistics = Parse("Mrsha du Marquin waved.", Character("Mrsha"), Character("Mrsha du Marquin"));

			Mentions(statistics, "Mrsha du Marquin").Should().Be(1);
			Mentions(statistics, "Mrsha").Should().Be(0);
		}
	}
}
