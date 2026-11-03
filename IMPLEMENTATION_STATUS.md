# Implementation Status — ClickStitch

**Project status:** Phase 1 in progress  
**Current authorized phase:** Phase 1 — Core Windows Input Engine  
**Current authorized milestone:** 1.7 Hardening, awaiting review  
**Human approval to proceed:** Milestones 1.1 through 1.7 were authorized. Milestone 1.6 was approved when 1.7 was authorized. Phase 2 is not started.

## Phase status

| Phase | Status |
|---|---|
| 0 — Requirements, Architecture, Project Foundation | Approved |
| 1 — Core Windows Input Engine | Milestone 1.7 awaiting review |
| 2 — Macro Execution Engine | Not authorized |
| 3 — Macro Recording | Not authorized |
| 4 — Plugin SDK and Host | Not authorized |
| 5 — Application UI and UX | Not authorized |
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

Milestone 1.7 is implemented and is not in this list until it is approved.

## Outstanding issues

- No application icon file yet.
- The dark theme is specified and not applied.
- High contrast and a non-default DPI were not exercised in CS-M-001.
- ARM64 was not tested.

## Test status

Release build and Debug build of `ClickStitch.sln` succeeded with 0 warnings and 0 errors. The timing benchmark was a Release run.

```text
dotnet test ClickStitch.sln -c Release --settings tests.runsettings
```

Passed: 102. Failed: 0. Skipped: 0.  
`ClickStitch.Core.Tests.dll`: 4 passed in 6 ms.  
`ClickStitch.Input.Tests.dll`: 97 passed in 835 ms.  
`ClickStitch.Performance.Tests.dll`: 1 passed in 60 ms. That test does not submit input.  
Release, 2 November 2026. The default run does not call `SendInput` and does not register a usable global hotkey.

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
