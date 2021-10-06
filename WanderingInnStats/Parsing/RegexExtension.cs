using System;
using System.Text.RegularExpressions;

namespace WanderingInnStats.Parsing
{
	public static class RegexExtension
	{
		public static string Context(this Match match, string originalString)
		{
			const int r = 50;
			var start = Math.Clamp(match.Index - r, 0, originalString.Length);
			var wantedLength = match.Index - start + match.Length + r;

			if (wantedLength + start > originalString.Length)
				wantedLength = originalString.Length - start;

			var substring = originalString.Substring(start, wantedLength);
			var replace = substring.Replace(match.Value, $"-->{match.Value}<--");

			if (!replace.Contains(match.Value))
			{
				throw new Exception();
			}
			
			return replace;
		}
	}
}