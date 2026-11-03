# Testing Guidelines — ClickStitch

## Test layers

1. Unit tests: isolated algorithms and components.
2. Integration tests: interactions among engine, recorder, plugin host, persistence, and UI services.
3. Performance tests: timing error, submission duration, CPU, memory, and sustained execution.
4. Manual acceptance tests: actual desktop input, hotkeys, multi-monitor/DPI behavior, usability, and packaging.
5. Compatibility tests: supported Windows versions, .NET runtime, plugin API, and macro schema migration.

## Test safety

- Tests that inject real input must be clearly marked and run only when explicitly started by a developer.
- Do not run destructive or disruptive desktop automation as part of an unattended default test suite.
- Provide a safe test target or controlled test window for manual input validation.
- Test emergency stop and cleanup under controlled conditions.
- Never assume a passing unit test proves physical input delivery works on every desktop.

## Projects

| Project | In the default run | Present |
|---|---|---|
| `ClickStitch.Core.Tests` | Yes. Format version, the document reader, the execution contract, the runner, repeats, pause, progress, validation before a run, the recording lifecycle, and macro file save/load. | Phase 0 through Phase 3 |
| `ClickStitch.Input.Tests` | Yes. Layout, submission policy, coordinate math, primitives, ownership, timing, the execution worker, global hotkeys, shutdown, the macro executor, macro stop, run diagnostics, capture through a scripted hook, sampling, simplification, and conversion to a macro that replays through the runner. No desktop injection, no usable global chord, and no low-level hook. | Phase 1.1 through Phase 3 |
| Execution, recording, and plugin-host test projects | Yes, once they exist, for tests that do not touch the desktop | Later phases |
| `ClickStitch.Performance.Tests` | Only its opt-in guard. The desktop benchmark is `Category=Performance` and is excluded. | Phase 1 |
| `ClickStitch.Bench` | No. `make bench` runs it directly. | Phase 1 |
| Manual input tests | No. `make manual` runs `Category=Manual` and sets `CLICKSTITCH_MANUAL_INPUT=1`. | Phase 1 |

Default command:

```text
dotnet test ClickStitch.sln -c Release --settings tests.runsettings
```

Performance and manual tests use the traits `Category=Performance` and `Category=Manual`. The default command passes `--filter "Category!=Manual&Category!=Performance"`, and `tests.runsettings` has the same filter. `make bench` is separate and does submit input.

## Phase 0 automated result

Command: `dotnet test ClickStitch.sln -c Release --settings tests.runsettings`  
Result on 2 November 2026: **4 passed, 0 failed, 0 skipped.**  
Assembly: `ClickStitch.Core.Tests.dll` (`net10.0`). Duration reported by the runner after the .NET 10 retarget: 6 ms.

The Phase 0 tests cover `MacroFormat`: version 1 is supported, and versions 0, 2, and -1 are rejected.

## Phase 2.1 automated result

Command: `dotnet test ClickStitch.sln -c Release --settings tests.runsettings --filter "Category!=Manual&Category!=Performance"`  
Result on 2 November 2026: **132 passed, 0 failed, 0 skipped.**

- `ClickStitch.Core.Tests.dll`: 34 passed, duration 40 ms.
- `ClickStitch.Input.Tests.dll`: 97 passed, duration 835 ms.
- `ClickStitch.Performance.Tests.dll`: 1 passed, duration 67 ms. This test does not submit input.

The new tests read and write format-1 documents. They cover the schema example, unknown members, the action types, repeat nesting, and rejected documents. They do not run actions and they do not call `SendInput`.

## Phase 2.2 automated result

Command: `dotnet test ClickStitch.sln -c Release --settings tests.runsettings --filter "Category!=Manual&Category!=Performance"`  
Result on 2 November 2026: **138 passed, 0 failed, 0 skipped.**

- `ClickStitch.Core.Tests.dll`: 40 passed, duration 41 ms.
- `ClickStitch.Input.Tests.dll`: 97 passed, duration 845 ms.
- `ClickStitch.Performance.Tests.dll`: 1 passed, duration 3 ms. This test does not submit input.

The new tests check the eight built-in action descriptors and the execution contract: one submission per click, move, hold, wheel, and key; wait pause and stop; a repeat that keeps its count; and a comment that submits nothing. They do not call `SendInput`.

## Phase 2.3 automated result

Command: `dotnet test ClickStitch.sln -c Release --settings tests.runsettings --filter "Category!=Manual&Category!=Performance"`  
Result on 2 November 2026: **143 passed, 0 failed, 0 skipped.**

- `ClickStitch.Core.Tests.dll`: 45 passed, duration 38 ms.
- `ClickStitch.Input.Tests.dll`: 97 passed, duration 853 ms.
- `ClickStitch.Performance.Tests.dll`: 1 passed, duration 4 ms. This test does not submit input.

The runner tests call a recording executor. They check order, a comment that is not dispatched, `stopOnError`, cancellation between actions, and a repeat that is not entered. They do not call `SendInput`.

## Phase 2.4 automated result

Command: `dotnet test ClickStitch.sln -c Release --settings tests.runsettings --filter "Category!=Manual&Category!=Performance"`  
Result on 2 November 2026: **150 passed, 0 failed, 0 skipped.**

- `ClickStitch.Core.Tests.dll`: 45 passed, duration 41 ms.
- `ClickStitch.Input.Tests.dll`: 104 passed, duration 849 ms.
- `ClickStitch.Performance.Tests.dll`: 1 passed, duration 67 ms. This test does not submit input.

The executor tests run a compiled document through `MacroRunner` and a capturing `SendInput`. They check one batch per click, move, hold, and wheel, key ownership, a zero wait, a short acceptance, a point outside the screen, and a cancelled wait. They do not call the real `SendInput`.

## Phase 2.5 automated result

Command: `dotnet test ClickStitch.sln -c Release --settings tests.runsettings --filter "Category!=Manual&Category!=Performance"`  
Result on 2 November 2026: **155 passed, 0 failed, 0 skipped.**

- `ClickStitch.Core.Tests.dll`: 49 passed, duration 43 ms.
- `ClickStitch.Input.Tests.dll`: 105 passed, duration 923 ms.
- `ClickStitch.Performance.Tests.dll`: 1 passed, duration 68 ms. This test does not submit input.

Repeat tests run a count of 3, a nested count, a count of 1,000,000 with an empty child list, cancellation between iterations, and `stopOnError` inside a repeat. The child list stays one list. One input test repeats a captured click twice. None of these call the real `SendInput`.

## Phase 2.6 automated result

Command: `dotnet test ClickStitch.sln -c Release --settings tests.runsettings --filter "Category!=Manual&Category!=Performance"`  
Result on 2 November 2026: **158 passed, 0 failed, 0 skipped.**

- `ClickStitch.Core.Tests.dll`: 51 passed, duration 54 ms.
- `ClickStitch.Input.Tests.dll`: 106 passed, duration 850 ms.
- `ClickStitch.Performance.Tests.dll`: 1 passed, duration 64 ms. This test does not submit input.

Pause tests block the runner on another thread. They check that the next action waits for resume, that cancellation during the pause does not start it, and that a held key stays owned while a wait keeps the remaining milliseconds. They capture `SendInput` and do not call the real one.

## Phase 2.7 automated result

Command: `dotnet test ClickStitch.sln -c Release --settings tests.runsettings --filter "Category!=Manual&Category!=Performance"`  
Result on 2 November 2026: **163 passed, 0 failed, 0 skipped.**

- `ClickStitch.Core.Tests.dll`: 51 passed, duration 39 ms.
- `ClickStitch.Input.Tests.dll`: 111 passed, duration 836 ms.
- `ClickStitch.Performance.Tests.dll`: 1 passed, duration 52 ms. This test does not submit input.

Session tests run a macro on the worker and capture `SendInput`. Stop during a wait, stop during a pause, and emergency stop during a repeat each release the held key and skip the rest. A macro that finishes or fails while a key is down is released too. They do not call the real `SendInput` and they do not register a hotkey.

## Phase 2.8 through 2.10 automated result

Command: `dotnet test ClickStitch.sln -c Release --settings tests.runsettings --filter "Category!=Manual&Category!=Performance"`  
Result on 2 November 2026: **171 passed, 0 failed, 0 skipped.**

- `ClickStitch.Core.Tests.dll`: 56 passed, duration 52 ms.
- `ClickStitch.Input.Tests.dll`: 114 passed, duration 839 ms.
- `ClickStitch.Performance.Tests.dll`: 1 passed, duration 50 ms. This test does not submit input.

Progress tests record each action once. A repeat of 1,000,000 empty steps is one start and one completion. An invalid document is not ready and does not start the worker. A failed action is on the diagnostic snapshot, and the held key is released. These tests capture `SendInput` and do not call the real one. There is no log file.

## Phase 3.1 automated result

Command: `dotnet test ClickStitch.sln -c Release --settings tests.runsettings --filter "Category!=Manual&Category!=Performance"`  
Result on 2 November 2026: **179 passed, 0 failed, 0 skipped.**

- `ClickStitch.Core.Tests.dll`: 62 passed, duration 37 ms.
- `ClickStitch.Input.Tests.dll`: 116 passed, duration 887 ms.
- `ClickStitch.Performance.Tests.dll`: 1 passed, duration 57 ms. This test does not submit input.

The new tests start, stop, and cancel a recording session, and they fill the 100,000-event limit. They do not install a hook and they do not call `SendInput`.

## Phase 3.2 automated result

Command: `dotnet test ClickStitch.sln -c Release --settings tests.runsettings --filter "Category!=Manual&Category!=Performance"`  
Result on 2 November 2026: **190 passed, 0 failed, 0 skipped.**

- `ClickStitch.Core.Tests.dll`: 62 passed, duration 41 ms.
- `ClickStitch.Input.Tests.dll`: 127 passed, duration 830 ms.
- `ClickStitch.Performance.Tests.dll`: 1 passed, duration 53 ms. This test does not submit input.

The new tests feed button and key samples through a scripted hook. Order, timestamps, injected samples, unknown keys, auto-repeat, the 100,000-event cap, stop, cancel, and a failed install are covered. Calling uninstall before install is safe. These tests do not call `SetWindowsHookEx`, `SendInput`, or `RegisterHotKey`.

## Phase 3.3 through 3.6 automated result

Command: `dotnet test ClickStitch.sln -c Release --settings tests.runsettings --filter "Category!=Manual&Category!=Performance"`  
Result on 2 November 2026: **194 passed, 0 failed, 0 skipped.**

- `ClickStitch.Core.Tests.dll`: 62 passed, duration 48 ms.
- `ClickStitch.Input.Tests.dll`: 131 passed, duration 839 ms.
- `ClickStitch.Performance.Tests.dll`: 1 passed, duration 53 ms. This test does not submit input.

Movement is off unless the test turns it on. A repeated point is dropped, and a new point keeps its coordinates and timestamp. The wheel keeps a delta of 120, -240, and a partial 40. The horizontal wheel is ignored. Injected buttons, moves, and wheel samples are dropped unless the filter is off. Elapsed time is computed from monotonic timestamps. These tests do not call `SetWindowsHookEx`, `SendInput`, or `RegisterHotKey`.

## Phase 3.7 and 3.8 automated result

Command: `dotnet test ClickStitch.sln -c Release --settings tests.runsettings --filter "Category!=Manual&Category!=Performance"`  
Result on 2 November 2026: **205 passed, 0 failed, 0 skipped.**

- `ClickStitch.Core.Tests.dll`: 62 passed, duration 49 ms.
- `ClickStitch.Input.Tests.dll`: 142 passed, duration 846 ms.
- `ClickStitch.Performance.Tests.dll`: 1 passed, duration 59 ms. This test does not submit input.

Sampling drops moves inside the interval and keeps the last point. A step shorter than the minimum distance is dropped. A point on the straight line is dropped once the tolerance is larger than its distance, and a corner past the tolerance stays. A button splits the path. The raw list and the session count stay put, revert discards the copy, and a rejected setting leaves the previous copy in place. These tests do not call `SetWindowsHookEx`, `SendInput`, or `RegisterHotKey`, and they do not write a file.

## Phase 3.9 and 3.10 automated result

Command: `dotnet test ClickStitch.sln -c Release --settings tests.runsettings --filter "Category!=Manual&Category!=Performance"`  
Result on 2 November 2026: **216 passed, 0 failed, 0 skipped.**

- `ClickStitch.Core.Tests.dll`: 64 passed, duration 42 ms.
- `ClickStitch.Input.Tests.dll`: 151 passed, duration 910 ms.
- `ClickStitch.Performance.Tests.dll`: 1 passed, duration 53 ms. This test does not submit input.

A move, a click, and the waits around the press become format-1 actions. A drag stays a hold around its moves. Wheel units add up to notches, and a count past 100 is split. Modifiers can be left out. A point outside the schema range is left out. The shortened copy is what gets saved. The document replays through the runner against a stand-in for `SendInput`. Save and load round-trip a file in a temporary folder, and saving the same id under a new name removes the old file. These tests do not call `SetWindowsHookEx` or `RegisterHotKey`, and they do not write under `%LOCALAPPDATA%`.

The tryout's Record, Stop recording, and Play recording buttons are covered without a real hook. A scripted move outside a stand-in window is kept, a click inside that window is left out, and playback submits through a stand-in for `SendInput`. `make run` is the manual check and is not part of this command.

Result on 2 November 2026 after that tryout: **218 passed, 0 failed, 0 skipped.** `ClickStitch.Core.Tests.dll` 64 passed in 51 ms. `ClickStitch.Input.Tests.dll` 153 passed in 828 ms. `ClickStitch.Performance.Tests.dll` 1 passed in 62 ms.

## Phase 1.1 automated result

Command: `dotnet test ClickStitch.sln -c Release --settings tests.runsettings`  
Result on 2 November 2026: **24 passed, 0 failed, 0 skipped.**

- `ClickStitch.Core.Tests.dll`: 4 passed, duration 6 ms.
- `ClickStitch.Input.Tests.dll`: 20 passed, duration 28 ms.

The input tests check x64 structure sizes and offsets, batch error handling through a stand-in for `SendInput`, and absolute/relative coordinate conversion. They do not call `SendInput`.

## Phase 1.2 automated result

Command: `dotnet test ClickStitch.sln -c Release --settings tests.runsettings`  
Result on 2 November 2026: **44 passed, 0 failed, 0 skipped.**

- `ClickStitch.Core.Tests.dll`: 4 passed, duration 5 ms.
- `ClickStitch.Input.Tests.dll`: 40 passed, duration 29 ms.

The new tests check clicks, moves, wheel notches, the schema key list, modifier chords, and the extended-key flag. They capture packets through a stand-in and do not call `SendInput`.

## Phase 1.3 automated result

Command: `dotnet test ClickStitch.sln -c Release --settings tests.runsettings`  
Result on 2 November 2026: **59 passed, 0 failed, 0 skipped.**

- `ClickStitch.Core.Tests.dll`: 4 passed, duration 6 ms.
- `ClickStitch.Input.Tests.dll`: 55 passed, duration 29 ms.

The ownership tests check held keys and buttons, refused second downs, ups that are not owned, click prefixes, and release-all including a short acceptance. They do not call `SendInput`.

## Phase 1.5 automated result

Command: `dotnet test ClickStitch.sln -c Release --settings tests.runsettings --filter "Category!=Manual&Category!=Performance"`  
Result on 2 November 2026: **84 passed, 0 failed, 0 skipped.**

- `ClickStitch.Core.Tests.dll`: 4 passed, duration 7 ms.
- `ClickStitch.Input.Tests.dll`: 79 passed, duration 691 ms.
- `ClickStitch.Performance.Tests.dll`: 1 passed, duration 96 ms. This test does not submit input.

The worker tests cover a background thread, stop during a wait, pause that keeps a held key and the remaining wait, a failed release, and a full progress queue. They use a stand-in for `SendInput`.

## Phase 1.6 automated result

Command: `dotnet test ClickStitch.sln -c Release --settings tests.runsettings --filter "Category!=Manual&Category!=Performance"`  
Result on 2 November 2026: **92 passed, 0 failed, 0 skipped.**

- `ClickStitch.Core.Tests.dll`: 4 passed, duration 6 ms.
- `ClickStitch.Input.Tests.dll`: 87 passed, duration 704 ms.
- `ClickStitch.Performance.Tests.dll`: 1 passed, duration 61 ms. This test does not submit input.

The hotkey tests use a stand-in for `RegisterHotKey` and a scripted message pump. They check the proposed chords, a conflict that leaves the other roles registered, replacement, and an emergency press that releases a held key while the worker is paused. One test calls the real `RegisterHotKey` with an undefined modifier flag (`0x1000`) and virtual key `0x70`. Windows returns false and error 1004 (`ERROR_INVALID_FLAGS`), so that call does not take a chord. A virtual key of 0 is accepted by Windows and is not used. The default suite does not register a usable global hotkey. The window is not connected to the listener, so a manual hotkey check is not available until a later phase wires the shell.

## Phase 1.7 automated result

Command: `dotnet test ClickStitch.sln -c Release --settings tests.runsettings --filter "Category!=Manual&Category!=Performance"`  
Result on 2 November 2026: **102 passed, 0 failed, 0 skipped.**

- `ClickStitch.Core.Tests.dll`: 4 passed, duration 6 ms.
- `ClickStitch.Input.Tests.dll`: 97 passed, duration 835 ms.
- `ClickStitch.Performance.Tests.dll`: 1 passed, duration 60 ms. This test does not submit input.

The hardening tests use a stand-in for `SendInput` and a stand-in for `RegisterHotKey`. They stop during a click that has not released, a held key, a drag, a pause, and 20 repeated holds. Shutdown during a run refuses another start, keeps one report, and releases a held key. An action exception and a failed release both appear on that report. A hotkey target that throws is on the report, and the held key is still released. A hotkey that cannot be unregistered stays listed. The window is not connected, so there is no manual shutdown check.

## Timing benchmarks

The methodology is in `docs/ARCHITECTURE.md` section 10. Summary of the Phase 1 plan:

- Clock: `Stopwatch.GetTimestamp()`.
- Workload: pointer-move submissions at 10 ms and at 50 ms, at least 1000 samples each after warmup.
- Record scheduling error and submission duration.
- Report P50, P95, P99, maximum, CPU, and working set.
- Record hardware, OS build, runtime version, build configuration, test duration, sample count, requested interval, and known interference.
- Baseline thread priority is normal. `timeBeginPeriod(1)` is off in the baseline.
- Compare results only under comparable conditions. A benchmark is a report of that run, not a guarantee about other machines.

`make bench` prints a line as each series starts, a "still running" line every 5 seconds from another thread, and the report at the end. The sample loop does not log. There is no separate numeric pass/fail target. This run is the baseline.

## Phase 1.4 benchmark result

Command: `make bench`  
Release, 2 November 2026. 1000 measured samples and 50 warmup samples per series. Thread priority Normal. `timeBeginPeriod` off. Relative pointer move of 0,0. Early samples: 0. Submission failures: 0.

Machine: AMD Ryzen 7 7800X3D, 16 logical processors. Windows registry ProductName "Windows 10 Home", DisplayVersion 25H2, build 26200.9711. `RtlGetVersion` 10.0.26200. .NET 10.0.14. Stopwatch frequency 10,000,000.

Scheduling error is how late the wait woke. Spacing is the time from one submit to the next, so it includes the requested interval and the `SendInput` call. Times are milliseconds.

| Wait | Interval | Error P50 | Error P99 | Error max | Spacing P50 | Spacing P99 | Core fraction |
|---|---:|---:|---:|---:|---:|---:|---:|
| Hybrid, no wait | 0 | 0.001 | 0.002 | 0.042 | 0.047 | 0.241 | 0.000 |
| Coarse sleep | 1 | 14.565 | 15.545 | 15.855 | 15.670 | 16.644 | 0.005 |
| Coarse sleep | 10 | 5.597 | 6.532 | 6.562 | 15.714 | 16.627 | 0.002 |
| Coarse sleep | 50 | 13.113 | 14.959 | 15.791 | 63.236 | 65.066 | 0.001 |
| High-resolution timer | 1 | 0.463 | 0.553 | 1.036 | 1.513 | 1.622 | 0.010 |
| High-resolution timer | 10 | 0.419 | 0.588 | 0.901 | 10.520 | 10.677 | 0.002 |
| High-resolution timer | 50 | 0.322 | 0.548 | 1.124 | 50.401 | 50.662 | 0.001 |
| Hybrid, 1.5 ms spin | 1 | 0.000 | 0.101 | 0.276 | 1.047 | 1.185 | 0.965 |
| Hybrid, 1.5 ms spin | 10 | 0.000 | 0.010 | 0.146 | 10.072 | 10.151 | 0.126 |
| Hybrid, 1.5 ms spin | 50 | 0.000 | 0.016 | 0.120 | 50.074 | 50.156 | 0.025 |

`SendInput` itself took about 0.05–0.11 ms at P50 and up to about 0.55 ms at the maximum, depending on the series. The cursor stayed put across the back-to-back submits. It changed once during the coarse 1 ms series, from (1602, 190) to (1090, 952), and stayed there afterward. That series also allows physical movement. Working set stayed between 26.7 and 29.6 MB.

The product wait is hybrid. A 1 ms request is met; coarse sleep cannot beat the 15.6 ms tick. `timeBeginPeriod(1)` was not enabled. One hybrid 50 ms gap reached 53.349 ms while the wait error on that series never exceeded 0.120 ms, so that outlier was time between actions rather than a late wake.

## Manual acceptance format

Each manual test has an id, a purpose, preconditions, steps, and expected results. Record the machine, the build, and what actually happened.

### CS-M-001 — Foundation window

**Purpose:** Confirm the Phase 0 shell opens and does not present macro controls.

**Preconditions:** Windows 11, 64-bit, with the .NET 10 desktop runtime.

**Steps:**

1. From the repository root, run `dotnet run --project src/ClickStitch.App -c Release`.
2. Read the window.
3. Close the window.

**Expected:** The window title is ClickStitch. The window says this build is the project foundation and that macro documents use format version 1. Creating, recording, and running macros are described as later work. No macro starts. Closing the window exits the app.

**Observed on 2 November 2026:** On Windows 11 Home 25H2 build 26200, the Release executable opened a responding window titled ClickStitch with that text, including the terracotta bar from the design tokens. The process was then closed. High contrast and a second DPI setting were not exercised.

### CS-M-002 — Real input inside the probe window

**Purpose:** Confirm a click, a key, a wheel notch, and a released hold reach a window this process owns, and nowhere else.

**Preconditions:** Windows 11, 64-bit. Leave the mouse and keyboard alone while the probe window is up. The run takes a few seconds.

**Steps:**

1. From the repository root, run `make manual`.
2. A window titled ClickStitch probe appears. The pointer moves into it, then returns.
3. Read the test output.

**Expected:** The probe receives one left click near its center, F13 down and up, one wheel notch, and F14 down and up. No right click and no Alt menu key. The owned-input set is empty at the end. The pointer is back where it started. If the probe cannot come to the front, the test stops and reports that no click, key, or wheel was sent.

**Observed on 2 November 2026:** `make manual` on this machine, Release. 1 passed, 0 failed, 0 skipped, duration 827 ms. The probe came to the front, accepted the click near the center, F13, one wheel notch, and the F14 hold released by the worker. The owned-input set was empty and the pointer was put back. No right click and no Alt menu key were observed by the window.

## Milestone completion

Every milestone must have:

- Defined acceptance criteria.
- Automated test output.
- Manual test instructions where necessary.
- Known limitations.
- Human approval before the next milestone.
