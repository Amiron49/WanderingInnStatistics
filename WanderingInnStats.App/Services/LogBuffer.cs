using Serilog.Core;
using Serilog.Events;

namespace WanderingInnStats.App.Services;

public record LogLine(DateTime Timestamp, LogEventLevel Level, string Message);

/// <summary>Serilog sink that keeps the last N log lines in memory for the UI.</summary>
public class LogBuffer : ILogEventSink
{
    private const int Capacity = 2000;
    private readonly LinkedList<LogLine> _lines = new();
    private readonly Lock _lock = new();

    public event Action? Changed;

    public LogEventLevel MinimumLevel { get; set; } = LogEventLevel.Information;

    public void Emit(LogEvent logEvent)
    {
        if (logEvent.Level < MinimumLevel)
            return;

        var message = logEvent.RenderMessage();
        if (logEvent.Exception != null)
            message += Environment.NewLine + $"{logEvent.Exception.GetType().Name}: {logEvent.Exception.Message} (details on the Jobs page)";

        lock (_lock)
        {
            _lines.AddLast(new LogLine(logEvent.Timestamp.LocalDateTime, logEvent.Level, message));
            while (_lines.Count > Capacity)
                _lines.RemoveFirst();
        }

        Changed?.Invoke();
    }

    public IReadOnlyList<LogLine> Tail(int count)
    {
        lock (_lock)
            return _lines.Skip(Math.Max(0, _lines.Count - count)).ToList();
    }

    public void Clear()
    {
        lock (_lock)
            _lines.Clear();
        Changed?.Invoke();
    }
}
