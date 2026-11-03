using System.Runtime.InteropServices;
using System.Text;
using ClickStitch.Core;

namespace ClickStitch.Input;

/// <summary>
/// A temporary panel for trying one move, click, key hold, or short repeat on the real desktop.
/// It is not the product window. Closing it stops the run and asks Windows to release held inputs.
/// </summary>
public sealed class DesktopTryout : IDisposable
{
    private readonly ExecutionWorker _worker;
    private readonly MacroPause _pause = new();
    private readonly VirtualScreen _screen;
    private readonly Lock _gate = new();
    private readonly StringBuilder _log = new();
    private bool _disposed;

    private DesktopTryout(ExecutionWorker worker, VirtualScreen screen)
    {
        _worker = worker;
        _screen = screen;
        Append($"Virtual desktop origin {_screen.Left}, {_screen.Top}. Size {_screen.Width} by {_screen.Height}.");
    }

    public static DesktopTryout Create()
    {
        VirtualScreen screen = ReadScreen();
        return new DesktopTryout(ExecutionWorker.CreateForWindows(), screen);
    }

    public string State
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _worker.State.ToString();
        }
    }

    public void MoveTo(int x, int y) => Begin(MoveDocument(x, y));

    public void ClickAt(int x, int y, int count) => Begin(ClickDocument(x, y, count));

    public void HoldKey(string key, int holdMilliseconds) => Begin(HoldDocument(key, holdMilliseconds));

    public void RepeatClick(int x, int y, int count, int repeat, int gapMilliseconds) =>
        Begin(RepeatClickDocument(x, y, count, repeat, gapMilliseconds));

    public void Pause()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _pause.Pause();
        Append("Pause requested. The next action waits. A wait keeps its remaining time.");
    }

    public void Resume()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _pause.Resume();
        Append("Resumed.");
    }

    public void Stop()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _worker.Stop();
        _pause.Resume();
        Append("Stopped. Held inputs were released when Windows accepted the release.");
    }

    /// <summary>New log lines since the last call. The window timer reads this.</summary>
    public string TakeLog()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        WorkerEvent[] events = _worker.DrainProgress();
        lock (_gate)
        {
            foreach (WorkerEvent item in events)
            {
                if (item.ActionId is not null)
                {
                    _log.Append(item.Kind).Append(' ').Append(item.ActionId);
                    if (item.Message is not null)
                    {
                        _log.Append(' ').Append(item.Message);
                    }

                    _log.AppendLine();
                }
            }

            string text = _log.ToString();
            _log.Clear();
            return text;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _worker.Stop();
        _pause.Resume();
        _worker.Dispose();
        _pause.Dispose();
    }

    internal static string MoveDocument(int x, int y) => Document("try-move", "Move", $$"""
        { "id": "move", "type": "mouse.move", "x": {{x}}, "y": {{y}} }
        """);

    internal static string ClickDocument(int x, int y, int count) => Document("try-click", "Click", $$"""
        { "id": "click", "type": "mouse.click", "button": "left", "clickCount": {{count}}, "x": {{x}}, "y": {{y}} }
        """);

    internal static string HoldDocument(string key, int holdMilliseconds)
    {
        RequireKey(key);
        return Document("try-hold", "Hold", $$"""
            { "id": "down", "type": "keyboard.key", "key": "{{key}}", "state": "down" },
            { "id": "wait", "type": "wait", "durationMs": {{holdMilliseconds}} },
            { "id": "up", "type": "keyboard.key", "key": "{{key}}", "state": "up" }
            """);
    }

    internal static string RepeatClickDocument(int x, int y, int count, int repeat, int gapMilliseconds) =>
        Document("try-repeat", "Repeat", $$"""
            {
              "id": "again",
              "type": "repeat.count",
              "count": {{repeat}},
              "actions": [
                { "id": "click", "type": "mouse.click", "button": "left", "clickCount": {{count}}, "x": {{x}}, "y": {{y}} },
                { "id": "gap", "type": "wait", "durationMs": {{gapMilliseconds}} }
              ]
            }
            """);

    private void Begin(string json)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _pause.Resume();
        if (_worker.State is WorkerState.Running or WorkerState.Paused or WorkerState.Stopping)
        {
            Append("A run is already going. Press Stop first.");
            return;
        }

        try
        {
            bool started = MacroSession.TryStart(
                _worker,
                json,
                () => _screen,
                result => Append(result.Status.ToString()),
                out PreparedMacro prepared);
            if (!started)
            {
                Append(prepared.Issues.Count == 0 ? "The document was not accepted." : prepared.Issues[0].Message);
            }
        }
        catch (Exception error)
        {
            Append(error.Message);
        }
    }

    private void Append(string line)
    {
        lock (_gate)
        {
            _log.AppendLine(line);
        }
    }

    private static string Document(string id, string name, string actions) => $$"""
        {
          "formatVersion": 1,
          "id": "{{id}}",
          "name": "{{name}}",
          "actions": [ {{actions}} ]
        }
        """;

    private static void RequireKey(string key)
    {
        if (!MacroKeys.IsKnown(key))
        {
            throw new ArgumentException($"Key {key} is not in the macro schema.", nameof(key));
        }
    }

    private static VirtualScreen ReadScreen()
    {
        int left = GetSystemMetrics(76);
        int top = GetSystemMetrics(77);
        int width = GetSystemMetrics(78);
        int height = GetSystemMetrics(79);
        if (width < 2 || height < 2)
        {
            throw new InvalidOperationException("Windows did not report a virtual desktop.");
        }

        return new VirtualScreen(left, top, width, height);
    }

    [DllImport("user32.dll", EntryPoint = "GetSystemMetrics")]
    private static extern int GetSystemMetrics(int index);
}
