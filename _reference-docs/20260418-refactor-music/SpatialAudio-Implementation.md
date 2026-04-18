# Spatial Audio — Safe-Zone Implementation Plan

Status: ready to implement, April 2026
Scope: executes Phase 2 of [SpatialOrchestrator-Plan.md](SpatialOrchestrator-Plan.md) on top of the post-refactor runtime. Companion to the creative brief in [SpatialOrchestrator-Brainstorm.md](SpatialOrchestrator-Brainstorm.md).

This plan is the **safe-zone subset** of the full orchestrator vision. It lands the single highest-impact perceptual change — "the room sounds like a room" — while explicitly deferring every item whose risk profile (voice-count explosion, gesture false-positives, new-schema migrations, on-device-only verification burden) makes it unsafe to bundle.

---

## 1. Grounded baseline (what the code actually is today)

Verified against the branch `feature/from-2d-engine-single-shape` at commit `3940996`.

### Already shipped by the "Single shape Refactor"

- Per-sample capture in [StrokeCapture.cs:149-158](../../Assets/RhythmForge/Interaction/StrokeCapture.cs#L149-L158): world position, `pressure`, `stylusRot`, `timestamp` are all retained.
- [ShapeProfile3D](../../Assets/RhythmForge/Core/Data/ShapeProfile3D.cs) holds `thicknessMean/Variance`, `tiltMean/Variance`, `planarity`, `elongation3D`, `helicity`, `centroidDepth`, `temporalEvenness`, `passCount`.
- [ShapeProfile3DCalculator.cs](../../Assets/RhythmForge/Core/Analysis/ShapeProfile3DCalculator.cs) derives the profile via PCA / Jacobi eigendecomposition; output is quantised to 2 decimals so identical strokes produce identical profiles.
- Unified shape derivers consume it ([ShapeProfile3DConsumerTests.cs](../../Assets/RhythmForge/Editor/ShapeProfile3DConsumerTests.cs), [UnifiedShapeDeriverBase.cs](../../Assets/RhythmForge/Core/Sequencing/UnifiedShapeDeriverBase.cs)).
- State migration is already versioned through [StateMigrator.cs](../../Assets/RhythmForge/Core/Session/StateMigrator.cs) (`AppState` version 4+).

Consequence: **Phase 1 and Phase 3 of the original plan are effectively complete.** Pen pressure, tilt, planarity, helicity etc. all feed into sound. What is missing is that none of it reaches 3D space — every voice still plays through a shared, non-spatialised pool.

### The confirmed gap — Phase 2 is untouched

- [SamplePlayer.cs:417](../../Assets/RhythmForge/Audio/SamplePlayer.cs#L417) — pool created with `src.spatialize = false`, pool size 24.
- [PatternInstance.cs:38-43](../../Assets/RhythmForge/Core/Data/PatternInstance.cs#L38-L43) — `RecalculateMixFromPosition` synthesises stereo `pan` from `position.x * 2 - 1`, `brightness = 1 - position.y`, `gain = 1.08 - depth * 0.58`.
- [AudioEngine.cs:46-134](../../Assets/RhythmForge/Audio/AudioEngine.cs#L46-L134) — `PlayDrum/Melody/Chord` take `pan/brightness/depth` scalars, no `instanceId`.
- [InstanceGrabber.cs:139](../../Assets/RhythmForge/Interaction/InstanceGrabber.cs#L139) — `float distance = 1.2f;` hard-coded.
- Pattern voices are never attached to a `PatternVisualizer` transform; moving a visualizer does not move its audio emitter.

---

## 2. Delivery order (safe-zone, five steps)

Each step is independently revertable, independently shippable, and leaves saved sessions loadable. Total: ~3–5 days if uninterrupted.

### Step 1 — Per-instance 3D AudioSource (keystone)

**Goal:** every active pattern instance has 2–3 dedicated `AudioSource`s parented to its `PatternVisualizer`, with `spatialize = true`, `spatialBlend = 1.0`, logarithmic rolloff `0.4 m → 8 m`. Drum/melody/chord voices play from the instance's world position; moving the visualizer moves the sound.

**Files touched:**

| File | Change |
|---|---|
| `Audio/InstanceVoicePool.cs` *(new)* | Small pooled `AudioSource` set, parented to a transform; `SetVoiceCount(n)`; `Play(clip, volume, startDelay)`. No stereo pan — listener + source position handle that. |
| `Audio/InstanceVoiceRegistry.cs` *(new)* | `Dictionary<string, InstanceVoicePool>` keyed by `instanceId`; registered on visualizer spawn, unregistered on despawn. Single authority — `AudioEngine` asks this by id. |
| [UI/PatternVisualizer.cs](../../Assets/RhythmForge/UI/PatternVisualizer.cs) | In `Initialize`, instantiate an `InstanceVoicePool` child GameObject. Register with `InstanceVoiceRegistry`. In `OnDestroy`, unregister + release. |
| [Audio/AudioEngine.cs](../../Assets/RhythmForge/Audio/AudioEngine.cs) | Add `string instanceId` parameter to `PlayDrum/PlayMelody/PlayChord` (and the three-arg convenience overloads). Route to per-instance pool if registered; otherwise fall back to the existing shared pool (for commit-card previews and demo playback). |
| [Audio/SamplePlayer.cs](../../Assets/RhythmForge/Audio/SamplePlayer.cs) | **No behaviour change.** Keep the 24-voice shared pool for previews, stingers, and fallback. The new instance pools live separately. |
| [Core/PatternBehavior/Behaviors/*Behavior.cs](../../Assets/RhythmForge/Core/PatternBehavior/Behaviors) | In each `Schedule`, pass `context.instance.id` into the new `PlayDrum/Melody/Chord` signature. |

**How per-instance routing actually works:**

The sequencer already calls `AudioEngine.PlayDrum(preset, lane, vel, pan, brightness, depth, fxSend, profile)` per scheduled event. The engine looks up `instanceId → InstanceVoicePool`, fetches a `CachedClip` from the existing [SamplePlayer](../../Assets/RhythmForge/Audio/SamplePlayer.cs) clip cache (by `ResolvedVoiceSpec.GetCacheKey()`), then calls `InstanceVoicePool.Play(clip, volume, startDelay)` on one of the pool's sources. The clip itself is the same procedural clip used today — it is just playing from a different, 3D-spatialised AudioSource.

**What moves through the source vs. what stays in the clip:**

| Axis | Before | After |
|---|---|---|
| Stereo position | Baked into `pan` argument → `source.panStereo` | Comes from listener ↔ source position, via Unity's HRTF |
| Distance attenuation | Baked into `gain = 1.08 - depth * 0.58` | Logarithmic rolloff on the source (`minDistance = 0.4`, `maxDistance = 8`) + a small `gainTrim` multiplier |
| Tone colour (brightness) | Baked into the clip via filter at render time | Unchanged — still baked in per [AudioEffectsChain](../../Assets/RhythmForge/Audio/Synthesis) |
| Reverb send | Baked into the clip via `reverbBias` in `ResolvedVoiceSpec` | **Unchanged in this step.** See Step 2 for depth-driven reverb. |

**Acceptance:**
- In headphones, walking around a playing session changes stereo image — drum at `position.x = -0.7` sounds left when facing forward, behind when you turn 90° right.
- Grabbing and moving a pattern smoothly relocates its sound. No pops, no duplicate playback (old shared-pool copy must not fire for instance-routed events).
- All existing editor tests still pass. One new test: `InstanceVoiceRegistryTests` — register, lookup, unregister.

---

### Step 2 — Rewrite `RecalculateMixFromPosition`

**Goal:** replace the stereo-pan synthesis with a spatial-native mix: primary loudness and direction come from the 3D source; `brightness`, `reverbSend`, `delaySend`, and a small `gainTrim` layer on top.

**Files touched:**

| File | Change |
|---|---|
| [Core/Data/PatternInstance.cs](../../Assets/RhythmForge/Core/Data/PatternInstance.cs) | Remove `pan`. Keep `brightness` (still `1 - position.y` for tone colour). Add `reverbSend`, `delaySend`, `gainTrim` (all clamped 0–1). |
| [Core/Data/AppState.cs](../../Assets/RhythmForge/Core/Data/AppState.cs) | Bump state version. |
| [Core/Session/StateMigrator.cs](../../Assets/RhythmForge/Core/Session/StateMigrator.cs) | Migration path: discard old `pan` (position now drives it); default `gainTrim = 1`, `reverbSend = depth * 0.55`, `delaySend = depth * 0.35`. |
| [Audio/AudioEngine.cs](../../Assets/RhythmForge/Audio/AudioEngine.cs) | Replace `pan` arg with `gainTrim`. Pass `reverbSend` / `delaySend` into the voice-spec resolver (see risk 2). |
| All `PatternBehavior.Schedule` call sites | Pass the new fields. |
| [UI/Panels/InspectorPanel.cs](../../Assets/RhythmForge/UI/Panels/InspectorPanel.cs) | Update any displayed "Pan" readout to show direction from listener instead. Cosmetic. |

**Formula rewrite** (replaces [PatternInstance.cs:38-43](../../Assets/RhythmForge/Core/Data/PatternInstance.cs#L38-L43)):

```csharp
public void RecalculateMixFromPosition()
{
    // brightness still colours tone — higher = airier
    brightness  = Mathf.Clamp01(1f - position.y);

    // depth drives ambient sends; distance-to-listener is unknown here
    // (we only have position), so depth is the authoritative "push/pull" axis
    reverbSend  = Mathf.Clamp01(depth * 0.55f);
    delaySend   = Mathf.Clamp01(depth * 0.35f);

    // gainTrim is a gentle secondary multiplier — the source handles
    // primary distance attenuation via its rolloff curve
    gainTrim    = Mathf.Clamp01(1.05f - depth * 0.15f);
}
```

**Acceptance:**
- Pushing a pattern away (via Step 3) makes it quieter AND more reverberant; pulling it close makes it drier and more present.
- Old saved sessions load without error and produce audibly similar mixes (they won't be bit-identical because the pan axis is now listener-relative, not camera-relative).
- New test: `StateMigratorTests` — verify v4-to-v5 migration produces sensible defaults.

---

### Step 3 — Unclamp the grab distance

**Goal:** left-thumbstick-Y during a grab pushes the held instance away (quieter, wetter) or pulls it close (louder, drier).

**Files touched:**

| File | Change |
|---|---|
| [Interaction/InstanceGrabber.cs](../../Assets/RhythmForge/Interaction/InstanceGrabber.cs) | Replace the constant `distance = 1.2f` with a member `_grabDistance` integrated from thumbstick-Y. Clamp to `[0.4, 6.0]`. Reset to `1.2` on grab-start. |

**Implementation** (replaces [InstanceGrabber.cs:133-144](../../Assets/RhythmForge/Interaction/InstanceGrabber.cs#L133-L144)):

```csharp
private float _grabDistance = 1.2f;
private const float MinGrabDistance = 0.4f;
private const float MaxGrabDistance = 6.0f;
private const float GrabPushSpeed   = 1.8f;   // m/s at full stick

private void DragInstance()
{
    if (_leftControllerTransform == null || _grabbedInstanceId == null) return;

    var input = _inputProvider ?? (IInputProvider)_input;
    float stickY = input != null ? input.LeftThumbstick.y : 0f;
    // Deadzone + only integrate while trigger held
    if (Mathf.Abs(stickY) > 0.15f)
        _grabDistance = Mathf.Clamp(
            _grabDistance + stickY * GrabPushSpeed * Time.deltaTime,
            MinGrabDistance, MaxGrabDistance);

    Ray ray = new Ray(_leftControllerTransform.position, _leftControllerTransform.forward);
    Vector3 targetPos = ray.GetPoint(_grabDistance) - _grabOffset;

    _hasMoved = true;
    _store.UpdateInstance(_grabbedInstanceId, position: targetPos);
}

// Reset on every new grab so the user doesn't pick something up at the wrong distance
private void TryGrab() { /* ... existing ... */ _grabDistance = 1.2f; }
```

**Why thumbstick-Y is safe:** thumbstick-X is already used by scene switching ([SceneController](../../Assets/RhythmForge/Core/Session/SceneController.cs)). Thumbstick-Y is currently unused. The integration only runs while `LeftTrigger` is held, so there is no collision.

**Acceptance:**
- Pushing thumbstick forward while grabbing moves the pattern out along the controller ray.
- Release + re-grab snaps back to 1.2 m (user's home zone).
- Paired with Step 2: pushing away audibly adds reverb.

---

### Step 4 — AudioListener on the center eye

**Goal:** HRTF is computed from the head, not the default camera. This is the difference between "3D audio works in editor" and "3D audio works on device".

**Files touched:**

| File | Change |
|---|---|
| [Bootstrap/RhythmForgeBootstrapper.cs](../../Assets/RhythmForge/Bootstrap/RhythmForgeBootstrapper.cs) | After VR rig resolution (`VRRigLocator.CenterEye`), ensure exactly one `AudioListener` component exists on that transform. Destroy any stray listeners found on other cameras in the scene. |

Tiny change, but critical — Quest 3 spatialisation is listener-transform-sensitive.

**Acceptance:**
- Exactly one `AudioListener` in the scene after boot.
- Turning the head changes stereo image in-headset (verified on device).

---

### Step 5 — Pen sidetone (small win)

**Goal:** while drawing, the user hears a continuous tone that responds to pressure and height. Zero-latency confirmation that the pen is alive — the brainstorm calls this out at [lines 80-83](SpatialOrchestrator-Brainstorm.md#L80-L83).

**Files touched:**

| File | Change |
|---|---|
| [Interaction/StrokeCapture.cs](../../Assets/RhythmForge/Interaction/StrokeCapture.cs) | On stroke start, create a short-lived `AudioSource` child with a simple looping sine/triangle `AudioClip`. Update its `volume` from `DrawPressure` and `pitch` from stylus height each frame. Destroy on `FinishStroke`/`ClearCurrentStroke`. |

Keep it dead simple — a single oscillator, no filtering, no tilt coupling (that can come later). The goal is "the pen has a voice", not "the pen is a synth". **This step is optional** — if it introduces any artefact on device, it gets ripped out without touching Steps 1–4.

**Acceptance:**
- Drawing any stroke produces a continuous audible tone.
- Tone stops instantly when pressure drops below 0.05.

---

## 3. Risk handling — staying in the safe zone

The risks from [SpatialOrchestrator-Plan.md §8](SpatialOrchestrator-Plan.md#L338) are real. Each one gets an explicit mitigation baked into the steps above.

### Risk 1 — Voice-count explosion

**Source:** [SamplePlayer.cs:18](../../Assets/RhythmForge/Audio/SamplePlayer.cs#L18) pool is sized 24. If every instance spawns its own pool of 2–3 voices, 16 instances × 3 = 48 voices on top of the shared 24. Quest 3 starts hitching above ~64 concurrent spatialised sources.

**Mitigation, enforced in code:**

1. **Hard global cap.** `InstanceVoiceRegistry` exposes a static `MaxSpatialVoices = 32`. When total active voices across all pools would exceed it, new registrations fall back to `voiceCount = 1` (enough for most patterns). A one-line log warns when the cap bites.
2. **Per-instance pool sized by pattern type.** Rhythm = 3 voices (kick / snare / perc overlap), Melody = 2 voices (legato + release tail), Harmony = 3 voices (three-note chord overlap). Chosen from the three-voice chord loop in [SamplePlayer.PlayChord](../../Assets/RhythmForge/Audio/SamplePlayer.cs#L230-L243).
3. **Eviction on instance delete.** `PatternVisualizer.OnDestroy` must unregister; otherwise voices leak. Assert in the registry on double-register.

**Failsafe:** if on-device profiling shows hitches, raise the fallback to use the shared pool even for registered instances (the instance still tracks spatial position, but routing temporarily degrades to the old path). This is a single boolean flip in `AudioEngine`.

### Risk 2 — Reverb is baked into the clip, not post-source

**Source:** [SpatialOrchestrator-Plan.md §8 risk 2](SpatialOrchestrator-Plan.md#L341) — [AudioEffectsChain.cs](../../Assets/RhythmForge/Audio/Synthesis) bakes filter, delay, reverb into the rendered clip. Changing depth → more reverb means re-rendering the clip, which blooms the cache.

**Mitigation:**

1. **Quantise `reverbSend` into 3 bands** (`dry`, `room`, `hall`) before it becomes part of the `ResolvedVoiceSpec` cache key. Smooth crossfade happens via the existing `VoiceSpecResolver.Quantize*` helpers. That is 3× cache size max, not N×.
2. **Use `_maxCachedClips = 96`** — already set in [SamplePlayer.cs:22](../../Assets/RhythmForge/Audio/SamplePlayer.cs#L22) — with LRU eviction ([TrimCacheIfNeeded](../../Assets/RhythmForge/Audio/SamplePlayer.cs#L427)). 3 reverb bands × ~30 distinct voices fits comfortably.
3. **Defer a true post-source reverb send bus.** See §4 Deferred Work — this is the right long-term answer but is a Unity AudioMixer rewrite, not in this PR.

### Risk 3 — Gesture recognizer false positives

**Source:** [SpatialOrchestrator-Plan.md §8 risk 4](SpatialOrchestrator-Plan.md#L343). Conducting gestures can feel broken if they trigger accidentally.

**Mitigation for this PR:** **no gesture recognizer in this plan.** See §4 Deferred Work. Zero risk because zero code.

### Risk 4 — On-device-only verification burden

**Source:** [SpatialOrchestrator-Plan.md §8 risk 1](SpatialOrchestrator-Plan.md#L340) — MX Ink bindings only work on-device; [EditorStylusSuppressor](../../Assets/RhythmForge/Interaction/) makes this explicit.

**Mitigation:**

1. **Every step has an editor-simulator fallback.** Step 1–4 use standard Unity `AudioSource`/`AudioListener` which work fine in-editor with a mouse-driven camera. Only Step 5 (pen sidetone) needs the stylus.
2. **Checkpoint after Step 4, before Step 5.** Steps 1–4 are the safe core. If anything in Step 5 misbehaves on device, it ships without the sidetone; the spatial audio work is unaffected.
3. **Device smoke test after each step.** Acceptance criteria for Steps 1, 2, 3 explicitly say "verify in headphones" and "verify on device".

### Risk 5 — State migration corrupts saved sessions

**Source:** Step 2 bumps `AppState` version and removes `pan`.

**Mitigation:**

1. **Follow the existing migration pattern.** [StateMigrator.cs](../../Assets/RhythmForge/Core/Session/StateMigrator.cs) already handles v3→v4 cleanly (see [Editor/StateMigratorTests.cs](../../Assets/RhythmForge/Editor/StateMigratorTests.cs)). Add a v4→v5 case with a matching test before touching `PatternInstance`.
2. **Keep `pan` as `[Obsolete]` for one version.** Old code paths that still read it (if any exist) get a compile warning rather than a null reference. Remove in a follow-up PR once verified.
3. **Round-trip test.** Load every session fixture in `_reference-docs` (if any) + one freshly-saved v5 session; assert no crashes, no null instances, deterministic mix output.

### Risk 6 — AudioListener duplication

**Source:** Step 4 — if the VR rig and a stray camera both have `AudioListener`s, Unity logs a warning and picks one arbitrarily.

**Mitigation:** bootstrapper destroys every `AudioListener` except the one on center-eye. Single assert after rig resolution: `Object.FindObjectsByType<AudioListener>().Length == 1`.

---

## 4. Deferred work (explicit, with references)

These are **not** in this PR. Each has a specific reason to defer and a pointer to the brainstorm / plan section that owns it.

### Deferred — Spatial zones (`DrumsFloor`, `MelodyFront`, `HarmonyBehind`, `PadsFar`, `AccentsOverhead`)

- **Source in vision docs:** [Brainstorm §"The room as a mixing console"](SpatialOrchestrator-Brainstorm.md#L88) and [§"Spatial zones"](SpatialOrchestrator-Brainstorm.md#L115). Plan §4.5 and §6.1.
- **Why defer:** zones are *opinions on top of* spatial audio. Until the base 3D audio is proven on-device, zones would be colouring something that could itself be broken. Also, auto-placement in the right zone on commit is a behaviour change the user may want to disable, which means a settings toggle — more surface area than fits here.
- **Dependency on this PR:** none. Zones layer cleanly on top once steps 1–4 land.
- **Scope when we do it:** new `Core/Data/SpatialZone.cs`, `Core/Session/SpatialZoneController.cs`, faint translucent sphere visuals, zone bus-profile overrides on `reverbSend` / `delaySend` in `AudioEngine.PlayXxx`.

### Deferred — Conducting gesture recognizer (sway / lift / fade / cutoff)

- **Source in vision docs:** [Brainstorm §"Conducting: the second creative act"](SpatialOrchestrator-Brainstorm.md#L141). Plan §6.2.
- **Why defer:** highest false-positive risk of anything in the plan. Needs a "Conducting Mode" toggle on the transport panel to even be safe. Needs zones (above) to have a target for crescendos/fades. Needs on-device tuning that can only happen after 1–4 are solid.
- **Dependency on this PR:** spatial audio must be in place so crescendos are audible as 3D envelopes, not just stereo gain changes.
- **Scope when we do it:** new `Interaction/ConductorGestureRecognizer.cs` (heuristic, no ML — windowed analysis of stylus pose at ~60 Hz), Transport panel toggle, bimanual modifier via `LeftGrip` already exposed in [InputMapper.cs:36](../../Assets/RhythmForge/Interaction/InputMapper.cs#L36).

### Deferred — MX Ink expressive buttons (ornament squeeze, accent brush)

- **Source in vision docs:** [Brainstorm §"The middle button as an expressive modifier"](SpatialOrchestrator-Brainstorm.md#L66) and [§"Back-button + short stroke as accent brush"](SpatialOrchestrator-Brainstorm.md#L73). Plan §3.4.
- **Why defer:** `MiddlePressure` and `BackButton` are both already wired through [InputMapper.cs:23,25](../../Assets/RhythmForge/Interaction/InputMapper.cs#L23). Making them expressive requires ornament-flag plumbing through `StrokeSample` → `ShapeProfile3D` → every unified deriver. That's mechanical but it is a separate surface from spatial audio and bundling it would double the blast radius.
- **Dependency on this PR:** none.
- **Scope when we do it:** add `ornamentFlag` to [StrokeSample](../../Assets/RhythmForge/Core/Analysis/ShapeProfile3DCalculator.cs#L11), propagate to derivers in [Core/Sequencing/UnifiedShapeDeriverBase.cs](../../Assets/RhythmForge/Core/Sequencing/UnifiedShapeDeriverBase.cs), add grace-notes/passing-tones logic per behaviour.

### Deferred — Post-source reverb/delay buses

- **Source in vision docs:** Plan §8 risk 2.
- **Why defer:** the current model bakes FX into the rendered clip via [AudioEffectsChain.cs](../../Assets/RhythmForge/Audio/Synthesis). Moving reverb to a post-source Unity AudioMixer send is a real audio-pipeline rewrite — multiple mixer groups, send levels exposed via `AudioMixer.SetFloat`, warm-up of new rendered clip keys. For this PR we get "good enough" depth-reverb by quantising into 3 pre-rendered bands (see Risk 2 mitigation).
- **Dependency on this PR:** none. Would replace the quantised-band hack with true sends.
- **Scope when we do it:** extend [RhythmForgeMixer.mixer](../../Assets/RhythmForge/Audio/RhythmForgeMixer.mixer) with reverb and delay send groups; route per-instance voice pools through them; remove `reverbBias`/`delayBias` from the clip cache key.

### Deferred — Pattern-to-pattern proximity influence, temporal evolution, multi-user

- **Source in vision docs:** [Brainstorm §"Instance lifecycle"](SpatialOrchestrator-Brainstorm.md#L201) and [§"Ideas ranked … Moonshot"](SpatialOrchestrator-Brainstorm.md#L308).
- **Why defer:** moonshots. They change the musical model substantially (patterns mutate; instances listen to each other). They should not be prototyped until the base spatial experience validates.
- **Dependency on this PR:** the moonshots require instances to have real spatial identity, which this PR delivers.
- **Scope when we do it:** separate design doc per idea.

### Deferred — Pen tint feedback, haptics, commit-card pressure readout, panel-recenter

- **Source:** [SpatialOrchestrator-Plan.md §7 smaller wins 2, 3, 5, 6](SpatialOrchestrator-Plan.md#L324).
- **Why defer:** each is independent, each takes under a day, none affect the safety profile. They are explicitly flagged "slot in along the way". We take Step 5 (pen sidetone) in this PR because it is the one that most reinforces the spatial feel; the rest are cosmetic polish that can ship after.

---

## 5. File-by-file cheat sheet (for the implementation session)

Order = the order in which files will actually be edited/created.

| # | File | Action | Belongs to step |
|---|---|---|---|
| 1 | `Assets/RhythmForge/Audio/InstanceVoicePool.cs` | **new** | 1 |
| 2 | `Assets/RhythmForge/Audio/InstanceVoiceRegistry.cs` | **new** | 1 |
| 3 | `Assets/RhythmForge/UI/PatternVisualizer.cs` | edit (register/unregister pool) | 1 |
| 4 | `Assets/RhythmForge/Audio/AudioEngine.cs` | edit (`instanceId` param, route to registry) | 1, 2 |
| 5 | `Assets/RhythmForge/Core/PatternBehavior/Behaviors/RhythmBehavior.cs` | edit (pass `instance.id`) | 1 |
| 6 | `Assets/RhythmForge/Core/PatternBehavior/Behaviors/MelodyBehavior.cs` | edit | 1 |
| 7 | `Assets/RhythmForge/Core/PatternBehavior/Behaviors/HarmonyBehavior.cs` | edit | 1 |
| 8 | `Assets/RhythmForge/Core/Data/PatternInstance.cs` | edit (drop `pan`, add `gainTrim` / `reverbSend` / `delaySend`) | 2 |
| 9 | `Assets/RhythmForge/Core/Data/AppState.cs` | edit (bump version) | 2 |
| 10 | `Assets/RhythmForge/Core/Session/StateMigrator.cs` | edit (v4→v5) | 2 |
| 11 | `Assets/RhythmForge/Audio/VoiceSpec/*` | edit (quantise `reverbSend` into 3 bands for cache key) | 2 |
| 12 | `Assets/RhythmForge/Interaction/InstanceGrabber.cs` | edit (variable grab distance from thumbstick-Y) | 3 |
| 13 | `Assets/RhythmForge/Bootstrap/RhythmForgeBootstrapper.cs` | edit (AudioListener on center eye, single-listener assert) | 4 |
| 14 | `Assets/RhythmForge/Interaction/StrokeCapture.cs` | edit (pen sidetone AudioSource lifecycle) | 5 |
| 15 | `Assets/RhythmForge/Editor/InstanceVoiceRegistryTests.cs` | **new** | 1 |
| 16 | `Assets/RhythmForge/Editor/StateMigratorTests.cs` | edit (v4→v5 case) | 2 |

Nothing else is in scope. If a change tempts me to touch a file not in this table, that's a signal it belongs in a deferred-work follow-up — not this PR.

---

## 6. Acceptance for the whole PR

Before merging:

1. All existing editor tests pass (`Assets/RhythmForge/Editor/*Tests.cs`).
2. New tests: `InstanceVoiceRegistryTests`, extended `StateMigratorTests`.
3. In-editor simulator: a session with 3+ patterns can be moved freely with the mouse-driven rig and each pattern audibly relocates.
4. On-device (Quest 3, headphones): walking around a playing session changes spatial image; pushing a pattern away makes it quieter and wetter; pulling it close makes it louder and drier; old save files from before this branch load without error and sound sensible.
5. No `AudioListener` warning in the Unity console. Exactly one listener present.
6. Voice count during a stress session (8 instances, all playing) stays under 32 according to a new debug HUD line.

Once those six pass, this PR is the foundation the rest of the orchestrator vision ([brainstorm's five-minute experience](SpatialOrchestrator-Brainstorm.md#L258)) can stand on.
