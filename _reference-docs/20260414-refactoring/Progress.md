# Spatial Orchestrator — Implementation Progress

Tracking incremental delivery of the [SpatialOrchestrator-Plan.md](./SpatialOrchestrator-Plan.md).
Delivery order follows the plan's Section 9: highest perceived impact first.

---

## Phase 2: True 3D Spatial Audio (DONE)

### 2.1 InstanceVoicePool.cs (new) ✅
- [x] Create `Audio/InstanceVoicePool.cs` — small per-instance pool (3 voices, capped at 4) of spatialized AudioSources
- [x] `spatialize=true`, `spatialBlend=1.0`, logarithmic rolloff, min 0.4m / max 8m
- [x] Mirrors SamplePlayer's PlayDrum/PlayNote/PlayChord API; pan param ignored (3D handles it)

### 2.2 Wire into PatternVisualizer + AudioEngine ✅
- [x] `PatternVisualizer.Initialize` creates and owns an `InstanceVoicePool` (AddComponent)
- [x] `AudioEngine` gains `Dictionary<string, InstanceVoicePool>` + Register/Unregister methods
- [x] `IAudioDispatcher` + `AudioEngine` Play methods gain `string instanceId = null`; routes to pool if found
- [x] `SamplePlayer` remains the fallback preview-only pool (`spatialize = false`)
- [x] All three `PatternBehavior.Schedule` call sites now pass `context.instance.id`

### 2.3 Replace pan/brightness/gain mapping ✅
- [x] `PatternInstance` gains `reverbSend`, `delaySend`, `gainTrim` fields (additive, old fields kept)
- [x] `RecalculateMixFromPosition` computes spatial mix: brightness from height vs head, reverbSend from depth+distance, delaySend from depth, gainTrim (shallow curve)
- [x] Legacy `pan`/`gain` preserved for SamplePlayer fallback and saved-session compat

### 2.4 Unclamp grab distance ✅
- [x] `InstanceGrabber.DragInstance` — `_grabDistanceProgress` driven by left thumbstick Y (rate 0.8/s)
- [x] Distance = Lerp(0.4, 6.0, progress); initialized from current instance distance on grab start (no snap)
- [x] Ray visual tracks actual grab depth instead of maxRayDistance

---

## Phase 1: Pen-as-Instrument (DONE)

### 1.1 Capture stroke kinematics
- [x] Extend `StrokeCapture` to record per-sample: tilt, speed, normalDepth
- [x] Create `Core/Analysis/StrokeKinematics.cs` data record

### 1.2 StrokeKinematicsAnalyzer + ShapeProfile extension
- [x] Create `Core/Analysis/StrokeKinematicsAnalyzer.cs`
- [x] Add additive fields to `ShapeProfile`: pressureMean/Variance/Peak/SlopeEnd, tiltMean/Variance, speedMean/Peak/TailOff, strokeSeconds
- [x] Update `ShapeProfile.Clone`, `StateMigrator`

### 1.3 Wire into SoundMappingProfiles
- [x] pressureMean → velocity + drive bias
- [x] tiltMean → filterMotion offset, resonance bias
- [x] tiltVariance → modDepth
- [x] speedMean → attackBias inverse
- [x] speedTailOff → releaseBias

---

## Small Wins (DONE)

- [x] SW-1: Pen sidetone AudioSource while drawing
- [x] SW-2: Tilt-colored trail (LineRenderer tint)
- [x] SW-3: Pressure readout on commit card
- [x] SW-4: AudioListener on VRRigLocator.CenterEye

---

## Phase 2.5: Spatial Zones (DONE)

- [x] Create `Core/Data/SpatialZone.cs`
- [x] Create `Core/Session/SpatialZoneController.cs`
- [x] Default zones: DrumsFloor, BassLow, MelodyFront, HarmonyBehind, AccentsOverhead
- [x] Auto-place new patterns into matching zone
- [x] Zone bus FX sends in AudioEngine
- [x] Zone sphere visuals via `UI/SpatialZoneVisualizer.cs`
- [x] `SpatialZoneChangedEvent` on `RhythmForgeEventBus`
- [x] NUnit tests: `Editor/SpatialZoneControllerTests.cs`

---

## Phase 3: 3D Stroke Features (DONE)

- [x] Compute planarity, thrustAxis, verticalityWorld in StrokeCapture.FinishStroke
- [x] Feed into behaviors (non-planar → shaker bed, thrust → single-shot, verticality → pitch contour)
- [x] Spawn-position corrections (lift horizontal, clamp head proximity)
- [x] NUnit tests: `Editor/StrokeFrame3DTests.cs`

---

## Phase 4: Conductor Gestures (DONE)

### 4.1 ConductorGestureRecognizer ✅
- [x] Create `Interaction/ConductorGestureRecognizer.cs`
- [x] Sway: ≥2 horizontal direction reversals in 2 s → BPM nudge toward sway period
- [x] Lift: slow monotonic rise ≥8 cm + ≥60% up-frames → crescendo on focused zone
- [x] Fade: slow monotonic drop ≥6 cm + pressure → 0 → fade focused zone to mute
- [x] Cutoff: single-frame lateral speed > 0.9 m/s → immediately mutes focused zone
- [x] Per-gesture cooldown (1.2 s) prevents rapid re-triggering
- [x] Only fires while `DrawPressure < 0.05` (no conflict with stroke capture)

### 4.2 OrchestratorStage ✅
- [x] Create `Core/Session/OrchestratorStage.cs`
- [x] Conductor origin tracks head with slow EMA
- [x] Focus = zone nearest stylus tip within 1.5× zone radius
- [x] Per-zone gain mods lerp toward targets each frame
- [x] Crescendo: gainTarget → 1.4, auto-returns to 1.0 after settled
- [x] Fade: gainTarget → 0, auto-restores after 4 s delay
- [x] Cutoff: gainCurrent = 0 immediately (no lerp), auto-restores after 4 s

### 4.3 Wire-up ✅
- [x] `ConductorGestureEvent` added to `RhythmForgeEventBus`
- [x] `LeftGrip` added to `IInputProvider`
- [x] `AudioEngine.SetOrchestratorStage` — gain mod applied in all three Play* methods
- [x] `TransportPanel` gains "Conduct" toggle button (default OFF)
- [x] `RhythmForgeBootstrapper` creates `OrchestratorStage`, wires to `AudioEngine` and `RhythmForgeManager`
- [x] `RhythmForgeManager.HandleConductorUpdate` ticks recognizer and dispatches gestures
- [x] Two-handed conducting: holding left grip applies gesture to all zones

### 4.4 Tests ✅
- [x] `Editor/ConductorGestureRecognizerTests.cs` — 5 tests: sway, no-sway, lift, fade, cutoff
- [x] `Editor/OrchestratorStageTests.cs` — 5 tests: focus near A, focus near B, no-focus, crescendo, fade, cutoff

---

## Change Log

| Date | Step | Files Changed | Notes |
|------|------|---------------|-------|
| 2026-04-16 | 2.1 | `Audio/InstanceVoicePool.cs` (new) | Per-instance spatialized voice pool |
| 2026-04-16 | 2.2 | `Audio/AudioEngine.cs`, `Audio/IAudioDispatcher.cs`, `UI/PatternVisualizer.cs`, `Core/PatternBehavior/Behaviors/*Behavior.cs` (×3) | instanceId routing, pool registration, behavior call sites |
| 2026-04-16 | 2.3 | `Core/Data/PatternInstance.cs` | reverbSend/delaySend/gainTrim + spatial brightness |
| 2026-04-16 | 2.4 | `Interaction/InstanceGrabber.cs` | Variable grab distance via thumbstick Y |
| 2026-04-18 | 1.1 | `Core/Analysis/StrokeKinematics.cs`, `Interaction/StrokeCapture.cs`, `Core/Session/DraftBuilder.cs` | Added `tiltXY` to KinematicPoint, `SetPlaneNormal` to StrokeKinematics; StrokeCapture passes planeNormal + kinematics to DraftBuilder; DraftResult gains `kinematics` field |
| 2026-04-18 | 1.2 | `Core/Analysis/StrokeKinematicsAnalyzer.cs` (new), `Core/Data/ShapeProfile.cs`, `Core/Analysis/ShapeProfileCalculator.cs`, `Core/Session/StateMigrator.cs` | StrokeKinematicsAnalyzer computes per-point tilt/speed + aggregate features; 10 kinematic fields added to ShapeProfile + Clone; PopulateKinematics wires into DraftBuilder; state bumped to v6 |
| 2026-04-18 | 1.3 | `Core/Data/SoundMappingProfileAsset.cs` | Kinematic weight fields added to SoundMetricWeights; Evaluate sums kinematic contributions; Rhythm/Melody/Harmony defaults wired with pressureMean, pressurePeak, tiltMean, tiltVariance, speedMean, speedTailOff |
| 2026-04-18 | 1 tests | `Editor/StrokeKinematicsAnalyzerTests.cs` (new) | 5 NUnit edit-mode tests covering empty/null, constant pressure, tilt projection, deceleration, fading pressure |
| 2026-04-18 | SW-1 | `Interaction/PenSidetoneSource.cs` (new), `Interaction/StrokeCapture.cs` | PenSidetoneSource MonoBehaviour generates looping sine AudioClip per PatternType; pressure-drives volume; wired into Configure/StartStroke/AddPoint/FinishStroke/ClearCurrentStroke |
| 2026-04-18 | SW-2 | `Interaction/StrokeCapture.cs` | AddPoint blends LineRenderer color toward amber (new Color 1f,0.55f,0.1f) based on sin(tiltAngle) — no signature change |
| 2026-04-18 | SW-3 | `UI/Panels/CommitCardPanel.cs` | ShowDraft appends "Pressure: X avg  Y peak" to _detailsText when shapeProfile.pressureMean > 0.01f; no new serialized fields |
| 2026-04-18 | SW-4 | `Bootstrap/RhythmForgeBootstrapper.cs` | EnsureAudioListenerOnHead() removes stray AudioListeners and adds one to CenterEye; called at end of RepositionPanels() |
| 2026-04-18 | 3 | `Core/Analysis/StrokeKinematics.cs`, `Interaction/StrokeCapture.cs`, `Core/Data/ShapeProfile.cs`, `Core/Analysis/ShapeProfileCalculator.cs`, `Core/Session/DraftBuilder.cs`, `Core/Session/StateMigrator.cs`, `Core/PatternBehavior/Behaviors/RhythmLoopBehavior.cs`, `MelodyLineBehavior.cs`, `HarmonyPadBehavior.cs`, `Editor/StrokeFrame3DTests.cs` (new) | Phase 3: planarity/thrustAxis/verticalityWorld computed in FinishStroke; RhythmLoop → single-shot jab + shaker bed; Melody → octave leaps / stepwise clamp; Harmony → wide / close voicing; spawn-position corrections; state v7; 7 NUnit tests |
| 2026-04-18 | 2.5 | `Core/Data/SpatialZone.cs` (new), `Core/Session/SpatialZoneController.cs` (new), `UI/SpatialZoneVisualizer.cs` (new), `Core/Events/RhythmForgeEventBus.cs`, `Audio/AudioEngine.cs`, `Core/Session/SessionStore.cs`, `RhythmForgeManager.cs`, `Bootstrap/RhythmForgeBootstrapper.cs`, `Editor/SpatialZoneControllerTests.cs` (new) | Phase 2.5 Spatial Zones: 5 named zones (DrumsFloor, BassLow, MelodyFront, HarmonyBehind, AccentsOverhead), zone FX routing in AudioEngine, auto-spawn bias in SessionStore.CommitDraft, SpatialZoneChangedEvent, translucent sphere visuals, 5 NUnit tests |
| 2026-04-18 | 4 | `Interaction/ConductorGestureRecognizer.cs` (new), `Core/Session/OrchestratorStage.cs` (new), `Core/Events/RhythmForgeEventBus.cs`, `Interaction/IInputProvider.cs`, `Audio/AudioEngine.cs`, `UI/Panels/TransportPanel.cs`, `RhythmForgeManager.cs`, `Bootstrap/RhythmForgeBootstrapper.cs`, `Editor/ConductorGestureRecognizerTests.cs` (new), `Editor/OrchestratorStageTests.cs` (new) | Phase 4: Sway/Lift/Fade/Cutoff heuristic recognizer; OrchestratorStage with per-zone gain mod + crescendo/fade/cutoff; AudioEngine gain mod in all Play*; Conduct toggle on TransportPanel; BPM nudge from sway; two-handed all-zone dispatch via LeftGrip; 9 NUnit tests |
