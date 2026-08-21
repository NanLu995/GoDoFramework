---
name: godo-framework-change
description: Modify a GoDo framework module with the required module-aware validation and contract updates. Use for changes under addons/godo_framework, not for ordinary questions, demo-only work, or unrelated documentation edits.
---

# GoDo Framework Change

Use this skill to deliver one scoped framework change without running unrelated
builds or regressions. Treat the repository `AGENTS.md` as the source of truth
for safety, coding style, authorization, and all project-wide requirements.

## Route the work before editing

- Confirm the requested change is limited to one concrete outcome. If it spans
  independent changes, give a short plan and wait for confirmation.
- For an existing module, read its `USAGE.md` before changing its behavior,
  public API, lifecycle, failure semantics, or performance characteristics.
- For a new module or a changed dependency direction, read
  `AI/FRAMEWORK_DESIGN_PLAN.md` and `AI/ARCHITECTURE.md` first.
- Inspect current workspace changes before edits and preserve unrelated work.
- Do not infer authorization to change project settings, dependencies, public
  API compatibility, or third-party source code.

## Implement narrowly

- Reuse the framework's existing event, log, error, resource, and pooling
  capabilities before adding another mechanism.
- Keep edits within the requested module and its direct dependencies. Do not
  refactor adjacent working code.
- For `.tscn` or node-tree changes, state the tree change before editing.
- Record whether the change affects public API, failure semantics, lifecycle,
  scene structure, or no external contract.

## Choose validation by impact

Run checks after a complete logical behavior change, not repeatedly after each
internal edit. Select only the necessary checks:

| Change type | Required validation |
| --- | --- |
| Documentation-only | Check links, examples, and source consistency; do not build solely for this change. |
| Internal C# implementation with changed behavior | Debug build, then the affected module's targeted regression when available. |
| Public API, failure semantics, lifecycle, or dependency change | Debug build, affected regression, `USAGE.md` update, and API Reference inspection. Review user-facing manual/`Docs/coverage.json` impact before updating the contract hash. |
| New or renamed `[GlobalClass]`, Godot resource, scene, importer, or editor registration | Run the applicable build and Godot Editor scan; list any manual verification required. |
| Scene, input, physics, node-lifecycle, or scene-switch behavior | Run the safe automated checks available and explicitly list the Godot manual verification still required. |

Do not substitute a full-repository regression for a module regression unless
the request, dependency impact, or a failed targeted check justifies it. Do not
claim an unrun check passed.

## Hand off

Report, in this order:

1. Files changed and the core implementation.
2. Public API and scene-tree impact, or explicitly state none.
3. Commands/checks actually run and their result.
4. Required manual Godot verification and known out-of-scope risks.
