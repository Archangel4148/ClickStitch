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

The shell uses the navy and violet from `assets/clickstitch_icon_rounded.png`. The window background is a short gradient between two close shades of that navy or lavender, so text still clears the lighter end. Start uses a violet gradient. Light and dark are both applied. Text, muted labels, and Stop each stay readable against the surface behind them.

Personality stays calm and specific. The shell is square: controls have no corner radius, and the window opens at the size of the Click page. Charm comes from type, alignment, and one accent. Glow and heavy shadows are out.

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
| Section | 20 | Semibold |
| Body | 13 | Regular |
| Label | 12 | Regular |

The shell body is 13px so the Click page can sit in a small window. The user can still scale Windows text, and layouts have to allow it.

### Space, shape, and controls

The spacing step is 4px: 8, 12, 16. Controls are square. Scrollbars are a 10px theme thumb drawn over the page, muted until the pointer is on them. The default control height is 34px. Start is 40px. Stop sits in the bottom bar at 32px. Choices that used to be separate radio buttons are one segmented row, with a Segoe MDL2 icon beside the label. Icons are not the only label.

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

The shell opens on Click. That page chooses one button or key, then either holds it or repeats it. The default place is the pointer. A point, a count, and a toggle shortcut are on the same page. Macros, Record, Plugins, and Settings are the other tabs. The editor does not send input. Record and Run do. Stop stays visible.
