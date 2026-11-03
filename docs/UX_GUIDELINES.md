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

Phase 0 defines the visual system and applies the light theme to the foundation window. Dark theme tokens are specified so later screens can use them. The dark theme is not implemented.

Personality stays friendly, calm, and specific. Charm comes from spacing, type, and one accent. Gradients, glow, and heavy shadows are out.

### Color

Light theme, used by the foundation window:

| Token | Hex | Use |
|---|---|---|
| Paper | `#F6F3EE` | Window background |
| Ink | `#1C2430` | Text |
| Thread | `#B4532A` | The single accent. A mark, a selection, a primary action. |
| Stop | `#9E2B2B` | Emergency stop only, so stop is not the same color as the accent. |

Ink on paper is the text pair. Thread and Stop are for marks and controls, with Ink or white text chosen per control so the label stays readable. The foundation window uses Thread as a vertical bar, not as text.

Dark theme, defined and not applied:

| Token | Hex |
|---|---|
| Paper | `#1C2430` |
| Ink | `#F6F3EE` |
| Thread | `#E07A4C` |
| Stop | `#F0B4B4` |

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

The application mark is a single stitch: two short joined diagonal strokes in Thread on Paper. An icon file is not in the repository yet.

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

The current window shows the product name, "Desktop macros for Windows 11.", a sentence that this build is the foundation, and the macro format version. It has no buttons that look like they run a macro.
