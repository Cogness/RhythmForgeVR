# Spatial Audio — Follow-up Implementation Plan (Zones, Polish, Ornaments)

Status: ready to implement, April 2026
Scope: builds on top of [SpatialAudio-Implementation.md](SpatialAudio-Implementation.md) (landed in commit `3a1f448`). Bundles the three lowest-risk deferred workstreams from that plan's §4, and re-justifies the deferral of the three higher-risk ones. Creative source: [SpatialOrchestrator-Brainstorm.md](SpatialOrchestrator-Brainstorm.md); full vision: [SpatialOrchestrator-Plan.md](SpatialOrchestrator-Plan.md).

This plan is a **safe additive PR on top of the spatial-audio foundation**. It adds musical/perceptual opinions (zones), rounds out the feedback loop (small wins), and extends expressive range (ornaments) — without touching the mixer, the gesture recognizer, or the clip pipeline. Every workstream is independently revertable and independently shippable.

---

## 1. Grounded baseline (what the code actually is today)

Verified against `feature/from-2d-engine-single-shape` at commit `3a1f448` ("Spatial audio routing with instance-based voice pooling and split reverb/delay send controls").

### Already shipped by the Step 1–5 implementation

- Per-instance spatialized `AudioSource` pools ([InstanceVoicePool.cs](../../Assets/RhythmForge/Audio/InstanceVoicePool.cs), [InstanceVoiceRegistry.cs](../../Assets/RhythmForge/Audio/InstanceVoiceRegistry.cs)) attached to [PatternVisualizer.cs](../../Assets/RhythmForge/UI/PatternVisualizer.cs). HRTF-driven direction, distance rolloff `0.4 m → 8 m`.
- [PatternInstance.cs](../../Assets/RhythmForge/Core/Data/PatternInstance.cs) exposes `brightness`, `reverbSend`, `delaySend`, `gainTrim` (no `pan`).
- [StateMigrator.cs](../../Assets/RhythmForge/Core/Session/StateMigrator.cs) at `AppState` v10 migrates legacy mix data.
- [InstanceGrabber.cs](../../Assets/RhythmForge/Interaction/InstanceGrabber.cs) supports left-thumbstick push/pull, clamped `[0.4, 6.0]`.
- [RhythmForgeBootstrapper.cs](../../Assets/RhythmForge/Bootstrap/RhythmForgeBootstrapper.cs) enforces single `AudioListener` on center-eye.
- [StrokeCapture.cs](../../Assets/RhythmForge/Interaction/StrokeCapture.cs) emits the pen sidetone during draw.
- `reverbSend`/`delaySend` are quantized into 3 bands inside the voice-spec resolver, so clip cache stays bounded.

### The confirmed gaps this plan addresses

- **The room has no grammar.** New patterns spawn at the committing stylus position — nothing pushes them into musically coherent zones. The brainstorm's "drums to the floor, pads behind" imagery has no runtime representation.
- **Stylus feedback is audio-only.** The pen plays a sidetone, but tilt and ornament input are invisible and unheard as input. Users don't know the expressive range exists.
- **MX Ink middle-pressure and back-button are wired but inert.** [InputMapper.cs:23,25](../../Assets/RhythmForge/Interaction/InputMapper.cs#L23-L25) exposes `MiddlePressure` and `BackButton`. Neither influences sound derivation or playback.
- **No haptic confirmation** when sequencer events fire against a held or focused instance.
- **No pressure readout** on the commit card, even though `ShapeProfile3D` already carries pressure-derived stats.
- **No panel recenter.** After room re-entry or a chair swap, panels float at their last head-forward anchor; there is no "bring everything back to me" action.

---

## 2. Workstream order (safe additive, three stages)

Total: ~5–7 days if uninterrupted. Each workstream is a separate commit; if desired, each can also ship as its own PR.

### Workstream A — Spatial zones (biggest perceptual payoff)
### Workstream B — Small wins (polish + feedback loop)
### Workstream C — MX Ink expressive buttons (ornament + accent)

Chosen order: zones first because they give conducting (deferred) a target, and because auto-placement is immediately felt on commit. Small wins second because they confirm inputs (tilt, pressure) that ornaments then weaponize. Ornaments last because they have the widest blast radius through the derivers.

---

## 3. Workstream A — Spatial zones

**Goal:** five translucent, barely-visible soft regions with default musical roles. New patterns auto-place into the zone matching their `PatternType`. While an instance sits inside a zone, the zone's bus-profile nudges its `reverbSend` / `delaySend` / `gainTrim`. Users can still drag freely; the zone membership updates in real time and the sends crossfade smoothly.

### 3.1 Zones and defaults

| Zone id         | Role               | Centre (local, metres) | Radius | `reverbSend` bias | `delaySend` bias | Target `PatternType` |
|-----------------|--------------------|------------------------|--------|-------------------|------------------|----------------------|
| `DrumsFloor`    | Rhythm, grounded   | `(0, -0.6, 0.9)`       | `0.8`  | `+0.00`           | `+0.05`          | Rhythm               |
| `MelodyFront`   | Lead voice         | `(0,  0.0, 1.0)`       | `0.7`  | `+0.05`           | `+0.10`          | Melody               |
| `HarmonyBehind` | Pads, harmony      | `(0,  0.1, -0.9)`      | `1.1`  | `+0.25`           | `+0.20`          | Harmony              |
| `PadsFar`       | Atmosphere         | `(0,  0.3,  2.4)`      | `1.2`  | `+0.35`           | `+0.15`          | Harmony (secondary)  |
| `AccentsOverhead` | Ornaments, stabs | `(0,  1.0,  0.6)`      | `0.8`  | `+0.10`           | `+0.30`          | Ornament (future)    |

Centres are relative to the `VRRigLocator.StageOrigin` (or head-forward anchor at boot). A single `SpatialZoneLayout` ScriptableObject holds these defaults so tuning does not require code changes.

### 3.2 Files touched

| File | Change |
|---|---|
| `Core/Data/SpatialZone.cs` *(new)* | Plain data: `id`, `centre`, `radius`, `reverbBias`, `delayBias`, `gainBias`, `targetType`. Immutable record. |
| `Core/Data/SpatialZoneLayout.asset` *(new ScriptableObject)* | Default layout table matching §3.1. Authored in `Assets/RhythmForge/Config/`. |
| `Core/Session/SpatialZoneController.cs` *(new)* | Singleton-style runtime owner. Tracks `instanceId → zoneId` via a position-sample on `LateUpdate`. Exposes `GetZoneFor(instanceId)`, `GetDefaultPlacementFor(PatternType)`. Uses a deadband (10 cm past the sphere edge) to prevent boundary flicker. |
| `Core/Data/PatternInstance.cs` | Add `string currentZoneId` (nullable). **Not** part of save — derived from position on load. |
| `Core/Session/StateMigrator.cs` | No version bump; `currentZoneId` is not persisted. On load, `SpatialZoneController` repopulates it. |
| `Core/Session/SessionStore.cs` | On new-instance commit, call `SpatialZoneController.GetDefaultPlacementFor(type)` to override the spawn position (unless the user explicitly placed it via grab). |
| [Audio/AudioEngine.cs](../../Assets/RhythmForge/Audio/AudioEngine.cs) | In the per-voice scheduling path, before routing to `InstanceVoicePool`, look up the instance's current zone and add the zone's `reverbBias` / `delayBias` / `gainBias` (clamped `[0, 1]` for sends, `[0.5, 1.25]` for gain). Re-uses the existing 3-band quantization — biases are added *before* the quantization bucket is picked. |
| `UI/SpatialZoneVisualizer.cs` *(new)* | Per-zone `GameObject` with a `MeshRenderer` (unlit sphere, alpha 0.04) and a thin equatorial ring (`LineRenderer`, alpha 0.12). Brightens to alpha 0.10 / 0.25 when any instance inside it is playing. Uses an additive shader variant (`Unlit/Transparent`) so it does not cast on the canvas. |
| [Bootstrap/RhythmForgeBootstrapper.cs](../../Assets/RhythmForge/Bootstrap/RhythmForgeBootstrapper.cs) | Instantiate `SpatialZoneController` under `VRRigLocator.StageOrigin`. Load `SpatialZoneLayout.asset` and spawn one `SpatialZoneVisualizer` per zone. |
| `Editor/SpatialZoneControllerTests.cs` *(new)* | Coverage: point-in-sphere with deadband, default placement per `PatternType`, zone-change crossfade timing, missing-layout fallback (no zones → no bias). |

### 3.3 How zone bias stacks with existing sends

Already-shipped formula in [PatternInstance.cs](../../Assets/RhythmForge/Core/Data/PatternInstance.cs):

```
reverbSend = clamp01(depth * 0.55)
delaySend  = clamp01(depth * 0.35)
gainTrim   = clamp01(1.05 - depth * 0.15)
```

Zone contribution happens *downstream* of the instance field, inside `AudioEngine`:

```csharp
var zone = _zoneController.GetZoneFor(ev.instanceId);
float reverb = Mathf.Clamp01(instance.reverbSend + (zone?.reverbBias ?? 0f));
float delay  = Mathf.Clamp01(instance.delaySend  + (zone?.delayBias  ?? 0f));
float gain   = Mathf.Clamp(instance.gainTrim * (zone?.gainBias ?? 1f), 0.5f, 1.25f);
```

Crossfades on zone transition are handled by the 3-band quantization itself — the band change is near-instant, and the `AudioSource` rolloff smooths perceived level. If on-device testing reveals audible zipper noise, the mitigation is a 200 ms linear ramp on `gain` (one float, one coroutine — no new subsystems).

### 3.4 Visualization rules

- Zones are **atmosphere**, not UI. No labels, no hard outlines, no floating text.
- Default alpha caps: sphere `0.04`, ring `0.12`. These are low enough to read-through during draw.
- Zone brightens when an instance inside it is currently playing a scheduled event (tap into `AudioEngine.OnEventScheduled`). Brightness decays over 250 ms.
- Focus state (pen pointer inside the sphere) brightens the ring to `0.25` — but only during Conducting Mode (future). Not wired in this PR.

### 3.5 Acceptance

- Committing a new Rhythm pattern places it near `DrumsFloor`; Harmony lands in `HarmonyBehind`; Melody lands in `MelodyFront`. User override via grab still works and persists.
- Dragging a pattern from `MelodyFront` into `PadsFar` audibly adds reverb and delay within 1–2 scheduled events.
- Zone spheres are visible but not distracting — the user should notice them only when looking for them.
- Loading a pre-v10 session places instances in zones correctly based on their saved positions (no explicit migration needed).
- New test: `SpatialZoneControllerTests` — at least 6 cases covering membership, deadband, default placement, zone load/missing-layout fallback.

### 3.6 Explicit non-goals for Workstream A

- **No musical constraints per zone** (scales, chord suggestions). Brainstorm §"Zones as improvisation lanes" — deferred to a separate design conversation.
- **No zone-level conducting** (fade the pads, crescendo the drums). That is the payload of workstream 4 (deferred).
- **No user-editable zones** in this PR. Layout is the ScriptableObject default; runtime repositioning waits for panel-recenter (Workstream B).

---

## 4. Workstream B — Small wins

Four independent polish items, each <1 day. Land as four commits inside one PR so each is individually revertable. Order below is the suggested implementation order (cheapest confirmation → widest surface).

### 4.1 Pressure readout on commit card

**Goal:** the commit card (`CommitCardPanel`) shows a one-line pressure summary so the user sees that pen pressure is being read.

| File | Change |
|---|---|
| [UI/Panels/CommitCardPanel.cs](../../Assets/RhythmForge/UI/Panels/CommitCardPanel.cs) | Add a small TMP label between existing shape-DNA rows. Format: `"Pressure: {avg:F2} avg · {peak:F2} peak"`. Pulls from `ShapeProfile3D.thicknessMean` (proxy for pressure avg) and a new `thicknessPeak` field. |
| [Core/Data/ShapeProfile3D.cs](../../Assets/RhythmForge/Core/Data/ShapeProfile3D.cs) | Add `thicknessPeak` (already computed transiently in [ShapeProfile3DCalculator.cs](../../Assets/RhythmForge/Core/Analysis/ShapeProfile3DCalculator.cs) — just persist it). Quantize to 2 decimals like siblings. |
| [Core/Session/StateMigrator.cs](../../Assets/RhythmForge/Core/Session/StateMigrator.cs) | Migration: backfill `thicknessPeak = thicknessMean * 1.4f` when loading old sessions. No version bump — additive field. |
| `Editor/ShapeProfile3DCalculatorTests.cs` | Add one assertion for `thicknessPeak` monotonicity vs `thicknessMean`. |

**Acceptance:** draw a pattern with varying pressure; commit card shows a peak noticeably higher than average. Draw a flat pressure pattern; values are close.

### 4.2 Width-and-color pen tint for tilt

**Goal:** the `LineRenderer` on the active stroke subtly shifts cyan (upright) to amber (tilted) and thickens `±15%` with stylus tilt. Confirms tilt is being read **before** it drives audio via ornaments (Workstream C).

| File | Change |
|---|---|
| [Interaction/StrokeCapture.cs](../../Assets/RhythmForge/Interaction/StrokeCapture.cs) | In the per-sample hook that already pushes to the current `LineRenderer`, compute `tilt = angle(stylusPose.up, worldUp)` once per sample. Write per-vertex color via `LineRenderer.colorGradient` and width via `widthCurve`. |

Tilt is already captured per sample (verified in [ShapeProfile3DCalculator.cs](../../Assets/RhythmForge/Core/Analysis/ShapeProfile3DCalculator.cs)). No new dependencies.

**Acceptance:** drawing with the pen held near-vertical produces a cool line; canting the pen at ~45° produces visibly warmer, slightly thicker strokes. No audible change yet — that arrives in Workstream C.

### 4.3 Haptics on sequenced events

**Goal:** left-hand controller gives a 6 ms / 0.12 amplitude pulse every time an instance the user is currently grabbing (or recently released, <1 s grace) fires a scheduled event. Tells the user "this pattern is alive in my hand."

| File | Change |
|---|---|
| [Interaction/InstanceGrabber.cs](../../Assets/RhythmForge/Interaction/InstanceGrabber.cs) | Track `_grabbedInstanceId` + `_lastReleaseTimeById`. Subscribe to `AudioEngine.OnEventScheduled`. When the event's `instanceId` matches and we are currently grabbing (or within 1 s of release), call `OVRInput.SetControllerVibration(freq: 0.2f, amp: 0.12f, LTouch)` for 6 ms (stop with a coroutine or `InvokeRepeating`-style delayed-zero). |
| [Audio/AudioEngine.cs](../../Assets/RhythmForge/Audio/AudioEngine.cs) | Expose `event Action<string> OnEventScheduled` — fired with `instanceId` from the per-voice schedule path. Lightweight; existing call sites already know the id. |

Only fires for **grabbed** instances to avoid amplifying a 16-instance session into a vibration festival.

**Acceptance:** grab a playing rhythm pattern — left controller pulses on every drum hit. Release, wait 2 s — pulses stop. Grab a silent (muted) pattern — no pulses.

### 4.4 Panel recenter

**Goal:** long-press `Y` (`ButtonTwo`) for ≥800 ms recentres both dock panels and the `SpatialZoneController` origin to the user's current head forward. Cheap fix for "I moved chairs and my controls are behind me."

| File | Change |
|---|---|
| [Interaction/InputMapper.cs](../../Assets/RhythmForge/Interaction/InputMapper.cs) | Add `ButtonTwoLongPress` (true for one frame after ≥800 ms hold). Track the press-start time; reset on release. |
| [Bootstrap/RhythmForgeBootstrapper.cs](../../Assets/RhythmForge/Bootstrap/RhythmForgeBootstrapper.cs) | In `Update`, when `ButtonTwoLongPress`: call existing `RepositionPanels()`, then `SpatialZoneController.Recentre(headPose)` (new method — translates all zone centres so `MelodyFront` sits 1 m in front of the new head forward, XZ-only, keeping Y fixed). |
| `Core/Session/SpatialZoneController.cs` | Implement `Recentre(Pose)` — translates cached centres, re-emits visualizer positions. |

**Acceptance:** user walks 2 m to the side, holds `Y` for a second, and both panels + the zone layout snap in front of their face. Release before 800 ms — nothing happens (normal `ButtonTwo` tap behaviour preserved).

---

## 5. Workstream C — MX Ink expressive buttons (ornament + accent)

**Goal:** give `MiddlePressure` and `BackButton` real musical roles. Middle-pressure squeeze adds an ornament flag to the current stroke; back-button during a short stroke flags it as an accent. Both flags thread through the `ShapeProfile3D` into every unified deriver and become audible changes in the pattern.

### 5.1 Input → flag mapping

| Input | Flag | Trigger condition |
|---|---|---|
| `MiddlePressure > 0.3` for ≥120 ms during a stroke | `ornamentFlag = true` on the stroke | Released when `MiddlePressure < 0.15` |
| `BackButton` held while a stroke's total duration is ≤450 ms AND path length ≤0.3 m | `accentFlag = true` on the stroke | Evaluated at `FinishStroke` |

Both flags are per-stroke, not per-sample. They live on the stroke record and get copied into the `ShapeProfile3D` once derivation happens.

### 5.2 Files touched

| File | Change |
|---|---|
| [Core/Analysis/ShapeProfile3DCalculator.cs](../../Assets/RhythmForge/Core/Analysis/ShapeProfile3DCalculator.cs) | Add `ornamentFlag` + `accentFlag` to `StrokeSample` (so callers can attach them on capture). `Derive()` signature gains two bool params (default false for back-compat). |
| [Core/Data/ShapeProfile3D.cs](../../Assets/RhythmForge/Core/Data/ShapeProfile3D.cs) | Add persisted `ornamentFlag`, `accentFlag`. Additive; no version bump. |
| [Core/Session/StateMigrator.cs](../../Assets/RhythmForge/Core/Session/StateMigrator.cs) | Backfill both flags to `false` on load. No version bump. |
| [Interaction/StrokeCapture.cs](../../Assets/RhythmForge/Interaction/StrokeCapture.cs) | Track middle-pressure dwell while drawing; set `_currentOrnamentFlag` when dwell exceeds 120 ms. At `FinishStroke`, compute `accentFlag` from back-button + stroke duration/length. Pass both into the capture record. |
| [Core/Sequencing/UnifiedShapeDeriverBase.cs](../../Assets/RhythmForge/Core/Sequencing/UnifiedShapeDeriverBase.cs) | Expose `protected bool Ornament => _profile.ornamentFlag;` and `Accent`. Derivers use them. |
| `Core/PatternBehavior/Behaviors/RhythmLoopBehavior.cs` | `Accent` → add a +20% velocity ghost on the off-beat adjacent to every primary hit. `Ornament` → add a 16th-note flam ahead of every 3rd primary hit. |
| `Core/PatternBehavior/Behaviors/MelodyLineBehavior.cs` | `Accent` → duplicate the highest note with +4 semitones on the next 16th as a stab. `Ornament` → insert a passing-tone grace on the strongest interval of each bar. |
| `Core/PatternBehavior/Behaviors/HarmonyPadBehavior.cs` | `Accent` → hard-panned stab clone on beat 1. `Ornament` → slow (bar-long) LFO on the top voice's pitch ±15 cents for living harmony. |
| `Core/PatternBehavior/Behaviors/MusicalShapeBehavior.cs` | Threads flags through to whichever specialised behavior it delegates to. |
| [UI/Panels/InspectorPanel.cs](../../Assets/RhythmForge/UI/Panels/InspectorPanel.cs) | Show "Ornamented" / "Accented" badges when the selected pattern carries either flag. |
| [UI/Panels/CommitCardPanel.cs](../../Assets/RhythmForge/UI/Panels/CommitCardPanel.cs) | Preview: if the stroke that just finished has either flag, show a one-line badge so the user knows they captured it. |
| `Editor/OrnamentAccentTests.cs` *(new)* | Deriver-level: derivation is deterministic with both flags set, and the output event stream differs from the flag-cleared baseline in the expected places (extra ghost on rhythm, passing tone on melody, LFO on harmony). |

### 5.3 Interaction with drawing

- Ornament (middle-pressure squeeze): can be held **during** drawing. The user draws normally; squeezing the middle while the tip is down adds the flag. Feels like "underline this stroke."
- Accent (back-button short stroke): the back-button is a modifier. Hold it, make a short jab with the pen — that jab commits as an accented stroke. A normal-length stroke with back-button held does **not** get the accent flag, because the short-stroke rule guards it.

Both rules are deliberately conservative to avoid false positives. Tuning will happen on-device; the thresholds in §5.1 are starting points.

### 5.4 Acceptance

- Drawing a rhythm with middle-pressure squeezes active produces flams on ~33% of hits.
- Drawing a melody with continuous middle pressure produces passing tones in every bar.
- Short stroke with `BackButton` held commits an accented pattern; the inspector shows the badge.
- Existing sessions load without the badges; derivation output is bit-identical to pre-PR.
- All existing editor tests pass. New `OrnamentAccentTests` covers the three deriver paths.

---

## 6. Risk handling

### Risk A1 — Zone spheres visually compete with strokes

**Mitigation:** alpha caps at `0.04` (sphere) / `0.12` (ring). No labels. Brightness only reacts to playback, not hover, in this PR. On-device check: read a freshly-drawn amber stroke through the `MelodyFront` sphere — it must remain fully legible.

### Risk A2 — Zone auto-placement surprises the user

**Mitigation:** auto-placement only fires when the user commits a pattern *without* manually positioning it (i.e., the commit flow used the default spawn location). Any grabbed-and-placed pattern keeps its placement. If on-device testing reveals this is still confusing, expose a per-session toggle "auto-place by type" defaulting to `true` on the Transport panel — but only if needed; don't pre-build it.

### Risk A3 — Crossfade zipper noise on zone transitions

**Mitigation:** the 3-band quantization already prevents continuous resends from re-rendering the clip cache. On-device: if a zipper is audible when dragging a pattern slowly across a zone boundary, add a 200 ms ramp on `gainBias` in `AudioEngine`. Single-value change; not built preemptively.

### Risk B1 — Haptics fatigue

**Mitigation:** only pulses while grabbing (or for 1 s after release). Pulse is 6 ms / 0.12 — well below the threshold at which Quest controller vibration becomes annoying in prolonged use. If a tester reports fatigue, halve the amplitude; do not build a user-facing toggle unless multiple testers agree.

### Risk C1 — Ornament flag false positives during normal drawing

**Mitigation:** 120 ms dwell threshold. Users who accidentally brush the middle cluster during a fast stroke will not cross it. On-device: if false positives still happen, raise dwell to 180 ms before building a UI affordance.

### Risk C2 — Ornament-driven deriver output grows the clip cache

**Mitigation:** `ornamentFlag` and `accentFlag` go into the `ResolvedVoiceSpec` cache key like any other input. Two new bits = up to 4× cache size for keys that actually carry them — most voices never will (accent/ornament are per-pattern, not per-voice). Observed worst-case: 96 × 4 = 384 cached clips; the LRU in [SamplePlayer.cs](../../Assets/RhythmForge/Audio/SamplePlayer.cs) caps at 96 and will churn. If churn shows in profiling, raise `_maxCachedClips` to 160. Quest 3 memory has headroom.

### Risk C3 — Deriver changes break existing session audio

**Mitigation:** flags default to `false` everywhere; unflagged patterns derive identically to pre-PR. Pin this with a test: `OrnamentAccentTests.DerivationIdenticalWhenFlagsFalse` compares scheduled event streams bit-for-bit against a fixture.

---

## 7. Deferred work (explicit, with reasons)

Three workstreams remain from [SpatialAudio-Implementation.md §4](SpatialAudio-Implementation.md#L260). Each is re-justified here with the decision criteria for picking them up.

### Deferred — Conducting gesture recognizer (sway / lift / fade / cutoff)

- **Source:** [SpatialAudio-Implementation.md §4 "Deferred — Conducting gesture recognizer"](SpatialAudio-Implementation.md#L271), [SpatialOrchestrator-Plan.md §6.2](SpatialOrchestrator-Plan.md#L295), [SpatialOrchestrator-Brainstorm.md §"Conducting: the second creative act"](SpatialOrchestrator-Brainstorm.md#L141).
- **Why still deferred:**
  1. Highest false-positive risk of any feature in the vision. Gestures that accidentally trigger during a think-pause feel broken.
  2. Needs a *Conducting Mode* toggle on the Transport panel to be safe — new UI surface, settings persistence, mode-entry/exit animation.
  3. Requires zones (Workstream A) to have a target for "fade the pads" / "bring up the drums". This PR satisfies that prerequisite but does not exercise it.
  4. Needs on-device tuning of recognition thresholds that can only happen once zones are validated.
- **What unblocks it:** Workstream A must land and be validated on-device first.
- **Scope when picked up:**
  - `Interaction/ConductorGestureRecognizer.cs` *(new)* — heuristic (no ML), windowed analysis of stylus pose at ~60 Hz. Four recognizers: sway (BPM nudge), lift/Tendu (zone crescendo), fade/Plié (zone fade), cut-off (zone mute next bar).
  - Runs only when `StrokeCapture.DrawPressure < 0.05` — never competes with drawing.
  - New Transport toggle "Conducting Mode" defaulting to off.
  - Bimanual modifier via `LeftGrip` (already in [InputMapper.cs:36](../../Assets/RhythmForge/Interaction/InputMapper.cs#L36)): "apply to all zones."
  - Two-handed cut-off guard: requires `LeftGrip` + pen chop to prevent cough-shaped false positives.
  - Minimum one week of on-device testing before merging.

### Deferred — Post-source reverb/delay buses

- **Source:** [SpatialAudio-Implementation.md §4 "Deferred — Post-source reverb/delay buses"](SpatialAudio-Implementation.md#L285), [SpatialOrchestrator-Plan.md §8 risk 2](SpatialOrchestrator-Plan.md#L341).
- **Why still deferred:**
  1. Current implementation bakes reverb/delay into the rendered clip via [AudioEffectsChain.cs](../../Assets/RhythmForge/Audio/Synthesis). Moving to post-source sends is a real audio-pipeline rewrite.
  2. The 3-band quantization in [VoiceSpecResolver](../../Assets/RhythmForge/Audio/VoiceSpec/VoiceSpecResolver.cs) (shipped in `3a1f448`) is "good enough" for the orchestrator feel. Until a user or tester complains, there is no forcing function.
  3. Touches the `RhythmForgeMixer.mixer` asset — merge-conflict hazard if done concurrently with anything else audio-related.
- **What unblocks it:**
  - A performance or fidelity issue that cannot be fixed inside the quantized model (e.g., noticeable stepping between reverb bands during slow drags; a tester reporting "the reverb feels computed, not felt").
  - OR a desire to add continuous per-instance reverb control (not currently on any roadmap).
- **Scope when picked up:**
  - Extend [RhythmForgeMixer.mixer](../../Assets/RhythmForge/Audio/RhythmForgeMixer.mixer) with `ReverbSend` and `DelaySend` groups and their return chains.
  - Route `InstanceVoicePool` `AudioSource`s through the main group, add `AudioMixer.SetFloat` send levels per source per frame (throttled — not per scheduled event).
  - Remove `reverbBias` / `delayBias` from the `ResolvedVoiceSpec` cache key; re-warm the cache.
  - Remove the 3-band quantization code path.
  - Regression test: A/B against current sessions. Perceived difference should be "smoother, not different."
  - Estimate: 3–5 days including on-device tuning.

### Deferred — Moonshots (pattern-to-pattern proximity, temporal evolution, multi-user)

- **Source:** [SpatialAudio-Implementation.md §4 "Deferred — Pattern-to-pattern proximity …"](SpatialAudio-Implementation.md#L292), [SpatialOrchestrator-Brainstorm.md §"Instance lifecycle"](SpatialOrchestrator-Brainstorm.md#L201) and [§"Ideas ranked … Moonshot"](SpatialOrchestrator-Brainstorm.md#L308).
- **Why still deferred:**
  1. Each one changes the musical model substantially, not just the audio pipeline.
     - *Proximity influence:* nearby instances cross-modulate each other's derivers. Turns patterns into semi-autonomous agents.
     - *Temporal evolution:* patterns mutate over time under listener attention / neglect. Requires a session clock, attention tracking, determinism guarantees for session save/load.
     - *Multi-user:* networking, ownership, synchronization clocks, conflict resolution — an entirely new engineering axis.
  2. All three should be prototyped only after the core spatial experience (including zones + conducting) has been validated with real users. Prototyping earlier means optimising things users do not yet want.
  3. Each deserves its own design doc before any code lands. Bundling them or even sketching them here is premature.
- **What unblocks them:** user-testing feedback that specifically calls for one of the three. Not "this would be cool" internally, but "I kept trying to X and the system wouldn't let me."
- **Scope when picked up:** separate design doc per idea, separate branch per idea, no bundling.

---

## 8. File-by-file cheat sheet (implementation order)

Order = the order files will actually be edited/created. Workstream boundaries shown so each can split into its own commit (or PR).

### Workstream A — Spatial zones
| # | File | Action |
|---|---|---|
| 1 | `Assets/RhythmForge/Core/Data/SpatialZone.cs` | **new** |
| 2 | `Assets/RhythmForge/Config/SpatialZoneLayout.asset` | **new ScriptableObject** |
| 3 | `Assets/RhythmForge/Core/Session/SpatialZoneController.cs` | **new** |
| 4 | `Assets/RhythmForge/Core/Data/PatternInstance.cs` | edit (transient `currentZoneId`) |
| 5 | `Assets/RhythmForge/Core/Session/SessionStore.cs` | edit (auto-placement on commit) |
| 6 | `Assets/RhythmForge/Audio/AudioEngine.cs` | edit (zone bias stacking) |
| 7 | `Assets/RhythmForge/UI/SpatialZoneVisualizer.cs` | **new** |
| 8 | `Assets/RhythmForge/Bootstrap/RhythmForgeBootstrapper.cs` | edit (spawn controller + visualizers) |
| 9 | `Assets/RhythmForge/Editor/SpatialZoneControllerTests.cs` | **new** |

### Workstream B — Small wins
| # | File | Action |
|---|---|---|
| 10 | `Assets/RhythmForge/Core/Data/ShapeProfile3D.cs` | edit (+`thicknessPeak`) |
| 11 | `Assets/RhythmForge/Core/Analysis/ShapeProfile3DCalculator.cs` | edit (persist `thicknessPeak`) |
| 12 | `Assets/RhythmForge/Core/Session/StateMigrator.cs` | edit (backfill `thicknessPeak`) |
| 13 | `Assets/RhythmForge/UI/Panels/CommitCardPanel.cs` | edit (pressure readout) |
| 14 | `Assets/RhythmForge/Interaction/StrokeCapture.cs` | edit (per-vertex color + width from tilt) |
| 15 | `Assets/RhythmForge/Audio/AudioEngine.cs` | edit (expose `OnEventScheduled`) |
| 16 | `Assets/RhythmForge/Interaction/InstanceGrabber.cs` | edit (haptics on grabbed-instance events) |
| 17 | `Assets/RhythmForge/Interaction/InputMapper.cs` | edit (`ButtonTwoLongPress`) |
| 18 | `Assets/RhythmForge/Core/Session/SpatialZoneController.cs` | edit (`Recentre(Pose)`) |
| 19 | `Assets/RhythmForge/Bootstrap/RhythmForgeBootstrapper.cs` | edit (long-press handler) |
| 20 | `Assets/RhythmForge/Editor/ShapeProfile3DCalculatorTests.cs` | edit (thicknessPeak test) |

### Workstream C — Ornament + accent
| # | File | Action |
|---|---|---|
| 21 | `Assets/RhythmForge/Core/Analysis/ShapeProfile3DCalculator.cs` | edit (`StrokeSample` + `Derive` take flags) |
| 22 | `Assets/RhythmForge/Core/Data/ShapeProfile3D.cs` | edit (+ornament/accent flags) |
| 23 | `Assets/RhythmForge/Core/Session/StateMigrator.cs` | edit (backfill flags) |
| 24 | `Assets/RhythmForge/Interaction/StrokeCapture.cs` | edit (dwell tracking + accent rule) |
| 25 | `Assets/RhythmForge/Core/Sequencing/UnifiedShapeDeriverBase.cs` | edit (expose flags to derivers) |
| 26 | `Assets/RhythmForge/Core/PatternBehavior/Behaviors/RhythmLoopBehavior.cs` | edit (flam + ghost) |
| 27 | `Assets/RhythmForge/Core/PatternBehavior/Behaviors/MelodyLineBehavior.cs` | edit (stab + passing tone) |
| 28 | `Assets/RhythmForge/Core/PatternBehavior/Behaviors/HarmonyPadBehavior.cs` | edit (stab + LFO) |
| 29 | `Assets/RhythmForge/Core/PatternBehavior/Behaviors/MusicalShapeBehavior.cs` | edit (thread flags) |
| 30 | `Assets/RhythmForge/UI/Panels/InspectorPanel.cs` | edit (badges) |
| 31 | `Assets/RhythmForge/UI/Panels/CommitCardPanel.cs` | edit (badges on preview) |
| 32 | `Assets/RhythmForge/Editor/OrnamentAccentTests.cs` | **new** |

Nothing outside this table is in scope. If a change tempts an edit to an unlisted file, that's the signal it belongs in a follow-up PR.

---

## 9. Acceptance for the whole plan

Before merging the combined PR (or the last of three sequential PRs):

1. All existing editor tests pass (`Assets/RhythmForge/Editor/*Tests.cs`). Current count at baseline: `86/86`.
2. New tests pass: `SpatialZoneControllerTests`, extended `ShapeProfile3DCalculatorTests`, `OrnamentAccentTests`.
3. In-editor simulator: creating a Rhythm / Melody / Harmony pattern in succession produces three patterns that auto-place into different zones. Dragging between zones audibly changes reverb.
4. On-device (Quest 3, headphones):
   - Zones are faintly visible but never distracting during draw.
   - Commit card shows pressure values that track pen behaviour.
   - Middle-pressure squeeze during a rhythm produces flams on playback.
   - Short back-button stab produces an accented pattern.
   - Long-press `Y` recentres panels and zones to the current head pose.
   - Haptics pulse the left controller only for the currently grabbed pattern.
5. No regressions: pre-PR save files load and sound identical (flags default to false; no `thicknessPeak` forces a re-render).
6. Voice count during a stress session (8 instances playing, 3 grabbed in sequence) stays under 32 — unchanged from the baseline.

Once those six pass, this PR completes the safe-zone subset of the orchestrator vision. The remaining deferred work (conducting, post-source buses, moonshots) stands on this foundation.
