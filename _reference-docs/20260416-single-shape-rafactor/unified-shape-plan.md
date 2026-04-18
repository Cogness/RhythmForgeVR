# Unified 3D-Shape Plan — One Shape Plays Rhythm + Melody + Harmony

*Companion to `_reference-docs/20260415-musical-refactoring/musical-coherence-plan.md`. This plan builds on top of the role / register / gain-staging scaffolding introduced there; it does not replace it.*

---

## Context & Why This Is Non-Trivial

Today every stroke is locked to a single `PatternType` (`Assets/RhythmForge/Core/Data/PatternType.cs:3-8` — `RhythmLoop` | `MelodyLine` | `HarmonyPad`). At capture time `StrokeCapture.FinishStroke()` **flattens the 3D input to a 2D plane** before analysis:

- `Assets/RhythmForge/Interaction/StrokeCapture.cs:181-186` — builds a best-fit plane from the 3D world points and projects every point onto it.
- `Assets/RhythmForge/Interaction/StrokeCapture.cs:304-315` — `ProjectTo2D` discards the out-of-plane dimension entirely.

Consequences for the current "one shape → one mode" pipeline:

- Thickness (pen pressure *along* the stroke), stylus tilt, depth, helicity, speed-over-time — **none reach the deriver**; they are either discarded or collapsed into scalar averages.
- `DraftBuilder.BuildFromStroke` picks exactly one deriver per stroke (`Assets/RhythmForge/Core/Session/DraftBuilder.cs:82-88`), so a stroke can never produce rhythm AND melody AND harmony together.
- Multi-shape blending depends entirely on the role / register / gain system introduced in the previous plan. That system prevents same-mode collisions (four kalimbas stepping on each other) but does not let **one** shape be a full ensemble.

The good news: the previous plan already built the right load-bearing infrastructure for this next step — `ShapeRoleProvider`, `RegisterPolicy`, `PatternContextScope`, per-mode gain in `VoiceSpecResolver`, `HarmonicContextProvider`. We **widen its scope** from per-mode to per-shape rather than re-engineering it.

---

## Goal

1. One drawn 3D shape produces **rhythm + melody + harmony simultaneously**, tightly locked to the same grid and key.
2. 3D shape properties (thickness, tilt, depth, helicity, speed-over-time, closure) drive **major audible variation** across all three facets — not just cosmetic differences.
3. Adding more shapes = layering full phrases, each one a distinct ensemble voice, all blending musically even when the user draws randomly.
4. Zero possibility of wrong-key notes, clashing chords, or rhythmic chaos — musicality is *structurally guaranteed*, not hoped for.

---

## Design Principles

1. **Shape is the song idea, not a layer.** A shape ships with its own rhythm, melody, and harmony woven from the same 3D source.
2. **3D-native capture.** Drop the 2D projection. The stroke's thickness profile, tilt profile, and temporal profile are first-class features.
3. **Shared `HarmonicFabric`.** Every shape's harmony writes into a scene-wide chord-per-bar scaffold; later shapes' melody and rhythm read from it. No shape invents its own key.
4. **Role-driven ensemble.** Shape 0 = lead phrase (full voicing, wide stereo). Shape 1 = counter (offset 1/3 bar, root+5, octave-up melody). Shape 2 = pad/pedal. Generalizes the current per-mode role system to per-shape.
5. **Bond-strength weighting.** A shape doesn't emit all three facets equally — its 3D character decides the mix (thick+angular → rhythm-heavy; tall+smooth → melody-heavy; deep+closed+flat → harmony-heavy). Audibly different shapes, not three identical voices.
6. **Human-safe quantization.** Pitch → chord-tones on strong beats, pentatonic on weak. Time → 16th grid with role offsets. Density → bounded per role. Random gestures can't escape musicality.

---

## Proposal

### 1. `ShapeProfile3D` — new 3D feature vector

Extends `Assets/RhythmForge/Core/Data/ShapeProfile.cs:6-57` with dimensions derived from the **raw 3D stroke + pressure stream + stylus rotation stream**:

| New dimension | Derived from | Drives |
|---|---|---|
| `thicknessMean`, `thicknessVariance` | pen pressure samples along the stroke | velocity curve, accent pattern, filter motion |
| `tiltMean`, `tiltVariance` | stylus `Quaternion` samples | modal flavor (sus2/sus4/drone5), reverb depth, filter cutoff |
| `depthSpan` | bounding-box depth relative to max-dimension | bars count, harmony extension, sustain length |
| `planarity` | ratio (plane deviation / total extent) | rhythm density — flat = sparse, volumetric = busy |
| `elongation3D` | PCA major / minor / mid axes | melody phrase length vs. harmony duration |
| `helicity` | net rotation around stroke's PCA major axis | groove swing, microtiming |
| `centroidDepth` | front/back placement relative to draw origin | brightness, mix depth |
| `temporalEvenness` | variance of inter-sample `dt` | rhythm micro-timing, melody legato feel |
| `passCount` | number of self-intersections in 3D | melody note density, chord inversions |

Legacy 2D fields (`circularity`, `angularity`, `tilt`, `symmetry`, etc.) stay populated — computed from the stroke's best-fit plane exactly as today — so the existing `SoundMappingProfileAsset` weights continue to function. The 3D fields are **additive**.

New files:

- `Assets/RhythmForge/Core/Data/ShapeProfile3D.cs`
- `Assets/RhythmForge/Core/Analysis/ShapeProfile3DCalculator.cs`

### 2. 3D-native `StrokeCapture`

`Assets/RhythmForge/Interaction/StrokeCapture.cs:142-164` already captures `pressure` and 3D world points. Extend `AddPoint` to record:

- Stylus rotation (`IInputProvider.StylusPose.rotation` — already available from `InputMapper`).
- Sample timestamp (`Time.unscaledTimeAsDouble`).

Then:

- Remove the `ProjectTo2D` call at `Assets/RhythmForge/Interaction/StrokeCapture.cs:181`.
- Feed the full 3D stream directly to `ShapeProfile3DCalculator`.
- 2D points remain stored on `PatternDefinition` for visual rendering and legacy migration, but analysis uses the 3D stream.

New data carrier (internal to the capture → analysis hand-off):

```csharp
public struct StrokeSample
{
    public Vector3 worldPos;
    public float   pressure;     // 0..1 (existing)
    public Quaternion stylusRot; // NEW
    public double  timestamp;    // NEW — seconds since stroke start
}
```

### 3. `MusicalShape` — new domain entity bundling all three facets

```csharp
// Conceptual — lives alongside PatternDefinition
public class MusicalShape
{
    public string id;
    public ShapeProfile3D profile3D;
    public SoundProfile   soundProfile;
    public Vector3 bondStrength;        // (rhythm, melody, harmony) ∈ R³, sums to 1
    public int    roleIndex;            // 0..N across all shapes in scene
    public DerivedShapeSequence facets; // { rhythm, melody, harmony }
    public string keyName;
    public int    bars;
    // ... plus position / pan / etc. on the PatternInstance side
}

public class DerivedShapeSequence
{
    public RhythmSequence  rhythm;   // reuses existing types from Core/Data/SequenceData.cs
    public MelodySequence  melody;
    public HarmonySequence harmony;
}
```

`PatternType` does not disappear — it becomes a **facet tag** used internally by the scheduler (to pick the drum/tonal/chord audio path) and by the legacy migration code. New shapes always carry all three facets.

### 4. `UnifiedShapeDeriver` — single entry point per genre

Replaces per-mode `IRhythmDeriver` / `IMelodyDeriver` / `IHarmonyDeriver` dispatch at the **draft layer**. The three sub-derivers survive as internals — the unified deriver calls them in sequence and applies the bonding.

```
UnifiedShapeDeriver.Derive(profile3D, sound, key, fabric, role, genre):

  1. bondStrength from profile3D:
       rhythmWeight  ∝ angularity + thicknessVariance + (1 − planarity)
       melodyWeight  ∝ elongation3D + verticalSpan + pathLength
       harmonyWeight ∝ circularity + closedness + depthSpan + planarity
     normalize → Vector3 bondStrength (always sums to 1)

  2. Harmony first (anchor):
       - fabric.ReserveSlot() or fabric.ChordAtBar(role.index) → target bar
       - root from centroidHeight snapped to fabric scale degree
       - flavor (sus2 / sus4 / drone5 / min7 / maj7) from tilt + depthSpan
       - voicing spread from horizontalSpan + depthSpan
       - role 0 full voicing ; role 1 root+5 ; role 2+ bass pedal
       - WRITE chord back to fabric

  3. Melody constrained by the harmony just chosen:
       - resample stroke to N slices (N from elongation3D + pathLength)
       - snap strong-beat pitches to chord tones, weak to pentatonic
       - role-offset step, RegisterPolicy.Clamp() per genre/mode
       - counter role (1): +12 semitones, chord-tones only
       - pedal role (2+): one sustained root per bar

  4. Rhythm aligned to melody onsets:
       - seed onsets at melody note starts on strong beats
       - fill with role-appropriate lanes:
           role 0 → bowl/kick + shaker
           role 1 → shaker only
           role 2+ → ghost perc only
         modulated by rhythmWeight and thicknessVariance
       - swing from helicity × temporalEvenness

  5. Apply bondStrength as facet-level velocity scaling:
       rhythm velocities  ×= (0.5 + bondStrength.x)
       melody velocities  ×= (0.5 + bondStrength.y)
       harmony velocities ×= (0.5 + bondStrength.z)
     so a harmony-heavy shape's rhythm is audibly softer, not silent.
```

All three facets share the **same 16-step grid** and the **same derived `totalSteps`**, so rhythm hits, melody onsets, and harmony changes lock to the same musical moment inside a single shape.

Files:

- `Assets/RhythmForge/Core/Sequencing/IUnifiedShapeDeriver.cs` (NEW — interface)
- `Assets/RhythmForge/Core/Sequencing/NewAge/NewAgeUnifiedShapeDeriver.cs` (NEW — composes existing `NewAgeRhythmDeriver`, `NewAgeMelodyDeriver`, `NewAgeHarmonyDeriver` + bond logic)
- `Assets/RhythmForge/Core/Sequencing/Electronic/ElectronicUnifiedShapeDeriver.cs` (NEW)
- `Assets/RhythmForge/Core/Sequencing/Jazz/JazzUnifiedShapeDeriver.cs` (NEW)

### 5. `HarmonicFabric` — scene-wide chord progression

Generalizes `Assets/RhythmForge/Core/Sequencing/HarmonicContextProvider.cs:11-29` from "current chord" to "chord per bar":

```csharp
public class HarmonicFabric
{
    public string key;
    public List<ChordPlacement> progression;  // chord per bar (or per N bars)
    public int nextFreeBar;

    public int  ReserveSlot();                                       // returns a bar index for a new shape
    public ChordPlacement ChordAtBar(int bar);
    public void Write(int bar, int rootMidi, List<int> chord, string flavor);
    public void Wrap();                                              // modulo when shapes overflow
}

public struct ChordPlacement
{
    public int rootMidi;
    public List<int> tones;
    public string flavor;
    public int sourceShapeRole;   // which shape authored this slot
}
```

Workflow:

- Shape 0 drawn → harmony deriver calls `fabric.ReserveSlot()` → bar 0; writes bar 0's chord.
- Shape 1 drawn → bar 1; its melody/rhythm already read shape 0's chord when playing over bar 0.
- Scheduler reads `fabric.ChordAtBar(currentBar)` each bar and re-evaluates melody quantization when a chord change is active (already supported by `HarmonicContext.NearestChordTone`).

This is the key mechanism that guarantees *any combination of random human shapes sounds composed*. The shapes are harmonically **choreographed**, not isolated.

File: `Assets/RhythmForge/Core/Sequencing/HarmonicFabric.cs` (NEW). Wraps and eventually replaces `HarmonicContextProvider` — during migration both coexist; `HarmonicContextProvider` becomes a thin view onto `fabric.ChordAtBar(currentBar)`.

### 6. Role widened from per-mode to per-shape

`Assets/RhythmForge/Core/Sequencing/PatternContextScope.cs:36-67` currently counts patterns of the **same `type`**. Change `ResolveRole` to count all shapes in the scene. Every facet of shape `k` then receives the same `roleIndex = k`, which is exactly what the musical-coherence design needs to stack lead / counter / pedal with zero re-work.

Back-compat: single-facet legacy patterns get a role computed across all patterns regardless of type — their audible separation actually improves, since a legacy rhythm at index 0 and a legacy melody at index 0 no longer both claim the "primary" slot.

### 7. New `MusicalShapeBehavior` — one behavior, three schedule calls

Replaces the three per-mode behaviors in `Assets/RhythmForge/Core/PatternBehavior/Behaviors/`. Its `Schedule(ctx)` fires all three facets at `ctx.localStep`:

```csharp
public void Schedule(PatternSchedulingContext ctx)
{
    using (PatternContextScope.ForShape(ctx.appState, ctx.shape))
    {
        ScheduleRhythm(ctx);   // reuses existing drum dispatch
        ScheduleMelody(ctx);   // reuses existing melody dispatch
        ScheduleHarmony(ctx);  // only at step 0 or chord-change step
    }
}
```

`Sequencer.ScheduleCurrentStep` (`Assets/RhythmForge/Sequencer/Sequencer.cs:176-209`) doesn't iterate facets itself — the behavior does. **Zero changes to `AudioEngine` / `SamplePlayer`.**

Legacy `RhythmLoopBehavior`, `MelodyLineBehavior`, `HarmonyPadBehavior` (`Assets/RhythmForge/Core/PatternBehavior/Behaviors/`) are retained so legacy single-facet patterns continue to work after migration. `PatternBehaviorRegistry` gains the new `MusicalShape` behavior as a fourth registered type.

### 8. Draw UX — free vs. solo-facet

`Assets/RhythmForge/Interaction/DrawModeController.cs:23-50` becomes:

- **Free (default):** stroke → `MusicalShape` with all three facets weighted by `bondStrength`.
- **Solo-Rhythm / Solo-Melody / Solo-Harmony (advanced):** zeros two of the three `bondStrength` components; still a `MusicalShape`, still lands on the shared fabric, but only one facet is audible. Lets the user deliberately "paint a drum groove" or "paint a bassline".

Color coding in `Assets/RhythmForge/Core/Data/TypeColors.cs` is preserved; in Free mode the shape's stroke color blends the three based on `bondStrength` (a harmony-dominant shape looks purple-ish, rhythm-dominant warm, melody-dominant cyan) so the user can **see** at a glance what their 3D gesture leans into before even hearing it.

### 9. Visualizer — one curve, three overlapping energies

`Assets/RhythmForge/VisualizerManager.cs:42-71` and `PatternVisualizer` already draw per-instance visuals. Replace `PatternVisualizer` with a `ShapeVisualizer` that owns the 3D curve (from raw world points, **no reprojection**) and three concurrent animation layers using the existing `VisualGrammarProfiles`:

- **Rhythm halo** pulses on each drum onset.
- **Melody arc** fills along the curve in step with notes.
- **Harmony bloom** inflates the curve's bounding aura during chord sustain.

Dominance comes from `bondStrength` — a rhythm-heavy shape's halo is most prominent; visual read matches audible read. One GameObject per shape, three concurrent animated layers — no perf regression vs. three separate pattern visualizers today.

### 10. Migration

`Assets/RhythmForge/Core/Session/StateMigrator.cs` translates old saves:

- Each legacy `PatternDefinition` with a single `type` becomes a `MusicalShape` whose `bondStrength` is `(1,0,0)`, `(0,1,0)`, or `(0,0,1)` — a degenerate single-facet shape.
- Audio output is bit-identical (only the dominant facet plays; the other two have zero velocity).
- The user can "upgrade" a single-facet shape by re-deriving; the other two facets populate from its profile.

Two-way compatibility during the transition: the new `MusicalShapeBehavior` is chosen when the `PatternDefinition` has a non-null `unifiedFacets` field; otherwise the legacy per-mode behaviors continue to handle it. No forced re-save.

---

## Critical Files

| # | File | Change |
|---|---|---|
| 1 | `Core/Data/ShapeProfile3D.cs` | **NEW** — extended 3D feature vector |
| 2 | `Core/Data/MusicalShape.cs` | **NEW** — unified shape entity |
| 3 | `Core/Data/DerivedShapeSequence.cs` | **NEW** — holds 3 facet sequences together |
| 4 | `Core/Analysis/ShapeProfile3DCalculator.cs` | **NEW** — 3D stream analyzer (pressure + tilt + time) |
| 5 | `Core/Sequencing/HarmonicFabric.cs` | **NEW** — scene-wide chord-per-bar scaffold |
| 6 | `Core/Sequencing/IUnifiedShapeDeriver.cs` | **NEW** — single deriver interface |
| 7 | `Core/Sequencing/NewAge/NewAgeUnifiedShapeDeriver.cs` | **NEW** — compose existing NewAge derivers + bond logic |
| 8 | `Core/Sequencing/Electronic/ElectronicUnifiedShapeDeriver.cs` | **NEW** — same wiring for Electronic |
| 9 | `Core/Sequencing/Jazz/JazzUnifiedShapeDeriver.cs` | **NEW** — same wiring for Jazz |
| 10 | `Core/Sequencing/ShapeRoleProvider.cs` | Role widens from per-mode to per-shape |
| 11 | `Core/Sequencing/PatternContextScope.cs` | `ResolveRole` counts all shapes, not just same-type |
| 12 | `Core/Sequencing/HarmonicContextProvider.cs` | Becomes a thin read-only view onto `HarmonicFabric` |
| 13 | `Core/PatternBehavior/Behaviors/MusicalShapeBehavior.cs` | **NEW** — one behavior, fans out to 3 facet schedules |
| 14 | `Core/PatternBehavior/PatternBehaviorRegistry.cs` | Register `MusicalShape` behavior |
| 15 | `Interaction/StrokeCapture.cs` | Capture 3D + pressure + stylus rotation + timestamp; remove `ProjectTo2D` before analysis |
| 16 | `Interaction/DrawModeController.cs` | Free mode (default) + Solo-Rhythm/Melody/Harmony |
| 17 | `Core/Session/DraftBuilder.cs` | Always calls `UnifiedShapeDeriver`; builds all three facets at once |
| 18 | `Core/Session/SessionStore.cs` | Re-derivation path rebuilds unified shapes against fabric |
| 19 | `Core/Session/StateMigrator.cs` | Legacy single-type → single-facet MusicalShape |
| 20 | `Sequencer/Sequencer.cs` | No logic change; behavior fan-out happens inside `MusicalShapeBehavior` |
| 21 | `VisualizerManager.cs` / `UI/ShapeVisualizer.cs` | Rename + three overlapping energy animations |
| 22 | `Core/Data/GenreRegistry.cs` | Register unified derivers alongside existing per-mode ones |

All files from the previous plan (`ShapeRoleProvider`, `RegisterPolicy`, bowl/kalimba/drone fixes, per-mode gain table, shared reverb bus) remain **as-is** and continue to apply per facet. No re-work.

---

## Reused Existing Utilities

- `ShapeRoleProvider`, `RegisterPolicy`, `MusicalKeys.QuantizeToKey`, `ShapeProfileSizing`, `PatternContextScope` — unchanged.
- `VoiceSpecResolver` per-mode gain-staging (from the previous plan) — already applies per facet since facets dispatch through the existing `Resolve{Drum,Melody,Harmony}` paths.
- `HarmonicContextProvider` → generalized through `HarmonicFabric`, kept as a read-only view for back-compat.
- `SoundMappingProfileAsset` weights — unchanged; consume the 2D fields on `ShapeProfile3D` which are still populated.
- `VisualGrammarProfiles` — unchanged; reused by the three animation layers on `ShapeVisualizer`.

---

## Implementation Order

1. **Phase A — foundation, no audible change.**
   Add `ShapeProfile3D`, `HarmonicFabric`, `MusicalShape`, `DerivedShapeSequence`. Populate `ShapeProfile3D` from existing 2D data + pressure stream (tilt = neutral until Phase D). No UX change; no new deriver yet. Existing runtime is untouched.

2. **Phase B — unified deriver (bonded patterns).**
   Add `UnifiedShapeDeriver` per genre. Each stroke still produces 1 visible pattern but internally 3 facets are derived. Sounds identical to today when `bondStrength = (1,0,0) | (0,1,0) | (0,0,1)` (i.e. driven by the current `DrawModeController` mode). De-risks the derivation merge without touching UX.

3. **Phase C — UX unification.**
   Collapse the three bonded facets into one visible `MusicalShape`. `DrawModeController` switches to Free + Solo. New `ShapeVisualizer`. **Now a single stroke audibly plays all three at once.** This is the first user-facing milestone.

4. **Phase D — 3D-native capture.**
   Drop `ProjectTo2D`. `ShapeProfile3DCalculator` computes from the real 3D stream (including stylus rotation and temporal samples). Tilt / thickness / helicity now drive audible variation for the first time.

5. **Phase E — bond-strength tuning.**
   Apply facet-level velocity scaling from `bondStrength`. Calibrate thresholds so a "rhythm-heavy" shape is obviously different from a "harmony-heavy" one (both visually and audibly). This is the "wow" moment.

6. **Phase F — progression fabric.**
   Expand `HarmonicFabric` from single-chord to per-bar progression. Shapes slot into successive chord positions; a 4-shape scene becomes a 4-chord progression automatically. An 8-shape scene feels like a composed piece rather than a loop.

Phases A–C are back-compat. Phase D begins to noticeably change the sound. Phase E is the "wow" moment. Phase F is what makes dense multi-shape scenes feel like composed music rather than loops.

---

## Verification

### Automated / deterministic

- **Single-shape test.** Draw one 3D shape programmatically; assert `derivedSequence.rhythm`, `.melody`, `.harmony` all non-empty, all share the same `totalSteps`, and harmony's `rootMidi` is present in the melody's chord-tone snap set for every strong-beat note.
- **Ensemble test.** Draw 4 shapes at 68 BPM (New Age) with increasing size / thickness; inspect: role 0 has full voicing, role 1 is offset `barSteps/3` with root+5 harmony, role 2 is bass pedal, role 3 is ghost-perc-only rhythm.
- **Character contrast test.** Draw (a) a flat thin circle, (b) a tall thick helix, (c) a wide zigzag. Assert `bondStrength` differs by at least 0.25 between any two of them across all three components — audibly confirms visible shape → audible identity.
- **Random-stroke regression.** Script 200 procedurally-varied strokes; assert 100% stay in key (`MusicalKeys.QuantizeToKey` coverage on every melody note), 100% land on the 16th grid, 100% of harmony roots belong to the fabric's current chord for the bar they sound in.
- **Migration test.** Load a save from before this refactor; confirm bit-identical audio playback (single-facet shapes = legacy patterns). Compare rendered WAV buffers sample-by-sample.

### Audible (in Unity)

- **Genre regression.** New Age 68 BPM, Electronic 110 BPM, Jazz 120 BPM; draw 1, 3, 6 shapes in each. Every combination must sound: in key, on grid, with no clashing chords, with clearly separated voices.
- **Shape-character A/B.** Same scene, swap one shape for a radically different 3D form; confirm the audible change is "obviously different" (not "subtly different") across all three facets.
- **Long session.** 12+ shapes at 90 BPM; confirm the progression fabric wraps gracefully and the mix does not turn to mud.

### Performance

- Per-stroke derivation budget: unified deriver ≤ 1.2× cost of the current single-mode path (it runs three derivers but skips three separate stroke analyses and three draft-builder pipelines).
- Scheduler budget: fan-out is one extra loop per instance, trivially below the 2 ms/frame budget.
- Render budget: `ShapeVisualizer` with three animation layers stays under the existing per-visualizer allocation (current code already runs three separate `PatternVisualizer` instances in many scenes).

---

## Open Design Questions

1. **Progression length.** Fixed 4-bar loop, or grows to match shape count (1 shape = 1 bar, 4 shapes = 4 bars, 8 shapes = 8 bars wrapping at 4)? *Recommendation: grow-then-wrap, with scene-configurable max.*
2. **Solo-facet persistence.** Does Solo-Rhythm/Melody/Harmony on a shape survive genre switches, or should changing genre reset every shape to Free? *Recommendation: persist; genre switch re-derives all three facets but keeps the user's bond weighting intent.*
3. **Shape edits after creation.** Dragging an instance in the scene currently re-mixes via `PatternInstance.RecalculateMixFromPosition`. Should dragging a shape across a bar boundary re-derive it against the new chord slot, or stay fixed at authoring bar? *Recommendation: stay fixed for now; revisit if/when a timeline mode is added.*
4. **Cross-shape bond rebalancing.** Should the ensemble auto-balance `bondStrength` (e.g. if three harmony-heavy shapes exist, demote one so the band isn't all pads)? *Recommendation: defer — first get per-shape bonding right, then consider ensemble post-processing.*

---

## Relation to the Previous Plan

This plan is **purely additive** relative to `musical-coherence-plan.md`:

- Every timbre fix (bowl fundamental + inharmonic partials, kalimba body thump + envelope coherence, drone pad attack/release, shaker per-trigger pan) applies **per facet** without modification.
- The per-mode gain table, register policy, and role-offset strategy all generalize automatically: per-mode becomes per-facet, per-shape-same-mode becomes per-shape, and the shared reverb bus handles all facets exactly as it handles today's three separate pattern types.
- Role index 0/1/2 semantics carry over verbatim — they now apply across shapes rather than within a single mode.

The minimum-viable unified runtime is effectively **Phase B** of this plan combined with **Phases A/B** of the previous plan. Everything else is progressive enhancement.
