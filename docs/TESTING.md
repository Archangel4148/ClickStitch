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
| `ClickStitch.Core.Tests` | Yes | Phase 0 |
| `ClickStitch.Input.Tests` | Yes. Layout, submission policy, coordinate math, primitives, and ownership. No desktop injection. | Phase 1.1 through 1.3 |
| Execution, recording, and plugin-host test projects | Yes, once they exist, for tests that do not touch the desktop | Later phases |
| `ClickStitch.Performance.Tests` | No | Phase 1 |
| Manual input tests | No | Phase 1 |

Default command:

```text
dotnet test ClickStitch.sln -c Release --settings tests.runsettings
```

Performance and manual tests use the traits `Category=Performance` and `Category=Manual`. The default command omits those traits by not containing them. When those tests exist, the default command gains `--filter "Category!=Manual&Category!=Performance"`. Until then, the filter is unnecessary because every current test is a unit test.

## Phase 0 automated result

Command: `dotnet test ClickStitch.sln -c Release --settings tests.runsettings`  
Result on 2 November 2026: **4 passed, 0 failed, 0 skipped.**  
Assembly: `ClickStitch.Core.Tests.dll` (`net10.0`). Duration reported by the runner after the .NET 10 retarget: 6 ms.

The Phase 0 tests cover `MacroFormat`: version 1 is supported, and versions 0, 2, and -1 are rejected.

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

## Timing benchmarks

The methodology is in `docs/ARCHITECTURE.md` section 10. Summary of the Phase 1 plan:

- Clock: `Stopwatch.GetTimestamp()`.
- Workload: pointer-move submissions at 10 ms and at 50 ms, at least 1000 samples each after warmup.
- Record scheduling error and submission duration.
- Report P50, P95, P99, maximum, CPU, and working set.
- Record hardware, OS build, runtime version, build configuration, test duration, sample count, requested interval, and known interference.
- Baseline thread priority is normal. `timeBeginPeriod(1)` is off in the baseline.
- Compare results only under comparable conditions. A benchmark is a report of that run, not a guarantee about other machines.

No benchmark has been run. There is no numeric performance target.

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

## Milestone completion

Every milestone must have:

- Defined acceptance criteria.
- Automated test output.
- Manual test instructions where necessary.
- Known limitations.
- Human approval before the next milestone.
