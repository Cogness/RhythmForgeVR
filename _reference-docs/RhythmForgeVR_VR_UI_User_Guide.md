# RhythmForge VR UI User Guide

See also: [RhythmForgeVR_Developer_Architecture_Snapshot.md](./RhythmForgeVR_Developer_Architecture_Snapshot.md)

## Scope

This guide describes the current Unity runtime in `Assets/RhythmForge`, not the older concept-only documents in `_reference-docs/Pilot` or `_reference-docs/Rythm Solution`. Those older docs are still useful for background, but they are not the source of truth for controls or live behavior.

## Purpose

RhythmForge VR lets you draw shapes in 3D space with Logitech MX Ink and turn them into musical patterns. The current interaction model is:

- Draw with the stylus.
- Review the generated draft card.
- Save it into the scene.
- Reposition, mute, duplicate, remove, or re-preset saved patterns.
- Control playback, scenes, and arrangement from floating world-space panels.

## Control Cheat Sheet

### MX Ink stylus

- Tip pressure: draw a stroke.
- Front button: click UI buttons with the stylus ray, or confirm a pending draft.
- Back button: discard a pending draft; when held over a panel, drag that panel.
- Back double tap: alternate discard path for a pending draft.
- Middle pressure / squeeze: available from the input layer, but not the main confirm action.
- Stylus pose: drives drawing, hovering, and UI pointing.

### Left Quest controller

- Trigger: select and drag placed pattern instances.
- Grip: exposed through input mapping; not the main selection action.
- Thumbstick left/right: switch active scene.
- `Y`: toggle play/stop.
- `X`: available through input mapping; not the primary transport action.

### Important current limitation

- The stylus UI pointer currently clicks `Button` controls only.
- That is why interactive panels are built around button-style controls instead of generic dropdown interaction.

## Startup And Orientation

At startup, `RhythmForgeBootstrapper` composes the runtime in code:

- Finds the VR rig and stylus handler.
- Builds the main panels and interaction systems.
- Disables the Logitech sample line drawing so RhythmForge owns stroke capture.
- Places the panel group in front of the user after tracking stabilizes.

Session behavior:

- The app loads the last saved session from `Application.persistentDataPath` if one exists.
- `RhythmForgeManager` autosaves roughly every 30 seconds.
- Scene, arrangement, patterns, instances, presets, and shape-derived data are persisted.

Editor/device note:

- On device, MX Ink input is expected to come from Logitech's `VrStylusHandler`.
- In the Unity Editor, MX Ink device bindings are intentionally suppressed, so stylus-specific behavior is not a full editor substitute for on-headset testing.

## Main Workflow

## Draw A Shape

1. Pick a draw mode with the transport panel mode button.
2. Press the stylus tip onto empty drawing space and move to draw.
3. Release to finish the stroke.
4. RhythmForge projects the stroke, analyzes the geometry, generates sound/music data, and shows a commit card.

What happens internally:

- The stroke is captured in 3D.
- It is projected to a 2D analysis plane.
- The system computes shape features such as span, curvature, angularity, symmetry, size, tilt, and closedness.
- It derives a musical pattern and a sound profile from those features.

## Commit The Draft

When the draft card is open:

- `Save`: commit one instance into the current scene.
- `Save + Dup`: commit and keep a copy-like workflow active for rapid repetition.
- `Discard`: throw away the draft.

Fast input equivalents:

- Stylus front button: confirm the pending draft.
- Stylus back button or back double tap: discard the pending draft.

## Switch Draw Modes

Current draw modes:

- `Rhythm`
- `Melody`
- `Harmony`

Mode switching is handled by the mode button on the transport panel. Each mode changes both the sequence derivation and the meaning of shape geometry.

## Select, Move, Duplicate, Mute, Remove

Placed patterns become scene instances.

Selection and movement:

- Aim with the left controller.
- Pull trigger on a pattern instance to select it.
- Hold and move to drag it.
- Release without moving to select.
- Release on empty space to deselect.

Inspector actions on the selected instance:

- Adjust depth.
- Toggle mute.
- Duplicate.
- Remove.
- Change preset.

Why movement matters:

- Instance position is not only visual.
- Horizontal placement affects pan.
- Depth affects mix brightness/gain behavior.
- The instance keeps its shape-derived musical identity while spatial placement changes mix perception.

## Change Scenes

The current scene can be changed in two ways:

- Click scene buttons `A-D` in the scene strip.
- Use left thumbstick left/right.

Playback behavior:

- If transport is stopped, scene changes happen immediately.
- If transport is running in normal scene mode, clicking another scene queues the change and applies it on a musical boundary.
- The strip indicates the active scene and can reflect the currently playing scene.

## Use Arrangement Playback

The arrangement panel exposes `Slot 1..8`.

Each slot has:

- A scene cycle button: `-- -> A -> B -> C -> D -> --`
- A bars cycle button: `4 -> 8 -> 16 -> 4`

Behavior:

- `--` means the slot is empty.
- Any slot with a scene assigned becomes part of the arrangement.
- On play, if at least one slot is populated, the sequencer starts from the first populated slot.
- Playback advances across populated slots only and loops across populated slots only.
- The currently driving slot is visually highlighted.

Important current behavior:

- During arrangement playback, the playback scene is authoritative for what you hear and what animates.
- Visuals and transport state follow the playing scene, not only the pre-play active scene.

## Panel Reference

## Transport Panel

Purpose:

- Main transport and mode control.

Current live controls:

- `Play / Stop`
- Draw mode button
- BPM readout
- Key readout
- Transport status text
- Parameter labels `ON/OFF`

What it affects:

- Transport starts or stops the sequencer.
- Mode changes how new strokes are interpreted.
- Parameter-label toggle changes how much diagnostic shape/sound information is shown in-world.

## Scene Strip

Purpose:

- Quick access to scenes `A-D`.

What it affects:

- Which scene is currently edited or queued.
- Which scene is immediately heard when transport is stopped.
- Which scene becomes active on the next boundary when transport is running in scene mode.

## Arrangement Panel

Purpose:

- Build an eight-slot song order from scenes.

What it affects:

- Whether transport runs as scene playback or arrangement playback.
- Which scene is driving playback over time.
- How many bars each slot lasts before advancing.

Current note:

- The panel is button-driven and VR-usable.
- It replaced an earlier unusable dropdown-style interaction.

## Inspector Panel

Shown when an instance is selected.

Current live content:

- Pattern / instance identity
- Shape DNA summary
- Shape and sound metrics
- Depth slider
- Mute toggle
- Remove
- Duplicate
- Preset selector
- Mix readout such as pan/gain/brightness

Why it matters:

- This is the main place to inspect how the drawn geometry was interpreted.
- Presets bias the sound family without replacing the geometry-derived structure.

## Dock Panel

Tabs shown:

- `Instruments`
- `Patterns`
- `Scenes`

Current-state warning:

- The dock shell exists and tab switching works.
- The actual list content is only partly wired in the current runtime.
- `Instruments` and `Patterns` depend on prefabs that are null in the current bootstrap path.
- `Scenes` does not currently have a full dynamic content refresh path in the dock.

Use the scene strip and inspector as the reliable live controls.

## Toasts And Parameter Labels

Toasts:

- Short transient messages used for feedback.

Parameter labels:

- Hovering and selection visuals can show summarized shape/sound information.
- These help explain why a shape sounds the way it does.

## How Shape Influences Music

## RhythmLoop

Typical influences:

- More closed, loop-like shapes: more rhythm-loop behavior.
- Rounder shapes: smoother, more even feel.
- Angular shapes: sharper transients and more percussive behavior.
- Wobblier or irregular loops: more instability and variation.
- Larger loops: fuller, heavier, longer-decay sound.
- Smaller loops: tighter, brighter, snappier sound.

Musically:

- Geometry drives drum density, pulse character, transient sharpness, body, and release behavior.

## MelodyLine

Typical influences:

- Height and vertical travel influence melodic contour.
- Span and direction bias influence phrase shape and register behavior.
- Curvature affects smoothness versus pointed phrasing.
- Larger drawings tend to sound fuller, wider, and more sustained.
- Smaller drawings tend to sound brighter, tighter, and more percussive.

Musically:

- The system derives note content and a tone profile together, so the shape changes both melody and timbre.

## HarmonyPad

Typical influences:

- Tilt, span, and path length affect chord feel and motion.
- Broader, more expansive shapes produce wider, slower, richer pad behavior.
- Smaller pads become drier, brighter, and narrower.

Musically:

- Harmony uses shape to derive chordal behavior plus pad-like tone characteristics such as spread, body, release, and motion.

## Size Matters

Current runtime behavior preserves drawn size.

- Small and large versions of similar shapes now stay visibly different after commit.
- That size strongly affects sound.
- Large shapes tend to feel fuller, wider, and longer-lived.
- Small shapes tend to feel tighter, brighter, and more immediate.

## Position And Depth After Spawning

After a pattern is placed:

- Horizontal position influences pan.
- Depth influences mix characteristics such as perceived gain/brightness.
- Repositioning is therefore both spatial and sonic.

## Preset Overrides vs Geometry

Geometry still defines the base pattern and sound profile.

Presets then bias the voice family:

- They color the result.
- They do not replace the underlying shape-derived structure.

This means two similar shapes can still sound different under different presets, but the shape identity remains audible.

## Playback-Reactive Visuals

During playback, currently sounding shapes animate.

- Rhythm shapes show timing-forward movement and hit pulses.
- Melody shapes show directional motion with tail-like behavior.
- Harmony shapes emphasize bloom and sustain more than per-hit flicker.

These animations are driven by the actual playback state plus the effective sound profile, so they reflect what the system is currently playing.

## Common Tasks

## Build A Simple Loop

1. Set mode to `Rhythm`.
2. Draw a closed loop.
3. Save the draft.
4. Press `Play`.
5. Add another rhythm loop or switch to `Melody` for layering.

## Create A Verse/Chorus Structure

1. Put different patterns into scenes `A-D`.
2. Open the arrangement panel.
3. Assign scenes to slots and choose slot bar counts.
4. Press `Play` to hear the arranged progression.

## Make Something Sound Wider Or Fuller

- Draw a larger shape.
- For harmony, prefer broader shapes with more span.
- Move the instance and try a different preset from the inspector.

## Make Something Tighter Or Sharper

- Draw a smaller shape.
- Use more angular geometry.
- For rhythm, prefer crisp corners or more compact loops.

## Current Limitations And Caveats

- The dock panel is partly scaffolded; do not rely on it as the primary control surface.
- The stylus UI pointer clicks button-driven controls only.
- MX Ink behavior is authoritative on device, not in the Editor.
- Older `_reference-docs` concept and pilot materials describe the product direction, not always the exact current runtime.

## Useful Historical Background

These are helpful for intent and older design rationale, but not for exact current controls:

- `_reference-docs/Pilot/RhythmForge-Concept-Handoff.md`
- `_reference-docs/Rythm Solution/Unity Implementation Guide.md`
- `_reference-docs/Rythm Solution/Shape-to-Sound-Algorithm.md`
