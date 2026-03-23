---
name: rhythmforge-concept-sync
description: Use when implementation, UX, scope, or feature decisions should be checked against the RhythmForgeVR concept, pitch, and reference documents under _reference-docs.
---

# RhythmForge Concept Sync

## When To Use

Use this skill when the task changes user-facing behavior, feature scope, interaction design, instrument workflow, or the overall product direction.

Typical triggers:
- New interaction concepts for rhythm, melody, or mixing
- Changes to instrument assignment or spatial UI
- Decisions about beginner friendliness versus professional depth
- Prioritization between prototype shortcuts and concept fidelity
- Writing docs, plans, or implementation notes that should reflect the pitch

## Primary References

Start with these files under `_reference-docs`:
- `Idea 2 - RhythmForge VR.md`
- `RhythmForgeVR_Project_Description.md`
- `RhythmForgeVR_Video_Pitch_Guide.md`
- `RhythmForgeVR_AI_Prompts.md`
- Concept images and HTML mockups for Rhythm Canvas, Melody Sculptor, and Spatial Mixing

## Product Pillars

Keep work aligned with these recurring ideas:
- Music is created through spatial drawing and gesture, not dense menu navigation.
- MX Ink is the defining input device, not an optional accessory.
- The app should be approachable for beginners while still useful to real producers.
- Spatial placement should have meaningful musical consequences.
- The product should feel like a creative tool, not only a toy or game effect sandbox.

## Decision Rules

When evaluating a feature, ask:
1. Does it strengthen drawing-as-music creation?
2. Does it preserve expressive use of pressure, tilt, position, or shape?
3. Does it make exportable, musically coherent output more likely?
4. Does it move the prototype toward the core pitch rather than away from it?

If a task intentionally departs from the concept for speed or technical reasons, document that tradeoff explicitly.

## Coordination

- Pair with `rhythmforge-audio-engine` for sound workflow decisions.
- Pair with `rhythmforge-mxink-integration` for stylus interaction changes.
- Pair with `rhythmforge-unity-vr` for implementation constraints in the Unity project.
