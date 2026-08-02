# AGENTS.override.md

This is the closest instruction file for work inside `Artemis-Moon/`.

## Unity Project Expectations
- Follow [Artemis-Moon/.editorconfig](.editorconfig). For `*.cs`, use spaces, width `4`, `LF`, and keep `insert_final_newline = false`.
- Prefer minimal, value-preserving edits. Avoid whitespace-only churn in `.prefab`, `.unity`, `.asset`, and `.meta` files.
- Treat `Assets/_Project/` as app-owned source. Treat `Library/`, `Temp/`, `Logs/`, `obj/`, and `UserSettings/` as non-source unless the task explicitly targets them.
- Do not use prototype/bootstrap status as a justification for shortcut wiring via `GetComponent*` or `AddComponent`.

## Task Routing
- C# code, config classes, singleton dependency rules, editor helpers, or review diff workflow:
  read [../Docs/codex/csharp-conventions.md](../Docs/codex/csharp-conventions.md).
- Runtime/editor behavior, scenes, prefabs, serialized references, or bootstrap ownership:
  read [../Docs/codex/unity-runtime-rules.md](../Docs/codex/unity-runtime-rules.md).
- Post-change checks or validation gaps:
  read [../Docs/codex/validation.md](../Docs/codex/validation.md).
- Review requests:
  read [../Docs/codex/code_review.md](../Docs/codex/code_review.md).
- Gameplay design or progression context:
  read [../Docs/AGENTS.md](../Docs/AGENTS.md).

## Mandatory Rules
- Keep Unity `.meta` files in sync with asset changes.
- For stable scene/prefab-owned objects, prefer fixing missing wiring in assets over adding silent runtime fallbacks.
- When a bug could live in prefab wiring, scene overrides, or bootstrap code, identify the owning layer before patching symptoms.

## Runtime Invariant and Failure Policy
- Treat dependencies, services, and UI elements required by a feature's normal runtime contract as mandatory. Do not hide their absence with null checks, fallback values, silent returns, or degraded behavior; invariant violations must surface as exceptions.
- Keep null guards and fallbacks only for data or UI explicitly documented as optional, or for lifecycle cleanup where absence is an allowed state. Do not use them to support partially initialized or stale runtime state.
- Do not add artificial validation or fallback for required values: avoid patterns such as `string.IsNullOrWhiteSpace(requiredId) ? string.Empty`, `<missing>`, silent default substitution, or a new `?? throw new ArgumentNullException(...)` used only to replace the normal failure with a custom message. Let the existing invariant failure remain visible.
