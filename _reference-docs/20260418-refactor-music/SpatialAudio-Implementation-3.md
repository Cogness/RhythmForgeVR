# Spatial Audio — Follow-up Implementation Plan #3 (Conducting, Post-Source Buses, Loose Ends)

Status: ready to implement, April 2026
Scope: builds on top of [SpatialAudio-Implementation-2.md](SpatialAudio-Implementation-2.md) (landed in commit `763f472`). Picks up the work that Plan #2 explicitly deferred to §7, plus a small set of loose ends discovered during verification of the Plan #2 baseline. Creative source: [SpatialOrchestrator-Brainstorm.md](SpatialOrchestrator-Brainstorm.md); full vision: [SpatialOrchestrator-Plan.md](SpatialOrchestrator-Plan.md).

This plan is **two safe additive PRs plus one trivial cleanup commit** on top of the spatial-audio + zones foundation. It gives the zones their first verbs (conducting), upgrades the FX pipeline from quantized bake to real sends (post-source buses), and tidies the small gaps left by Plan #2. The third group of deferred work — "moonshots" — stays deferred for the same reasons as before, with the unblock criteria spelled out.

---

## 1. Verification of Plan #2 baseline

Verified against `feature/from-2d-engine-single-shape` at commit `763f472` ("Spatial audio zones with expression flags (ornament/accent) and dynamic zone-based FX routing").

### What landed (Plan #2, Workstreams A, B, C)

| Plan #2 workstream | Status | Notes |
|---|---|---|
| **A — Spatial zones** | Shipped | [SpatialZone.cs](../../Assets/RhythmForge/Core/Data/SpatialZone.cs), [SpatialZoneLayout.cs](../../Assets/RhythmForge/Core/Data/SpatialZoneLayout.cs), [SpatialZoneController.cs](../../Assets/RhythmForge/Core/Session/SpatialZoneController.cs), [SpatialZoneVisualizer.cs](../../Assets/RhythmForge/UI/SpatialZoneVisualizer.cs); zone-bias stacking in [AudioEngine.cs:178-190](../../Assets/RhythmForge/Audio/AudioEngine.cs#L178-L190); spawn resolver wired in [RhythmForgeManager.cs:145](../../Assets/RhythmForge/RhythmForgeManager.cs#L145) |
| **B — Small wins** | Shipped | Pressure readout in [CommitCardPanel.cs](../../Assets/RhythmForge/UI/Panels/CommitCardPanel.cs); `thicknessPeak` in [ShapeProfile3D.cs](../../Assets/RhythmForge/Core/Data/ShapeProfile3D.cs); tilt color/width in [StrokeCapture.cs:186-199](../../Assets/RhythmForge/Interaction/StrokeCapture.cs#L186-L199); haptics in [InstanceGrabber.cs:228-246](../../Assets/RhythmForge/Interaction/InstanceGrabber.cs#L228-L246); `OnEventScheduled` in [AudioEngine.cs:23](../../Assets/RhythmForge/Audio/AudioEngine.cs#L23); `ButtonTwoLongPress` + zone recentre in [RhythmForgeBootstrapper.cs:170-186](../../Assets/RhythmForge/Bootstrap/RhythmForgeBootstrapper.cs#L170-L186) |
| **C — Ornament + accent** | Shipped (unified path only) | Flags flow `StrokeCapture → StrokeSample → ShapeProfile3D → MusicalShape → MusicalShapeBehavior`. The legacy per-type behaviors (`RhythmLoopBehavior` / `MelodyLineBehavior` / `HarmonyPadBehavior`) were **not** edited; in this codebase the unified `MusicalShapeBehavior` owns all three facets, so the legacy edits in Plan #2 §5.2 are obsolete and should be considered descoped — not a gap |

### Loose ends inside the shipped workstreams (carried into Workstream G below)

1. **No `SpatialZoneLayout.asset` ScriptableObject file.** Plan #2 §3.2 specified `Assets/RhythmForge/Config/SpatialZoneLayout.asset`. The `Config/` directory does not exist; runtime falls back to `SpatialZoneLayout.CreateDefault()` at [RhythmForgeBootstrapper.cs:311](../../Assets/RhythmForge/Bootstrap/RhythmForgeBootstrapper.cs#L311). Tuning currently requires code edits.
2. **`SpatialZoneControllerTests` only has 1–2 cases.** Plan #2 §3.5 asked for "at least 6 cases".
3. **`PadsFar` and `AccentsOverhead` are unreachable via auto-placement.** [SpatialZone.MatchesTarget](../../Assets/RhythmForge/Core/Data/SpatialZone.cs#L17-L30) does first-match-wins; `HarmonyBehind` is listed before `PadsFar` in the default layout, so a `HarmonyPad` commit always lands in `HarmonyBehind`. `AccentsOverhead` has `targetRole = "Ornament"` but no `PatternType` maps to it.
4. **Schema drift vs Plan #2 §3.2.** Plan said `targetType : PatternType`; actual is `targetRole : string`. Functional behavior is identical, so no schema change is required — but the code-vs-plan delta should be captured here so future readers don't think it's a bug.

### What was deferred in Plan #2 §7 (this plan picks up)

| Deferred from Plan #2 | Picked up here as | Stays deferred? |
|---|---|---|
| Conducting gesture recognizer (sway / lift / fade / cutoff) | **Workstream D** | No |
| Post-source reverb/delay buses | **Workstream E** | No |
| Moonshots (proximity, temporal, multi-user) | **Workstream F** | Yes — explicitly stays deferred; unblock criteria documented |
| Plan #2 minor loose ends (above) | **Workstream G** | No |

---

## 2. Workstream order (two PRs + one cleanup commit, ~6–9 days)

### Workstream D — Conducting gesture recognizer (biggest expressive payload)
### Workstream E — Post-source reverb/delay buses (FX pipeline upgrade)
### Workstream F — Moonshots (re-justified, stays deferred)
### Workstream G — Plan #2 loose-end cleanup (trivial, ships with D)

Chosen order: D first because zones are now landed and have no verbs yet; E only after D has been validated on-device, because E touches the entire audio pipeline and a regression in E would mask any tuning issues found in D. G ships with D as a single PR — it is small enough that it does not deserve its own.

PR shape:
- **PR #1 = D + G** (Conducting + cleanup) — 4–6 days including on-device tuning week.
- **PR #2 = E** (Post-source buses) — 3–5 days; only triggered by a real signal (see §5).
- **F** is not in scope for any PR in this plan.

---

## 3. Workstream D — Conducting gesture recognizer

**Goal:** when *Conducting Mode* is engaged on the Transport panel **and** the pen is not drawing, four heuristic gestures nudge zone-level audio: **sway** (BPM nudge), **lift / Tendu** (zone crescendo), **fade / Plié** (zone fade), **cut-off** (zone mute next bar). The bimanual modifier `LeftGrip` (already exposed at [InputMapper.cs:36](../../Assets/RhythmForge/Interaction/InputMapper.cs#L36)) means "apply to all zones, not just the targeted one." Cut-off requires `LeftGrip` + chop simultaneously to prevent cough-shaped false positives.

### 3.1 Recognition contract

Each recognizer fires **at most once per gesture** (no auto-repeat within a 600 ms refractory window per gesture kind). All four recognizers run in parallel; first one to satisfy its predicate wins the frame.

| Gesture | Detect when (defaults — tunable) |
|---|---|
| **Sway** | Pen tip XZ swings ≥ 0.25 m peak-to-peak, sustained ≥ 1.0 s, with at least 3 zero-crossings of XZ velocity. Drawing must be disabled. |
| **Lift / Tendu** | Pen tip Y delta > **+0.20 m** over 300 ms, with horizontal travel < 0.10 m in the same window. |
| **Fade / Plié** | Pen tip Y delta < **−0.20 m** over 300 ms, with horizontal travel < 0.10 m. |
| **Cut-off** | XZ peak velocity > **1.5 m/s** AND `LeftGrip` held during the chop. Two-handed guard is non-negotiable — the dominant false positive of Plan #2-style recognizers is sneeze/cough motion. |

**Zone selection:** the zone whose `transform.TransformPoint(zone.center)` is closest to the pen tip *at the moment the gesture started* (use the leading edge of the detection window, not the firing frame). If no zone is within 1.5 m, the gesture is dropped silently — gestures in empty space do nothing rather than affect the nearest zone (anti-surprise rule).

**Bimanual modifier:** if `LeftGrip` is held when the gesture fires, the targeted-zone field is replaced by the literal sentinel `"*"` and the controller applies the effect to **all** zones.

### 3.2 Files

| File | Change |
|---|---|
| `Core/Data/ConductorGesture.cs` *(new)* | `enum ConductorGestureKind { Sway, LiftTendu, FadePlie, CutOff }`. Plain record `ConductorGestureEvent { kind, targetZoneId (or "*"), magnitude (0..1), originPosition, dspTime }`. |
| `Core/Events/RhythmForgeEventBus.cs` | Two new events: `ConductingModeChangedEvent { On }`, `ConductorGestureFiredEvent { gesture }`. Subscriber-bus pattern; no breaking changes. |
| `Interaction/ConductorGestureRecognizer.cs` *(new)* | `MonoBehaviour`, `[DefaultExecutionOrder(15)]` (after `InputMapper -10`, before `StrokeCapture 20` so it can early-out when drawing). Maintains a 60 Hz rolling buffer (size 36 = 600 ms) of `(stylusTipPos, stylusVelocity, leftGrip)`. Runs four predicate checks each `LateUpdate`. Honors `_conductingModeOn`. Publishes `ConductorGestureFiredEvent` and calls `SpatialZoneController.ApplyConductorGesture(...)` directly (not via bus — latency budget). |
| `Core/Session/SpatialZoneController.cs` | New per-zone transient state (separate from the static `gainBias`/`reverbBias`/`delayBias` so the static layout stays pure): `Dictionary<string, ConductorState>` where `ConductorState { liveGainMult = 1f, liveReverbBoost = 0f, liveCutArmed = false, decayUntilDspTime }`. New methods: `ApplyConductorGesture(zoneIdOr*, kind, mag)`, `GetLiveBiases(zoneId, out float gainMult, out float reverbBoost, out bool cutArmed)`, `OnBarStart(absoluteBar, dspTime)` — drains armed cuts and decays live values. |
| `Audio/AudioEngine.cs` | `ApplyZoneBias` reads the live conductor biases on top of the static zone biases. Multiplicative for gain, additive (clamped 0..1) for reverb. If `cutArmed` for the instance's zone, the engine **skips the call** (no scheduling) for the current bar window. |
| `Sequencer/Sequencer.cs` | New `event Action<int, double> OnBarStart` fired exactly once per `transport.absoluteBar` change in the schedule loop (around [Sequencer.cs:122](../../Assets/RhythmForge/Sequencer/Sequencer.cs#L122)). Carries `(absoluteBar, dspTime)`. Recognizer + `SpatialZoneController` subscribe. |
| `Interaction/InputMapper.cs` | No new inputs; recognizer reads `StylusPose`, `LeftGrip`, `IsDrawing` directly. |
| `UI/Panels/TransportPanel.cs` | Add `_conductingButton + _conductingLabel` (defaults off). On click → `_conductingModeOn = !_conductingModeOn`, publish `ConductingModeChangedEvent`, recolor the button (orange when on). Subscribe to `ConductorGestureFiredEvent` to flash a tiny pulse on the button as a "you did a thing" affordance. |
| `Bootstrap/RhythmForgeBootstrapper.cs` | (a) `BuildTransportPanel`: add the Conducting button at `Rect(420, 8, 100, 84)` — note this collides with the existing Params button at the same rect; rebalance the layout (see §3.6). (b) `BuildSubsystems`: instantiate `ConductorGestureRecognizer` GO under root, configure with `InputMapper`, `SpatialZoneController.Shared`, `Sequencer.OnBarStart`, `EventBus`. (c) Subscribe `SpatialZoneController.OnBarStart` to `Sequencer.OnBarStart`. |
| `Editor/ConductorGestureRecognizerTests.cs` *(new)* | ≥ 8 cases covering each gesture's positive and negative path: see §3.5. Uses synthetic sample buffers — no Unity coroutines. |
| `Editor/SpatialZoneControllerLiveBiasTests.cs` *(new)* | Coverage for `ApplyConductorGesture`, decay, cut-armed bar transition, "*" all-zones modifier. |

### 3.3 Musical effect of each gesture

Effects are **transient overlays** on top of the existing static zone biases; they decay, they do not replace.

| Gesture | Per-zone effect | Decay |
|---|---|---|
| **Sway** | `Sequencer.BPM *= 1 + 0.03 · mag · sign(direction)`. `direction` = sign of XZ peak velocity at firing. | Linear decay back to baseline over **2 bars**. |
| **Lift / Tendu** | `liveGainMult *= 1.08^mag`, capped at `1.35`. | Linear decay over **4 bars**. |
| **Fade / Plié** | `liveGainMult *= 0.92^mag`, floored at `0.50`. | Linear decay over **4 bars**. |
| **Cut-off** | `liveCutArmed = true` for the targeted zone. On the next `OnBarStart`, the engine skips scheduling for instances whose `currentZoneId` matches the targeted zone for **1 bar**, then clears the flag. | Self-clears after 1 bar. |

**Sway** is intentionally tempo-only because that is the cleanest mapping that does not require subsystem knowledge of *which* zone the user means (BPM is global).

**Magnitude** is normalized 0..1 from the gesture's peak metric (e.g. for Lift: `mag = clamp01((y_delta - 0.20) / 0.30)` so a 50 cm lift saturates).

### 3.4 Conducting Mode — UX

- Default is **off**. The button must be deliberately enabled at the start of a session.
- When on, the recognizer runs only while `IsDrawing == false` AND no `PendingDraft` exists.
- Any new stroke commit (even a discarded one) **does not** auto-disable the mode — the user has explicit ownership of the toggle.
- The button glows orange while on and pulses briefly on each fired gesture so the user has positive evidence the system saw the gesture.
- A tiny `Toast` confirms the first gesture of each session (`"Conducting: sway → BPM +3%"`) and then stops toasting unless the gesture kind changes — avoids notification fatigue.

### 3.5 Test plan

`ConductorGestureRecognizerTests` (synthetic sample buffers, no Unity playmode):

1. `Sway_Detected_OnSustainedPeakToPeak` — feed 1.2 s of sinusoidal XZ with 0.30 m amplitude → `Sway` fires once.
2. `Sway_NotDetected_BelowAmplitudeThreshold` — 0.18 m amplitude → no fire.
3. `LiftTendu_Detected_FastVerticalRise` — +0.25 m Y over 300 ms, no horizontal travel → fires.
4. `LiftTendu_RejectedWhenHorizontalDriftPresent` — same Y rise but with 0.20 m XZ drift → no fire.
5. `FadePlie_Detected_FastVerticalDrop` — symmetric to #3.
6. `CutOff_RequiresLeftGrip` — fast chop without `LeftGrip` → no fire; same chop with `LeftGrip` → fires.
7. `Recognizer_DisabledWhileDrawing` — `IsDrawing = true`, all four predicates satisfied → no fire.
8. `Recognizer_DisabledWhenConductingModeOff` — same as #7 but `_conductingModeOn = false`.
9. `Refractory_Window_Prevents_Double_Fire` — same gesture inside 600 ms → only first fires.

`SpatialZoneControllerLiveBiasTests`:

1. `ApplyLiftTendu_RaisesGainMult_AndDecays`
2. `ApplyFadePlie_LowersGainMult_AndDecays`
3. `ApplyCutOff_ArmsZone_OnBarStart_ClearsAfterOneBar`
4. `BimanualStar_AppliesToAllZones`
5. `LiveBias_StacksOverStaticBias_WithoutMutatingIt`

### 3.6 Acceptance

1. With Conducting Mode **off**, idle pen movements (chopping, gesturing while talking, scratching nose with the stylus tip) have **zero** audible effect. This is the primary failure mode being guarded against.
2. With Conducting Mode on, deliberate left-to-right sway over the rig nudges BPM up by ~3% (then decays).
3. Slow lift above the `MelodyFront` zone audibly crescendos the melody pattern in that zone over 4 bars.
4. Plié over `HarmonyBehind` audibly fades the pads.
5. Chop with `LeftGrip` mutes all instances in the targeted zone for exactly the next bar; the bar after, they resume at full level.
6. Drawing is never interrupted by a gesture — `StrokeCapture` always wins (`IsDrawing` short-circuits the recognizer).
7. The Transport panel layout reflows so the new Conducting button does not collide with the existing Params button. Suggested rebalance: Play (8,8,100,84) → Mode (120,8,100,84) → Shape (232,8,100,84) → Conducting (344,8,100,84) → Params (456,8,100,84) → BPM/Key text rows shifted down to (568+,…). Total panel width grows from 640 to **680 px**; update the Canvas size in `BuildTransportPanel`.
8. `≥ 1 week of on-device testing` before merging — thresholds in §3.1 are starting points; record any threshold change in this file via PR amend.

### 3.7 Risks

| Risk | Mitigation |
|---|---|
| **D1** False positives during think-pauses (the dominant Plan #2 concern). | The two-handed guard on cut-off is the strongest lever and is non-negotiable. For sway/lift/fade, the conservative thresholds in §3.1 plus the 1.5 m proximity guard mean an idle user has to deliberately reach toward a zone. If false positives still occur on-device, **raise** the duration thresholds (300 → 400 ms for lift/fade; 1.0 → 1.4 s for sway) before adding any UI affordance. |
| **D2** Conducting Mode forgotten on. | Pulsing orange button + first-gesture-toast give visible evidence. Do not auto-disable on stroke — the user toggle is explicit, and surprise-disable is worse than surprise-fire. |
| **D3** Bar-quantized cut-off feels delayed. | Acceptable trade-off — quantizing to bar boundaries avoids the unmusical mid-pattern silence that frame-quantized cuts would produce. If on-device testing finds this jarring, switch to **beat-quantized** (4× more granular). Do not switch to frame-quantized. |
| **D4** Recognizer competes with `PanelDragCoordinator`. | The drag coordinator only acts on `LeftGrip` while the ray hits a panel collider. The recognizer ignores frames where `PanelDragCoordinator.IsDragging == true`. Add a single `if (_dragCoordinator?.IsDragging ?? false) return;` early-out at the top of `LateUpdate`. |

### 3.8 Explicit non-goals for Workstream D

- **No machine-learning** of any kind. Heuristics only. If the heuristics need to grow beyond simple windowed thresholds, the right next step is to write a separate data-collection skill, not bolt ML into this PR.
- **No new gestures beyond the four named.** Brainstorm §"Conducting" lists more (drumroll cue, scoop). They wait until the four base recognizers are validated.
- **No per-zone independent BPM.** Sway is global. Per-zone BPM means polyrhythm, which is a much bigger musical decision.
- **No keyboard shortcut to enable Conducting Mode.** The button is the only path. Avoids accidental keyboard-trigger during in-VR sessions.

---

## 4. Workstream E — Post-source reverb/delay buses

**Goal:** replace the current per-event 3-band quantization of `reverbSend`/`delaySend` with **real `AudioMixer` send chains** routed through `RhythmForgeMixer.mixer`. Outcomes: continuous (not stepped) reverb when dragging across zone boundaries, a smaller `ResolvedVoiceSpec` cache (because reverb/delay biases drop out of the cache key), and a foundation for any future continuous-reverb UI affordance.

**Trigger to take this on:** a tester reports stepping artifacts when slow-dragging a pattern across a zone boundary, **or** a product decision to expose continuous per-instance reverb. **Without one of those triggers, do not start E.** The 3-band quantization is "good enough" today.

### 4.1 Files

| File | Change |
|---|---|
| `Audio/RhythmForgeMixer.mixer` | **Mixer asset edit (Unity Editor).** Add two new mixer groups under each existing genre submix: `<Genre>_ReverbSend` and `<Genre>_DelaySend`, each routed through an `AudioReverbFilter` and `AudioEchoFilter` respectively. Expose three parameters per send group: send level (dB, default −80), wet/dry mix, return level. Document the parameter names in `Audio/RhythmForgeMixer.md` (new). |
| `Audio/RhythmForgeMixer.md` *(new)* | Reference for mixer parameter names so other devs do not have to open the asset to know what to `SetFloat`. |
| `Audio/InstanceVoicePool.cs` | Add `SetReverbSendDb(float db)` and `SetDelaySendDb(float db)`. These call `_mixer.SetFloat(_reverbParamName, db)` where `_reverbParamName` is resolved per-instance at pool creation (e.g. `"Inst{shortId}_ReverbSend"`). **Throttle:** SetFloat call only when the target value changes by ≥ 0.5 dB **and** at most once every 4 audio frames (~67 ms at 60 Hz). |
| `Audio/InstanceVoiceRegistry.cs` | When pool is created, allocate a unique mixer parameter index from a small pool (16 slots reused via LRU). Pools beyond slot 16 fall back to the legacy baked-in path — capacity is unchanged. Document the slot table in `Audio/RhythmForgeMixer.md`. |
| `Audio/AudioEngine.cs` | In `ApplyZoneBias`, instead of mutating `ref reverbSend` / `ref delaySend` on the resolved spec, compute a target dB and call `pool.SetReverbSendDb(targetDb)` / `pool.SetDelaySendDb(targetDb)`. The `ResolvedVoiceSpec` no longer carries `reverbSend`/`delaySend`. |
| `Audio/VoiceSpec/VoiceSpecResolver.cs` | **Remove** `reverbSend` and `delaySend` from the cache key. **Keep** them as input parameters to the resolver methods so caller signatures stay stable, but ignore them in the cache key. Add a `preset.embedReverbTail : bool` (default `false`) — when `true` (e.g. for pads that need a tail baked into the sample), preserve the legacy quantized bake; otherwise drop the bake and rely on sends. Pads keep `embedReverbTail = true`. |
| `Audio/Synthesis/AudioEffectsChain.cs` | Make the reverb/delay synthesis paths conditional on `embedReverbTail` (or equivalent — the chain currently always renders; switch to a no-op pass when sends will handle it). |
| `Audio/SamplePlayer.cs` | No interface change; the cache shrinks naturally. Bump default `_maxCachedClips` down from `96` → `64` after measuring real cache size on a typical 8-instance session. |
| `Editor/AudioMixerRoutingTests.cs` *(new)* | Playmode smoke tests: (a) muting `<Genre>_ReverbSend` produces a dry output at the master; (b) `SetReverbSendDb(0)` vs `SetReverbSendDb(−80)` produces a measurable RMS difference at the listener position; (c) per-instance send isolation — adjusting Instance A's send does not change Instance B. |
| `Editor/VoiceSpecResolverCacheKeyTests.cs` | Update existing cache-key tests: same input with `reverbSend = 0.1` vs `reverbSend = 0.6` must now produce the **same** cache key (the opposite of pre-PR). |

### 4.2 Migration of existing sessions

Existing saved sessions store `reverbSend` / `delaySend` on `PatternInstance`. **No data migration needed** — those fields keep their meaning, but their effect path moves from "baked into clip via cache key" to "applied via SetFloat per pool". The fields are still the source of truth; only their downstream rendering changes.

If pre-PR session files were rendered and bounced to disk (none currently), the bounce would need to be re-rendered. Confirm with the user that no offline bounces exist before merging.

### 4.3 Acceptance

1. Slow drag of a pattern across the boundary between `MelodyFront` and `HarmonyBehind` produces a **continuous** reverb swell — no audible bands or steps.
2. `ResolvedVoiceSpec` cache size in `SamplePlayer` diagnostics drops by **≥ 25%** over a representative 8-instance session.
3. A 3-listener A/B blind test: pre-PR vs post-PR sessions sound "smoother but not different" — no listener identifies the post-PR mix as the wrong reference more than chance (50%).
4. CPU regression on Quest 3 ≤ **1.5%** measured over a 5-minute stress session (8 instances playing, 3 grabbed in sequence, 2 conducted).
5. All existing editor tests pass (current count baseline: per Plan #2 §9, `86/86` plus the tests added in Plan #2 — verify the actual count at PR open).
6. New tests pass: `AudioMixerRoutingTests`, updated `VoiceSpecResolverCacheKeyTests`.
7. No memory regression: peak audio memory on Quest 3 within ±5% of baseline.

### 4.4 Risks

| Risk | Mitigation |
|---|---|
| **E1** Mixer asset merge conflicts. | Single-developer PR. No concurrent audio edits. Ask the user to confirm no other audio work is in flight before opening this PR. |
| **E2** Some presets actually rely on the baked tail (pads, atmospheric stabs). | The `embedReverbTail` opt-in preserves the legacy path for those presets. Default is `false`; pad-family presets opt in. |
| **E3** `SetFloat` spam degrades audio thread. | Throttle to ≥ 0.5 dB delta AND ≤ 1 call per 4 audio frames per pool. At 16 instances and 60 Hz that's a worst-case 240 calls/s, well under Oculus Audio's documented thresholds. |
| **E4** Mixer slot pool exhausted (> 16 simultaneous instances). | Fall back to the legacy baked-in path for instances beyond slot 16. They lose continuous-reverb behavior but still play correctly. Document this ceiling in `Audio/RhythmForgeMixer.md`. |
| **E5** Continuous reverb makes everything sound "wet". | Re-tune the per-zone `reverbBias` defaults in `SpatialZoneLayout.CreateDefault()` after E lands. Likely halve them, since the perceptual response curve of continuous-send is steeper than the quantized one. |

### 4.5 Estimate

3–5 days including on-device A/B and zone-bias re-tuning.

### 4.6 Explicit non-goals for Workstream E

- **No new user-facing reverb/delay UI.** The send levels remain derived from instance position + zone biases. Adding a manual reverb slider is a separate design conversation.
- **No mixer-snapshot system** for genre changes (snapshots could blend genre→genre over time). Out of scope; current snap-cut behavior is preserved.
- **No master FX bus changes.** Only the per-genre submix groups gain new sends.

---

## 5. Workstream F — Moonshots (stays deferred)

Three workstreams from Plan #2 §7 and earlier brainstorms. Each remains deferred for the same reasons as Plan #2; the unblock criteria are restated so a future agent can recognize when a moonshot becomes ready.

### F.1 Pattern-to-pattern proximity influence

**What it is:** instances within a configurable radius of each other cross-modulate each other's derivers (e.g. neighbor's brightness pulls this one's brightness toward it; neighbor's BPM nudges this one's quantization). Turns instances into semi-autonomous agents.

**Why still deferred:** changes the musical model substantially (deterministic-derivation invariant breaks unless we add an explicit influence-graph snapshot to `AppState`). Risks emergent feedback loops that are very hard to debug.

**Unblock criteria:**
- A user/tester says, in their own words, *"I kept moving these two shapes close together expecting them to interact"* — or equivalent. Not "this would be cool from inside the team."
- A separate design doc covering the influence-graph data model, determinism guarantees for save/load, and the UI affordance for "what's influencing this pattern right now."

**Scope when picked up:** separate branch, separate design doc, **no bundling**.

### F.2 Temporal evolution

**What it is:** patterns mutate over time under listener attention or neglect — for example, an unattended pattern slowly wanders its rhythm seed, while an attended pattern locks. Requires a session clock, attention tracking, and explicit determinism guarantees for save/load.

**Why still deferred:** undermines the determinism invariant the entire test suite relies on. Either we sacrifice determinism (huge testing cost) or we add a per-instance deterministic time-step that survives save/load (large engineering cost).

**Unblock criteria:**
- A user/tester reports that a pattern feels "stuck" or "lifeless" after extended listening, in a way that simply tweaking the deriver cannot fix.
- A design decision on how attention is measured (gaze? grab-history? proximity?).

**Scope when picked up:** separate design doc covering attention model, mutation budget, save/load semantics. Separate branch.

### F.3 Multi-user

**What it is:** two or more headsets in the same session, each contributing patterns and conducting gestures.

**Why still deferred:** entirely new engineering axis (networking, ownership, conflict resolution, sync clocks). No code in this repo today is designed for it.

**Unblock criteria:** a product decision. No engineering precondition.

**Scope when picked up:** separate repo branch, full design doc, likely a multi-month engagement. Should not bundle with any deriver or audio-pipeline work.

---

## 6. Workstream G — Plan #2 loose-end cleanup (ships with D)

Trivial commit inside PR #1 (Conducting). Each item is < 30 minutes.

### 6.1 Author the SpatialZoneLayout asset

| File | Change |
|---|---|
| `Assets/RhythmForge/Config/SpatialZoneLayout.asset` *(new ScriptableObject asset)* | Authored in the editor via `Assets > Create > RhythmForge > Spatial Zone Layout`. Populate with the same defaults as `SpatialZoneLayout.CreateDefault()`. Add **two more zones** (see §6.3) so `PadsFar` and `AccentsOverhead` become reachable. |
| `Bootstrap/RhythmForgeBootstrapper.cs` | The `[SerializeField] _spatialZoneLayout` slot is already present; just assign the new asset in the prefab/scene. The `CreateDefault()` fallback stays as a safety net. |

### 6.2 Expand `SpatialZoneControllerTests`

| File | Change |
|---|---|
| `Editor/SpatialZoneControllerTests.cs` | Bring count to ≥ 6: (1) point-in-sphere positive, (2) point-in-sphere negative, (3) deadband retention when crossing boundary outward by < 10 cm, (4) handoff when crossing by > 10 cm, (5) default placement for each `PatternType`, (6) missing-layout fallback (no zones → no bias and no exception), (7) `Recentre(headPose)` re-applies assignments correctly. |

### 6.3 Make `PadsFar` and `AccentsOverhead` reachable

The current `MatchesTarget` is first-match-wins, so `PadsFar` (also `targetRole = "Harmony"`) is unreachable. Two options; pick **A** by default:

**Option A (recommended): depth-aware Harmony placement.**

| File | Change |
|---|---|
| `Core/Data/SpatialZoneLayout.cs` | Add an optional `float depthZThreshold` per zone (default `0` = ignored). When a `HarmonyPad` commit happens, if the draft's `spawnPosition.z` (in `SpatialZoneController` local space) is greater than the threshold, prefer the deeper zone. Implementation: extend `SpatialZone.MatchesTarget(PatternType, Vector3? localPosition)` to take an optional position and break ties by closest-z when multiple zones match. |
| `Core/Session/SpatialZoneController.cs` | `TryGetDefaultPlacementFor` now passes the would-be spawn position into `MatchesTarget`. Falls back to first-match if no position given. |

**Option B (smaller change): introduce `PatternType.OrnamentStab` and a deeper Harmony commit modifier.** Defer this — adds more surface area than the loose-end deserves.

### 6.4 Reconcile schema drift

| File | Change |
|---|---|
| Inline doc comments in `Core/Data/SpatialZone.cs` | Add `// Schema note: Plan #2 §3.2 specified PatternType targetType; we use string targetRole for forward-compat with future role names ("Ornament", "Pad-Far") that don't map cleanly to PatternType.` |

### 6.5 Acceptance for G

1. `SpatialZoneLayout.asset` exists in `Assets/RhythmForge/Config/`; the bootstrapper picks it up automatically (no `CreateDefault()` fallback in normal play).
2. Editing the asset in the inspector and re-entering Play mode applies the change without code edits.
3. Committing 5 sequential `HarmonyPad` patterns at increasing depth (`z` from 0.5 m to 3.0 m) places them across both `HarmonyBehind` and `PadsFar` based on depth, instead of all in `HarmonyBehind`.
4. `SpatialZoneControllerTests` has ≥ 6 cases, all green.

---

## 7. File-by-file cheat sheet (implementation order)

Order = the order files are actually edited/created. PR boundaries shown so each PR ships independently.

### PR #1 — Workstream D (Conducting) + Workstream G (Cleanup)

| # | File | Action |
|---|---|---|
| 1 | `Assets/RhythmForge/Core/Data/ConductorGesture.cs` | **new** |
| 2 | `Assets/RhythmForge/Core/Events/RhythmForgeEventBus.cs` | edit (+ `ConductingModeChangedEvent`, `ConductorGestureFiredEvent`) |
| 3 | `Assets/RhythmForge/Sequencer/Sequencer.cs` | edit (`OnBarStart` event) |
| 4 | `Assets/RhythmForge/Core/Session/SpatialZoneController.cs` | edit (per-zone live state, `ApplyConductorGesture`, `OnBarStart`) |
| 5 | `Assets/RhythmForge/Audio/AudioEngine.cs` | edit (`ApplyZoneBias` reads live biases, honors `cutArmed`) |
| 6 | `Assets/RhythmForge/Interaction/ConductorGestureRecognizer.cs` | **new** |
| 7 | `Assets/RhythmForge/UI/Panels/TransportPanel.cs` | edit (Conducting button + label, layout reflow) |
| 8 | `Assets/RhythmForge/Bootstrap/RhythmForgeBootstrapper.cs` | edit (panel widened to 680 px, recognizer GO, wire `OnBarStart`, assign `SpatialZoneLayout.asset`) |
| 9 | `Assets/RhythmForge/Editor/ConductorGestureRecognizerTests.cs` | **new** |
| 10 | `Assets/RhythmForge/Editor/SpatialZoneControllerLiveBiasTests.cs` | **new** |
| 11 | `Assets/RhythmForge/Config/SpatialZoneLayout.asset` | **new (ScriptableObject asset, edit-only)** |
| 12 | `Assets/RhythmForge/Core/Data/SpatialZone.cs` | edit (schema-drift comment + optional position-aware match) |
| 13 | `Assets/RhythmForge/Core/Data/SpatialZoneLayout.cs` | edit (defaults match the asset; add `depthZThreshold` if Option A from §6.3) |
| 14 | `Assets/RhythmForge/Core/Session/SpatialZoneController.cs` | edit (depth-aware placement) — second pass after step 4 |
| 15 | `Assets/RhythmForge/Editor/SpatialZoneControllerTests.cs` | edit (expand to ≥ 6 cases) |

### PR #2 — Workstream E (Post-source buses)

| # | File | Action |
|---|---|---|
| 16 | `Assets/RhythmForge/Audio/RhythmForgeMixer.mixer` | edit (per-genre `<Genre>_ReverbSend`, `<Genre>_DelaySend` groups + return chains) |
| 17 | `Assets/RhythmForge/Audio/RhythmForgeMixer.md` | **new** (parameter naming reference) |
| 18 | `Assets/RhythmForge/Audio/InstanceVoiceRegistry.cs` | edit (mixer-slot allocator, 16 slots) |
| 19 | `Assets/RhythmForge/Audio/InstanceVoicePool.cs` | edit (`SetReverbSendDb`, `SetDelaySendDb`, throttle) |
| 20 | `Assets/RhythmForge/Audio/VoiceSpec/VoiceSpecResolver.cs` | edit (drop reverb/delay from cache key, gate on `embedReverbTail`) |
| 21 | `Assets/RhythmForge/Audio/Synthesis/AudioEffectsChain.cs` | edit (no-op the chain when sends will handle it) |
| 22 | `Assets/RhythmForge/Audio/AudioEngine.cs` | edit (route via `pool.SetReverbSendDb`, drop `ref reverbSend`/`ref delaySend` mutation) |
| 23 | `Assets/RhythmForge/Audio/SamplePlayer.cs` | edit (`_maxCachedClips` 96 → 64 after measuring) |
| 24 | `Assets/RhythmForge/Editor/AudioMixerRoutingTests.cs` | **new** |
| 25 | `Assets/RhythmForge/Editor/VoiceSpecResolverCacheKeyTests.cs` | edit (cache-key invariance under reverb/delay changes) |
| 26 | `Assets/RhythmForge/Core/Data/SpatialZoneLayout.cs` + `.asset` | edit (re-tune `reverbBias` defaults — likely halve — based on continuous-send response curve) |

Nothing outside this table is in scope for either PR. If a change tempts an edit to an unlisted file, that's the signal it belongs in a follow-up PR.

---

## 8. Acceptance for the whole plan (per PR)

### PR #1 — D + G

Before merging:

1. All existing editor tests pass. Baseline at PR open: confirm count.
2. New tests pass: `ConductorGestureRecognizerTests` (≥ 8 cases), `SpatialZoneControllerLiveBiasTests` (≥ 5 cases), expanded `SpatialZoneControllerTests` (≥ 6 cases).
3. In-editor simulator: toggle Conducting on, fire each of the four gestures via synthetic input, observe corresponding mixer/state change.
4. On-device (Quest 3, headphones), with Conducting **off**:
   - Plan #2 acceptance criteria still hold (no regressions): zones visible, commit card shows pressure, ornament/accent flags still produce flams/passing tones, `Y` long-press recentres.
   - Idle gesturing produces zero audio change.
5. On-device with Conducting **on**:
   - Sway nudges BPM audibly.
   - Lift/Plié change zone level audibly within 1–2 bars.
   - Two-handed cut-off mutes the targeted zone for exactly one bar.
   - At least 1 week of mixed-use sessions without false-positive complaints.
6. `SpatialZoneLayout.asset` is the runtime source of truth — `CreateDefault()` only fires if the asset is missing.
7. Voice count during stress session unchanged from Plan #2 baseline (≤ 32).

### PR #2 — E

Before merging:

1. All editor tests pass, including the rewritten `VoiceSpecResolverCacheKeyTests` (cache-key invariance under reverb/delay).
2. `AudioMixerRoutingTests` pass in playmode.
3. Slow-drag across zone boundaries produces continuous reverb swell, no steps.
4. `_voiceCache` size measured on a typical 8-instance session shrinks by ≥ 25%.
5. CPU on Quest 3 within +1.5% of baseline; audio memory within ±5%.
6. 3-listener A/B blind test confirms "smoother, not different".
7. Pre-PR sessions load and play with the new pipeline (no save migration needed; `reverbSend`/`delaySend` fields keep their meaning).

---

## 9. Notes for the implementing agent

- **Read Plan #2 first.** This plan assumes you understand the Plan #2 baseline as actually shipped, not as originally specified. Any drift (e.g. `targetRole` vs `targetType`) is documented in §1 and §6.4.
- **PR #2 (E) is conditional.** Do not start E until a real trigger (per §4 opening) exists. Until then, this plan is one PR + one deferred-roadmap section.
- **No moonshots in either PR.** §5 is roadmap, not scope.
- **On-device tuning week for D is not optional.** The thresholds in §3.1 are starting points; record any change in this file via PR amend.
- **If anything in the Plan #2 baseline drifted further between writing and implementation** (new commits on `feature/from-2d-engine-single-shape`), re-verify §1 before starting and update §1 in the same PR.
- **All four named gestures must ship together.** Do not partial-merge (e.g. ship sway + lift, defer fade + cutoff). The user's mental model is "I can conduct" — anything less is worse than nothing because it teaches the wrong affordance.
- **F (moonshots) stays a roadmap section.** If a moonshot becomes urgent during implementation of D or E, write a separate design doc rather than expanding this plan.
