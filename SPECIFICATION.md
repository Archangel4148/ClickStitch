# Project Specification
## ClickStitch
**Product name:** ClickStitch  
**Document status:** Draft — architecture and requirements baseline  
**Target platform:** Windows desktop  
**Primary audience:** Non-developers who want approachable, reliable desktop automation  
**Secondary audience:** Power users and plugin developers

---

# 1. Product Vision

Build a polished, approachable, modular Windows desktop automation application. The application should make common repetitive tasks easy to automate without requiring users to write code, while providing a capable execution engine and extensible plugin platform for advanced workflows.

The product is not merely an auto-clicker. It is a visual macro and desktop automation studio whose first-class capabilities include mouse and keyboard automation, recording, playback, reusable macros, triggers, and visual control flow.

## 1.1 Product principles

1. **Approachable by default.** A first-time user should be able to create, save, and run a simple click sequence without understanding programming.
2. **Progressive disclosure.** Keep the default experience simple. Reveal loops, conditions, variables, triggers, and advanced timing controls as users need them.
3. **Visual clarity.** The block editor should communicate what a macro will do. Blocks should have readable names, sensible defaults, clear input labels, and helpful descriptions.
4. **Reliability before novelty.** Input cleanup, cancellation, recovery, and predictable execution are more important than adding a large number of flashy features.
5. **Responsive and transparent.** Users should always be able to tell whether a macro is idle, running, paused, stopping, or has failed.
6. **Extensible without core modification.** Plugins may add actions, blocks, triggers, conditions, services, and carefully controlled UI extensions through documented contracts.
7. **Measurable performance.** Timing and resource claims must be supported by repeatable measurements rather than marketing language.
8. **Local-first operation.** Core automation, macro storage, and plugin loading should work without an account or network connection. Network access must not be required for ordinary use.
9. **User control.** Users must be able to stop automation immediately, inspect what is running, and recover from errors without restarting Windows.
10. **Polish and charm.** The product should feel like a thoughtfully designed consumer application, not a developer debugging utility.

## 1.2 Product experience goals

- A guided first-run experience with a sample macro and a clear explanation of the emergency stop.
- A home screen that makes the next action obvious: create, record, open, or run a macro.
- A block editor with a small, coherent starter palette rather than dozens of intimidating options.
- Helpful empty states, inline explanations, sensible defaults, and contextual tooltips.
- A visible execution state and a prominent stop control.
- A compact Mini Mode for users who want controls without the full editor.
- Consistent visual language, spacing, typography, iconography, and motion.
- Keyboard accessibility and usable scaling across common display sizes and DPI settings.
- No deceptive claims that automation is undetectable, human, or guaranteed to work in every application.

## 1.3 Non-goals and boundaries

- The initial product is Windows-only.
- The initial core does not attempt to automate every application or bypass operating-system security boundaries.
- The application does not promise real-time scheduling or exact physical input delivery.
- Plugins are trusted in-process code unless a future isolated plugin-host design is explicitly approved.
- The application must not be designed or marketed for anti-cheat evasion, bypassing access controls, or defeating application protections.
- Cloud synchronization, accounts, a plugin marketplace, and cross-platform support are not initial requirements.

---

# 2. Technical Baseline and Architectural Principles

## 2.1 Technology decisions

The exact choices must be confirmed in Phase 0 and recorded in `docs/ARCHITECTURE.md`.

Initial recommendations:

- **Language/runtime:** C# on a supported .NET LTS release. Prefer the current supported LTS at project kickoff; do not hard-code an obsolete target framework.
- **UI:** Prefer WPF for the initial Windows desktop application unless a Phase 0 prototype demonstrates a compelling reason to choose WinUI 3.
- **Visual macro editor:** Evaluate Blockly hosted in WebView2 after the basic application and macro model are working. Keep the editor replaceable; it must not own execution semantics.
- **Testing:** xUnit or NUnit, selected once and used consistently.
- **Logging:** A structured logging abstraction with bounded asynchronous sinks.
- **Persistence:** Versioned JSON for macro documents and human-readable application configuration where practical.
- **Packaging:** Self-contained single-file application executable, with external plugin assemblies and any explicitly documented runtime prerequisites.

Do not add major frameworks or dependencies without documenting the need, licensing, maintenance status, distribution impact, and alternatives.

## 2.2 Architectural rules

1. The execution engine must not depend on the UI framework.
2. The block editor must produce the same versioned macro document that other authoring methods use.
3. The macro document is a declarative description, not a serialized runtime object graph.
4. Native Windows input APIs must be isolated behind a small, testable interop layer.
5. All simulated held inputs must be tracked centrally.
6. Cancellation and emergency stop must be available throughout execution.
7. UI, logging, persistence, plugin loading, and other potentially blocking work must not run on the timing-critical execution path.
8. Plugin capabilities must be registered through documented host contracts.
9. Core startup and core automation must work when the Plugins directory is absent or empty.
10. All persistent formats and plugin contracts must be versioned.
11. Prefer simple, explicit designs over speculative frameworks and premature abstraction.
12. Any deviation from an approved architecture must be explained and approved before implementation.

## 2.3 Timing terminology and performance expectations

Windows is not a real-time operating system. The application must not promise that a scheduled action will execute at an exact sub-millisecond instant.

Distinguish:

- **Clock resolution:** The resolution and stability of the monotonic clock used to measure elapsed time.
- **Scheduling error:** The difference between the requested action time and the time the application submits the action.
- **Submission duration:** The time spent submitting an input event to Windows.
- **Delivery/processing:** When the target application or device actually processes the event. `SendInput` does not provide a reliable acknowledgment of this.

Requirements:

- Use `Stopwatch.GetTimestamp()` or an equivalent monotonic high-resolution source for scheduling measurements.
- Benchmark a simple scheduler before adding complexity.
- Evaluate high-resolution waitable timers and, only if measurements justify it, a hybrid wait/spin strategy.
- Use `timeBeginPeriod(1)` only if measurements demonstrate a benefit; pair it with correct cleanup and document system-wide implications.
- Do not automatically use the highest thread priority. Select a conservative priority based on measured behavior and document the choice.
- Never perform blocking disk I/O, UI dispatch, plugin discovery, or unbounded logging on the timing-critical path.
- Record requested versus actual submission timestamps in performance tests.
- Report P50, P95, P99, maximum scheduling error, CPU use, and memory use under defined test conditions.
- Establish numerical performance targets only after a baseline exists on documented hardware and Windows versions.

A benchmark result is not a guarantee about every machine, workload, or target application.

---

# 3. Safety, Reliability, and Input Ownership

This is a foundational subsystem, not an optional feature.

## 3.1 Input State Manager

The application must maintain a centralized record of every mouse button and keyboard key currently held by the automation engine.

Responsibilities:

- Track successful key-down and mouse-down submissions.
- Track matching key-up and mouse-up submissions.
- Prevent conflicting execution contexts from taking ownership of the same simulated input state.
- Provide an idempotent `ReleaseAllOwnedInputs` operation.
- Release owned inputs after cancellation, exceptions, stop, shutdown, and plugin execution failure.
- Report cleanup failures clearly.
- Avoid releasing inputs that the application does not own.
- Define behavior when Windows reports a partial or failed input submission.

**Safety invariant:** When the engine is idle, stopped, or shutting down, it must not retain ownership of any simulated held input.

The implementation must be honest about the limits of cleanup after process termination or operating-system failure. A process cannot execute cleanup code after it has been forcibly terminated or the machine loses power.

## 3.2 Emergency stop

- Provide a global emergency-stop hotkey, configurable by the user.
- Make the emergency stop available even when the main window is unfocused.
- Give it priority over ordinary macro controls.
- Stop scheduling new actions immediately.
- Cancel active execution.
- Release all inputs owned by the engine.
- Update the UI to a stopped/error state.
- Ensure the emergency-stop path does not depend on a plugin.
- Detect hotkey conflicts and provide a clear way to reconfigure the binding.
- Explain the emergency stop during first-run onboarding.

Emergency stop must be tested under load, during holds, during drags, while paused, and while a plugin action is active.

## 3.3 Failure and shutdown behavior

- Every background worker must have an explicit lifecycle.
- Cancellation must be cooperative and propagated through actions.
- Shutdown must stop new work, cancel current work, release owned inputs, stop hooks/timers, shut down plugins, flush bounded logs, and then exit.
- Plugin exceptions must not crash the entire application unless a process-level failure makes continued operation unsafe.
- Broken configuration or plugins must not make core startup impossible.
- Errors must be visible and actionable; do not silently swallow exceptions.
- Avoid claiming recovery from hard process termination. Document residual risks.

## 3.4 Responsible automation

The application should automate actions the user is authorized to perform. It must not advertise stealth, anti-cheat bypass, access-control circumvention, or evasion of application protections. Plugins must follow the same product boundary.

---

# 4. Core Domain Model

## 4.1 Macro document

Define a versioned, validated, declarative macro format. A macro document should include:

- Format version.
- Stable macro identifier.
- Display name and optional description.
- Authoring metadata where useful.
- Coordinate/display assumptions where relevant.
- Action/control-flow tree.
- Optional user-defined variables and defaults.
- Optional execution settings that are safe to persist.
- Plugin dependencies and required capability/API versions, when applicable.

Do not serialize live threads, delegates, UI objects, native handles, plugin instances, or runtime state.

## 4.2 Example document

This is illustrative, not the final schema:

```json
{
  "formatVersion": 1,
  "id": "sample-click-sequence",
  "name": "Sample Click Sequence",
  "actions": [
    {
      "type": "mouse.click",
      "button": "left",
      "coordinateMode": "absolute",
      "x": 500,
      "y": 300
    },
    {
      "type": "delay",
      "durationMs": 100
    },
    {
      "type": "mouse.click",
      "button": "right"
    }
  ]
}
```

The final schema must define required fields, optional fields, defaults, value ranges, coordinate semantics, and compatibility rules.

## 4.3 Validation

Before execution:

- Validate document version and structure.
- Validate action types and required parameters.
- Validate numeric ranges and timing limits.
- Validate control-flow structure and references.
- Check required plugin capabilities and versions.
- Detect missing or disabled actions.
- Produce user-readable errors that identify the affected block/action.
- Do not begin execution when validation fails.

## 4.4 Versioning and migration

- Macro documents must have a format version.
- Plugin API versions must be distinct from macro format versions.
- Add migration tests whenever a format migration is introduced.
- Preserve unknown optional metadata where safe and specified.
- Never silently reinterpret an old document in a way that changes its meaning.

---

# 5. Phased Development Roadmap

Development proceeds in order. Each phase contains milestones. The agent must stop at every milestone checkpoint and wait for human approval before continuing. A human may authorize multiple explicitly named milestones in one instruction, but the default is one milestone at a time.

## Phase 0 — Requirements, Architecture, and Project Foundation

**Objective:** Resolve foundational decisions before implementing product features.

### Deliverables

1. Confirm supported Windows versions and target .NET LTS.
2. Choose WPF or WinUI 3 using a small technical evaluation.
3. Decide the WebView2/Blockly integration strategy without implementing the full editor.
4. Define solution and project boundaries.
5. Define dependency direction and prohibit circular references.
6. Define macro document model and initial schema.
7. Define input ownership, cancellation, emergency stop, and shutdown invariants.
8. Define timing measurement methodology.
9. Define plugin lifecycle, trust model, API versioning, and registration model.
10. Define logging, configuration, persistence, and error-reporting strategy.
11. Define automated test projects and manual acceptance test format.
12. Define visual design direction and basic accessibility requirements.
13. Create initial documentation.

### Required documentation

- `docs/ARCHITECTURE.md`
- `docs/DEVELOPMENT.md`
- `docs/TESTING.md`
- `docs/PLUGIN_SDK.md` (initial design)
- `docs/UX_GUIDELINES.md`
- `IMPLEMENTATION_STATUS.md`

### Acceptance criteria

- Proposed architecture and dependency graph reviewed by the human.
- Key interfaces and subsystem responsibilities documented.
- Macro schema draft documented with examples.
- Plugin trust limitations documented.
- Timing claims and benchmark plan documented.
- Solution builds.
- Minimal application launches.
- Minimal automated test runs.
- No premature feature implementation.

**Human checkpoint:** Approve the architecture, project layout, technology choices, data model, and UX direction.

## Phase 1 — Core Windows Input Engine

**Objective:** Implement reliable input primitives and lifecycle management independently of the UI.

### Milestones

- **1.1 Native interop:** Correctly defined and isolated Windows `SendInput` structures, constants, and error handling.
- **1.2 Input primitives:** Mouse click/down/up, movement, wheel, keyboard key-down/up, and supported modifier handling.
- **1.3 Input State Manager:** Ownership tracking, release-all behavior, idempotency, and cleanup paths.
- **1.4 Timing subsystem:** Monotonic clock, scheduler prototype, measurement instrumentation, and benchmark harness.
- **1.5 Execution worker:** Dedicated worker lifecycle, cancellation, synchronization, and bounded communication with other subsystems.
- **1.6 Global hotkeys:** Register/unregister configurable start, pause/resume, stop, and emergency-stop hotkeys. Prefer `RegisterHotKey` for simple global shortcuts unless requirements demonstrate a need for a low-level hook.
- **1.7 Hardening:** Shutdown, exception handling, stress tests, and diagnostics.

### Technical requirements

- Isolate P/Invoke definitions and native structures.
- Verify structure layout and architecture assumptions.
- Handle DPI awareness and multiple monitors deliberately.
- Define absolute and relative coordinate behavior.
- Do not confuse physical input with injected input.
- Keep global hook callbacks, if used, extremely short.
- A `WH_KEYBOARD_LL` hook requires an appropriate message loop and careful hook lifetime management; do not use it by default when `RegisterHotKey` suffices.
- Do not block the UI thread.
- Do not allow multiple execution workers to race over shared input state.

### Acceptance criteria

- Unit tests cover each supported input primitive and error path that can be tested without physical desktop interaction.
- Input State Manager tests verify held-input tracking and cleanup.
- Emergency stop is exercised during click, hold, drag, pause, and repeated execution.
- Worker starts and stops cleanly.
- Hotkeys register, detect conflicts, and unregister.
- Performance harness records requested and actual submission times.
- Build and relevant test suite pass.
- Manual tests confirm expected behavior on a real Windows desktop.

**Human checkpoint:** Approve the input engine and its safety behavior before macro sequencing begins.

## Phase 2 — Macro Execution Engine

**Objective:** Execute validated macro documents through a deterministic, cancellable action pipeline.

### Deliverables

1. Macro document model and JSON serialization.
2. Action descriptor and execution contracts.
3. Sequential action runner.
4. Click, move, key, hold/release, wheel, and delay actions.
5. Repeat/loop primitives.
6. Pause/resume semantics.
7. Cancellation and emergency-stop propagation.
8. Execution state and progress events.
9. Validation before execution.
10. Runtime diagnostics.

### Requirements

- Keep runtime execution state separate from persisted macro data.
- Define precise pause semantics: whether elapsed delays pause, what happens to held inputs, and how resume proceeds.
- Define behavior for stop during every action type.
- Ensure loop cancellation is responsive.
- Prevent invalid or unbounded control flow from hanging the application.
- Expose progress without forcing the UI onto the execution thread.
- Use bounded event/log queues where appropriate.
- Do not execute arbitrary code embedded in macro JSON.

### Acceptance criteria

- Programmatically constructed macros execute in the expected order.
- Save/load round trips preserve meaning.
- Invalid macros fail validation before execution.
- Pause/resume and cancellation tests pass.
- Nested or repeated sequences behave according to documented semantics.
- Failures release owned inputs.
- No UI is required to test execution.

**Human checkpoint:** Approve a headless, testable macro engine.

## Phase 3 — Macro Recording

**Objective:** Capture physical user input and produce editable macro documents.

### Deliverables

- Recording lifecycle and state model.
- Mouse button and keyboard capture.
- Optional mouse movement capture.
- Mouse-wheel capture.
- Timestamp capture using a monotonic clock.
- Filtering of the application's own injected events where supported.
- Configurable sampling and movement simplification.
- Raw recording retention or a reversible optimization workflow.
- Conversion from captured events to macro actions.
- Save/load of recorded macros.

### Recording options

- Capture keyboard input.
- Capture mouse buttons.
- Capture mouse movement.
- Capture mouse wheel.
- Movement sampling frequency.
- Minimum movement distance.
- Movement simplification tolerance.
- Ignore or include modifier keys.
- Recording countdown and stop hotkey.

### Requirements

- Clearly explain which input is being recorded.
- Avoid recording the application's own replayed input as new physical input.
- Avoid unbounded event accumulation; define memory limits and long-recording behavior.
- Preserve event ordering and timestamps.
- Handle hook installation failure and cleanup.
- Do not perform expensive processing inside low-level hook callbacks.

### Acceptance criteria

- Known input sequences can be recorded, saved, loaded, and replayed.
- Ordering and timing are preserved within measured tolerances.
- Injected-input filtering is tested.
- High-frequency mouse movement does not cause unbounded memory growth.
- Recording can be cancelled and stopped cleanly.

**Human checkpoint:** Approve recording and replay without a graphical editor.

## Phase 4 — Plugin SDK and Host

**Objective:** Allow trusted extensions to add capabilities without changing core code.

### Initial contracts

Start small. Exact names are subject to Phase 0 design.

```csharp
public interface IMacroPlugin
{
    PluginMetadata Metadata { get; }
    void Initialize(ICoreHost host);
    void Shutdown();
}
```

Possible capability interfaces:

```csharp
public interface IMacroActionProvider
{
    IEnumerable<MacroActionDescriptor> GetActions();
}

public interface IMacroTriggerProvider
{
    IEnumerable<MacroTriggerDescriptor> GetTriggers();
}

public interface IMacroBlockProvider
{
    IEnumerable<MacroBlockDescriptor> GetBlocks();
}
```

Do not implement all possible interfaces speculatively. Add contracts only when an actual extension point needs them.

### Plugin manifest

Illustrative only:

```json
{
  "id": "com.example.humanclicks",
  "name": "HumanClicks",
  "version": "1.0.0",
  "apiVersion": "1.0",
  "entryAssembly": "HumanClicks.dll",
  "entryType": "HumanClicks.Plugin"
}
```

### Deliverables

- Plugin metadata and manifest validation.
- Plugin discovery under `/Plugins/`.
- API compatibility checks.
- Plugin lifecycle and registration.
- Action and block descriptor registration.
- Event subscription/unsubscription.
- Plugin enable/disable behavior.
- Diagnostics and failure reporting.
- Shutdown behavior.
- SDK documentation and sample plugin.

### Trust and isolation

In-process .NET plugins are trusted code and are not a security sandbox. They can generally execute with the application's permissions. Clearly warn users before loading third-party plugins. Do not imply that `AssemblyLoadContext` provides security isolation. Consider out-of-process isolation only if a future requirement justifies the complexity.

### Acceptance criteria

- Application launches without a Plugins directory.
- A separate dummy plugin registers and executes an action.
- Invalid manifests, duplicate IDs, missing dependencies, incompatible API versions, and initialization failures are handled.
- Plugin execution exceptions are reported and do not leave held inputs active.
- Plugin shutdown is called reliably.
- Plugin API documentation is sufficient to build the sample independently.

**Human checkpoint:** Approve the SDK and sample plugin before building the full UI.

## Phase 5 — Application UI and User Experience

**Objective:** Deliver a polished, understandable application for non-developers.

### 5.1 UX principles

- Start with common tasks, not a wall of settings.
- Use plain-language labels; expose technical terms in advanced settings or documentation.
- Provide sensible defaults and explain unusual settings inline.
- Make the running state unmistakable.
- Make emergency stop easy to find and understand.
- Avoid modal dialogs for routine actions.
- Use consistent block shapes, colors, icons, and interaction rules.
- Support keyboard navigation, readable contrast, and DPI scaling.
- Provide undo/redo for destructive editor operations where feasible.
- Preserve work and warn before discarding unsaved changes.

### 5.2 Initial screens

1. **Home:** Create macro, record macro, open recent macro, sample projects.
2. **Macro editor:** Action list initially; visual block editor after the basic editor works.
3. **Run view:** Current macro, state, progress, elapsed time, stop/pause controls.
4. **Macro library:** Search, sort, rename, duplicate, delete, import, export, and organize.
5. **Settings:** Hotkeys, recording, appearance, startup, storage, and advanced options.
6. **Plugin manager:** Installed plugins, version, compatibility, enabled state, permissions/trust warning, and errors.
7. **Mini Mode:** Compact status and essential controls, always with a clear stop affordance.

### 5.3 Block editor

The editor is a core product differentiator, but it must remain a view/editor over the application's macro document model.

Initial blocks:

- Click mouse button.
- Move mouse.
- Press/release key.
- Hold/release mouse button.
- Wait.
- Repeat N times.
- Repeat until condition.
- If/else.
- Comment/annotation.

Later blocks:

- Variables and simple expressions.
- Call another macro.
- Random choice.
- Window/application conditions.
- Pixel color conditions.
- Plugin-provided actions, triggers, and conditions.

Requirements:

- Blocks have friendly names, clear inputs, defaults, and inline help.
- Invalid or missing inputs are highlighted before execution.
- Users can duplicate, delete, reorder, and group blocks.
- The editor supports undo/redo.
- The editor can show a readable summary or preview of the macro.
- Block definitions map to stable action identifiers and schema versions.
- Plugin blocks are registered through the SDK, not hard-coded into the editor.
- The editor must not directly inject input or execute macros.
- Blockly/WebView2 integration must have a narrow, documented bridge to the host.

### 5.4 UI milestone sequence

- **5.1:** Application shell, navigation, theme, settings foundation.
- **5.2:** Functional list-based macro editor.
- **5.3:** Macro library, save/load, import/export.
- **5.4:** Run controls, status, progress, diagnostics.
- **5.5:** Plugin manager.
- **5.6:** Blockly visual editor and host bridge.
- **5.7:** Mini Mode, tray behavior, polish, accessibility.

### Acceptance criteria

- A new user can create and run a simple macro without reading developer documentation.
- A user can record, save, reopen, edit, and replay a macro.
- UI remains responsive during sustained macro execution.
- Stop and emergency stop remain accessible.
- Invalid blocks show actionable explanations.
- Settings and macros persist across restart.
- Plugin-provided blocks appear and behave through the documented extension path.
- Manual usability review is completed.

**Human checkpoint:** Approve an end-to-end usable application before release hardening.

## Phase 6 — Hardening, Packaging, and Release Candidate

**Objective:** Deliver a stable, distributable application.

### Deliverables

- Release build configuration.
- Self-contained single-file executable publishing.
- Version and assembly metadata.
- Application icon and product identity.
- Installer or portable distribution decision.
- Configuration and macro storage locations.
- External plugin directory handling.
- Crash/error logging.
- Diagnostics export.
- Graceful shutdown.
- Configuration backup and recovery.
- Release notes and user-facing documentation.

### Distribution requirements

- Core application is self-contained and does not require users to install .NET separately.
- Third-party plugins remain external assemblies in `/Plugins/`.
- The application works with no plugin directory.
- Document any WebView2 runtime requirement and choose a distribution strategy deliberately.
- Do not claim the entire product is one file if plugins, WebView2, or other external dependencies are required.
- Test signing and SmartScreen implications; do not promise that an unsigned binary will avoid warnings.

### Acceptance criteria

- Clean Windows environment test.
- Standard-user execution test.
- Missing and broken plugin tests.
- Corrupt configuration recovery.
- Interrupted execution and restart tests.
- Multi-monitor and DPI tests.
- Release build and automated tests pass.
- User documentation and known limitations are included.

**Human checkpoint:** Approve the release candidate and distribution format.

## Phase 7 — First-Party and Community Plugins

**Objective:** Demonstrate the SDK with useful, independently maintained extensions.

### First-party example: HumanClicks

Potential capabilities:

- Configurable statistical delay variation.
- Coordinate variation within explicit user-defined boundaries.
- Bézier movement paths.
- Movement speed profiles.
- Acceleration/deceleration profiles.
- Reusable movement presets.

Requirements:

- Do not claim that statistical randomness automatically creates human behavior.
- Allow reproducibility in tests using seeded random generators.
- Respect configured coordinate boundaries.
- Integrate through plugin contracts.
- Support cancellation and cleanup.
- Do not market features as anti-cheat or security-control evasion.

### Additional plugin ideas

- Pixel color triggers.
- Template/image matching.
- Window/application focus triggers.
- Window position/size conditions.
- Sound and visual cues.
- Additional input devices, if justified.
- Specialized domain-specific action packs.

**Human checkpoint:** Approve each plugin independently. A plugin release must not destabilize the core application.

---

# 6. Feature Backlog and Prioritization

Priorities are provisional. Do not implement backlog items until authorized in the roadmap.

| Feature | Priority | Notes |
|---|---|---|
| Save/load and profiles | Essential | Core usability |
| Recording and replay | Essential | Core workflow |
| Repeat/loop blocks | Essential | Fundamental automation |
| Conditional blocks | High | Advanced workflows |
| Variables | High | Reusable logic |
| Subroutines/call macro | High | Reuse |
| Import/export | High | Sharing and backup |
| Window focus detection | High | Context awareness |
| Pixel color triggers | Medium-high | Reactive automation |
| Execution history | Medium | Troubleshooting |
| Undo/redo | High for editor | Prevents lost work |
| Template matching | Medium | More complex and resource-intensive |
| Multi-target coordinate lists | Medium | Useful for repetitive workflows |
| Speed ramping | Medium | Timing feature |
| Profile auto-switching | Medium | Requires reliable window identification |
| Sound/visual cues | Medium | Polish and accessibility |
| Macro scheduling | Later | Requires careful lifecycle and consent UX |
| Plugin marketplace | Later | Security, trust, distribution burden |
| Cloud sync/accounts | Not initial | Avoid unnecessary service dependency |

Additional macro capabilities to consider after the basic editor:

- Sequential, random, weighted-random, and random-without-replacement selection.
- Repeat a fixed number of times or until a condition is met.
- Variables and basic expressions.
- Call another macro/subroutine.
- Action-level error handling and timeouts.
- Execution history and diagnostic export.

---

# 7. Testing and Quality Strategy

Every milestone must define tests before implementation is considered complete.

## 7.1 Test categories

1. **Unit tests:** Individual classes and algorithms in isolation.
2. **Integration tests:** Interactions among engine, recorder, plugin host, persistence, and UI services.
3. **Performance tests:** Scheduling error, submission duration, CPU, memory, and sustained operation.
4. **Manual acceptance tests:** Real desktop input, global hotkeys, multi-monitor behavior, DPI, usability, and packaging.
5. **Compatibility tests:** Supported Windows versions, runtime versions, plugin API versions, and macro schema migrations.

## 7.2 Required benchmark reporting

For each benchmark, record:

- Hardware and CPU.
- Windows version/build.
- .NET runtime version.
- Build configuration.
- Test duration and sample count.
- Requested interval or workload.
- P50/P95/P99/max scheduling error.
- Input submission duration.
- CPU and memory use.
- Known environmental interference.

Do not compare benchmark results unless test conditions are sufficiently similar.

## 7.3 Acceptance criteria template

Each milestone must have explicit, verifiable acceptance criteria. Avoid statements such as "works smoothly" or "is reliable" without a testable definition.

Example Phase 1 checklist:

- Native interop definitions are isolated and reviewed.
- Each input primitive has appropriate tests.
- Held-input ownership is tracked centrally.
- Emergency stop releases all inputs owned by the engine.
- Cancellation cannot leave a held input behind in tested failure paths.
- The execution worker has a defined lifecycle.
- Hotkeys register and unregister safely.
- Timing benchmark reports P50/P95/P99/max.
- No UI-thread scheduling dependency exists in the execution loop.
- Shutdown during active execution is tested.
- Release build succeeds.
- Manual desktop tests are documented and completed by the human.

A checklist being checked is not proof by itself. Link or summarize the actual test output and manual observations.

---

# 8. AI Development Rules

These rules apply to Cursor, Codex, and any other AI coding agent.

## 8.1 Scope control

1. Work only on the currently authorized phase and milestone.
2. Never jump ahead to future phases.
3. Do not add unrequested features.
4. Do not refactor unrelated code.
5. Do not introduce unnecessary abstractions, frameworks, dependencies, or architectural complexity.
6. If the approved architecture blocks implementation, stop and explain the conflict.
7. Do not silently change requirements or acceptance criteria.
8. Do not declare a phase complete without human approval.

## 8.2 Version control restrictions

1. Never execute Git commands or other version-control operations.
2. Never create commits, branches, tags, or remote repository changes.
3. Never interact with GitHub or other remote repositories.
4. Version control and backups are exclusively the human developer's responsibility.

## 8.3 Command and filesystem restrictions

1. Do not execute commands that modify the host operating system or unrelated projects.
2. Do not install system-wide dependencies, modify environment variables, or change machine configuration without explicit approval.
3. Do not launch installers or modify system-level configuration without approval.
4. Build, test, and diagnostic commands are permitted only within the authorized project scope.
5. Never perform destructive filesystem operations without explicit approval.
6. Do not access unrelated user files, credentials, or private data.
7. Do not access the network or remote repositories unless the human explicitly authorizes a specific task requiring it.

## 8.4 Implementation standards

1. Use clear, descriptive naming.
2. Document public interfaces and non-obvious behavior.
3. Keep UI work off timing-critical threads.
4. Give every background worker explicit lifecycle and shutdown behavior.
5. Dispose of external resources correctly.
6. Handle exceptions at appropriate subsystem boundaries.
7. Do not silently swallow exceptions.
8. Do not use placeholder production implementations unless explicitly authorized.
9. Do not claim functionality works unless implementation and tests support the claim.
10. Prefer simple, maintainable solutions over speculative optimization.

## 8.5 Testing standards

1. Add appropriate automated tests for new functionality.
2. Run relevant existing tests after changes.
3. Do not weaken, delete, or bypass tests to obtain a passing result.
4. Report build errors, warnings, failing tests, and skipped tests honestly.
5. Distinguish automated verification from manual tests requiring the human.
6. Never fabricate test output or performance measurements.
7. Do not change acceptance criteria to accommodate incomplete work.

## 8.6 Dependency management

1. Document the purpose of each new dependency.
2. Prefer maintained, established libraries.
3. Evaluate license, security, distribution, and compatibility implications.
4. Document native Windows API dependencies.
5. Obtain approval before introducing a new major framework or architectural dependency.

## 8.7 Checkpoint behavior

At the end of every milestone, stop and provide the checkpoint report defined in Section 9. Do not continue until the human explicitly approves the next milestone.

---

# 9. Standard Development Checkpoint Report

The AI must use this report after each milestone.

```text
# Development Checkpoint Report

Project: ClickStitch
Phase:
Milestone:
Status: Awaiting Human Review

## 1. Implementation Summary
What was implemented and how it works.

## 2. Files Changed
Every created, modified, or deleted file and its purpose.

## 3. Architecture
How the implementation interacts with existing subsystems.
Any design decisions made within the approved architecture.

## 4. Build Results
- Configuration:
- Command:
- Status:
- Warnings:
- Errors:

## 5. Automated Test Results
- Tests executed:
- Passed:
- Failed:
- Skipped:
- Relevant output:

## 6. Performance Results
Measurements, environment, methodology, and comparison to established targets.
Write "Not applicable" if the milestone has no performance requirements.

## 7. Known Issues
Incomplete functionality, limitations, technical debt, and unresolved concerns.

## 8. Manual Testing Instructions
Exact steps the human should perform, expected results, and observed results if already tested.

## 9. Specification Compliance
Deviations from the specification and reasons. State "None" if there are no deviations.

## 10. Next Milestone
Describe the next milestone, but do not implement it.

STOP: Wait for explicit human approval.
```

---

# 10. Project Documentation and Status Management

Maintain these files:

- `SPECIFICATION.md`: Authoritative requirements, roadmap, and acceptance criteria.
- `AI_INSTRUCTIONS.md`: AI operating rules and checkpoint requirements.
- `IMPLEMENTATION_STATUS.md`: Current phase/milestone, completed work, test status, known issues, and approved deviations.
- `docs/ARCHITECTURE.md`: System boundaries, dependency graph, threading, lifecycle, and major decisions.
- `docs/DEVELOPMENT.md`: Build/test instructions and local development requirements.
- `docs/TESTING.md`: Test strategy, manual test procedures, benchmark methodology.
- `docs/PLUGIN_SDK.md`: Plugin contracts, lifecycle, manifest, compatibility, and sample plugin guidance.
- `docs/UX_GUIDELINES.md`: Design principles, terminology, interaction patterns, accessibility, and visual consistency.

`IMPLEMENTATION_STATUS.md` must be updated at approved checkpoints. The AI must not mark a milestone human-approved unless the human explicitly says so.

---

# 11. Definition of Done

The initial product is ready for release when:

- A non-developer can create a basic macro without writing code.
- Users can record, edit, save, load, and replay macros.
- The block editor uses the same macro model as other authoring paths.
- Execution can be paused, resumed, cancelled, and emergency-stopped.
- Held-input cleanup is implemented and tested for supported failure paths.
- The application provides clear state, progress, and error feedback.
- Plugins can extend documented capabilities without core modifications.
- Missing or broken plugins do not prevent core startup.
- Settings and macros persist reliably.
- The application is responsive and accessible at supported display scales.
- Release packaging works on a clean supported Windows environment.
- Documentation covers ordinary use, troubleshooting, and plugin development.
- Known limitations are documented honestly.

The definition of done does not require every item in the future feature backlog.

---

# 12. Product Design Direction: Polish, Charm, and Usability

The application should feel welcoming and thoughtfully crafted. Avoid the visual language of a generic system utility or an overly technical IDE.

## 12.1 Visual identity

Establish a consistent design system during Phase 0:

- A restrained color palette with one recognizable accent color.
- Clear typography and hierarchy.
- Consistent corner radii, spacing, icon style, and control sizing.
- Distinct but accessible colors for block categories.
- Subtle animation that reinforces state changes without slowing interaction.
- Light and dark themes if they can be implemented consistently.
- A distinctive application icon and coherent naming/branding.

Do not overuse gradients, glow effects, excessive shadows, or animation. Charm should come from thoughtful details, not visual noise.

## 12.2 Friendly language

Prefer user-facing labels such as:

- "Wait" instead of "Delay primitive."
- "Repeat" instead of "Loop construct."
- "When this happens" instead of "Trigger condition."
- "Stop macro" instead of "Cancel execution context."

Advanced settings can expose technical terminology with explanations.

## 12.3 Delightful but practical details

Consider:

- A small sample macro gallery.
- A first-run interactive tutorial.
- Helpful empty states.
- Tooltips that explain blocks in plain language.
- A live, readable macro summary.
- A clear timeline or progress indicator during execution.
- A reassuring but unmistakable emergency-stop control.
- Undo/redo and autosave indicators.
- Friendly error messages with a "show details" option.
- Optional completion sounds and visual cues.
- A compact Mini Mode that does not obscure the user's work.

## 12.4 Usability principles

- Common actions should require minimal navigation.
- Do not expose every advanced option at once.
- Avoid requiring users to understand coordinates when a capture/select-on-screen workflow can be offered.
- Clearly distinguish screen coordinates, window-relative coordinates, and other coordinate modes.
- Provide a preview or explanation before executing complex macros.
- Make destructive actions reversible where practical.
- Do not hide important safety controls behind menus.
- Test with people who have not seen the application before.

---

# 13. Initial Development Instruction

When development begins, the AI agent must:

1. Read `SPECIFICATION.md`, `AI_INSTRUCTIONS.md`, and `IMPLEMENTATION_STATUS.md`.
2. Confirm the currently authorized phase and milestone.
3. Identify any unresolved decisions that block that milestone.
4. Work only on the authorized scope.
5. Make the smallest coherent implementation.
6. Build and test within the authorized scope.
7. Produce the standard checkpoint report.
8. Stop and wait for human approval.

**Initial authorization:** Phase 0 only. Do not implement the input engine or product features until Phase 0 has been reviewed and approved.
