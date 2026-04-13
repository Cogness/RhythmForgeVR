# RhythmForge VR Developer Architecture Snapshot

See also: [RhythmForgeVR_VR_UI_User_Guide.md](./RhythmForgeVR_VR_UI_User_Guide.md)

## Scope

This is a current-state architecture snapshot for the Unity implementation under `Assets/RhythmForge` and the Logitech stylus integration under `Assets/Logitech`. Older concept docs in `_reference-docs/Pilot` and `_reference-docs/Rythm Solution` are background material only.

## High-Level Summary

RhythmForge is a code-composed VR music tool where:

- Logitech MX Ink provides the main drawing and UI-pointing input.
- Drawn strokes are projected and normalized into 2D shape data.
- Geometry analysis derives both sequence content and sound parameters.
- Patterns are persisted separately from their scene instances.
- The sequencer plays either a single scene or an arrangement of scene slots.
- Audio is rendered procedurally at runtime.
- Pattern visuals react to real playback state during transport.

## Compact Architecture Diagram

```text
MX Ink / Left Controller
  -> InputMapper
  -> StrokeCapture / StylusUIPointer / InstanceGrabber / PanelDragCoordinator
  -> DraftBuilder
     -> StrokeAnalyzer / ShapeProfileCalculator / SoundProfileMapper / Derivers
  -> SessionStore
     -> AppState / Patterns / Instances / Scenes / Arrangement / Selection
  -> RhythmForgeManager
     -> Sequencer
        -> AudioEngine -> SamplePlayer -> ProceduralSynthesizer
        -> PatternPlaybackVisualState
     -> PatternVisualizer / Panels / Toasts
  -> SessionPersistence
```

## Runtime Assembly

## Composition Root

`RhythmForgeBootstrapper` is the code-first composition root.

It is responsible for:

- Finding the VR rig and anchors.
- Discovering `StylusHandler` through `VRRigLocator`.
- Building world-space UI panels at runtime.
- Creating interaction systems and audio/runtime services.
- Disabling Logitech sample line drawing so the app owns stroke capture.
- Positioning the main panel group in front of the user after tracking is stable.

This project is not primarily prefab-authored at the UX layer; a large part of the runtime UI is built in code.

## VR Rig And Stylus Discovery

`VRRigLocator` resolves:

- `OVRCameraRig`
- head/eye anchors
- controller anchors
- stylus handler

Logitech integration points:

- `Assets/Logitech/Scripts/StylusHandler.cs`
- `Assets/Logitech/Scripts/VrStylusHandler.cs`

The app reads the stylus through `InputMapper`, not directly from each gameplay component.

## Editor Suppression Path

The current project explicitly suppresses `VrStylusHandler` in editor contexts.

Why:

- MX Ink action bindings are device-driven.
- Logitech sample scripts should not interfere with editor testing paths.

Result:

- On-device behavior is authoritative for MX Ink validation.

## Subsystem Map

## Interaction

`InputMapper`

- Central input adapter for stylus and left controller.
- Normalizes tip pressure, middle pressure, front button, back button, back double tap, pose, trigger, grip, thumbstick, and face buttons.
- Provides consumption flags so UI clicks do not double-trigger other actions.

`StrokeCapture`

- Owns draw-start, draw-update, draw-end, 3D point collection, projection, and draft creation.
- Shows the commit-card workflow after shape derivation.

`StylusUIPointer`

- World-space stylus ray interactor.
- Current implementation clicks button controls only.

`InstanceGrabber`

- Left-controller selection and drag for committed pattern instances.
- Repositioning flows back into mix-related state recalculation.

`PanelDragCoordinator`

- Allows panel dragging with stylus back-button hold.

`DrawModeController`

- Cycles rhythm/melody/harmony creation mode.

## Session And Data

`SessionStore`

- Primary in-memory authority over `AppState`.
- Owns scenes, arrangement, patterns, instances, selected instance, active scene, queued scene, and derived effective profiles.
- Exposes mutation methods for commit, duplicate, remove, move, mute, preset override, scene selection, and arrangement edits.

`SessionPersistence`

- JSON save/load layer rooted in `Application.persistentDataPath`.

Persisted model highlights:

- `AppState`
- `PatternDefinition`
- `PatternInstance`
- `SceneData`
- `ArrangementSlot`
- `ShapeProfile`
- `SoundProfile`

Current persisted-version note:

- App-state versioning includes normalization/backfill on load.
- Recent changes include persisted world-size fields in `ShapeProfile`.

## Analysis And Derivation

`DraftBuilder`

- Main stroke-to-pattern pipeline.

`StrokeAnalyzer`

- Prepares stroke metrics from captured geometry.

`ShapeProfileCalculator`

- Computes normalized geometric features and preserved world-size metrics.
- Current fields include both topology-style features and absolute size such as `worldWidth`, `worldHeight`, `worldLength`, `worldAverageSize`, and `worldMaxDimension`.

`SoundProfileMapper`

- Maps shape features into a sound profile.
- Current implementation gives preserved size a stronger role than earlier normalized-only mappings.

`PresetBiasResolver`

- Applies preset-family bias on top of geometry-derived sound behavior.

Mode-specific derivation

- Rhythm derives drum-oriented sequence behavior from loop-like geometry.
- Melody derives note phrases from line behavior.
- Harmony derives chordal/pad behavior from broader contour characteristics.

## Playback And Audio

`Sequencer`

- Owns transport, playback timing, scene playback, arrangement playback, and playback visual state.
- Can operate in normal scene mode or arrangement mode depending on whether arrangement slots are populated.
- During arrangement mode, the playback scene is authoritative.

`AudioEngine`

- Stable external playback API used by the sequencer.

`SamplePlayer`

- Runtime pooled playback of rendered clips.
- Includes bounded cache behavior for reusing quantized voice renders.

`ProceduralSynthesizer`

- Current procedural audio backend.
- Replaces earlier fixed-bank/sample-pitch behavior with rendered drum, melody, and harmony voices.
- Uses runtime-only resolved specs rather than adding synthesis fields to persisted shape data.

## Visual And UI

`RhythmForgeManager`

- High-level runtime orchestrator.
- Loads saves, initializes panels, owns autosave cadence, handles scene/thumbstick shortcuts, and rebuilds visible pattern visuals.
- Pushes playback visual state into live visualizers during update.

`PatternVisualizer`

- Renders committed shapes in world space.
- Preserves drawn-size scaling through `ShapeProfile` world-size metrics.
- Hosts playback-reactive subvisuals such as marker and halo behavior.

`ShapeParameterLabel`

- Displays concise shape/sound summaries and metrics in-world.

Panels

- `TransportPanel`
- `SceneStripPanel`
- `ArrangementPanel`
- `InspectorPanel`
- `CommitCardPanel`
- `DockPanel`

## End-To-End Data Flow

```text
Stylus pose + pressure
-> InputMapper
-> StrokeCapture
-> 3D stroke samples
-> projected 2D stroke
-> DraftBuilder
-> ShapeProfile + SoundProfile + SequenceData
-> SessionStore commit
-> PatternDefinition + PatternInstance in scene
-> RhythmForgeManager rebuild/update
-> Sequencer schedules events
-> AudioEngine/SamplePlayer/ProceduralSynthesizer render sound
-> PatternVisualizer animates playback state
```

## Core Algorithms Snapshot

## Geometry Normalization And Feature Extraction

The stroke pipeline keeps normalized points for topology-sensitive derivation while also preserving absolute size. This split matters:

- Normalized points drive pattern structure and comparability.
- World-size metrics drive rendering scale and a stronger portion of the sound mapping.

Representative shape features:

- closedness
- circularity
- aspect ratio
- angularity
- symmetry
- vertical/horizontal span
- path length
- curvature
- wobble
- centroid height
- tilt
- direction bias

## Size-Aware Sound Mapping

Recent behavior shifted sound mapping away from relying mainly on clipped normalized span/path values.

Current intent:

- Larger drawings sound fuller, wider, and longer-lived.
- Smaller drawings sound tighter, brighter, and more immediate.
- This affects rhythm, melody, and harmony differently, but size is now a major driver in all three.

## Mode-Specific Sequence Derivation

RhythmLoop

- Uses loop-like and contour features to derive drum-oriented behavior and slot density.

MelodyLine

- Uses direction, span, curvature, and height behavior to derive note phrase shape.

HarmonyPad

- Uses broader contour character, tilt, path, and size to derive sustained chord behavior.

## Playback Scene vs Active Scene

A key current-state distinction:

- `activeSceneId` is the user-selected scene in store state.
- `playbackSceneId` is the scene actually sounding during transport.

During arrangement playback or queued scene transitions, playback visuals and playback-driven logic should follow `playbackSceneId`, not only `activeSceneId`.

## Playback-Reactive Visual State

The sequencer tracks runtime-only activity such as:

- last trigger timing
- active-until timing
- phase
- pulse
- sustain amount
- playback scene

This state is queried as a single playback visual state object and consumed by `PatternVisualizer`.

Current visual grammar:

- Rhythm emphasizes readable beat travel and hit pulses.
- Melody emphasizes directional travel and expressive tail behavior.
- Harmony emphasizes bloom, sustain, and slower aura-like motion.

## Unity / Meta / Logitech Integration Notes

Meta/OVR dependency points:

- `OVRCameraRig` discovery
- controller anchors
- VR-space panel placement

Logitech MX Ink dependency points:

- `StylusHandler`
- `VrStylusHandler`
- stylus pose and action state polling
- haptic pulse support path

Why the Logitech sample drawing is disabled:

- RhythmForge must own stroke capture, projection, derivation, and draft lifecycle.
- The sample line drawer would conflict with that ownership.

## UI Runtime Truth Table

## Fully Wired

- Runtime bootstrap and panel creation
- Drawing and draft commit flow
- Scene strip scene switching and scene queueing
- Arrangement slot scene/bar cycling
- Instance select/drag/mute/duplicate/remove
- Preset override path
- Autosave/load
- Procedural audio rendering
- Playback-reactive pattern visuals

## Partially Scaffolded Or Incomplete

- Dock panel content lists
- Some left-controller mapped buttons are present in the input layer but not major user-facing actions
- Editor stylus behavior is not a full stand-in for device behavior

## Built In Code Rather Than Prefabs

Large parts of the world-space UI are created through `RhythmForgeBootstrapper` and helper factories at runtime rather than being entirely prefab-authored.

## Persistence And Versioning

Persisted:

- app state
- patterns
- instances
- scenes
- arrangement
- shape profile
- sound profile
- preset overrides

Runtime-only:

- transport live timing
- playback visual state
- cached procedural audio clips
- current hover/raycast interaction state

Normalization responsibilities on load:

- migrate older saved versions
- backfill new fields where needed
- refresh derived summaries when schema meaning changes

## Testing Snapshot

Edit-mode coverage currently exists around several active areas, including:

- arrangement and sequencer behavior
- procedural audio rendering and cache behavior
- size-preservation and size-aware sound behavior
- playback animation state and arc-length marker behavior

Still best validated manually:

- on-device MX Ink input fidelity
- world-space VR panel ergonomics
- audio feel and musical quality
- headset readability of playback-reactive visuals

## Extension Guidance

Safest entrypoints by concern:

UI feature work

- `RhythmForgeBootstrapper`
- panel classes in `Assets/RhythmForge/UI/Panels`
- `UIFactory`

Input changes

- `InputMapper`
- `StylusUIPointer`
- `StrokeCapture`
- `InstanceGrabber`
- `PanelDragCoordinator`

Shape/music algorithm changes

- `DraftBuilder`
- `ShapeProfileCalculator`
- `SoundProfileMapper`
- mode-specific derivation helpers

Audio changes

- `AudioEngine`
- `SamplePlayer`
- `ProceduralSynthesizer`

Playback or transport changes

- `Sequencer`
- `RhythmForgeManager`
- `ArrangementPanel`
- `SceneStripPanel`

Persistence changes

- data classes under `Assets/RhythmForge/Core/Data`
- `SessionStore.NormalizeState`
- `SessionPersistence`

## Historical Reference-Only Docs

These are useful for product intent, but not for exact current runtime behavior:

- `_reference-docs/Pilot/RhythmForge-Concept-Handoff.md`
- `_reference-docs/Rythm Solution/Unity Implementation Guide.md`
- `_reference-docs/Rythm Solution/Shape-to-Sound-Algorithm.md`
