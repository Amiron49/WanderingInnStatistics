namespace WanderingInnStats.App.Services;

public enum JobState
{
    Idle,
    Running,
    Succeeded,
    Failed,
    Cancelled
}

public class JobRecord
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public DateTime StartedAt { get; init; }
    public DateTime? FinishedAt { get; set; }
    public JobState State { get; set; } = JobState.Running;
    public string? Status { get; set; }
    public double? Percent { get; set; }
    public string? ResultMessage { get; set; }
    public Exception? Exception { get; set; }

    /// <summary>Optional structured details a job can attach (e.g. per-item failures), shown on the Jobs page.</summary>
    public List<(string Title, string Details)> Notes { get; } = new();

    public TimeSpan? Duration => FinishedAt - StartedAt;
}

/// <summary>
/// Runs at most one long-running job (download, wiki scrape, statistics run) at a time, independent of the
/// page that started it, so navigating away doesn't kill it. Keeps a history of recent jobs including their exceptions.
/// </summary>
public class JobRunner
{
    private const int HistorySize = 50;

    private readonly LinkedList<JobRecord> _history = new();
    private readonly Lock _lock = new();
    private CancellationTokenSource? _cts;
    private int _nextId = 1;

    public event Action? Changed;

    /// <summary>The running job, or the most recently finished one.</summary>
    public JobRecord? Current { get; private set; }

    public JobState State => Current?.State ?? JobState.Idle;
    public string? Name => Current?.Name;
    public string? Status => Current?.Status;
    public double? Percent => Current?.Percent;
    public string? ResultMessage => Current?.ResultMessage;
    public Exception? Exception => Current?.Exception;
    public string? Error => Current?.Exception?.Message;

    public bool IsRunning => State == JobState.Running;

    public IReadOnlyList<JobRecord> History
    {
        get
        {
            lock (_lock)
                return _history.ToList();
        }
    }

    public JobRecord? Get(int id)
    {
        lock (_lock)
            return _history.FirstOrDefault(x => x.Id == id);
    }

    /// <summary>Report status from inside a job. Safe to call from any thread.</summary>
    public void Report(string status, double? percent = null)
    {
        if (Current == null) return;
        Current.Status = status;
        Current.Percent = percent;
        Changed?.Invoke();
    }

    /// <summary>Attach extra diagnostic details to the running job.</summary>
    public void AddNote(string title, string details)
    {
        Current?.Notes.Add((title, details));
    }

    /// <returns>False when another job is already running.</returns>
    public bool TryStart(string name, Func<CancellationToken, Task<string?>> job)
    {
        JobRecord record;
        lock (_lock)
        {
            if (IsRunning)
                return false;

            record = new JobRecord { Id = _nextId++, Name = name, StartedAt = DateTime.Now, Status = "Starting…" };
            _history.AddFirst(record);
            while (_history.Count > HistorySize)
                _history.RemoveLast();
            Current = record;
            _cts = new CancellationTokenSource();
        }

        var token = _cts.Token;
        Changed?.Invoke();

        _ = Task.Run(async () =>
        {
            try
            {
                record.ResultMessage = await job(token);
                record.State = JobState.Succeeded;
                record.Percent = 100;
            }
            catch (OperationCanceledException)
            {
                record.State = JobState.Cancelled;
            }
            catch (Exception e)
            {
                record.Exception = e;
                record.State = JobState.Failed;
                Serilog.Log.Error(e, "Job {name} failed", name);
            }
            finally
            {
                record.FinishedAt = DateTime.Now;
                lock (_lock)
                {
                    _cts?.Dispose();
                    _cts = null;
                }

                Changed?.Invoke();
            }
        });

        return true;
    }

    public void Cancel()
    {
        lock (_lock)
            _cts?.Cancel();
        Report("Cancelling…", Percent);
    }
}
