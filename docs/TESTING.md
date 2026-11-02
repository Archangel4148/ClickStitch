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

## Timing benchmarks

Record hardware, OS build, runtime version, build configuration, test duration, sample count, requested interval, P50/P95/P99/max scheduling error, submission duration, CPU, and memory.

Compare results only under comparable conditions. Do not present a benchmark as a universal guarantee.

## Milestone completion

Every milestone must have:
- Defined acceptance criteria.
- Automated test output.
- Manual test instructions where necessary.
- Known limitations.
- Human approval before the next milestone.
