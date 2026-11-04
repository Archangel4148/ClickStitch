# Implementation Status — ClickStitch

**Project status:** Phase 5 in progress  
**Current authorized phase:** Phase 5 — Application UI and User Experience  
**Current authorized milestone:** Phase 5.5, plugin manager, plus a shell layout pass, awaiting review  
**Human approval to proceed:** Phases 5.3 and 5.4 were approved when milestone 5.5 was authorized. The visual editor is not started.

## Phase status

| Phase | Status |
|---|---|
| 0 — Requirements, Architecture, Project Foundation | Approved |
| 1 — Core Windows Input Engine | Approved |
| 2 — Macro Execution Engine | Approved |
| 3 — Macro Recording | Approved |
| 4 — Plugin SDK and Host | Approved |
| 5 — Application UI and UX | In progress. Milestone 5.5 is awaiting review. |
| 6 — Hardening and Distribution | Not authorized |
| 7 — Plugins | Not authorized |

## Approved architectural decisions

Approved with Phase 0. The record is `docs/ARCHITECTURE.md`.

- Windows 11, 64-bit, minimum build 22000. Windows 10, 32-bit, and ARM64 are outside the support claim.
- .NET 10, SDK 10.0.403. WPF. xUnit.
- Macro format version 1, the plugin trust model, input-ownership invariants, timing methodology, and the light-theme design tokens.
- Phase 6 packaging plan: portable self-contained `win-x64`, with plugins outside the executable.

## Proposed decisions awaiting review

None from Phase 0.

## Completed milestones

- Phase 0 — Requirements, Architecture, and Project Foundation. Approved before Phase 1.
- Phase 1.1 — Native interop. Approved when milestone 1.2 was authorized.
- Phase 1.2 — Input primitives. Approved when milestone 1.3 was authorized.
- Phase 1.3 — Input State Manager. Approved when milestone 1.4 was authorized.
- Phase 1.4 — Timing subsystem. Approved when milestone 1.5 was authorized.
- Phase 1.5 — Execution worker. Approved when milestone 1.6 was authorized.
- Phase 1.6 — Global hotkeys. Approved when milestone 1.7 was authorized.
- Phase 1.7 — Hardening. Approved when Phase 2 was authorized.
- Phase 2.1 — Macro document model and JSON serialization. Approved when milestone 2.2 was authorized.
- Phase 2.2 — Action descriptors and execution contracts. Approved when milestone 2.3 was authorized.
- Phase 2.3 — Sequential action runner. Approved when milestone 2.4 was authorized.
- Phase 2.4 — Click, move, key, hold, wheel, and wait. Approved when milestone 2.5 was authorized.
- Phase 2.5 — Repeat counts. Approved when milestone 2.6 was authorized.
- Phase 2.6 — Pause and resume. Approved when milestone 2.7 was authorized.
- Phase 2.7 — Cancellation and emergency stop. Approved when milestones 2.8–2.10 were authorized.
- Phase 2.8–2.10 — Progress, validation before a run, and diagnostics. Approved with the tryout when Phase 3 was authorized.

- Phase 3.1 — Recording lifecycle. Approved when milestone 3.2 was authorized.
- Phase 3.2 — Mouse-button and keyboard capture. Approved when milestones 3.3–3.6 were authorized.
- Phase 3.3–3.6 — Optional movement, wheel capture, monotonic timestamps, and injected-event filtering. Approved when milestones 3.7–3.8 were authorized.
- Phase 3.7–3.8 — Sampling, simplification, and raw retention. Approved when the rest of Phase 3 was authorized.
- Phase 3 — Conversion, save/load, and the record/playback tryout. Approved when Phase 4 was authorized.
- Phase 4.1 — Plugin manifest validation. Approved when milestones 4.2–4.5 were authorized.
- Phases 4.2–4.5 — Discovery, API checks, load and shutdown, and action registration. Approved when the rest of Phase 4 was authorized.
- Phase 4 — Events, enable and disable, action execution, and the sample plugin. Approved when Phase 5 was authorized.
- Phase 5.1 — Application shell, navigation, theme, and settings foundation. Approved when milestone 5.2 was authorized.
- Phase 5.2 — List editor. Approved when milestones 5.3 and 5.4 were authorized.
- Phases 5.3 and 5.4 — Library and run view. Approved when milestone 5.5 was authorized.

## Outstanding issues

- No application icon file yet.
- The visual editor and Mini Mode are not built yet.
- Opening a macro and saving it again keeps the actions and drops extra document fields the editor does not show, such as timestamps.
- High contrast and a non-default DPI were not exercised in CS-M-001.
- ARM64 was not tested.

## Test status

Release build and Debug build of `ClickStitch.sln` succeeded with 0 warnings and 0 errors. The timing benchmark was a Release run.

```text
dotnet test ClickStitch.sln -c Release --settings tests.runsettings
```

Passed: 258. Failed: 0. Skipped: 0.  
`ClickStitch.Core.Tests.dll`: 77 passed in 172 ms.  
`ClickStitch.Input.Tests.dll`: 162 passed in 876 ms.  
`ClickStitch.Plugins.Sdk.Tests.dll`: 6 passed in 186 ms.  
`ClickStitch.Plugins.Host.Tests.dll`: 12 passed in 780 ms.  
`ClickStitch.Performance.Tests.dll`: 1 passed in 232 ms. That test does not submit input. The tryout window is not opened by this run.  
Release, 2 November 2026. The default run does not call `SendInput`, does not register a usable global hotkey, and does not install a low-level hook. The host tests load the probe and the sample plugin from a temporary folder. The save/load test uses a temporary folder. The record button is covered with a scripted hook. The library tests use a temporary folder. The playback test uses the capturing stand-in. The plugin manager test discovers a temporary folder and does not load those assemblies.

`make bench` was run the same evening on the Ryzen 7 7800X3D, Windows 11 build 26200.9711, .NET 10.0.14, Release. The product wait is hybrid. The table is in `docs/TESTING.md`.

Manual test CS-M-001 was run on Windows 11 Home 25H2 build 26200. The foundation window opened, showed the expected text, and was closed. Details are in `docs/TESTING.md`.

## Approved deviations

None. The foundation targets .NET 10, which is the current long-term support release.

## Change log

- Initial project specification and AI operating rules created.
- Product name set to ClickStitch. Specification, status, and agent rules live at the repository root. Design notes live in `docs/`. Icons live in `assets/`.
- Phase 0 foundation added: solution, WPF shell, `MacroFormat`, xUnit tests, and the architecture, development, testing, plugin, and UX documents. Supported operating system set to Windows 11.
- Retargeted the foundation from .NET 8 to .NET 10 after SDK 10.0.403 was installed. Release and Debug builds succeeded, and the same 4 tests passed.
- Phase 0 approved by the human. Added a root `Makefile` for build, test, clean, and run before Phase 1.
- Phase 1 authorized. Milestone 1.1 adds `ClickStitch.Input` with `SendInput` structures, constants, submission results, and absolute/relative coordinate conversion. No input is injected by the tests or the shell.
- Milestone 1.2 adds mouse move, button, click, and wheel gestures, plus key down and key up for the schema key list. Tests capture packets through a stand-in and do not call `SendInput`.
- Milestone 1.3 adds `InputStateManager`: ownership of successful presses, refusal of a second press, and idempotent release-all that keeps any input Windows did not accept.
- Milestone 1.4 adds a monotonic wait and `make bench`. The measured product wait is hybrid: a high-resolution timer, then a spin for the last 1.5 ms. `timeBeginPeriod` stays off.
- Milestone 1.5 adds `ExecutionWorker`: one background thread, pause that keeps holds, stop that releases them, and a progress queue that drops the oldest event when it is full. Tests use a stand-in for `SendInput`.
- Milestone 1.6 adds global hotkeys through `RegisterHotKey` on a background thread. Proposed chords are Ctrl+Alt+Shift with F9, F10, F11, and F12. A rejected chord does not stop the others. The shell does not listen yet.
- Milestone 1.7 adds `InputRuntime` shutdown: refuse new work, cancel, release owned inputs, unregister hotkeys, and return one report. A hotkey exception stays on that report. The process is not exited.
- `make manual` sends one click, F13, one wheel notch, and a held F14 into a window the test owns, then restores the pointer. It is not part of `make test`. The first run passed in 827 ms.
- Phase 2 authorized. Milestone 2.1 adds `MacroJson`: format-1 documents can be read, validated, and written. Actions are not run.
- Milestone 2.2 adds the built-in action catalog and `MacroExecution.Compile`. The contract describes each action. It does not submit input.
- Milestone 2.3 adds `MacroRunner`. It runs top-level actions in order through an executor. A repeat is not entered. The executor in the tests records calls and does not submit input.
- Milestone 2.4 adds `MacroInputExecutor`. It submits click, move, hold, wheel, key, and wait through the input state. Tests capture `SendInput` and do not touch the desktop. The executor does not start the worker.
- Milestone 2.5 runs `repeat.count` on the same child list once per count. A count of 1,000,000 with an empty body is not expanded into copies.
- Milestone 2.6 adds `MacroPause`. The next action waits until resume. A wait keeps its remaining time. A held key stays owned. The pause is not connected to a hotkey.
- Milestone 2.7 adds `MacroSession`. A macro runs on the execution worker. Stop and emergency stop release held inputs, including during a wait, a pause, or a repeat. Tests capture `SendInput`.
- Milestones 2.8–2.10 report each macro action on the worker's progress queue, refuse to start an invalid document, and snapshot that queue as the run diagnostic. A repeat is not reported once per iteration. There is no log file.
- A temporary tryout panel on the main window can move, click, hold a key, and repeat a click. `make run` opens it. It sends real input. It is not the Phase 5 product UI, and it does not record. The tryout was approved with Phase 2.
- Phase 3 authorized. Milestone 3.1 adds `RecordingSession`: start, stop, and cancel, and a stop after 100,000 events. No hook is installed.
- Milestone 3.2 adds button and key capture. The hook callback enqueues one record and returns. Injected events are ignored by default. Movement, the wheel, and saving a document are not included. The default tests use a scripted hook.
- Milestones 3.3–3.6 add optional movement, wheel capture, monotonic elapsed time, and the same injected-event filter on moves and the wheel. Sampling and saving a document are not included. The default tests use a scripted hook.
- Milestones 3.7–3.8 thin movement after the recording stops. The raw list stays in memory, and revert discards the shorter copy. No file is written.
- Phase 3 closes with conversion of a recording into a format-1 macro, save and load of that file, and replay through the existing runner.
- The tryout window can record one take and play it back. The take stays in memory. Clicks on the window are left out. Pause now reaches the playback. The default tests use a scripted hook.
- Phase 3 approved. Phase 4 authorized. Milestone 4.1 adds `ClickStitch.Plugins.Sdk`. It validates `clickstitch-plugin.json` and reports a duplicate id. It does not discover a folder or load an assembly.
- Milestones 4.2–4.5 add discovery, the API check on that catalog, in-process load and shutdown, and action registration during `Initialize`. The shell discovers a Plugins folder and does not load it. The probe is a test plugin, not the product sample.
- Phase 4 closes with event subscription, enable and disable behind the trust warning, action execution, failure reporting, and `samples/ClickStitch.SamplePlugin`. A plugin exception is recorded. The host asks the engine to release inputs it still owns. Startup still opens the window.
- Phase 4 approved. Phase 5.1 adds the shell: Home, Tryout, and Settings. Appearance is light, dark, or match Windows, and it is saved. High contrast still uses the system colors. Stop stays at the bottom of the window.
- Phase 5.1 approved. Phase 5.2 adds the list editor. It builds a format-1 document from the eight built-in actions, explains invalid fields, undoes the last 50 edits, and saves through `MacroStore`. It does not run the macro.
- Phase 5.2 approved. Phases 5.3 and 5.4 add the library and the run view. Macros are stored in Documents\ClickStitch. Run plays the open macro on the existing worker. A saved click is covered with the capturing stand-in.
- Phases 5.3 and 5.4 approved. Phase 5.5 adds the plugin manager. It lists installed plugins, version, compatibility, and errors, and it saves enablement for the next start. It does not load a plugin during the session.
- The shell drops the system title bar. Click is the first page. Macros, Record, Plugins, and Settings are tabs. Colors come from the app icon. No new click behavior was added.
- Click is one control: a button or key, Hold or Spam, at the pointer or a point, and an optional toggle shortcut. Spam shows the gap. The document is covered without sending input. The shortcut is not registered by the tests.
