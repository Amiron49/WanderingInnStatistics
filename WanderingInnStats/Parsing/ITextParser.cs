namespace WanderingInnStats.Parsing
{
	public interface ITextParser
	{
		void Parse(string content, WanderingInnStatistics statistics, WanderingInnDefinitions wanderingInnDefinitions);
	}
}