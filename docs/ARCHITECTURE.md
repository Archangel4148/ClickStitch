# Architecture — ClickStitch

**Status:** Approved with Phase 0. Phase 1 has not started.  
**This document does not authorize implementing Phase 1 until that phase is explicitly started.**

The foundation solution builds a WPF shell, a UI-free core library, the input engine, a plugin host, and a sample plugin. The shell loads a plugin only after the trust warning is accepted and that plugin is enabled. The list editor edits a format-1 document and does not run it. The visual editor is specified here and is not implemented.

## 1. Decision record

| Decision | Choice | Status |
|---|---|---|
| Supported operating system | Windows 11, 64-bit. Minimum build 22000 (Windows 11 21H2). | Approved |
| Unsupported systems | Windows 10. 32-bit Windows. Windows 11 on ARM64. | Approved |
| Runtime | C# on .NET 10 (`net10.0` and `net10.0-windows10.0.22000.0`), SDK 10.0.403 | Approved |
| UI | WPF | Approved. See section 3. |
| Tests | xUnit | Approved |
| Visual editor | Blockly in WebView2, after a list editor exists | Approved. See section 12. |
| Macro format | Version 1, JSON, as defined in section 5 | Approved |
| Distribution shape | Portable self-contained `win-x64` publish in Phase 6. Plugins stay outside the executable. | Approved as the plan. Packaging is Phase 6. |

## 2. Windows and .NET

ClickStitch supports Windows 11, 64-bit. The app project targets `net10.0-windows10.0.22000.0`, so the declared minimum operating system is Windows 11 build 22000. The manifest asks for per-monitor DPI v2. The Windows compatibility GUID in that manifest is the shared Windows 10 and Windows 11 identifier; it does not block Windows 10. Support is a product promise, and this build does not add an operating-system version check.

The development machine used for this phase was Windows 11 Home, version 25H2, build 26200, x64. Older Windows 11 feature updates were not tested.

The runtime is .NET 10, the current long-term support release. The human installed SDK 10.0.403, and the foundation was retargeted from .NET 8 to that SDK. `global.json` requests 10.0.403 and allows a newer 10.0 SDK. .NET 8 remains installed and is not used by this solution.

The project uses Windows platform version 10.0.22000 so the API surface matches the minimum supported Windows 11 build. The target framework brings in the reference pack `Microsoft.Windows.SDK.NET.Ref` 10.0.22000.57. Application code does not call native Windows APIs in this phase.

## 3. UI framework

WPF is the proposed UI. A WinUI 3 prototype was not built. WinUI 3 would add the Windows App SDK, which is a new major dependency, and its supported self-contained layout is a folder rather than the single-file executable Phase 6 asks for. WPF ships in the installed desktop workload, publishes as a self-contained executable, hosts WebView2 when the editor arrives, and is the specification's preferred starting point. The window uses the design tokens in `docs/UX_GUIDELINES.md` so the Windows 11 look comes from ClickStitch's own design system.

## 4. Solution layout and dependencies

Projects that exist now:

| Project | Responsibility |
|---|---|
| `ClickStitch.App` | WPF shell with its own title bar. Click is the first page. Macros, Record, Plugins, and Settings are tabs. References Core, Input for Click, Record, and Run, and the plugin host. Startup loads enabled plugins and still opens when a plugin fails. The Plugins tab lists them and does not load one during the session. The editor does not reference the input engine. |
| `ClickStitch.Core` | Shared contracts with no UI and no native calls. References no ClickStitch project. |
| `ClickStitch.Input` | `SendInput` interop, ownership, and the monotonic wait. References no ClickStitch project. |
| `ClickStitch.Core.Tests` | xUnit tests for Core. |
| `ClickStitch.Input.Tests` | Layout, submission, coordinates, gestures, ownership, and timing. They do not inject input. |
| `ClickStitch.Performance.Tests` | Opt-in desktop benchmark. Excluded from the default test run. |
| `ClickStitch.Bench` | Console runner for that benchmark. `make bench`. Prints progress. Not a test project. |
| `ClickStitch.Plugins.Sdk` | Plugin contracts, metadata, and `clickstitch-plugin.json` validation. References Core. |
| `ClickStitch.Plugins.Host` | Discovers plugin folders and loads a trusted plugin with `Assembly.LoadFrom`. That load is not a sandbox. References Core and the SDK. |
| `ClickStitch.Plugins.Sdk.Tests` | xUnit tests for the manifest reader. They use JSON text only. |
| `ClickStitch.Plugins.Host.Tests` | Discovery, API checks, load, shutdown, and action registration. They load the probe assembly. |
| `ClickStitch.Plugins.Probe` | The plugin those host tests load. It is not the product sample. |
| `ClickStitch.SamplePlugin` | Sample plugin. References only the SDK. The application does not reference it. |

```text
ClickStitch.App
  ├── ClickStitch.Core
  ├── ClickStitch.Input
  └── ClickStitch.Plugins.Host
        ├── ClickStitch.Plugins.Sdk
        └── ClickStitch.Core
```

Core stays at the bottom. The App is the only project that references WPF. A later UI test project would be the only other project allowed to reference the App. Cycles are not allowed. The current solution has none.

## 5. Macro document model

A macro is a versioned JSON document. It describes actions. It does not contain threads, delegates, UI objects, native handles, plugin instances, or live execution state. Encoding is UTF-8. One macro is one file with the extension `.clickstitch.json`. A reader rejects a document larger than 2 MiB.

`ClickStitch.Core.MacroFormat.CurrentVersion` is `1`. `MacroFormat.IsSupported` returns true only for that version. `MacroJson` reads and writes that document in Core. A document larger than 2 MiB is rejected. Unknown object members are kept and written back. Unknown action types are errors. Execution of the actions is a later milestone.

### 5.1 Top-level fields

| Field | Required | Rule |
|---|---|---|
| `formatVersion` | yes | Integer `1`. No default. Any other value, or a string, rejects the document. |
| `id` | yes | Stable identity, 1–64 characters, `[A-Za-z0-9._-]`. |
| `name` | yes | Display name, trimmed, 1–120 characters. |
| `description` | no | String, at most 2000 characters. Default empty. |
| `metadata` | no | Authoring information. Execution ignores it. |
| `metadata.createdUtc`, `metadata.modifiedUtc` | no | ISO-8601 timestamps in UTC. |
| `coordinateSpace` | no | Object. Default system is `virtualDesktopPixels`. |
| `settings` | no | Persistent settings that are safe to keep. |
| `settings.stopOnError` | no | Boolean. Default `true`. |
| `variables` | no | Array. Default empty. Shape is checked. No v1 action reads a variable. |
| `pluginDependencies` | no | Array. Default empty. A non-empty array is invalid in format 1. |
| `actions` | yes | Array. It may be empty. |

Unknown object members are preserved when a document is written back, and execution ignores them. Unknown action types are errors. A missing required field is an error. Duplicate action ids or variable names are errors. A non-empty `pluginDependencies` array is invalid in format 1.

### 5.2 Coordinates

`coordinateSpace.system` in version 1 is `virtualDesktopPixels`.

The origin is the top-left of the Windows virtual desktop. That origin can be negative when a monitor sits left of or above the primary display. Units are physical pixels at authoring time. The document does not store a monitor layout. Playback on a different layout is best-effort, and the product must not promise that a point still lands on the same object.

Per-action `coordinateMode`:

| Mode | Meaning |
|---|---|
| `virtualDesktop` | `x` and `y` are pixels from the virtual desktop origin. Default when a point is required. |
| `primaryDisplay` | `x` and `y` are pixels from the primary display's top-left. Allowed so a document can say what it means. Recording should write `virtualDesktop`. |
| `relative` | `x` and `y` are deltas from the current cursor. |
| `cursor` | The current cursor. `x` and `y` are omitted. Valid for `mouse.click` and `mouse.hold`. Invalid for `mouse.move`. |

`x` and `y` are integers from -32768 to 32767. A value inside that range can still fall outside the current virtual screen. That action fails and nothing is sent for it. When `stopOnError` is true, later actions do not start. Releasing inputs that are already held is still the execution worker's stop path. The macro executor does not start that worker.

### 5.3 Actions

Every action has:

| Field | Required | Rule |
|---|---|---|
| `id` | yes | Unique in the document. Same character rules as the macro id. |
| `type` | yes | One of the version 1 types below. |
| `comment` | no | String, at most 500 characters. Not executed. |

**`mouse.click`.** `button` is `left`, `right`, or `middle`. `coordinateMode` defaults to `virtualDesktop`. `clickCount` defaults to 1 and is 1–3. The action submits button-down and button-up before it returns. A stop request is observed before the click or after it, so a click does not leave a button down.

**`mouse.move`.** `x` and `y` are required. `coordinateMode` defaults to `virtualDesktop` and cannot be `cursor`.

**`mouse.hold`.** `button` and `state` (`down` or `up`) are required. Coordinates are optional. When they are present, the pointer moves and then the button changes. When they are absent, the button changes at the current cursor.

**`mouse.wheel`.** `notches` is a non-zero integer from -100 to 100. Positive means away from the user. Execution will scale one notch to the Windows wheel delta of 120.

**`keyboard.key`.** `key` and `state` (`down` or `up`) are required. Modifiers are their own actions, in order, so a chord cannot hide a held key from the ownership tracker. Version 1 keys are `A`–`Z`, `Digit0`–`Digit9`, `F1`–`F24`, `Shift`, `Control`, `Alt`, `LeftWindows`, `RightWindows`, `Space`, `Enter`, `Tab`, `Escape`, `Backspace`, `Delete`, `Insert`, `Home`, `End`, `PageUp`, `PageDown`, `Left`, `Right`, `Up`, `Down`, `Comma`, `Period`, `Minus`, `Equal`, `Semicolon`, `Slash`, `Backslash`, `Apostrophe`, `LeftBracket`, and `RightBracket`. Numpad keys are outside version 1.

**`wait`.** `durationMs` is an integer from 0 to 3600000. Zero completes the wait immediately. The specification's illustrative example used the type name `delay`. The schema uses `wait`, which is the word shown in the product. That example was not the final schema.

**`repeat.count`.** `count` is an integer from 1 to 1000000. `actions` is a nested action array and may be empty. Nesting deeper than 8 `repeat.count` levels is invalid. A large count stays cancellable. The count limit is not a substitute for cancellation. The runner checks cancellation before each iteration, so an empty child list can still be stopped.

**`comment`.** `text` is a string from 0 to 2000 characters.

These type names are reserved and are invalid in version 1: `repeat.until`, `branch.if`. Plugin action names are also invalid in version 1.

### 5.7 Execution contract

`MacroActionCatalog` names the eight built-in types. `MacroExecution.Compile` turns a validated document into a `MacroRunContract`. That contract is data for a later runner. Compiling it does not submit input.

- A click, move, hold, wheel notch, and key are each one submission. Stop is observed before that submission or after it. A click's button-down and button-up stay in the one submission. A hold that includes a point moves and changes the button in that same submission.
- A wait can be stopped while it is running. Pause keeps the remaining time. Held buttons and keys stay down.
- `repeat.count` keeps its count and its child list. The contract does not copy the children once per iteration. Stop is observed before the next child and before the next iteration.
- A comment submits nothing.
- Unknown members and variables are not copied into the contract. Format 1 does not evaluate variables.
- `stopOnError` defaults to true. A submission Windows does not fully accept ends the run when the flag is true.
- One wheel notch is 120 Windows wheel units. The contract stores the notch count.

`MacroRunner` walks the contract on the calling thread. It calls an `IMacroExecutor` for a click, move, hold, wheel, key, or wait. It does not submit input itself. A comment is marked complete and is not dispatched. `repeat.count` runs that same child list once per count. The children are not copied. The repeat's own id is completed after the last iteration. Cancellation is checked before each child and before each iteration, including a repeat whose child list is empty. A failure follows `stopOnError` for the rest of the iteration, later iterations, and actions after the repeat.

`MacroPause` holds the next action. A click, move, hold, wheel, or key that has already started still finishes as one submission. A wait that is paused keeps the remaining time and continues that remainder on resume. Held buttons and keys stay down, and resume does not press them again. Cancelling while the run is paused does not start the next action. This pause is not wired to a hotkey.

`MacroSession` runs that contract on the execution worker. `Stop` and emergency stop both call the worker stop. The run leaves a wait, a pause, or a repeat before the next child, and the worker then releases owned inputs. A macro that finishes or fails while a key is still down is released the same way. A release Windows does not fully accept can still leave that input held.

`MacroRuns.Prepare` is the gate in front of a run. An invalid document has no contract, and `MacroSession.TryStart` does not start the worker. Progress is a `MacroProgressEvent` for action start, action completion, pause, resume, action failure, and the finish. The session copies those onto the worker's bounded queue. A repeat is one start and one completion, not one record per iteration. `MacroDiagnostic` snapshots the result, the worker state, owned inputs, and that queue. Reading it drains the queue. There is still no log file.

`MacroInputExecutor` is that executor. It submits through `InputStateManager`, so a hold or a key down is owned and a second down is refused. A click's down and up are one batch. A point on the same action is in that same batch. `virtualDesktop` and `primaryDisplay` both use an absolute virtual-desktop move, because the primary display origin is (0, 0) on that desktop. The executor does not clip a primary-display point to the primary monitor. `relative` is a pointer delta and does not read the screen. A wait of zero returns immediately. Any other wait uses `DeadlineWait`. If that wait is cancelled, the action does not complete. A short `SendInput` acceptance fails the action. The executor does not start the worker and does not release held inputs.

### 5.4 Variables

Each variable has `name`, `type`, and `default`. The name matches `[A-Za-z][A-Za-z0-9_]{0,31}`. The type is `number`, `text`, or `boolean`, and the default matches the type. A number must be finite. Text is at most 2000 characters. Version 1 stores variables and does not evaluate expressions.

### 5.5 Compatibility

- `formatVersion` changes when a new reader would have to reinterpret an old document, when a required field changes, or when an action type is added.
- An optional field with a default can be added without a version change. Older readers ignore unknown members.
- A reader of version 1 rejects every other `formatVersion` instead of guessing.
- Plugin API versions are separate strings. They are not `formatVersion`.
- A migration needs tests in the same phase that introduces it. There is no migration in this phase.

### 5.6 Example

```json
{
  "formatVersion": 1,
  "id": "sample-click-sequence",
  "name": "Sample Click Sequence",
  "description": "Clicks two points, with a short wait between them.",
  "coordinateSpace": { "system": "virtualDesktopPixels" },
  "settings": { "stopOnError": true },
  "variables": [],
  "pluginDependencies": [],
  "actions": [
    {
      "id": "click-start",
      "type": "mouse.click",
      "button": "left",
      "coordinateMode": "virtualDesktop",
      "x": 500,
      "y": 300
    },
    {
      "id": "pause-briefly",
      "type": "wait",
      "durationMs": 100
    },
    {
      "id": "click-next",
      "type": "mouse.click",
      "button": "right",
      "coordinateMode": "cursor"
    }
  ]
}
```

## 6. Input interop boundary

`ClickStitch.Input` holds `SendInput`, its structures, and the managed gestures that build those packets. The structures stay internal. The app does not reference this project. Core never declares a native structure. `ExecutionWorker` is the caller that submits input while a run is in progress. The tryout panel is the one caller in the shell. Core parses a document and does not submit input. `MacroSession` is the piece that runs a prepared document on the worker. `DesktopTryout` reads the virtual desktop with `GetSystemMetrics` and passes that rectangle in. The gesture code still does not read the screen itself.

Milestone 1.2 gestures are move, button down, button up, click, wheel, key down, and key up. A click is one batch of button-down and button-up, repeated for a count from 1 through 3. A click or button change at a point puts the absolute move in that same batch, ahead of the button. A point outside the virtual screen sends nothing. Modifiers are keys: Shift, Control, Alt, and the two Windows keys are pressed and released on their own, so a chord cannot hide a held key. Arrow keys, Insert, Delete, Home, End, Page Up, Page Down, and both Windows keys set the extended-key flag. The gesture builders do not remember which inputs are held. A batch that Windows only partly accepts is reported and is not retried. Presses that must be tracked go through the input state manager.

On x64 the native packet is 40 bytes. `type` is at offset 0, and the mouse, keyboard, and hardware union starts at byte 8. Mouse `dwExtraInfo` is at byte 24 of the mouse struct. Keyboard `dwExtraInfo` is at byte 16 of the keyboard struct. Layout tests lock these sizes. Hardware packets exist so the union matches Win32, and submission rejects them.

`SendInput` reports how many events Windows accepted. A short count is a failure. The Win32 error is read immediately after the call and kept only when the count is short. A successful call can leave a stale error code, and that code is not reported as a failure. An empty batch does not call `SendInput`.

Absolute positions are physical pixels of the virtual desktop, not WPF device-independent units. `TryToAbsolute` maps a pixel inside that screen to the 0–65535 range used with `MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK`:

`((pixel - origin) * 65535) / (extent - 1)`

The division uses 64-bit integers and truncates toward zero. A pixel outside the screen is rejected. The screen rectangle comes from `GetSystemMetrics`: `SM_XVIRTUALSCREEN` (76), `SM_YVIRTUALSCREEN` (77), `SM_CXVIRTUALSCREEN` (78), and `SM_CYVIRTUALSCREEN` (79). The primitives take a `VirtualScreen` from the caller and do not call `GetSystemMetrics` yet. Relative deltas stay in the schema range -32768 through 32767. A relative `SendInput` move is pointer motion, and Windows may apply acceleration, so a relative delta is not a promise of an exact pixel. Wheel notches are multiplied by 120. Positive notches scroll away from the user.

## 7. Input ownership, cancellation, emergency stop, and shutdown

`InputStateManager` is the single record of buttons and keys this process has successfully pressed and not yet released. A lock covers the ownership check and the `SendInput` call. It is not held across a wait.

- A successful down adds that key or button. A second down of an owned input is refused and nothing is sent, so two callers cannot both believe they hold it.
- A successful up removes it. An up for an input the engine does not own is not sent.
- `ReleaseAllOwnedInputs` submits an up for each owned input, mouse buttons first and then keys in schema order. An input leaves the set only after Windows accepts that up. Calling it again when the set is empty submits nothing.
- Ownership follows the accepted prefix of a batch. A click that Windows accepts only through the down leaves the button held. The batch is not retried. A failed release leaves the remaining inputs in the set and reports the shortfall.
- A fully accepted click ends with that button not held, because the batch contains the matching up.
- Move and wheel do not change the set. A point outside the screen sends nothing.
- Physical keys the user is holding are not owned. Startup does not guess about keys left down by a previous process. After a forced process exit or a power loss, no cleanup runs.
- Pause leaves holds down and does not send another down on resume. Stop ends the current wait and calls `ReleaseAllOwnedInputs`. A release Windows does not fully accept leaves the worker failed and the remaining inputs held. Emergency stop uses that same stop path and does not call plugin code.

Global hotkeys, milestone 1.6:

- `RegisterHotKey` with a null window handle, on a background thread named `ClickStitch.Hotkeys` that pumps `WM_HOTKEY`. Stopping the listener posts `WM_QUIT`. There is no low-level keyboard hook.
- Four roles: start, pause/resume as one toggle, stop, and emergency stop. Each registration adds `MOD_NOREPEAT`. A chord needs at least one modifier, and the main key cannot itself be a modifier.
- Proposed defaults, not yet chosen by the user: Ctrl+Alt+Shift+F9 starts, F10 pauses or resumes, F11 stops, and F12 is the emergency stop already named below. Two roles cannot share a chord.
- Emergency dispatch calls only the emergency method, which is `ExecutionWorker.Stop`. It does not call start, pause, or a plugin.
- A chord Windows rejects is recorded, including error 1409 when another program owns it, and the other roles are still registered. Replacing a role unregisters that id and registers the new chord on the hotkey thread. If the new call fails, the role stays unregistered. If the old registration cannot be removed, the new chord is not registered. The shell does not show this yet, and it does not offer another binding. That screen, and the first-run explanation, are a later phase. The in-window stop control is also later. `ClickStitch.exe` does not register hotkeys. The tryout uses on-screen Pause, Resume, and Stop.

Emergency stop, built in Phase 1:

- Proposed default binding: Ctrl+Alt+Shift+F12. The user can change it.
- Registered with `RegisterHotKey` so it works while the window is unfocused. A low-level hook is not the default.
- It wins over start, pause, and ordinary stop.
- The worker stops taking new actions, the execution token is cancelled, and owned inputs are released. That path does not call plugin code.
- If the binding is already taken, registration of the other roles continues and the failed role can be replaced. The app still starts. Explaining the conflict and offering another binding in the window is a later phase. The in-window stop control is also later.
- First-run explains the binding. That screen is a later phase.

Shutdown order, performed by `InputRuntime` for the pieces that exist:

1. Refuse new work. A later start of the worker or the hotkey listener throws.
2. Cancel the active run.
3. Release owned inputs. A release Windows does not fully accept stays on the shutdown report, and the worker stays failed.
4. Unregister hotkeys and dispose the wait timer. A hotkey that cannot be unregistered stays on the report. Recording hooks do not exist yet.
5. On exit the shell calls `PluginHost.Shutdown`. That calls `Shutdown` on plugins that finished `Initialize`, in reverse order. One throw is recorded and the rest still run. A plugin that failed to initialize is not called. This input runtime does not call plugin code while it releases keys.
6. There is no log sink. The report keeps the worker's progress queue, including a dropped-event count. A two-second flush arrives with the sink.
7. The process is not exited. The shell is not connected to this runtime, so shutdown returns the report to the caller.

A hotkey target that throws is stored on the listener and copied onto the report. The listener still unregisters, and shutdown still releases owned inputs. The exception is not discarded. Calling shutdown again returns the same report.

The worker has an explicit start and stop. Stop is safe to call more than once. Exceptions at the worker boundary are reported on the progress queue, owned inputs are released, and the state becomes failed. They are not discarded.

## 8. Scheduler and threading

One execution worker owns scheduling and input submission. A separate background thread owns global hotkey registration and delivery. While a recording is running, a third background thread named `ClickStitch.Recording` owns the low-level hooks. The WPF dispatcher owns the UI. Disk, plugin discovery, plugin calls, and log sinks run on other threads. The window is not connected to these threads yet.

`ExecutionWorker` schedules from `Stopwatch.GetTimestamp()` through `DeadlineWait` on a background thread named `ClickStitch.Execution`. It does not read the wall clock for delays. The product wait is the hybrid strategy in section 10. The lock that guards the owned-input set is not held during a wait. One worker may bind an input state; a second worker is refused. Progress events travel on a bounded queue of 1000. The worker does not block on the reader or the dispatcher. If the queue is full, the oldest event is dropped and a drop count is kept. The repeat contract is in section 5.7. `MacroRunner` runs that count and does not copy the child list. `MacroSession` runs the contract on this worker, so stop and emergency stop end the macro and release owned inputs. Progress for each macro action is copied onto this worker's queue, and the UI is not called. `MacroPause` can hold that run between actions and keep the rest of a wait. It is not connected to the worker pause or to a hotkey.

Execution states: Idle, Running, Paused, Stopping, Stopped, Failed. The window does not show them yet.

Pause semantics:

- The next action is not started.
- A wait keeps its remaining time and continues on resume.
- Holds stay down across the pause.
- Stop during a wait ends the wait.
- Stop during a repeat exits before the next child action and before the next iteration.
- Move, wheel, key, hold, and click are single submissions. Stop applies before the action or after it has been submitted. Click includes its matching up, as section 5.3 says.

## 9. Recording

`RecordingSession` is the life of one recording: idle, recording, stopped, or cancelled. Stop keeps the count. Cancel drops it. The 100,000th event is kept, and the session then stops with a notice.

`RecordingCapture` holds the events for that session. `Win32RecordingHook` installs `WH_MOUSE_LL` and `WH_KEYBOARD_LL` on `ClickStitch.Recording`, which pumps messages. The callback copies one sample, accepts it, and enqueues that record. It calls the next hook and returns. Unhooking and document work stay off the callback. `Collect` copies the queue into the session. When the session reaches the limit, that collector uninstalls the hook.

Buttons and schema keys are accepted. The wheel is accepted by default. Movement is off unless the recording options turn it on. A move stores the virtual-screen point from the hook. A second sample at that same point is left out. A wheel sample stores the signed Windows delta, and one notch is 120 units. A partial notch is kept with the raw delta. The horizontal wheel, X buttons, and virtual keys outside the schema are left out. A second down of a button or key that is already down is left out, so auto-repeat does not fill the list.

The default option ignores events Windows marks as injected, including moves and the wheel, so playback is less likely to record itself. That flag is a filter, not a guarantee, and the user-facing text should say so. Each record stores `Stopwatch.GetTimestamp()` from the callback. `RecordingTime` turns two of those timestamps into elapsed milliseconds. The recording also stores the timestamp from the moment it started.

After the recording has stopped, `Simplify` builds a shorter copy. A move closer in time than the sample interval, or closer in pixels than the minimum distance, is left out of that copy. The last move before a button, key, or wheel is kept. A tolerance then drops a move that sits within that many pixels of the straight line between the moves kept on either side. Buttons, keys, and the wheel stay in place and split the path. Zero for any of those settings skips that step. The raw list is unchanged, and `Revert` discards the copy. The session count stays the raw count.

`ToDocument` writes a format-1 macro from the shortened copy when one exists, and otherwise from the raw list. A move becomes `mouse.move` in virtual-desktop pixels. A button down immediately followed by the same button's up becomes one `mouse.click` at the cursor, so the move before it is what places the pointer. The time from that press to its release stays in the wait before the next action. Left and right Shift, Control, and Alt are stored as those schema keys. Anything between that down and up becomes `mouse.hold`. Wheel units add up until they make a notch of 120, and a notch count past 100 is split. Keys become `keyboard.key`. Shift, Control, Alt, and the Windows keys can be left out. Gaps between samples become `wait` actions, from the monotonic clock, split at one hour. A release with no recorded press is left out. The raw list stays in memory, so the copy is not a second file format.

`MacroStore` writes that document as `.clickstitch.json`. The file name is a slug of the name, a hyphen, and the macro id. Saving the same id again removes the older file in that folder. The default folder is `%USERPROFILE%\Documents\ClickStitch`. The tryout window can record and play one take. It keeps that take in memory and does not write a file. Clicks and movement on the tryout window itself are left out when recording stops. The default tests drive a scripted hook and do not call `SetWindowsHookEx`. Replay goes through the existing runner.

## 10. Timing measurement

Definitions:

- **Clock.** `Stopwatch.GetTimestamp()`, monotonic.
- **Scheduling error.** The timestamp when the worker is about to submit, minus the requested timestamp.
- **Submission duration.** The timestamp after `SendInput` returns, minus the timestamp just before the call.
- **Delivery.** When the target application handles the input. `SendInput` does not report this, and ClickStitch does not claim it.

`DeadlineWait` does not return before the deadline unless the caller cancelled. One instance is used by one thread. It never calls `timeBeginPeriod` and never changes thread priority.

Three waits were measured:

- **Coarse sleep.** A millisecond wait on a cancellation event. This is the simple scheduler.
- **High-resolution timer.** `CreateWaitableTimerEx` with `CREATE_WAITABLE_TIMER_HIGH_RESOLUTION`.
- **Hybrid.** That timer until the last 1.5 ms, then a tight timestamp loop that does not yield. A requested wait of 1.5 ms or less is entirely a spin.

The product wait is hybrid. On the baseline machine a coarse sleep cannot fire faster than about 15.6 ms. The high-resolution timer wakes about 0.3–0.5 ms late. Hybrid median error is under 0.001 ms at 1, 10, and 50 ms. At 1 ms it uses about one core, because the whole wait is inside the spin window. At 10 ms it uses about 0.13 of one core, and at 50 ms about 0.03. That cost is what removes the timer's late wake. `timeBeginPeriod(1)` was not turned on: the high-resolution timer already beats the 15.6 ms tick without changing the system timer.

The benchmark is `make bench`. It is not part of `dotnet test`. Each sample asks for `now + interval` and then submits a relative pointer move of 0,0. A late sample does not make the next one fire early. The report lists P50, P95, P99, and maximum scheduling error, submission duration, and spacing, plus CPU and working set. Thread priority stays normal. A result describes that run on that machine. There is no separate numeric pass/fail target; later runs are compared with this baseline only when the machine and the setup match.

No timing measurement was taken in Phase 0. The first baseline is in `docs/TESTING.md`.

## 11. Logging, configuration, persistence, and errors

No logging package is referenced. The proposed logger is `Microsoft.Extensions.Logging`, added when a sink is added. Milestone 1.7 diagnostics are the shutdown report and the worker progress queue, which holds 1000 records. When that queue is full, the oldest record is dropped, a drop count increases, and the worker does not wait. Shutdown copies that queue into the report. It does not flush a file.

Configuration will live in `%LOCALAPPDATA%\ClickStitch\settings.json`. Macros live in `%USERPROFILE%\Documents\ClickStitch\`, one `.clickstitch.json` file each. The macro `id` is the identity. The file name is a slug of the name plus the id, so two names can coexist. `MacroStore.Save` creates the folder it is given. The editor Save button calls it, and nothing is written until then. A corrupt settings file is renamed aside and replaced with defaults, and the app still opens and explains that. This build does not create the settings file.

Errors are reported at the subsystem boundary with a plain-language message and a technical detail the user can open. Empty catch blocks are not used.

## 12. Blockly and WebView2

The visual editor arrives in Phase 5, after a list editor works. Blockly runs inside WebView2. The editor is a view over the same macro document. The bridge carries JSON documents: load a document, return a document, and report editor errors. It has no operation that submits input or starts a run. The host validates the returned document with the same rules as every other authoring path.

Windows 11 includes the WebView2 runtime. The distribution plan is to use that inbox runtime. If it is missing, the visual editor is unavailable and the list editor still works. Phase 6 confirms that on a clean machine. This phase does not reference the WebView2 package.

## 13. Tests and packaging

Test layout and the manual-test format are in `docs/TESTING.md`. The default `dotnet test` command does not inject input.

Phase 6 publishes a self-contained single-file `win-x64` executable. Plugins remain files in a `Plugins` directory beside the executable. The app runs when that directory is missing. The product is not described as one file if a plugin or the WebView2 runtime is required. Phase 0 builds a framework-dependent app and uses the installed .NET 10 desktop runtime.

## 14. What this phase implements

The solution, the WPF shell, `MacroFormat`, and the tests in `ClickStitch.Core.Tests`. The shell shows the product name, the Windows 11 support line, and the format version from `MacroFormat.CurrentVersion`. It does not create, record, or run a macro.
