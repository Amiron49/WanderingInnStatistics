namespace WanderingInnStats.Pipeline.Data;

public enum RunStatus
{
    Running,
    Succeeded,
    Failed,
    Cancelled
}

public class StatisticsRun
{
    public int Id { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public RunStatus Status { get; set; }
    public int ChapterCount { get; set; }
    public int CharacterCount { get; set; }
    public int? UnknownCount { get; set; }
    public string? StatisticsFile { get; set; }
    public string? DefinitionsFile { get; set; }
    public string? Error { get; set; }
}
