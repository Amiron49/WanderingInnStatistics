using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace WanderingInnStats.Parsing
{
	public abstract class AbstractDestructiveRegexParser : IDestructiveParser
	{
		protected abstract string Name { get; }
		protected abstract IEnumerable<Regex> Regexes { get; }

		protected readonly ILogger Logger;

		public AbstractDestructiveRegexParser(ILogger logger)
		{
			Logger = logger;
		}

		public string Parse(string content, WanderingInnStatistics statistics, WanderingInnDefinitions wanderingInnDefinitions)
		{
			var updatedContent = content;

			foreach (var regex in Regexes)
				updatedContent = ParseSingleRegex(regex, updatedContent, statistics, wanderingInnDefinitions);

			return updatedContent;
		}

		private string ParseSingleRegex(Regex regex, string content, WanderingInnStatistics statistics, WanderingInnDefinitions wanderingInnDefinitions)
		{
			var matches = regex.Matches(content);

			var offset = 0;
			var updatedContent = content;

			foreach (Match match in matches.OrderBy(x => x.Index))
			{
				if (match.Length > 1000)
					throw new Exception("Too much");

				if (match.Length > 100)
				{
					var context = match.Context(content);
					Logger.LogWarning("{name} Regex '{regex}' suspect match: {context}", Name, regex.ToString(), context);
				}

				var success = HandleMatch(match, statistics, content, wanderingInnDefinitions);
				if (!success)
					continue;

				updatedContent = updatedContent.Remove(match.Index + offset, match.Length);
				offset -= match.Length;
			}

			return updatedContent;
		}

		protected abstract bool HandleMatch(Match match, WanderingInnStatistics statistics, string original, WanderingInnDefinitions wanderingInnDefinitions);
	}
}