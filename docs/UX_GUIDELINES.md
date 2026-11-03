# UX Guidelines — ClickStitch

## Product personality

Friendly, capable, calm, and dependable. The application should feel like a polished creative tool rather than a technical control panel.

## Core principles

- Make the next step obvious.
- Use plain language.
- Reveal complexity gradually.
- Make execution state unmistakable.
- Keep stop controls visible and dependable.
- Prefer useful defaults over configuration overload.
- Provide immediate feedback for user actions.
- Make errors understandable and actionable.
- Respect accessibility and user control.

## Terminology

Prefer:
- Macro
- Block
- Wait
- Repeat
- Condition
- Record
- Run
- Pause
- Stop
- Plugin

Avoid exposing implementation terms such as AST, scheduler, worker thread, hook, assembly loader, or serialization in ordinary user-facing UI.

## Block design

Every block should have:
- A clear verb or purpose.
- Readable parameter labels.
- Sensible defaults.
- Inline validation.
- A concise help description.
- Consistent category styling.
- A predictable interaction model.

Color must not be the only way to distinguish block categories.

## Motion and feedback

Use animation to reinforce state transitions and direct manipulation. Keep it subtle, quick, and optional where motion sensitivity is relevant. Never let animation delay emergency controls or execution feedback.

## Accessibility

- Keyboard navigation for primary workflows.
- Visible focus indicators.
- Readable contrast.
- DPI-aware layouts.
- Scalable text.
- Tooltips and labels that do not rely on color alone.
- Clear status text in addition to icons.

## First-run experience

Explain:
1. How to create or record a macro.
2. How to run and pause it.
3. How to stop it immediately.
4. Where macros are saved.
5. That plugins are trusted code and should come from trusted sources.

## Usability validation

Test with people unfamiliar with the project. Observe whether they can create, save, reopen, and run a simple macro without developer assistance. Record confusion points and fix them before adding more advanced features.

## Design system

The shell uses the navy and violet from `assets/clickstitch_icon_rounded.png`. Light and dark are both applied. Text, muted labels, the accent, and Stop each stay readable against the surface behind them.

Personality stays friendly, calm, and specific. Charm comes from spacing, type, and one accent. Gradients, glow, and heavy shadows are out.

### Color

Light theme:

| Token | Hex | Use |
|---|---|---|
| Paper | `#F7F4FC` | Window background |
| Ink | `#020718` | Text |
| Thread | `#5E35C4` | The accent. A selected tab, a primary button, a problem line. |
| Stop | `#A61E45` | Stop only, so stop is not the same color as the accent. |
| Card | `#FFFFFF` | Grouped controls |
| Muted | `#3C3554` | Secondary text |
| Line | `#D5C8EA` | Borders |

Ink on paper, muted on paper, white on Thread, and white on Stop are each above 4.5:1.

Dark theme, applied when Settings chooses Dark or when Match Windows finds a dark app theme:

| Token | Hex |
|---|---|
| Paper | `#0E1324` |
| Ink | `#F4F1FB` |
| Thread | `#D2B4FF` |
| Stop | `#FFB4C6` |
| Card | `#1A2238` |
| Muted | `#C9C0DC` |
| Line | `#3E4666` |

On the dark theme, Thread and Stop are light fills. Text drawn on those fills is `#1A1030` and `#2A0E18`, not white. Ink and muted stay light against Paper and Card. Those pairs are each above 4.5:1.

High contrast uses the system window, window-text, and highlight colors. The foundation window does this when Windows high contrast is on.

### Block categories

Color is paired with the category name. Color is never the only distinction.

| Category | Color | Label text |
|---|---|---|
| Mouse | `#2F6FED` | White |
| Keyboard | `#6B4C9A` | White |
| Wait | `#C4841D` | Ink `#1C2430` |
| Flow | `#2A7A6A` | White |
| Logic | `#3D4A5C` | White |
| Plugin | `#B4532A` | White |

### Type

Segoe UI Variable, with Segoe UI as the fallback. Both ship with Windows 11.

| Role | Size | Weight |
|---|---|---|
| Window title in the shell | 32 | Semibold |
| Section | 20 | Semibold |
| Body | 16 | Regular |
| Label | 13 | Regular |

Body text is at least 16px. The user can scale Windows text, and layouts have to allow it.

### Space, shape, and controls

The spacing step is 4px: 8, 12, 16, 24, 32, 48. Cards use a 12px corner radius. Controls use 8px. The default control height is 36px. A primary or stop control is at least 44px tall. Icons sit on a 20px grid, with a stroke near 1.75px and round caps.

The application mark is `assets/clickstitch_icon_rounded.png`. The window shows that icon in its own title bar and does not use the system caption.

### Motion

State changes use about 120ms. Emergency stop, press feedback, and execution-state changes that tell the user something is running or stopped happen immediately, with no animation ahead of them. Respect the Windows setting that reduces motion.

### Accessibility requirements

- Primary flows are reachable from the keyboard.
- Focus is visible.
- Text meets a 4.5:1 contrast ratio against its background. Large headings may use 3:1.
- Layout is per-monitor DPI aware. The shell manifest requests PerMonitorV2.
- Status is text plus an icon. Color alone does not communicate running, paused, stopped, or failed.
- The stop control, once it exists, stays visible in the main window and in Mini Mode.

### Foundation window

The shell opens on Click. That page sets a point, a gap, and a repeat, then starts. It sends real input. Macros, Record, Plugins, and Settings are the other tabs. Inside Macros, Library, Editor, and Run stay available. The editor does not send input. Record and Run do. Stop stays visible.
