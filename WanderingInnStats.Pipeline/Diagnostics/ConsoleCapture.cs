using System.Text;

namespace WanderingInnStats.Pipeline.Diagnostics;

/// <summary>
/// Tees <see cref="Console.Out"/> into a buffer for the duration of the scope. The scrapping assembly reports swallowed
/// exceptions only via Console.WriteLine, which is invisible in a desktop app. Scopes are serialised, so use them only
/// around scrapper calls.
/// </summary>
public sealed class ConsoleCapture : IDisposable
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private readonly TextWriter _original;
    private readonly StringWriter _buffer = new();

    private ConsoleCapture()
    {
        _original = Console.Out;
        Console.SetOut(new TeeWriter(_original, _buffer));
    }

    public static async Task<ConsoleCapture> StartAsync(CancellationToken ct = default)
    {
        await Gate.WaitAsync(ct);
        try
        {
            return new ConsoleCapture();
        }
        catch
        {
            Gate.Release();
            throw;
        }
    }

    public string Text
    {
        get
        {
            lock (_buffer)
                return _buffer.ToString();
        }
    }

    /// <summary>Returns the captured text and empties the buffer.</summary>
    public string Take()
    {
        lock (_buffer)
        {
            var text = _buffer.ToString();
            _buffer.GetStringBuilder().Clear();
            return text;
        }
    }

    public void Dispose()
    {
        Console.SetOut(_original);
        Gate.Release();
    }

    private sealed class TeeWriter(TextWriter first, StringWriter second) : TextWriter
    {
        public override Encoding Encoding => first.Encoding;

        public override void Write(char value)
        {
            first.Write(value);
            lock (second) second.Write(value);
        }

        public override void Write(string? value)
        {
            first.Write(value);
            lock (second) second.Write(value);
        }

        public override void WriteLine(string? value)
        {
            first.WriteLine(value);
            lock (second) second.WriteLine(value);
        }
    }
}
