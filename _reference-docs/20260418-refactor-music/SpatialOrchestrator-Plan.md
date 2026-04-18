# RhythmForge VR — Spatial Orchestrator Plan

Status: draft, April 2026
Scope: incremental plan that builds on the current Unity runtime in `Assets/RhythmForge`. It does not propose a rewrite. Every item below names the file(s) it touches and the smallest change that unlocks it.

Priorities (set by author): **shape → sound**, **MX Ink expressivity**, **3D spatial audio / routing**. Performance/live-play ideas are called out at the end but are not the focus of this plan.

---

## 1. What the runtime actually does today

This section is the honest baseline, grounded in the code, not the concept docs.

### 1.1 MX Ink input surface

`Interaction/InputMapper.cs` exposes, per frame:

- `TipPressure`, `MiddlePressure`, `DrawPressure = max(Tip, Middle)`
- `FrontButton(Down)`, `BackButton(Down)`, `BackDoubleTap`
- `StylusPose` — full 6DoF position + rotation
- Left controller: trigger, grip, thumbstick, X/Y

### 1.2 Stroke capture

`Interaction/StrokeCapture.cs`:

- Captures 3D world points while `DrawPressure > 0.05`.
- Stores `_pressures` per-sample, but the only use of pressure is the `LineRenderer` width curve.
- Once the stroke ends, `BuildStrokeFrame` fits a best plane through the 3D points, then `ProjectTo2D` projects to that plane. **Every out-of-plane excursion is discarded here.**
- The 2D projection is passed to `DraftBuilder.BuildFromStroke`.

### 1.3 Shape analysis

`Core/Analysis/ShapeProfileCalculator.cs` produces a `ShapeProfile` with: closedness, circularity, aspectRatio, angularity, symmetry, vertical/horizontal span, pathLength, speedVariance, curvatureMean/Variance, centroidHeight, directionBias, tilt / tiltSigned, wobble, and world-space width/height/length/average/max.

These features are fed to `SoundProfileMapper` → `PatternBehaviorRegistry` → `SoundMappingProfiles` to derive a `SoundProfile` (brightness, resonance, drive, attack/release bias, detune, modDepth, stereoSpread, grooveInstability, delay/reverb bias, waveMorph, filterMotion, transientSharpness, body).

### 1.4 Instance placement → mix

`Core/Data/PatternInstance.cs` → `RecalculateMixFromPosition()`:

```
pan        = clamp(position.x * 2 - 1, -1, 1)
brightness = clamp01(1 - position.y)
gain       = clamp01(1.08 - depth * 0.58)
```

That is the entire spatial audio pipeline today. It is a **stereo-pan + filter-brightness mapper**, not 3D audio.

### 1.5 Audio routing

`Audio/AudioEngine.cs` hands drum/melody/chord calls to `Audio/SamplePlayer.cs`, which renders procedural clips, caches them, and plays them through a fixed pool of 24 `AudioSource`s. The pool sources are created in `EnsurePool()` with `source.spatialize = false`. No source is attached to a pattern instance transform. All spatialization is baked into stereo pan at the clip level.

### 1.6 Instance grabbing

`Interaction/InstanceGrabber.cs.DragInstance()` projects the grabbed instance to a fixed 1.2 m along the controller ray. In practice the user cannot push a pattern away or pull it close because `distance = 1.2f` is hard-coded.

### 1.7 Sequencer

`Sequencer/Sequencer.cs` is a lookahead scheduler (0.12 s) that iterates over `_store.GetSceneInstances(sceneId)` each step and calls the relevant `PatternBehavior.Schedule`. Behaviours pass `instance.pan`, `instance.brightness`, `instance.depth` into `PlayDrum/PlayMelody/PlayChord`.

### 1.8 Gap summary

| Promise in user guide / pilot | Reality in code |
|---|---|
| "Horizontal placement affects pan" | ✓ stereo pan only |
| "Depth affects mix brightness/gain" | ✓ but depth drag is clamped to 1.2 m |
| "3D audio emitter per object" (pilot §7) | ✗ `spatialize = false`, no per-instance AudioSource |
| "Pressure → dynamics" | ✗ pressure only controls line width |
| "Tilt → filter/timbre" | ✗ `StylusPose.rotation` never read past position |
| "Middle button / squeeze" | Present in `InputMapper`, used nowhere |
| "Z (depth) → reverb send" | ✗ |
| Ballet-inspired gestures (pirouette, arabesque, grand jeté, plié fade, tendu) | ✗ not implemented |
| "Trail becomes a living musical object" | Static: shape is analysed once at commit |

The plan below closes these gaps in small, shippable steps.

---

## 2. Plan at a glance

Four phases, each ~1–2 weeks of work, each independently shippable.

| Phase | Theme | Code area of biggest change |
|---|---|---|
| 1 | Pen-as-instrument: make pressure, tilt, and speed audible | `StrokeCapture`, `ShapeProfile`, mappers |
| 2 | True 3D spatial audio routing per instance | `SamplePlayer`, `AudioEngine`, `PatternVisualizer`, `InstanceGrabber` |
| 3 | 3D shape interpretation (stop flattening) | `StrokeCapture`, `ShapeProfileCalculator`, new `StrokeProfile3D` |
| 4 | Orchestrator layer: zones, busses, conducting | new `OrchestratorStage`, `SpatialZone`, `ConductorGesture` |

Nothing in these phases breaks existing sessions — `SessionPersistence` keeps loading old `AppState`s because new fields are additive. The `StateMigrator` tests (`Editor/StateMigratorTests.cs`) make that contract explicit.

---

## 3. Phase 1 — Make the pen a real instrument

Goal: when the user draws, every expressive axis of the MX Ink — pressure, tilt, speed, middle button — produces audible change. Today only shape does.

### 3.1 Capture and keep stroke kinematics

Extend `StrokeCapture` to also record, per sample:

- `pressures[i]` — already captured, keep it
- `tiltXY[i] = StylusPose.rotation * forward` decomposed to a pen-relative tilt vector
- `speeds[i] = distance / dt` in m/s
- `normalDepths[i]` — signed distance from the fitted stroke plane (for Phase 3; zero for now)

Add a new record `Core/Analysis/StrokeKinematics.cs`:

```csharp
public sealed class StrokeKinematics {
    public float[] pressures;
    public Vector2[] tilt;         // pen-local XY tilt projected into the stroke frame
    public float[] speed;
    public float[] normalDepth;    // 0 until phase 3
    public float durationSeconds;
}
```

Pass it alongside the 2D points into `DraftBuilder.BuildFromStroke` so every deriver can use it. Today's derivers keep working because they only read `points` + `metrics`.

### 3.2 Extend `ShapeProfile` with kinematic features

Additive fields on `Core/Data/ShapeProfile.cs` (and `Clone`, migrator, tests):

- `pressureMean`, `pressureVariance`, `pressurePeak`, `pressureSlopeEnd`
- `tiltMean`, `tiltVariance` (magnitude of tilt vector)
- `speedMean`, `speedPeak`, `speedTailOff` — quantifies a grand-jeté-like decelerating stroke
- `strokeSeconds` — stroke duration

Compute these in a new `Core/Analysis/StrokeKinematicsAnalyzer.cs`. Keep them out of `ShapeProfileSizing` so size math stays stable.

### 3.3 Let the new features drive sound

In each `SoundMappingProfile.Evaluate`, plug the kinematic features into parameters they naturally belong to. Concrete assignments (tunable):

| Input | Drives | Where |
|---|---|---|
| `pressureMean` | overall `velocity` passed into `Schedule`, and a small bias on `drive` | Per behavior `Schedule` (multiplicative gain on `evt.velocity` and `note.velocity`) |
| `pressureVariance` | `grooveInstability` for rhythm, expressive variance for melody/harmony | `SoundMappingProfile` |
| `tiltMean` | `filterMotion` start offset (static tilt = tone filter cut), `resonance` bias | `SoundMappingProfile` |
| `tiltVariance` | `modDepth` (wobbling pen adds vibrato/chorus) | `SoundMappingProfile` |
| `speedMean` | `attackBias` inverse (fast = sharp attack) | `SoundMappingProfile` |
| `speedTailOff` | `releaseBias` bias (decelerating stroke = longer release) | `SoundMappingProfile` |
| `strokeSeconds` | default `bars` ceiling for melody/harmony (long stroke = long phrase) | Per deriver |

Each mapping is a small multiplicative term so old presets still sit in range.

### 3.4 Use the MX Ink buttons as *expressive* inputs, not just UI

- **Middle-button / squeeze during draw** → treat as a second layer. While squeezed, the current stroke's pressure curve is multiplied by a "grain / ornament" flag that, on commit, causes the melody/harmony deriver to add embellishments (passing tones, neighbor notes). Grand conceptual cost: one bool in `StrokeKinematics`, one branch in each deriver.
- **Back-button held + short stroke** → "accent brush" mode: does not commit a pattern, instead applies a short velocity bump + filter-open transient to patterns inside a small world-space radius of the stroke centroid. Implement as a new `AccentBrushInteractor` in `Interaction/`, using the same `StylusUIPointer.IsHoveringUI` guard to avoid clashing with panels.
- **Front-button double-tap during idle** → opens a radial gesture menu. Hackathon-friendly: reuse the `GenreSelectorPanel` build pattern (world-space canvas with 3–4 buttons) but position it around the stylus tip.

Back button double-tap is already used to discard a draft, so do not reassign it.

### 3.5 Acceptance for Phase 1

- Drawing the same shape softly vs forcefully produces clearly different velocity/volume.
- Tilting the pen while drawing a pad produces an audibly different filter character.
- A fast, tapering stroke produces a longer tail than a steady stroke of the same geometry.
- All 67 existing editor tests still pass. New tests: `Editor/StrokeKinematicsAnalyzerTests.cs`, extensions to `ShapeSizeBehaviorTests`.

---

## 4. Phase 2 — True 3D spatial audio routing

Goal: every pattern instance is actually a 3D audio emitter at its world position. Moving a pattern behind you sounds like it's behind you; pushing it away makes it quieter and more reverberant.

This phase has the highest "spatial orchestrator" payoff per engineering hour.

### 4.1 Per-instance AudioSources

Today `SamplePlayer` round-robins a shared pool. Split responsibilities:

- Keep the pool for **stingers and pre-hearing** (commit card preview, demo clips).
- Add `Audio/InstanceVoicePool.cs` — a small pool of `AudioSource`s parented to each `PatternVisualizer`. Expose `IVoiceSink.Play(clip, volume, voiceParams)` so behaviors don't need to know which pool they're hitting.

Wire it up:

1. `PatternVisualizer` creates an `InstanceVoicePool` on `Initialize`.
2. `RhythmForgeManager`/`VisualizerManager` registers the pool with the store so `AudioEngine` can look it up by `instanceId`.
3. `AudioEngine.PlayDrum/PlayMelody/PlayChord` takes a new `string instanceId` argument, routes to that instance's pool if present, falls back to the global pool for previews.
4. Update all three `PatternBehavior.Schedule` call sites to pass `context.instance.id`.

### 4.2 Enable `spatialize`, add spatial blend and rolloff

On each instance `AudioSource`:

- `spatialize = true`
- `spatialBlend = 1.0f` for instance voices, `0.0f` for the preview pool
- `rolloffMode = AudioRolloffMode.Logarithmic`, `minDistance = 0.4f`, `maxDistance = 8f`
- Transform follows the visualizer, so `InstanceGrabber.DragInstance` automatically changes the audible position. Stop computing stereo `pan` from `position.x` — it's now handled by the listener.

### 4.3 Replace the pan/brightness/gain mapping

`PatternInstance.RecalculateMixFromPosition` currently conflates three things. Split it:

```csharp
// ── kept, but semantics now additive on top of HRTF ──
brightness = clamp01(0.35f + (height - headHeight) * 0.6f);    // vertical bias still colors tone
reverbSend = clamp01(depth * 0.55f + distanceToListener * 0.1f);
delaySend  = clamp01(depth * 0.35f);
gainTrim   = clamp01(1.05f - depth * 0.25f);                   // subtle extra, not primary gain
```

Key change: **primary loudness and stereo position now come from the 3D AudioSource**, not from `pan`/`gain`. That is what makes it sound spatial.

### 4.4 Unclamp the grab distance

In `InstanceGrabber.DragInstance`, change `float distance = 1.2f;` to derive from the trigger depth:

```csharp
float distance = Mathf.Lerp(0.4f, 6f, _grabDistanceProgress);
_grabDistanceProgress = Mathf.Clamp01(_grabDistanceProgress
                         + input.LeftThumbstick.y * Time.deltaTime * 0.8f);
```

Left thumbstick Y while grabbing = push/pull. The thumbstick X is already used for scene switching, so this is non-conflicting so long as we only poll Y while `LeftTrigger` is held. Add a visible depth indicator on the ray (scale the existing `rayLine` end alpha).

### 4.5 Busses as spatial zones

Define four named `SpatialZone` regions in world space (e.g. `DrumsFloor`, `MelodyAboveHead`, `PadsFar`, `AccentsFront`). A zone is a sphere with center, radius, and a `busFx` profile (additional reverb, delay, filter cutoff). When a pattern instance is inside a zone, its voice sends additional reverb/delay equal to the zone's profile.

Implementation:

- `Core/Data/SpatialZone.cs` — data only (id, center, radius, color, bus profile overrides).
- `Core/Session/SpatialZoneController.cs` — evaluates which zone each instance is in, emits `SpatialZoneChangedEvent` via `RhythmForgeEventBus`.
- `AudioEngine` reads the zone's bus profile and adds to `fxSend` in `PlayDrum/Melody/Chord`.
- Simple visual: a faint translucent sphere per zone with its color.

This alone turns the space into an orchestrator: "pads live behind me, drums live at my feet, leads live above my right shoulder."

### 4.6 Acceptance for Phase 2

- Walking around a playing session changes what you hear positionally. Validated with headphones.
- Pushing a pattern away makes it quieter AND wetter (more reverb).
- Moving a pattern into the `PadsFar` zone makes it noticeably more ambient without changing its notes.
- `ProceduralAudioRendererTests` still pass. New tests: `SpatialZoneControllerTests`.

---

## 5. Phase 3 — Use the third dimension of the stroke

Goal: stop flattening strokes to 2D at analysis time. The user is drawing in air; a vertical stroke, a forward jab, and a flat circle should all read differently.

This phase is smaller and more focused than Phase 2 but it's what makes "shape" really 3D.

### 5.1 Capture 3D signatures alongside the 2D projection

Today `StrokeCapture.BuildStrokeFrame` picks the best plane, then `ProjectTo2D` flattens everything onto it. Keep this projection for all existing derivers (unchanged) but also compute, for the new kinematics record:

- `planeNormal` — the fitted plane's normal (already computed internally)
- `planarity` — 1 − (mean squared out-of-plane distance / max squared distance). 1 = flat, 0 = sphere-like.
- `thrustAxis` — signed dominant direction along `planeNormal` (for grand-jeté-style forward strokes).
- `verticalityWorld` — how aligned the stroke's long axis is with world up.

These are cheap to compute inside `BuildStrokeFrame` with a second pass over `worldPoints`.

### 5.2 Feed them into behaviors

- `planarity < 0.5` on a Rhythm stroke → deriver adds a "shakers / ride bed" lane on top of the base kit, because non-planar strokes are three-dimensional and read as textural.
- `thrustAxis` on a short Rhythm stroke (≤ 300 ms, `angularity` high) → single-shot percussive accent rather than a full loop. This is the pilot's "Grand Jeté Strike".
- `verticalityWorld` on a Melody stroke biases the pitch contour: a strongly vertical arabesque favors octave leaps; a horizontal one favors stepwise motion. Adjust inside `MelodyDeriver`s.
- `verticalityWorld` on a Harmony stroke biases chord voicing spread (vertical = wide, horizontal = close voicing).

### 5.3 Spawn-position encodes the draw

Currently `DraftBuilder.BuildFromStroke` passes `strokeCenter` as `spawnPosition`. Extend so that:

- Vertical strokes spawn at a height matching the stroke centroid (they already do).
- Horizontal strokes are slightly lifted so they are visible while the user still sees the floor.
- Strokes drawn very close to the head spawn at arm's length, not inside the user's face, by clamping `distanceToListener` to `[0.5, 3.5]`.

### 5.4 Acceptance for Phase 3

- Drawing a flat circle vs a spherical-ish scribble produces audibly different textures with the same key presses.
- A quick forward jab triggers a single-shot hit, not a looping pattern.
- Existing `Editor/*Tests.cs` still green. New tests: `StrokeFrame3DTests`.

---

## 6. Phase 4 — The orchestrator layer

Goal: once Phase 1–3 exist, unify them into a coherent "conduct your stage" experience. This is the concept doc's promise and the core of "spatial orchestrator".

### 6.1 Stage metaphor

Add `Core/Session/OrchestratorStage.cs` that owns:

- A list of `SpatialZone`s (Phase 2.5) with defaults: `DrumsFloor`, `BassLow`, `MelodyFront`, `HarmonyBehind`, `AccentsOverhead`.
- A "conductor origin" = the user's last stable head position. Zones are defined relative to this origin so the experience follows the user around the room.
- A "focus" property — the zone currently closest to the stylus tip. When the user aims into a zone, its outline brightens and its patterns are slightly duckable.

### 6.2 Conducting gestures (recognizer, not classifier)

Implement a lightweight heuristic recognizer in `Interaction/ConductorGestureRecognizer.cs`. No ML, no per-frame classifier — just look at the last N seconds of stylus pose sampled at ~60 Hz, and detect:

- **Sway (Balancé)** — oscillating horizontal motion with ≥ 2 direction changes in 2 s. Output: small BPM nudge (±1–3 BPM) toward the sway period.
- **Lift (Tendu)** — slow vertical extension with monotonically rising pressure. Output: crescendo on the currently focused zone (scales its gain trim up over 2 bars).
- **Fade (Plié)** — downward motion with decreasing pressure ending near zero. Output: fade the focused zone's gain to mute over N beats.
- **Cut-off** — sharp sideways chop past a speed threshold while over a zone. Output: mute that zone on the next bar.

These gestures **do not create patterns**. They modulate existing ones. Recognition runs only when `StrokeCapture` is not drawing (i.e., `DrawPressure < 0.05`), so it never competes with shape drawing.

### 6.3 Two-handed conducting

When the user holds **left grip** while the stylus is doing a conducting gesture, treat it as "apply to all zones" rather than just the focused zone. This preserves the asymmetric bimanual model from the pilot (§8) without needing another radial menu.

### 6.4 Scene routing through zones

Right now scenes contain instances; instances have positions. Add a soft rule: when a new pattern is committed, place it inside the zone whose role matches its `PatternType` (rhythm → DrumsFloor, melody → MelodyFront, harmony → HarmonyBehind). The user can still drag it anywhere, but the default is spatially coherent.

### 6.5 Acceptance for Phase 4

- Swaying the pen slightly faster for a few seconds measurably nudges BPM in the transport panel.
- A clear Tendu lift over the pads zone makes the pads come up and stay louder until a Plié.
- Newly committed patterns land in the "right" zone by default.
- Users can still freely drag patterns between zones; their bus sends update in real time.

---

## 7. Smaller wins to slot in along the way

These are independent and each take under a day. They round out the "spatial orchestrator" feel without waiting on a phase.

1. **Attach an `AudioSource` to the stroke while drawing.** `StrokeCapture.AddPoint` already has position + pressure; run a tiny pressure-driven oscillator so the user hears themselves drawing. Zero-latency confirmation and a real instrument feel.
2. **Width-and-color pen feedback for tilt.** Use `StylusPose.rotation` to tint the `LineRenderer` cyan-to-amber and modulate width slightly. Confirms tilt is being read before it drives audio.
3. **Pressure readout on the commit card.** `CommitCardPanel` already shows shape DNA; add a one-line "Pressure: 0.42 avg, 0.78 peak" so users see the new dimension.
4. **Spatial audio listener at the head.** Attach an `AudioListener` to `VRRigLocator.CenterEye` (instead of the default camera one if different) so Phase 2 works consistently on device and in the editor simulator.
5. **Haptics on draw.** `OVRInput.SetControllerVibration` on the left controller when a Schedule event fires inside the focused zone — subtle, tells the user "this pattern is live".
6. **Panel anchor to gesture origin.** `RhythmForgeBootstrapper.RepositionPanels` currently clamps to head forward. After Phase 4, also offer "recenter stage" via long-press Y — repositions both panels and zones relative to current head pose.

---

## 8. Risks and non-goals

**Risks worth naming explicitly:**

- MX Ink bindings only work on-device (`EditorStylusSuppressor` makes this explicit). Tilt and pressure behaviour has to be verified on Quest, not in the editor simulator. Plan testing sessions accordingly.
- `Audio/Synthesis/AudioEffectsChain.cs` bakes filter, delay, reverb into the rendered clip, not post-source. That means enabling Unity 3D audio on the source does not conflict, but it also means "depth → more reverb" cannot be retuned post-hoc without a small rework. For Phase 2.3 we add reverb send by re-rendering with a higher `reverbBias` in the voice spec — the existing clip cache (`VoiceSpecResolver.Quantize*`) already keys on that, so this is fine.
- The pool has a fixed size of 24 (`SamplePlayer._poolSize`). Per-instance pools (Phase 2.1) need to be small (2–3 per instance) or overall voice count will explode. A cap of `min(2 * activeInstances, 32)` is a safe starting point.
- Gesture recognizer false-positives (Phase 4.2) will feel broken. Land it behind a "conducting mode" toggle on the transport panel initially; default off.

**Explicit non-goals for this plan:**

- No ML. All gesture recognition is heuristic.
- No DAW export or MIDI. Stays in-engine.
- No networked multi-user.
- No change to the save format beyond additive fields. `StateMigrator` keeps old sessions loading.
- No rework of the dock panel — that is scaffolded and not on the critical path for spatial orchestration.

---

## 9. Delivery order suggested

If prioritising perceived impact per day:

1. **Phase 2.1–2.4** — per-instance 3D AudioSources and unclamped depth drag. Single biggest leap in "spatial" feel, and it works with zero derivation changes.
2. **Phase 1.1–1.3** — pressure and tilt into sound profiles. Makes the pen feel like a real instrument in the first minute of use.
3. **Small wins 1–4.**
4. **Phase 2.5** — spatial zones.
5. **Phase 3** — 3D stroke features.
6. **Phase 4** — conductor gestures, behind a toggle.

Each step leaves the app runnable, demoable, and saveable.

---

## 10. File-by-file change summary (cheat sheet)

| File | Phase | Change |
|---|---|---|
| `Interaction/StrokeCapture.cs` | 1, 3 | Record tilt/speed/normal-depth; compute planarity/thrustAxis |
| `Core/Analysis/StrokeKinematicsAnalyzer.cs` *(new)* | 1 | Kinematic feature extraction |
| `Core/Data/ShapeProfile.cs` | 1 | Additive fields + Clone |
| `Core/Session/StateMigrator.cs` | 1 | Backfill defaults |
| `Core/Analysis/SoundMappingProfiles.*` | 1 | Plug kinematics into existing SoundProfile params |
| `Core/PatternBehavior/Behaviors/*Behavior.cs` | 1, 3 | Use new features in derivers; accept embellishment flag |
| `Audio/AudioEngine.cs` | 2 | Add `instanceId` param, route to per-instance pool |
| `Audio/SamplePlayer.cs` | 2 | Split preview pool vs instance pool |
| `Audio/InstanceVoicePool.cs` *(new)* | 2 | Per-visualizer pooled AudioSources |
| `UI/PatternVisualizer.cs` | 2 | Own an `InstanceVoicePool`, register with engine |
| `Core/Data/PatternInstance.cs` | 2 | Replace pan with reverbSend/delaySend/gainTrim |
| `Interaction/InstanceGrabber.cs` | 2 | Variable grab distance from left thumbstick Y |
| `Core/Data/SpatialZone.cs` *(new)* | 2, 4 | Zone data |
| `Core/Session/SpatialZoneController.cs` *(new)* | 2, 4 | Zone membership + bus send |
| `Interaction/ConductorGestureRecognizer.cs` *(new)* | 4 | Sway/lift/fade/cutoff recognizer |
| `Core/Session/OrchestratorStage.cs` *(new)* | 4 | Zones + conductor origin |
| `UI/Panels/TransportPanel.cs` | 4 | Conducting-mode toggle |
| `Bootstrap/RhythmForgeBootstrapper.cs` | 2, 4 | Wire new subsystems |
| `Editor/*Tests.cs` | all | Add tests for each new module |

Everything else stays unchanged.
