# ClickStitch

ClickStitch is a Windows desktop application for automating repetitive mouse and keyboard work without writing code. People build macros visually, record what they do, and play those macros back. Power users and plugin authors can extend it through a documented plugin platform.

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

- The initial product runs on Windows only.
- It automates actions the user is already allowed to perform.
- Timing is measured and reported honestly. Windows is not a real-time system, and ClickStitch does not promise exact sub-millisecond delivery.
- Plugins are trusted in-process code. They are not sandboxed.
- Accounts, cloud sync, a plugin marketplace, and cross-platform support are outside the initial release.

## Status

**Not started.** The authorized work is Phase 0 only: requirements, architecture, and project foundation. There is no solution, source project, or test project yet.

Update this section as phases are approved and finished. The authoritative record is [`IMPLEMENTATION_STATUS.md`](IMPLEMENTATION_STATUS.md).

| Phase | Focus | Status |
|---|---|---|
| 0 | Requirements, architecture, and project foundation | Not started |
| 1 | Windows input engine | Not authorized |
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
assets/                    Application icons
```

`docs/ARCHITECTURE.md`, `docs/DEVELOPMENT.md`, and `docs/PLUGIN_SDK.md` are Phase 0 placeholders. Build steps, the target .NET version, and the choice between WPF and WinUI 3 will be written down there before implementation starts.

## Development

When implementation begins, start with [`SPECIFICATION.md`](SPECIFICATION.md), [`AI_INSTRUCTIONS.md`](AI_INSTRUCTIONS.md), and [`IMPLEMENTATION_STATUS.md`](IMPLEMENTATION_STATUS.md). Work stays inside the authorized phase. Build and test instructions will live in [`docs/DEVELOPMENT.md`](docs/DEVELOPMENT.md) once a solution exists.

Phase 0 still has to settle supported Windows versions, the .NET LTS target, WPF or WinUI 3, the solution layout, the macro document schema, and the plugin trust model.

## Further reading

- [Specification](SPECIFICATION.md)
- [Implementation status](IMPLEMENTATION_STATUS.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Development](docs/DEVELOPMENT.md)
- [Testing](docs/TESTING.md)
- [Plugin SDK](docs/PLUGIN_SDK.md)
- [UX guidelines](docs/UX_GUIDELINES.md)
