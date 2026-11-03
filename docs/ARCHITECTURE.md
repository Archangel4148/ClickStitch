# Architecture — ClickStitch

**Status:** Approved with Phase 0. Phase 1 has not started.  
**This document does not authorize implementing Phase 1 until that phase is explicitly started.**

The foundation solution builds a WPF shell and a UI-free core library. Input, execution, recording, plugins, and the editor are specified here and are not implemented.

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
| `ClickStitch.App` | WPF shell. Opens one window. References Core. |
| `ClickStitch.Core` | Shared contracts with no UI and no native calls. References no ClickStitch project. |
| `ClickStitch.Core.Tests` | xUnit tests for Core. |

Projects to add in later phases, and not created now:

| Project | Phase | May reference |
|---|---|---|
| `ClickStitch.Input` | 1 | Nothing in the UI, execution, or plugin host. Native `SendInput` stays inside this project. |
| `ClickStitch.Execution` | 2 | Core and Input. |
| `ClickStitch.Recording` | 3 | Core and Input. |
| `ClickStitch.Plugins.Sdk` | 4 | Core. |
| `ClickStitch.Plugins.Host` | 4 | Core and the SDK. |
| `ClickStitch.Input.Tests`, `ClickStitch.Execution.Tests`, `ClickStitch.Recording.Tests`, `ClickStitch.Plugins.Host.Tests` | Same phase as the project they test | The project under test. |
| `ClickStitch.Performance.Tests` | 1 | Input. Opt-in. Excluded from the default test run. |

```text
ClickStitch.App
  └── ClickStitch.Core

Later:

ClickStitch.App
  ├── ClickStitch.Core
  ├── ClickStitch.Execution ── ClickStitch.Input
  ├── ClickStitch.Recording ── ClickStitch.Input
  └── ClickStitch.Plugins.Host
        ├── ClickStitch.Plugins.Sdk ── ClickStitch.Core
        └── ClickStitch.Core
```

Core stays at the bottom. The App is the only project that references WPF. A later UI test project would be the only other project allowed to reference the App. Cycles are not allowed. The current solution has none.

## 5. Macro document model

A macro is a versioned JSON document. It describes actions. It does not contain threads, delegates, UI objects, native handles, plugin instances, or live execution state. Encoding is UTF-8. One macro is one file with the extension `.clickstitch.json`. A reader rejects a document larger than 2 MiB.

`ClickStitch.Core.MacroFormat.CurrentVersion` is `1`. `MacroFormat.IsSupported` returns true only for that version. Parsing and validation are not implemented yet.

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

Unknown object members are preserved when a later phase round-trips a document, and execution ignores them. Unknown action types are errors. A missing required field is an error. Duplicate `id` values, variable names, or plugin ids are errors.

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

`x` and `y` are integers from -32768 to 32767. A value inside that range can still fall outside the current virtual screen. That is an execution failure in a later phase, reported to the user, and it releases owned inputs.

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

**`repeat.count`.** `count` is an integer from 1 to 1000000. `actions` is a nested action array and may be empty. Nesting deeper than 8 `repeat.count` levels is invalid. A large count remains cancellable when execution exists. The count limit is not a substitute for cancellation.

**`comment`.** `text` is a string from 0 to 2000 characters.

These type names are reserved and are invalid in version 1: `repeat.until`, `branch.if`. Plugin action names are also invalid in version 1.

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

Phase 1 puts `SendInput` and its structures in `ClickStitch.Input`. Those structures stay internal to that project. Callers use managed operations for move, button, wheel, and key. Core never declares a native structure. The execution worker is the only caller that submits input while a macro is running.

## 7. Input ownership, cancellation, emergency stop, and shutdown

The input state manager, built in Phase 1, is the single record of buttons and keys the engine has successfully pressed and not yet released.

- A successful down adds that key or button to the owned set. A second down for an already owned item is refused and reported.
- A successful up removes it. An up for an item the engine does not own is not sent.
- `ReleaseAllOwnedInputs` submits an up for each owned item and removes an item only after Windows accepts that up. Calling it again submits nothing.
- `SendInput` can accept a prefix of a batch. Ownership follows the accepted prefix, and the shortfall is an error.
- Idle, stopped, and shut-down states own nothing. A failed release leaves the remaining items in the set, sets the state to failed, and reports the failure. The set is not cleared by pretending the release worked.
- Physical keys the user is holding are not owned.
- After a forced process exit or a power loss, no cleanup runs. Startup does not try to invent which keys were held. That residual risk stays in the user-facing documentation.

Pause keeps held buttons and keys held. Resume does not send another down. Stop and emergency stop release them.

Emergency stop, built in Phase 1:

- Proposed default binding: Ctrl+Alt+Shift+F12. The user can change it.
- Registered with `RegisterHotKey` so it works while the window is unfocused. A low-level hook is not the default.
- It wins over start, pause, and ordinary stop.
- The worker stops taking new actions, the execution token is cancelled, and owned inputs are released. That path does not call plugin code.
- If the binding is already taken, the app still starts, explains the conflict, and offers another binding. The in-window stop control still works.
- First-run explains the binding. That screen is a later phase.

Shutdown order:

1. Refuse new work.
2. Cancel the active macro.
3. Release owned inputs.
4. Unregister hotkeys and stop timers. Remove hooks when recording exists.
5. Shut plugins down in reverse load order, with a bounded wait. A plugin that does not return is logged, and the process still exits.
6. Flush logs with a bounded wait of 2 seconds.
7. Exit.

The worker has an explicit start and stop. Stop is safe to call more than once. Exceptions at the worker boundary are logged, owned inputs are released, and the state becomes failed. They are not discarded.

## 8. Scheduler and threading

One execution worker owns scheduling and input submission. The WPF dispatcher owns the UI. Disk, plugin discovery, plugin calls, and log sinks run on other threads.

The worker schedules from `Stopwatch.GetTimestamp()`. It does not read the wall clock for delays. The lock that guards the owned-input set is not held during a wait. Progress events travel on a bounded queue. The worker does not block on the dispatcher. If the queue is full, older progress events are dropped and a drop count is kept.

Execution states, for later UI and engine code: Idle, Running, Paused, Stopping, Stopped, Failed.

Pause semantics:

- The next action is not started.
- A wait keeps its remaining time and continues on resume.
- Holds stay down across the pause.
- Stop during a wait ends the wait.
- Stop during a repeat exits before the next child action and before the next iteration.
- Move, wheel, key, hold, and click are single submissions. Stop applies before the action or after it has been submitted. Click includes its matching up, as section 5.3 says.

## 9. Recording

Phase 3 captures physical input with low-level mouse and keyboard hooks. The hook callback enqueues a small record and returns. Timestamps use the same monotonic clock as the scheduler. Sampling, simplification, and document conversion happen off the hook.

The default recording option ignores events Windows marks as injected, so playback is less likely to record itself. That flag is a filter, not a guarantee, and the user-facing text should say so. A recording stops and tells the user after 100,000 events. The raw event list lives in memory until the user saves. The saved file is the macro document, so simplification can be undone before save and is not a second file format in this design.

## 10. Timing measurement

Definitions:

- **Clock.** `Stopwatch.GetTimestamp()`, monotonic.
- **Scheduling error.** The timestamp when the worker is about to submit, minus the requested timestamp.
- **Submission duration.** The timestamp after `SendInput` returns, minus the timestamp just before the call.
- **Delivery.** When the target application handles the input. `SendInput` does not report this, and ClickStitch does not claim it.

Phase 1 adds an opt-in benchmark. It is not part of `dotnet test`. The baseline workload submits pointer moves at 10 ms and at 50 ms, at least 1000 samples each after a short warmup. The report lists P50, P95, P99, maximum scheduling error, submission duration, CPU, and working set, plus machine, Windows build, runtime, configuration, and anything else that would affect the machine. Thread priority for the baseline is normal. `timeBeginPeriod(1)` stays off in the baseline. A later paired run may turn it on and must turn it back off. There is no numeric target until a baseline exists. A result describes that run on that machine.

No timing measurement was taken in Phase 0.

## 11. Logging, configuration, persistence, and errors

No logging package is referenced in this phase. The proposed logger is `Microsoft.Extensions.Logging`, added in the phase that first needs it. The execution worker enqueues log records onto a bounded channel of 1000. When the channel is full, the oldest record is dropped, a drop count increases, and the worker does not wait. Sinks write elsewhere. Shutdown flushes for up to 2 seconds.

Configuration will live in `%LOCALAPPDATA%\ClickStitch\settings.json`. Macros will live in `%LOCALAPPDATA%\ClickStitch\Macros\`, one `.clickstitch.json` file each. The macro `id` is the identity. The file name is a slug plus a short suffix so two names can coexist. A corrupt settings file is renamed aside and replaced with defaults, and the app still opens and explains that. This build does not create those files.

Errors are reported at the subsystem boundary with a plain-language message and a technical detail the user can open. Empty catch blocks are not used.

## 12. Blockly and WebView2

The visual editor arrives in Phase 5, after a list editor works. Blockly runs inside WebView2. The editor is a view over the same macro document. The bridge carries JSON documents: load a document, return a document, and report editor errors. It has no operation that submits input or starts a run. The host validates the returned document with the same rules as every other authoring path.

Windows 11 includes the WebView2 runtime. The distribution plan is to use that inbox runtime. If it is missing, the visual editor is unavailable and the list editor still works. Phase 6 confirms that on a clean machine. This phase does not reference the WebView2 package.

## 13. Tests and packaging

Test layout and the manual-test format are in `docs/TESTING.md`. The default `dotnet test` command does not inject input.

Phase 6 publishes a self-contained single-file `win-x64` executable. Plugins remain files in a `Plugins` directory beside the executable. The app runs when that directory is missing. The product is not described as one file if a plugin or the WebView2 runtime is required. Phase 0 builds a framework-dependent app and uses the installed .NET 10 desktop runtime.

## 14. What this phase implements

The solution, the WPF shell, `MacroFormat`, and the tests in `ClickStitch.Core.Tests`. The shell shows the product name, the Windows 11 support line, and the format version from `MacroFormat.CurrentVersion`. It does not create, record, or run a macro.
