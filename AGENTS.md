# RhythmForgeVR Agents Guide

This repository is a Unity VR project for RhythmForge VR, a spatial music creation tool built around Logitech MX Ink, Meta Quest, and a sample-driven audio workflow.

## Purpose

Use this file as the project-specific operating guide for any agent working in this repo. Keep decisions grounded in the current Unity project, the MX Ink interaction model, the product concept in `_reference-docs`, and the Unity guidance collected in `docs/ai`.

## Source Of Truth

- Gameplay and runtime code: `Assets/**`
- Audio engine and presets: `Assets/Features/Audio/**`
- Scenes: `Assets/Scenes/**`
- MX Ink and drawing integration: `Assets/Logitech/**`
- Unity package config: `Packages/**`
- Project config: `ProjectSettings/**`
- Product concept and pitch materials: `_reference-docs/**`
- AI workflow and best-practice docs: `docs/ai/**`

## Ignore And Treat As Generated

Do not spend tokens reviewing or editing generated or machine-local output unless the user explicitly asks.

- `Library/**`
- `Temp/**`
- `Logs/**`
- `obj/**`
- `Build/**`
- `Builds/**`
- `UserSettings/**`
- `.vs/**`
- `MemoryCaptures/**`
- `Assets/**/BurstDebugInformation_DoNotShip/**`
- `Assets/**/BackUpThisFolder_ButDontShipItWithYourGame/**`

## Unity Working Rules

- Preserve `.meta` files. If you add, move, or delete Unity assets, keep the matching `.meta` in sync.
- Prefer editing C# scripts, docs, and asset configuration over hand-editing scene or prefab YAML.
- Avoid direct edits to `.unity`, `.prefab`, `.mat`, `.anim`, and similar serialized assets unless there is no safer path.
- When scene wiring is required, prefer creating bootstrap scripts or inspector-driven components that minimize risky serialized changes.
- Keep runtime logic out of generated folders and vendor packages unless the task is specifically about those packages.
- Respect existing package boundaries under `Assets/Logitech`, `Assets/Oculus`, `Assets/XR`, and `Assets/XRI`.
- Follow the Unity guidance in `docs/ai/unity.md` and the specialist docs under `docs/ai/` when the task enters those domains.

## Audio Rules

- The current audio core lives under `Assets/Features/Audio`.
- Treat `RhythmSoundEngine` as the central runtime API for trigger, loop, BPM, pitch, reverb, and preset control.
- Sample presets load from `Assets/Resources/AudioPresets/<PresetName>/`.
- Slot assignment is filename-driven: clips must begin with `1` through `9`, such as `1_Kick.wav` or `5_Strings_A.wav`.
- Missing samples should degrade gracefully to the procedural fallback, not break playback.
- Keep keyboard testing support working until equivalent MX Ink or VR UI controls replace it.
- Use `docs/ai/audio.md` for higher-level audio direction and implementation boundaries.

## MX Ink And Interaction Rules

- RhythmForge is built around drawing and gesture input, not menu-heavy controller workflows.
- Pressure, tilt, and spatial stroke shape are intended to map to musical expression, so preserve those extension points when refactoring.
- Keep the audio engine decoupled from input adapters. Drawing systems should call engine APIs rather than embedding audio logic directly into stylus handlers.
- When adding loop or melody drawing features, keep desktop test paths available so features can be verified without a headset.

## Concept Alignment Rules

Check `_reference-docs` before making product-shaping decisions. The current concept emphasizes:

- Rhythm loops drawn as spatial paths
- Melody creation through drawn strokes and vertical pitch mapping
- Instrument assignment through an in-world library
- Spatial mixing by moving sound objects around the user
- Export-ready output rather than toy-only interaction

If a task changes user-facing behavior, verify it still supports those pillars or explicitly note the deviation.

## Recommended Local Skills

Use the smallest skill that matches the task.

- `rhythmforge-unity-vr`: Unity scene, prefab, XR, and runtime workflow guidance for this repo.
- `rhythmforge-unity-ui`: Unity UI Toolkit, UGUI, and VR-facing UI guidance.
- `rhythmforge-unity-shaders`: Shader, VFX, and render-pipeline guidance for VR readability and performance.
- `rhythmforge-unity-addressables`: Addressables migration and asset-loading strategy guidance.
- `rhythmforge-unity-dots`: DOTS and ECS evaluation guidance for future performance work.
- `rhythmforge-audio-engine`: Sound engine, presets, samples, transport, and audio API changes.
- `rhythmforge-audio-direction`: Higher-level audio palette, preset, event, and mix guidance.
- `rhythmforge-mxink-integration`: Logitech MX Ink drawing, stylus handlers, and gesture-to-audio integration.
- `rhythmforge-concept-sync`: Product concept, pitch, and feature alignment against `_reference-docs`.

## Delivery Expectations

- Explain assumptions when Unity editor validation is not available.
- Prefer changes that are testable in Play Mode without requiring full headset-only setup.
- Keep documentation current when adding new runtime controls, preset conventions, or integration points.
- Call out any step that still requires Unity editor wiring, imported sample assets, or headset testing.
