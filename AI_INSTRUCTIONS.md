# AI Instructions — ClickStitch

These instructions are mandatory for every AI coding agent working on this repository.

## Before doing anything

1. Read `SPECIFICATION.md`.
2. Read `IMPLEMENTATION_STATUS.md`.
3. Read `docs/ARCHITECTURE.md` and other relevant project documentation if they exist.
4. Identify the exact phase and milestone currently authorized.
5. If the status is ambiguous, ask the human. Do not guess.

## Scope

- Work only on the explicitly authorized milestone.
- Do not implement future phases.
- Do not add features that were not requested.
- Do not refactor unrelated code.
- Do not silently change architecture, requirements, or acceptance criteria.
- If a conflict or missing requirement blocks progress, stop and explain it.

## Version control and remote access

- Never run Git or other version-control commands.
- Never create commits, branches, tags, or remote changes.
- Never access GitHub or another remote repository.
- Never access the network unless the human explicitly authorizes a specific task that requires it.
- Backups and version control are the human developer's responsibility.

## Commands and filesystem

- Build, test, and diagnostic commands are allowed only within the authorized project scope.
- Do not install system-wide dependencies or modify machine configuration without approval.
- Do not launch installers or alter system-level configuration without approval.
- Do not perform destructive filesystem operations without explicit approval.
- Do not inspect unrelated user files, credentials, or private data.

## Engineering standards

- Keep the execution engine independent of the UI.
- Keep blocking work off timing-critical threads.
- Give background workers explicit lifecycle and shutdown behavior.
- Track and release all simulated inputs owned by the application.
- Propagate cancellation and handle errors at subsystem boundaries.
- Do not silently swallow exceptions.
- Document public contracts and non-obvious behavior.
- Avoid premature optimization and unnecessary abstractions.
- Do not claim functionality works unless implementation and tests support that claim.
- Do not treat in-process plugins as sandboxed.

## Testing

- Add appropriate tests for new behavior.
- Run relevant existing tests after changes.
- Do not weaken or remove tests to obtain a passing result.
- Report actual build output, warnings, test counts, failures, and skipped tests.
- Never fabricate performance results.
- Clearly identify manual tests that require the human.

## Checkpoints

At the end of each milestone, stop and report:

1. Implementation summary.
2. Every file created, modified, or deleted.
3. Architectural decisions made.
4. Build command and actual result.
5. Test command and actual result.
6. Performance measurements, if applicable.
7. Known issues and limitations.
8. Exact manual test instructions.
9. Specification deviations.
10. The next milestone, without implementing it.

Then wait. Do not proceed until the human explicitly approves the next milestone.

## Initial instruction

The initial authorized work is Phase 0 only. Produce the architecture, project foundation, and documentation deliverables. Do not implement the input engine or later product features. Stop for human review.
