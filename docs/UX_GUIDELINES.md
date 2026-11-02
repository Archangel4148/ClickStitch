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
