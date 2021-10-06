namespace WanderingInnStats.Parsing
{
	public interface IDestructiveParser
	{
		string Parse(string content, WanderingInnStatistics statistics, WanderingInnDefinitions wanderingInnDefinitions);
	}
}