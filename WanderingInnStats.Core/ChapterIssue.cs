using System.Collections.Generic;

namespace WanderingInnStats.Core
{
	/// <summary>
	/// A chapter an error is about. <see cref="Url"/> identifies it; <see cref="Tab"/> is the chapter page tab that shows
	/// the problem ("Fixed", "Stats preview", …) so the UI can link straight there.
	/// </summary>
	public record ChapterIssue(string Name, string Url, string? Detail = null, string? Tab = null);

	/// <summary>Implemented by exceptions about specific chapters, so the UI can link to them.</summary>
	public interface IHasChapterIssues
	{
		IReadOnlyList<ChapterIssue> ChapterIssues { get; }
	}
}
