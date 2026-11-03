# ClickStitch

ClickStitch is a Windows 11 desktop application for automating repetitive mouse and keyboard work without writing code. People build macros visually, record what they do, and play those macros back. Power users and plugin authors can extend it through a documented plugin platform.

The first release is local-first. Ordinary use does not require an account or a network connection. The product is a macro studio: clicks, keys, recording, playback, reusable macros, triggers, and visual control flow.

## Who it is for

- People who want a simple way to create, save, and run a click sequence.
- People who later need loops, conditions, variables, triggers, and finer timing controls.
- Plugin authors who want to add actions, blocks, triggers, and conditions without changing the core application.

## What you can do with it

These are the intended product capabilities. None of them are implemented yet.

- Create, save, open, and run macros.
- Record physical mouse and keyboard input and turn it into an editable macro.
- Build macros from blocks such as click, move, press a key, wait, repeat, and if/else.
- Pause, resume, and stop a running macro, including a global emergency stop that releases inputs the app is holding.
- Keep macros in a library, with import and export.
- Use a compact Mini Mode when the full editor is in the way.
- Add trusted plugins for extra actions, triggers, and conditions.

A first-run experience should include a sample macro and a clear explanation of the emergency stop.

## Boundaries

- The initial product runs on Windows 11. The proposed architecture is 64-bit, and that part is still awaiting review.
- It automates actions the user is already allowed to perform.
- Timing is measured and reported honestly. Windows is not a real-time system, and ClickStitch does not promise exact sub-millisecond delivery.
- Plugins are trusted in-process code. They are not sandboxed.
- Accounts, cloud sync, a plugin marketplace, and cross-platform support are outside the initial release.

## Status

**Phase 1 is in progress.** Milestone 1.3, the input state manager, is awaiting review. See [`IMPLEMENTATION_STATUS.md`](IMPLEMENTATION_STATUS.md).

Update this section as phases are approved and finished. The authoritative record is [`IMPLEMENTATION_STATUS.md`](IMPLEMENTATION_STATUS.md).

| Phase | Focus | Status |
|---|---|---|
| 0 | Requirements, architecture, and project foundation | Approved |
| 1 | Windows input engine | Milestone 1.3 awaiting review |
| 2 | Macro execution | Not authorized |
| 3 | Macro recording | Not authorized |
| 4 | Plugin SDK and host | Not authorized |
| 5 | Application UI | Not authorized |
| 6 | Hardening and distribution | Not authorized |
| 7 | Plugins | Not authorized |

Each phase stops for review before the next one begins.

## Repository

```text
README.md                  This overview
SPECIFICATION.md           Requirements, roadmap, and acceptance criteria
IMPLEMENTATION_STATUS.md   Current phase, decisions, and known issues
AI_INSTRUCTIONS.md         Rules for coding agents working in this repo
docs/                      Architecture, development, testing, plugins, and UX
src/                       WPF shell, core library, and input interop
tests/                     xUnit tests
ClickStitch.sln           Solution
Makefile                   build, test, clean, and run
```

An application icon is not in the repository yet. `docs/ARCHITECTURE.md` is the approved Phase 0 design. The foundation targets .NET 10 and WPF.

## Development

Start with [`SPECIFICATION.md`](SPECIFICATION.md), [`AI_INSTRUCTIONS.md`](AI_INSTRUCTIONS.md), and [`IMPLEMENTATION_STATUS.md`](IMPLEMENTATION_STATUS.md). Work stays inside the authorized phase. Build and test steps are in [`docs/DEVELOPMENT.md`](docs/DEVELOPMENT.md).

Windows 11 is the supported operating system, and the foundation targets .NET 10 and WPF. Build with `make` from the repository root. Details are in [`docs/DEVELOPMENT.md`](docs/DEVELOPMENT.md) and [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md).

## Further reading

- [Specification](SPECIFICATION.md)
- [Implementation status](IMPLEMENTATION_STATUS.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Development](docs/DEVELOPMENT.md)
- [Testing](docs/TESTING.md)
- [Plugin SDK](docs/PLUGIN_SDK.md)
- [UX guidelines](docs/UX_GUIDELINES.md)
