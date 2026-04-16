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

## Phase 1: Pen-as-Instrument (pending)

### 1.1 Capture stroke kinematics
- [ ] Extend `StrokeCapture` to record per-sample: tilt, speed, normalDepth
- [ ] Create `Core/Analysis/StrokeKinematics.cs` data record

### 1.2 StrokeKinematicsAnalyzer + ShapeProfile extension
- [ ] Create `Core/Analysis/StrokeKinematicsAnalyzer.cs`
- [ ] Add additive fields to `ShapeProfile`: pressureMean/Variance/Peak/SlopeEnd, tiltMean/Variance, speedMean/Peak/TailOff, strokeSeconds
- [ ] Update `ShapeProfile.Clone`, `StateMigrator`

### 1.3 Wire into SoundMappingProfiles
- [ ] pressureMean → velocity + drive bias
- [ ] tiltMean → filterMotion offset, resonance bias
- [ ] tiltVariance → modDepth
- [ ] speedMean → attackBias inverse
- [ ] speedTailOff → releaseBias

---

## Small Wins (pending)

- [ ] SW-1: Pen sidetone AudioSource while drawing
- [ ] SW-2: Tilt-colored trail (LineRenderer tint)
- [ ] SW-3: Pressure readout on commit card
- [ ] SW-4: AudioListener on VRRigLocator.CenterEye

---

## Phase 2.5: Spatial Zones (pending)

- [ ] Create `Core/Data/SpatialZone.cs`
- [ ] Create `Core/Session/SpatialZoneController.cs`
- [ ] Default zones: DrumsFloor, BassLow, MelodyFront, HarmonyBehind, AccentsOverhead
- [ ] Auto-place new patterns into matching zone
- [ ] Zone bus FX sends in AudioEngine

---

## Phase 3: 3D Stroke Features (pending)

- [ ] Compute planarity, thrustAxis, verticalityWorld in StrokeCapture.BuildStrokeFrame
- [ ] Feed into behaviors (non-planar → shaker bed, thrust → single-shot, verticality → pitch contour)

---

## Phase 4: Conductor Gestures (pending)

- [ ] Create `Interaction/ConductorGestureRecognizer.cs`
- [ ] Create `Core/Session/OrchestratorStage.cs`
- [ ] Sway (BPM nudge), Lift (crescendo), Fade (plié), Cutoff (mute)
- [ ] Conducting-mode toggle on TransportPanel
- [ ] Two-handed conducting (left grip = apply to all zones)

---

## Change Log

| Date | Step | Files Changed | Notes |
|------|------|---------------|-------|
| 2026-04-16 | 2.1 | `Audio/InstanceVoicePool.cs` (new) | Per-instance spatialized voice pool |
| 2026-04-16 | 2.2 | `Audio/AudioEngine.cs`, `Audio/IAudioDispatcher.cs`, `UI/PatternVisualizer.cs`, `Core/PatternBehavior/Behaviors/*Behavior.cs` (×3) | instanceId routing, pool registration, behavior call sites |
| 2026-04-16 | 2.3 | `Core/Data/PatternInstance.cs` | reverbSend/delaySend/gainTrim + spatial brightness |
| 2026-04-16 | 2.4 | `Interaction/InstanceGrabber.cs` | Variable grab distance via thumbstick Y |
