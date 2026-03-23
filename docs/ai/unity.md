# Unity Guide

Adapted from the Unity-specific agents, rules, and standards in the Claude Code Game Studios repository and narrowed to RhythmForgeVR.

## Scope

Use this document for Unity architecture, runtime C# patterns, scene integration, performance, and project-level best practices.

## Architecture

- Prefer composition over inheritance-heavy MonoBehaviour trees.
- Use ScriptableObjects for authoring data, defaults, and reusable configuration.
- Keep data separate from behavior. Components and assets define data; runtime systems execute behavior.
- Use clear interfaces for cross-system contracts.
- Keep input, audio, drawing, and UI decoupled so they can evolve independently.
- For this project, prefer MonoBehaviours and scene services unless profiling proves DOTS is needed.

## Unity C# Standards

- Prefer `[SerializeField] private` over public fields.
- Cache component references in `Awake` or `Start`; do not perform repeated `GetComponent` work in hot paths.
- Do not use `Find`, `FindObjectOfType`, or `SendMessage` in production runtime code.
- Avoid `Update` when an event, callback, coroutine, or transport tick is sufficient.
- Use `const`, `readonly`, and small focused methods.
- Follow naming conventions already used in this repo: `PascalCase` for types and methods, `_camelCase` for private fields.

## Unity Asset And Scene Safety

- Preserve `.meta` files.
- Avoid direct YAML edits to `.unity`, `.prefab`, `.mat`, `.anim`, and similar serialized assets unless there is no safer option.
- Prefer bootstrap components and inspector wiring over large serialized scene edits.
- Keep generated folders out of scope: `Library`, `Temp`, `Logs`, `obj`, `Build`, `Builds`, `UserSettings`, `.vs`.

## Performance Rules

- Avoid allocations in hot paths.
- Pool frequently reused runtime objects.
- Use non-alloc APIs where practical.
- Measure before and after optimization; do not cargo-cult micro-optimizations.
- For VR, stability matters more than feature sprawl. Favor predictable frame time over visual excess.

## Input And XR

- Prefer input abstraction over direct hardcoded keyboard-only or controller-only logic.
- Desktop fallback controls are acceptable for prototyping, but they should be adapters around the same runtime APIs.
- Respect existing vendor boundaries under `Assets/Logitech`, `Assets/Oculus`, `Assets/XR`, and `Assets/XRI`.

## Gameplay And Runtime Rules

- Keep gameplay values configurable rather than scattered magic numbers.
- Use explicit interfaces and state boundaries.
- UI should not directly own game state.
- Runtime systems should expose clear testable methods where possible.
- Document any user-facing behavior change that affects rhythm, melody, mixing, or instrument workflow.

## Audio-Specific Unity Notes

- The current runtime audio core is in `Assets/Features/Audio`.
- Keep `RhythmSoundEngine` as the central control surface.
- Input systems such as keyboard drivers or MX Ink adapters should call the engine API, not duplicate playback logic.
- If sample loading or preset workflow changes, update `Assets/Features/Audio/README.md` in the same task.

## References

Read these alongside this guide when relevant:
- `docs/ai/unity-ui.md`
- `docs/ai/unity-shaders-vfx.md`
- `docs/ai/unity-addressables.md`
- `docs/ai/unity-dots.md`
- `docs/ai/audio.md`
- `docs/ai/testing.md`
