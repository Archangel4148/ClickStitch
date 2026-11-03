# Development Guide — ClickStitch

Phase 0 foundation. Commands below were run on this repository on 2 November 2026.

## Requirements

- Windows 11, 64-bit.
- .NET SDK 10.0.403, or a newer SDK on the 10.0 line. `global.json` selects it.
- The .NET desktop workload, which provides WPF. This machine has `Microsoft.WindowsDesktop.App` 10.0.14.

Check the SDK:

```text
dotnet --version
```

Expected on this machine: `10.0.403`.

## Build

From the repository root, `make` wraps the .NET commands. GNU Make on this machine is 4.4.1 and runs recipes with `sh.exe`.

```text
make
make CONFIG=Debug
make test
make clean
make run
make help
```

`make` and `make build` compile Release. `CONFIG=Debug` selects the other configuration. `make test` runs the automated tests with `tests.runsettings`. `make clean` cleans Debug and Release, then deletes `bin` and `obj` under `src` and `tests`. `make run` opens the window. The tryout buttons send real input.

The same actions without Make:

```text
dotnet build ClickStitch.sln -c Release
dotnet build ClickStitch.sln -c Debug
dotnet test ClickStitch.sln -c Release --settings tests.runsettings
dotnet clean ClickStitch.sln -c Release
dotnet run --project src/ClickStitch.App -c Release
```

Both configurations succeeded after the .NET 10 retarget, with 0 warnings and 0 errors. The app output is `src/ClickStitch.App/bin/Release/net10.0-windows10.0.22000.0/ClickStitch.exe`.

## Tests

```text
make test
```

That runs:

```text
dotnet test ClickStitch.sln -c Release --settings tests.runsettings
```

`tests.runsettings` treats a run with zero tests as a failure. After the .NET 10 retarget the result was 4 passed, 0 failed, 0 skipped.

`make test` skips `Category=Performance` and `Category=Manual`. `make bench` is the timing benchmark. It takes about 4 minutes, submits a relative mouse move of 0,0, and prints a line at the start and end of each series plus a "still running" line every 5 seconds. The full report is printed at the end.

`make manual` is the real-input probe. It is not part of `make test`. It opens a small window, moves the pointer into that window, and only then sends one left click, F13, one wheel notch, and a held F14 that the worker releases. F13 and F14 do not type a character. If the window is not in front, or the pointer is not inside it, the probe sends no further input and puts the pointer back.

## Run the shell

```text
make run
```

That runs `dotnet run --project src/ClickStitch.App -c Release`.

The window is titled ClickStitch. The tryout can move the pointer, click, hold a key, repeat a click, record the desktop, and play that recording back. Pause, Resume, and Stop are on the window. Pause holds the next action of a playback. Record keeps buttons, keys, and the wheel, and pointer movement when the box is checked. Clicks on the tryout window are left out when recording stops. Play sends that recording as real input. The take stays in memory. The shell does not write settings or a macro file. Closing the window stops the run, uninstalls the hook, shuts plugins down, and exits the process. Startup looks for a `Plugins` folder beside the executable and loads only plugins enabled in settings after the trust warning. A missing folder, a bad settings file, or a plugin that fails to start still opens the window. The first plugin faults are written into the tryout log.

## Release build

The Release command above is the release build for this phase. Publishing a self-contained single-file executable is Phase 6 and is not set up.

## Configuration and macros

Planned locations, not created by this build:

- `%LOCALAPPDATA%\ClickStitch\settings.json`
- `%LOCALAPPDATA%\ClickStitch\Macros\`

## Plugins

`PluginHost.LoadEnabled` loads an enabled plugin in-process. A missing `Plugins` directory still starts the app. The design is in `docs/PLUGIN_SDK.md`. `samples/ClickStitch.SamplePlugin` is the sample and is not part of the application. `tests/ClickStitch.Plugins.Probe` exists only so the host tests have plugins to load.

## Dependencies

Production projects have no NuGet package references. The Windows target framework brings in the reference pack `Microsoft.Windows.SDK.NET.Ref` 10.0.22000.57.

Test packages, kept from the original xUnit template and verified on .NET 10:

| Package | Version | Role |
|---|---|---|
| xunit | 2.5.3 | Test framework. Used for every automated test. |
| xunit.runner.visualstudio | 2.5.3 | `dotnet test` adapter. |
| Microsoft.NET.Test.Sdk | 17.8.0 | Test host. |

The template also offered `coverlet.collector` 6.0.0. It is not referenced. This phase does not collect coverage.
