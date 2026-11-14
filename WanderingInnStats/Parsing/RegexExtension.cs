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

			// Mark this match by position: replacing by value would also mark identical text around it.
			var matchStart = match.Index - start;
			var matchEnd = Math.Min(matchStart + match.Length, wantedLength);
			var substring = originalString.Substring(start, wantedLength);
			return substring[..matchStart] + "-->" + substring[matchStart..matchEnd] + "<--" + substring[matchEnd..];
		}
	}
}