using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using ClickStitch.Core;

namespace ClickStitch.Input;

/// <summary>
/// A temporary panel for trying a move, a click, a key hold, a short repeat, or one recording on the real desktop.
/// It is not the product window. Closing it stops the run, uninstalls a recording hook, and asks Windows to release held inputs.
/// </summary>
public sealed class DesktopTryout : IDisposable
{
    private readonly ExecutionWorker _worker;
    private readonly MacroPause _pause = new();
    private readonly VirtualScreen _screen;
    private readonly IRecordingHook _hook;
    private readonly Lock _gate = new();
    private readonly StringBuilder _log = new();
    private RecordingCapture? _capture;
    private string? _recordingJson;

    /// <summary>The latest recording as format-1 JSON, or null when there is nothing to play.</summary>
    public string? RecordingJson
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _recordingJson;
        }
    }
    private Stopwatch? _runClock;
    private bool _disposed;

    public string RunningName { get; private set; } = "";

    public string CurrentStep { get; private set; } = "";

    public string ElapsedText { get; private set; } = "0:00";

    private DesktopTryout(ExecutionWorker worker, VirtualScreen screen, IRecordingHook hook)
    {
        _worker = worker;
        _screen = screen;
        _hook = hook;
        Append($"Virtual desktop origin {_screen.Left}, {_screen.Top}. Size {_screen.Width} by {_screen.Height}.");
    }

    public static DesktopTryout Create()
    {
        VirtualScreen screen = ReadScreen();
        return new DesktopTryout(ExecutionWorker.CreateForWindows(), screen, new Win32RecordingHook());
    }

    internal static DesktopTryout Create(ExecutionWorker worker, VirtualScreen screen, IRecordingHook hook) =>
        new(worker, screen, hook);

    public string State
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_capture?.Session.State == RecordingState.Recording)
            {
                return "Recording";
            }

            return _worker.State.ToString();
        }
    }

    public bool IsRecording
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _capture?.Session.State == RecordingState.Recording;
        }
    }

    public void MoveTo(int x, int y) => Begin(MoveDocument(x, y));

    public void ClickAt(int x, int y, int count) => Begin(ClickDocument(x, y, count));

    public void HoldKey(string key, int holdMilliseconds) => Begin(HoldDocument(key, holdMilliseconds));

    public void RepeatClick(int x, int y, int count, int repeat, int gapMilliseconds) =>
        Begin(RepeatClickDocument(x, y, count, repeat, gapMilliseconds));

    public void StartRecording(bool captureMovement)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_worker.State is WorkerState.Running or WorkerState.Paused or WorkerState.Stopping)
        {
            Append("A run is already going. Press Stop first.");
            return;
        }

        if (IsRecording)
        {
            Append("A recording is already going. Press Stop recording first.");
            return;
        }

        _capture?.Dispose();
        _recordingJson = null;
        _capture = new RecordingCapture(_hook, RecordingOptions.Default with { CaptureMovement = captureMovement });
        try
        {
            _capture.Start();
            Append(captureMovement
                ? "Recording the desktop. Buttons, keys, the wheel, and pointer movement are kept. Clicks on this window are left out when you stop. Playback of injected input is ignored."
                : "Recording the desktop. Buttons, keys, and the wheel are kept. Pointer movement is off, so playback clicks at the cursor. Clicks on this window are left out when you stop.");
        }
        catch (Exception error)
        {
            Append(error.Message);
            _capture.Dispose();
            _capture = null;
        }
    }

    public void StopRecording(int windowLeft, int windowTop, int windowWidth, int windowHeight)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_capture is null || _capture.Session.State != RecordingState.Recording)
        {
            Append("There is no recording to stop.");
            return;
        }

        _capture.Stop();
        if (windowWidth > 0 && windowHeight > 0)
        {
            _capture.OmitInside(windowLeft, windowTop, windowLeft + windowWidth, windowTop + windowHeight);
        }

        string name = "Recording " + DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss", CultureInfo.InvariantCulture);
        string id = "r" + Guid.NewGuid().ToString("N");
        RecordedDocument recorded = _capture.ToDocument(name, id);
        foreach (string notice in recorded.Notices)
        {
            Append(notice);
        }

        if (!recorded.IsReady || recorded.Document is null)
        {
            _recordingJson = null;
            Append("The recording could not be turned into a macro.");
            return;
        }

        _recordingJson = MacroJson.Write(recorded.Document);
        Append($"Recording stopped. {_capture.Session.Count} samples became {recorded.Document.Actions.Count} actions. Kept in memory for Play recording.");
    }

    public void PlayRecording()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsRecording)
        {
            Append("Stop recording before playback. Playback sends real input.");
            return;
        }

        if (_recordingJson is null)
        {
            Append("Record something first.");
            return;
        }

        Begin(_recordingJson, "Recording");
    }

    /// <summary>Runs a saved macro on the worker. This sends real input when the worker is the Windows one.</summary>
    public void PlayMacro(string name, string json)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(json);
        if (IsRecording)
        {
            Append("Stop recording before playback. Playback sends real input.");
            return;
        }

        Begin(json, name);
    }

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

    /// <summary>
    /// Asks the engine to release inputs it still owns. A running tryout is stopped, which is the same release path.
    /// </summary>
    public void ReleaseHeldInputs()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        WorkerState state = _worker.State;
        if (state is WorkerState.Running or WorkerState.Paused or WorkerState.Stopping)
        {
            Stop();
            return;
        }

        _worker.ReleaseOwnedInputs();
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
                if (item.Kind == WorkerEventKind.MacroActionStarted && item.ActionId is not null)
                {
                    CurrentStep = item.ActionId;
                }
                else if (item.Kind == WorkerEventKind.MacroFailed && item.ActionId is not null)
                {
                    CurrentStep = item.ActionId;
                }
                else if (item.Kind == WorkerEventKind.MacroFinished)
                {
                    CurrentStep = "Finished";
                }

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

            UpdateClock();

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
        _capture?.Dispose();
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

    private bool Begin(string json, string? name = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _pause.Resume();
        if (_worker.State is WorkerState.Running or WorkerState.Paused or WorkerState.Stopping)
        {
            Append("A run is already going. Press Stop first.");
            return false;
        }

        try
        {
            bool started = MacroSession.TryStart(
                _worker,
                json,
                () => _screen,
                result => Append(result.Status.ToString()),
                out PreparedMacro prepared,
                _pause);
            if (!started)
            {
                Append(prepared.Issues.Count == 0 ? "The document was not accepted." : prepared.Issues[0].Message);
                return false;
            }

            RunningName = name ?? "Tryout";
            CurrentStep = "Starting";
            _runClock = Stopwatch.StartNew();
            UpdateClock();
            return true;
        }
        catch (Exception error)
        {
            Append(error.Message);
            return false;
        }
    }

    private void UpdateClock()
    {
        if (_runClock is null)
        {
            ElapsedText = "0:00";
            return;
        }

        if (_worker.State is not (WorkerState.Running or WorkerState.Paused or WorkerState.Stopping))
        {
            _runClock.Stop();
        }

        TimeSpan elapsed = _runClock.Elapsed;
        ElapsedText = elapsed.TotalHours >= 1
            ? $"{(int)elapsed.TotalHours}:{elapsed.Minutes:00}:{elapsed.Seconds:00}"
            : $"{elapsed.Minutes}:{elapsed.Seconds:00}";
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
