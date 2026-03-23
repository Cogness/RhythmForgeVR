---
name: rhythmforge-audio-engine
description: Use for RhythmForgeVR audio engine changes, including samples, presets, looping, BPM, pitch, reverb, and engine API work under Assets/Features/Audio.
---

# RhythmForge Audio Engine

## When To Use

Use this skill for any task in `Assets/Features/Audio/**` or any feature that changes how sounds are triggered, looped, mixed, loaded, or exposed to future drawing and VR controls.

## Primary References

Read these first:
- `docs/ai/unity.md`
- `docs/ai/audio.md`
- `Assets/Features/Audio/README.md`

## Current Architecture

The current sound system is sample-first with procedural fallback.

Key expectations:
- Runtime engine entry point: `RhythmSoundEngine`
- Keyboard test adapter: `KeyboardSoundEngineDriver`
- Bootstrap path for quick scene hookup: `RhythmAudioBootstrap`
- Preset loading path: `Assets/Resources/AudioPresets/<PresetName>/`
- Slot mapping: filenames must start with `1` through `9`

Examples:
- `1_Kick.wav`
- `1_Kick_Alt.wav`
- `5_StringsShort.wav`
- `9_FX_Riser.wav`

## Working Rules

- Preserve the engine API shape where possible so future drawing systems can call the same functions.
- Keep missing samples non-fatal by preserving procedural fallback behavior.
- Prefer inspector-friendly or folder-driven conventions over hardcoded absolute asset paths.
- Keep desktop keyboard audition controls working until equivalent spatial UI exists.
- If a change alters artist workflow, update `Assets/Features/Audio/README.md` in the same task.
- If a change alters higher-level audio behavior or preset direction, update `docs/ai/audio.md` too.

## Handoff To Other Systems

Future drawing or MX Ink systems should call engine methods such as:
- `TriggerSound`
- `ToggleLoop`
- `SetPreset`
- `SetBpm`
- `SetPitchSemitones`
- `SetReverb`

Do not bury audio behavior directly inside stylus handlers unless the task is specifically a temporary prototype.
