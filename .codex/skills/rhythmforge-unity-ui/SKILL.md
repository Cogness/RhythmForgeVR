---
name: rhythmforge-unity-ui
description: Use for UI Toolkit, UGUI, menu architecture, world-space UI, and VR-facing interface work in RhythmForgeVR.
---

# RhythmForge Unity UI

## When To Use

Use this skill for menu systems, world-space UI, hand-anchored UI, inspector-driven UI wiring, and any task that changes how players navigate or read information.

## Primary Reference

Read `docs/ai/unity-ui.md` first.

## RhythmForge Rules

- Favor UI patterns that complement spatial creation.
- Keep flat debug UI separate from the intended VR interaction model.
- UI should request actions from runtime systems; it should not own the authoritative state.
- Route UI sounds through the audio system.
