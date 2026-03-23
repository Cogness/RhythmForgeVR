---
name: rhythmforge-unity-vr
description: Use for Unity VR scene, prefab, XR, and runtime workflow changes in RhythmForgeVR, especially under Assets, Scenes, Packages, and ProjectSettings.
---

# RhythmForge Unity VR

## When To Use

Use this skill when the task involves Unity project structure, scenes, runtime components, prefabs, XR setup, package boundaries, or editor-safe repo changes.

Typical triggers:
- Editing scripts under `Assets/**`
- Working around scene wiring without risky serialized asset edits
- Integrating new systems into `Assets/Scenes/MXInkSample.unity`
- Coordinating code with `Packages/**` or `ProjectSettings/**`
- Adding testable runtime bootstrap behavior for features that will later be driven by VR input

## Workflow

1. Read `docs/ai/unity.md` first.
2. Inspect the target Unity area before editing.
3. Prefer C# or documentation changes over hand-editing serialized Unity YAML.
4. Preserve `.meta` files whenever assets are added, removed, or moved.
5. Keep generated folders out of scope unless the user explicitly asks.
6. If scene wiring is needed, favor bootstrap scripts or minimal inspector assumptions.

## Project Boundaries

Treat these as the main domains in this repo:
- `Assets/Features/**`: project-owned feature code
- `Assets/Scenes/**`: playable scenes, especially `MXInkSample.unity`
- `Assets/Logitech/**`: MX Ink and stylus integration
- `Assets/Oculus/**`, `Assets/XR/**`, `Assets/XRI/**`: vendor and XR framework areas that should be changed carefully
- `Packages/**` and `ProjectSettings/**`: package and project configuration

## Related Specialist Guides

Read the narrower doc when the task demands it:
- `docs/ai/unity-ui.md`
- `docs/ai/unity-shaders-vfx.md`
- `docs/ai/unity-addressables.md`
- `docs/ai/unity-dots.md`
- `docs/ai/testing.md`

## Coordination

Use narrower local skills when the task is domain-specific:
- For audio engine work, use `rhythmforge-audio-engine`.
- For Logitech stylus or drawing interactions, use `rhythmforge-mxink-integration`.
- For concept or pitch alignment, use `rhythmforge-concept-sync`.
