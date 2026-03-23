---
name: rhythmforge-mxink-integration
description: Use for Logitech MX Ink stylus, drawing, stroke capture, and gesture-to-audio integration work in RhythmForgeVR, especially under Assets/Logitech and scene input wiring.
---

# RhythmForge MX Ink Integration

## When To Use

Use this skill when the task touches Logitech MX Ink input, stroke capture, drawing behavior, stylus handlers, or the bridge between drawn shapes and musical actions.

Typical triggers:
- Editing `Assets/Logitech/Scripts/LineDrawing.cs`
- Editing `Assets/Logitech/Scripts/StylusHandler.cs`
- Editing `Assets/Logitech/Scripts/VrStylusHandler.cs`
- Converting strokes, shapes, or pressure data into rhythm or melody actions
- Wiring MX Ink interactions into audio engine calls or scene feedback

## Project Intent

RhythmForge is not using MX Ink as a generic pointer. The stylus is the main instrument input.

Protect these design assumptions:
- Stroke shape can become rhythm or melody structure.
- Pressure should remain available for musical dynamics or effect intensity.
- Tilt and spatial movement should remain available for expressive control.
- Input adapters should stay separable from the core audio engine.

## Working Rules

- Keep stylus capture and audio playback responsibilities separated.
- Prefer translating gesture output into engine calls rather than embedding synthesis or sample logic inside Logitech scripts.
- Maintain a desktop fallback path for testing when possible.
- If a feature depends on scene objects or inspector wiring, document the required setup clearly.

## Integration Pattern

A good boundary in this repo is:
1. Logitech script captures stroke, shape, pressure, tilt, or trigger intent.
2. A translator or controller interprets that gesture.
3. The translator calls `RhythmSoundEngine` methods.
4. Visual feedback stays local to the drawing or scene layer.

## Related Skills

- Use `rhythmforge-audio-engine` for engine-side playback or preset changes.
- Use `rhythmforge-concept-sync` if interaction changes affect the intended product experience.
- Use `rhythmforge-unity-vr` for scene and XR workflow constraints.
