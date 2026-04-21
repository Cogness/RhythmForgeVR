# Phased Music Creation — Implementation Plan

Single source of truth for agents implementing the guided, shape-driven, phase-based music creation flow in RhythmForge VR.

This document combines:
- the music-theory foundation distilled from `base-lnowledge-creating-in-phases.md` (only the parts the implementation needs),
- the current architecture context derived from `../20260414-refactoring/Refactoring-Architecture-Evolution.md` and the live codebase,
- the target phased flow (6 user-facing steps → 10 implementation phases A–J).

All file paths below are relative to the repo root. All MIDI numbers follow the convention used in the codebase: `C4 = 60`, `A4 = 69`, `G4 = 67`.

---

## 1. Purpose & Design Principles

The guided flow turns RhythmForge from a free-form pattern drawing tool into a **step-by-step composer for non-professional music creators**. At each step the user draws one shape, and the shape parameters modulate a musically-safe default so the result always sounds intentional.

Core principles:

- **One shape per phase per piece.** Each of the 5 drawing phases (Harmony, Melody, Groove, Bass, Percussion) holds exactly one committed stroke. Redrawing replaces it.
- **Always musical.** Every derivation is clamped to the active key and, where applicable, to chord tones. The shape never moves notes out of the scale.
- **Beginner lock by default.** Strong beats favor chord tones; hat keeps a steady backbeat; bass locks to the root on beat 1. Shape parameters enrich but never violate these floors.
- **Order is advisory, navigation is free.** User can jump to any phase at any time. Changing an upstream phase triggers automatic re-derivation of downstream phases (chord-tone locks still hold against the new progression).
- **Key + time are hardcoded** for the first release of this flow (see §2). Later releases can open them up.

---

## 2. Locked Musical Foundation (Step 1)

Derived from the complex 8-bar band example in the base-knowledge file, picked because it gives enough surface (8 bars, I–vi–IV–V loop) for the shape-driven variation to have meaningful range while staying beginner-friendly.

| Parameter | Value |
|---|---|
| Key | **G major** (notes G–A–B–C–D–E–F♯) |
| Time signature | **4/4** (4 beats per bar) |
| Bars | **8** |
| Tempo | **100 bpm** (mid-tempo, energetic enough for groove, slow enough to be forgiving) |
| Progression | **G (I) – Em (vi) – C (IV) – D (V) – G – Em – C – D** |
| Phrase structure | Bars 1–4 = "question", Bars 5–8 = "answer" with slight variation |

These values live in a new `GuidedDefaults` static class and are seeded into every new `Composition`. They remain the only valid foundation until guided mode is explicitly extended.

### 2.1 Chord-by-chord reference

Each row lists the triad's pitch classes, the root MIDI used as a voicing anchor (middle-register defaults), and the base voicing (diatonic triad in G major). Voicing is the starting point; the Harmony phase's shape modulator can swap triad ↔ maj7 ↔ sus and adjust register and inversion.

| Bar | Chord | Degree | Pitch classes | Root MIDI | Base voicing (MIDI) |
|---|---|---|---|---:|---|
| 1 | **G major** | I | G – B – D | 67 (G4) | 67, 71, 74 |
| 2 | **E minor** | vi | E – G – B | 64 (E4) | 64, 67, 71 |
| 3 | **C major** | IV | C – E – G | 60 (C4) | 60, 64, 67 |
| 4 | **D major** | V | D – F♯ – A | 62 (D4) | 62, 66, 69 |
| 5 | **G major** | I | G – B – D | 67 | 67, 71, 74 |
| 6 | **E minor** | vi | E – G – B | 64 | 64, 67, 71 |
| 7 | **C major** | IV | C – E – G | 60 | 60, 64, 67 |
| 8 | **D major** | V | D – F♯ – A | 62 | 62, 66, 69 |

Diatonic triads are built through `MusicalKeys.BuildScaleChord(rootMidi, "G major", new[]{0,2,4})` (existing utility in `Assets/RhythmForge/Core/Data/MusicalKey.cs`).

### 2.2 Bass register targets

Bass notes must be clamped via `RegisterPolicy.ClampBass(midi, "electronic")` (range E1–E3 = MIDI 28–52 for Electronic genre).

| Bar | Chord | Bass root MIDI | Bass fifth MIDI |
|---|---|---:|---:|
| 1, 5 | G | 43 (G2) | 50 (D3) |
| 2, 6 | Em | 40 (E2) | 47 (B2) |
| 3, 7 | C | 48 (C3) | 43 (G2, down) or 55 (G3, up) |
| 4, 8 | D | 50 (D3) | 45 (A2) |

### 2.3 Melody guidance (encoded in derivation rules)

From the base-knowledge file, the beginner melody rules the Melody phase must enforce:

1. All pitches belong to **G major** (`A, B, C, D, E, F♯, G`).
2. On **strong beats** (step % 4 == 0, i.e. beat 1 and 3 of each bar) the pitch must be a **chord tone of the current bar's chord**.
3. Passing notes (other beats) may be any in-key pitch — usually the nearest scale step, which produces natural "stepwise motion".
4. Bars 1–4 form a "question" phrase; bars 5–8 an "answer" — the shape modulator can optionally lift the melody an interval higher in bars 5–8 (detected from the shape's horizontal tilt).
5. Bar 8's final note should be **held longer** (2–4 beats) to signal a cadence/loop point.

### 2.4 Groove (melody rhythm) guidance

From base-knowledge Step 4 ("define the rhythm inside the melody"):

- Default quantization grid = 8th notes.
- Allowed values for note durations (in 16th-note steps): 2 (8th), 4 (quarter), 6 (dotted quarter), 8 (half), 16 (whole).
- Accent curve across 4 beats = `[1.0, 0.7, 0.85, 0.7]` (beat 1 strongest, beat 3 second-strongest — the backbeat is carried by percussion).
- Shape-driven density multiplier: 0.5× (sparse, half notes predominate) → 1.5× (busy, 16ths appear on strong shape curvature).
- Syncopation allowance: shifts off-beat notes by up to ±25% of a step, never enough to cross a strong beat.
- Hard rule: at least 1 note must land on bar-1 beat-1 and bar-5 beat-1 regardless of shape (phrase anchors).

### 2.5 Percussion pattern (step 6)

Base pattern every drum shape starts from, before shape-driven enrichment:

| Voice | Pattern (per 4/4 bar) |
|---|---|
| Kick | Beats 1 and 3 |
| Snare | Beats 2 and 4 (backbeat) |
| Closed hi-hat | 8th notes on `1 & 2 & 3 & 4 &` |

Shape-driven enrichments (must preserve the base pattern):

- `sp.aspectRatio < 0.52` → swap kick to `[0, 6, 10, 13]` (more pickup hits)
- `sp.circularity > 0.75` → kick `[0, 8, 12]` (rounded, fewer hits)
- `sp.symmetry < 0.6` → snare ghost hits on steps 5 and 13
- `sp.angularity > 0.68` → 16th hat stride (doubles the hat density)
- Bar 4 and bar 8 → automatic short snare/tom fill leading into bar 5 / loop point
- Beginner safety net: **bar 1 always has a kick on step 0 and a snare on step 8**, regardless of shape extremes.

### 2.6 Arrangement energy shape (base-knowledge step 5)

The 8-bar loop should have a shallow energy curve even with identical chords — a small difference that rewards careful drawing:

- Bars 1–2: melody + bass + chords + kick/snare/hat only.
- Bars 3–4: add small drum fill at end of bar 4.
- Bars 5–6: add shaker or tambourine (if future genre adds them); hat may open on "& of 4".
- Bars 7–8: tom/snare fill at bar 8 into bar 1 of the repeat.

For v1 of the phased flow the Percussion phase is the only place that implements bar 4/8 fills. Shaker/tambourine tracks are **not required** and are out of scope until a later genre pass.

---

## 3. Current Architecture Context

The refactored architecture (see `../20260414-refactoring/Refactoring-Architecture-Evolution.md`) already provides everything we need to extend without a rewrite:

- `PatternBehaviorRegistry` — pluggable per-type behaviors (add new types cheaply).
- `SessionStore` facade with `PatternRepository`, `SceneController`, `StateMigrator`, `SoundProfileResolver` — session mutations and persistence go through clear seams.
- `RhythmForgeEventBus` — typed events between subsystems.
- `GenreRegistry` — Electronic / Jazz / NewAge routing for derivers; guided mode defaults to Electronic.
- `HarmonicContextProvider` — a thread-static bridge the derivers already read for chord-tone snapping (melody).
- Asset-backed profiles: `InstrumentRegistryAsset`, `SoundMappingProfileAsset`, `VisualGrammarProfileAsset`.

### 3.1 What exists today vs. what the phased flow needs

| Area | Today | Needed for phased flow | Disposition |
|---|---|---|---|
| `PatternType` (`Assets/RhythmForge/Core/Data/PatternType.cs`) | 3 values: `RhythmLoop`, `MelodyLine`, `HarmonyPad` | 5 values: `Harmony`, `Melody`, `Groove`, `Bass`, `Percussion` | **Rename + extend** |
| `HarmonicContext` (`Core/Data/AppState.cs`) | Single chord snapshot | Full 8-bar progression | **Replace with `ChordProgression`**, keep `HarmonicContext` as per-bar view for existing deriver code |
| `ShapeRoleProvider` (`Core/Sequencing/ShapeRoleProvider.cs`) + role-index branches in derivers | Fakes bass/counter-melody by counting how many patterns of a type exist | Obsolete — bass and groove are first-class phases | **Remove after phases G + H** |
| Harmony pad role-based output (`HarmonyDeriver.cs:44-58`) producing root+5 or bass-pedal when a 2nd pad is drawn | Fake bass | Obsolete | **Remove in phase D** |
| Melody role-based output (`MelodyDeriver.cs:42-61`) producing counter / fill when additional melodies drawn | Fake counter-melody | Obsolete | **Remove in phase E** |
| Rhythm role-based output (`RhythmDeriver.cs:121-165`) for counter/ghost | Fake aux percussion | Obsolete | **Remove in phase H** |
| `SceneData` A/B/C/D + `SceneStripPanel` | 4 bins of pattern instances | Not required for guided mode (one piece = Scene A) | **Hide in guided mode**; keep data for future "variations" feature |
| `ArrangementSlot` × 8 + `ArrangementPanel` | Song-form sequencer of scenes | Not required for guided mode (piece is the 8-bar loop) | **Hide in guided mode**; keep for a later free mode |
| `DrawModeController` cycling through types | Free cycling between 3 types | Phase-driven selection | **Wrap** behind new `PhaseController` |
| `DemoSession.cs` | Procedurally seeds 3 demo patterns | A guided demo that seeds the foundation and leaves phases empty | **Replace** with `GuidedDemoComposition` |
| `PatternInstance` mix (pan/brightness/gain from position) | Applied to any pattern placed in the scene | Kept as a per-phase voice-placement control | **Keep unchanged** |
| Genre routing (Electronic / Jazz / NewAge) | Drives derivation tuning | Independent of phase flow | **Keep unchanged**; guided mode locks to Electronic for v1 |
| `AppState.tempo` / `AppState.key` | User-editable via TransportPanel | Read-only in guided mode | **Gate** behind guided-mode flag |

---

## 4. Target Flow — Mapping to Base-Knowledge Steps

| User step (base-knowledge) | Phase label | Artifact written | Upstream dependency |
|---|---|---|---|
| 1. Choose key + time | **Foundation** (hardcoded) | `Composition` seeded with G major / 4/4 / 100 bpm / progression | None |
| 2. Pick/adjust chord progression | **Harmony** phase | 1 shape → modulated `ChordProgression` with 8 `ChordSlot`s | Foundation |
| 3. Create melody over chords | **Melody** phase | 1 shape → melody `DerivedSequence` locked to progression | Harmony |
| 4. Define rhythm inside melody | **Groove** phase | 1 shape → `GrooveProfile` that mutates the melody at schedule time | Melody |
| 5. Add bass part | **Bass** phase | 1 shape → bass `DerivedSequence` locked to progression roots | Harmony |
| 6. Add percussion | **Percussion** phase | 1 shape → percussion `DerivedSequence` (kick/snare/hat + optional fills) | None (independent timing) |

Phase navigation is always available; editing any upstream phase invalidates caches for downstream phases and triggers a background re-derivation (Groove/Bass/Percussion re-derive cheaply; Melody re-derive is a full pass).

---

## 5. Domain Model Additions

New and modified types. All additive where possible; migrations handled in `StateMigrator`.

### 5.1 `CompositionPhase` enum
```csharp
namespace RhythmForge.Core.Data
{
    public enum CompositionPhase { Harmony, Melody, Groove, Bass, Percussion }
}
```

### 5.2 `PatternType` (expanded, renamed)
```csharp
public enum PatternType { Harmony, Melody, Groove, Bass, Percussion }
// Legacy mapping (used by StateMigrator only):
// "RhythmLoop"  -> Percussion
// "MelodyLine"  -> Melody
// "HarmonyPad"  -> Harmony
```

### 5.3 `ChordProgression`
```csharp
[Serializable]
public class ChordSlot
{
    public int barIndex;        // 0..7
    public int rootMidi;        // e.g. 67 for G4
    public string flavor;       // "major", "minor", "sus", "maj7"
    public List<int> voicing;   // resolved MIDI pitches (roots + chord tones)
}

[Serializable]
public class ChordProgression
{
    public int bars = 8;
    public List<ChordSlot> chords = new List<ChordSlot>();
}
```

### 5.4 `GrooveProfile`
```csharp
[Serializable]
public class GrooveProfile
{
    public float density;        // 0.5 .. 1.5 — multiplies melody note count
    public float syncopation;    // 0 .. 0.5 — fraction of a step off-beat shift
    public float swing;          // 0 .. 0.42 — shared with percussion
    public int   quantizeGrid;   // 8 or 16
    public float[] accentCurve;  // length == 4, per-beat velocity multiplier
}
```

### 5.5 `Composition`
```csharp
[Serializable]
public class Composition
{
    public string id;
    public float tempo = 100f;
    public string key = "G major";
    public int bars = 8;
    public ChordProgression progression;
    public GrooveProfile groove;   // optional; null until Groove phase committed
    public Dictionary<CompositionPhase, string> phasePatternIds
        = new Dictionary<CompositionPhase, string>();
    public CompositionPhase currentPhase = CompositionPhase.Harmony;
}
```

### 5.6 `AppState` additions
```csharp
public bool guidedMode = true;
public Composition composition;  // new; null for legacy saves, created by migrator
```
Version bump `AppState.version` 5 → 6. `StateMigrator` creates an empty `Composition` with `GuidedDefaults` if absent.

### 5.7 `IPatternBehavior` additions
```csharp
CompositionPhase Phase { get; }
bool AllowsRedraw { get; }  // always true in guided mode
```

### 5.8 `GuidedDefaults`
```csharp
public static class GuidedDefaults
{
    public const string Key = "G major";
    public const float Tempo = 100f;
    public const int Bars = 8;

    public static ChordProgression CreateDefaultProgression()
    {
        // G - Em - C - D - G - Em - C - D
        // Uses MusicalKeys.BuildScaleChord with scaleDegreeSteps {0,2,4}
    }
}
```

---

## 6. Implementation Phases (A → J)

Each phase **must end with a compilable, runnable build**. Edit-mode tests must pass. The `GuidedDemoComposition` must load without exceptions at every phase boundary.

### Phase A — Domain rename + expansion (non-breaking)

**Scope.** Make room for 5 phase types without changing runtime behavior.

Changes:
- `PatternType` enum values: rename `RhythmLoop → Percussion`, `HarmonyPad → Harmony`, `MelodyLine → Melody`. Add `Bass` and `Groove` values.
- For `Bass` and `Groove`, register temporary "stub" behaviors in `PatternBehaviorRegistry` that alias to `MelodyBehavior`'s derivation (so nothing breaks if the enum is encountered). Display names: `"Bass"`, `"Groove"`. Draft prefixes: `"Bass"`, `"Groove"`.
- `DraftCounters`: convert the fixed `rhythm/melody/harmony` fields to a `Dictionary<PatternType,int>` or add `bass`/`groove` counters explicitly.
- `TypeColors`: add colors for `Bass` (warm red) and `Groove` (amber/yellow).
- `StateMigrator`: when loading `AppState.version < 6`, rewrite any persisted enum strings (`"RhythmLoop" → "Percussion"` etc.) before deserialization. Bump to `version = 6`.

Acceptance:
- Full build passes.
- Legacy saves load, patterns display with renamed types, playback unchanged.
- `PatternBehaviorRegistry.GetRegisteredTypes()` returns all 5.

Tests:
- `StateMigratorTests.UpgradesV5ToV6_RewritesLegacyEnumStrings`
- `PatternBehaviorRegistryTests.AllFivePhaseTypesResolveToABehavior`

---

### Phase B — Composition model + guided defaults

**Scope.** Add the data structures; wire them through SessionStore; no UI yet.

Changes:
- Add `Composition.cs`, `ChordProgression.cs`, `ChordSlot.cs`, `GrooveProfile.cs`, `GuidedDefaults.cs` under `Assets/RhythmForge/Core/Data/`.
- `AppState` gains `Composition composition` and `bool guidedMode = true`.
- `StateMigrator.NormalizeState` seeds `composition = GuidedDefaults.Create()` when null.
- `SessionStore`:
  - `Composition GetComposition()`, `SetComposition(Composition)`, `UpdateProgression(ChordProgression)`, `UpdateGroove(GrooveProfile)`.
  - Replace the single-`HarmonicContext` field usage pattern. Keep `HarmonicContext` as a per-bar view built from `composition.progression`.
- New event: `ChordProgressionChangedEvent` on the event bus.
- `HarmonicContextProvider`: add `SetFromProgression(ChordProgression p, int barIndex)` that populates the thread-static context for the bar currently being derived.

Acceptance:
- New empty session has a Composition with G major, 100 bpm, 8 bars, and the I–vi–IV–V–I–vi–IV–V progression pre-populated (triads only, no modulation yet).
- Fetching `HarmonicContextProvider.Current` at bar 3 returns C major chord tones; at bar 4 returns D major chord tones.

Tests:
- `GuidedDefaultsTests.DefaultProgression_RootsFollowIviIVV()`
- `GuidedDefaultsTests.DefaultProgression_AllPitchesInGMajor()`
- `HarmonicContextProviderTests.SetFromProgression_ReturnsBarSpecificChordTones()`

---

### Phase C — Phase controller + guided-mode toggle

**Scope.** Introduce phase navigation as the primary UI mechanic when `guidedMode == true`.

Changes:
- New `PhaseController` under `Assets/RhythmForge/Interaction/`:
  - Exposes `CurrentPhase`, `GoToPhase(phase)`, `Next()`, `Prev()`.
  - Publishes `PhaseChangedEvent`.
  - Drives `DrawModeController.SetMode(phase.ToPatternType())`.
- New `PhasePanel` under `Assets/RhythmForge/UI/Panels/`: 5 phase buttons, a "Current phase" banner. Each button shows filled / current / empty state.
- `RhythmForgeManager` wires `PhaseController` + `PhasePanel` into the panel list.
- When `guidedMode == true`:
  - `SceneStripPanel` and `ArrangementPanel` `gameObject.SetActive(false)`.
  - `DockPanel.CycleMode` no-ops (or removes the cycle button from UI).
  - `TransportPanel` tempo/key controls become read-only labels.
- `CompositionPhase.ToPatternType()` extension method maps phase → type 1:1.

Acceptance:
- Launching in guided mode shows PhasePanel with Harmony selected.
- Clicking a phase button updates `DrawModeController.CurrentMode` and toggles the banner.
- Scene/Arrangement panels are hidden; tempo/key are read-only.
- Playback still works; drawing in any phase still produces a pattern (using the stub behaviors for Bass/Groove).

Tests:
- `PhaseControllerTests.GoToPhase_UpdatesDrawMode()`
- `PhaseControllerTests.NextPrev_Wrap()` (Harmony → Melody → Groove → Bass → Percussion → Harmony)

---

### Phase D — Harmony phase rewrite (step 2)

**Scope.** One drawn stroke modulates the predefined progression; outputs all 8 chords as a single `DerivedSequence`.

Changes:
- New `HarmonyShapeModulator` under `Assets/RhythmForge/Core/Sequencing/`:
  - Input: one stroke (points, `StrokeMetrics`, `ShapeProfile`) + `ChordProgression` defaults.
  - Output: modulated `ChordProgression` (still rooted on I–vi–IV–V but with adjusted flavor/voicing/register).
  - Rules:
    - `sp.tiltSigned > 0.28` → upgrade triads to **maj7** (`scaleDegreeSteps = {0,2,4,6}`).
    - `sp.tiltSigned < -0.22` → swap to **sus2** (`{0,3,4,6}`) — keeps I–V cadence intact.
    - `sp.verticalSpan` → voicing spread: add an upper octave copy when > 0.5.
    - `sp.horizontalSpan` → inversion choice applied uniformly across the progression (root / 1st / 2nd inversion).
    - `sp.centroidHeight` → overall register shift, clamped by `RegisterPolicy.GetRange(Harmony, "electronic")` (C3..A4 = 48..69).
    - `sp.symmetry < 0.45` → cadence lift: bar 4 and bar 8 get a stronger voicing (add the 7th even if triad mode).
  - Guaranteed invariant: **every returned pitch is in G major** (use `MusicalKeys.QuantizeToKey`). Roots never move off the I–vi–IV–V progression.
- Rewrite `HarmonyPadBehavior` → `HarmonyBehavior`:
  - Remove `ShapeRoleProvider.Current` reads.
  - Delete role-index branches in `HarmonyDeriver.cs:44-92`; keep only the "role 0" voicing path, now routed through `HarmonyShapeModulator`.
  - `Schedule()` iterates the progression bar-by-bar, scheduling one long chord voice per bar.
- Expand `DerivedSequence` (or add `ChordEvents` list) to carry per-bar chord events:
  ```
  DerivedSequence {
      kind = "harmony",
      totalSteps = 128,  // 8 bars * 16 steps
      chordEvents = List<ChordSlot>
  }
  ```
  Keep the old `rootMidi` / `chord` / `flavor` fields for the first (v1) chord to preserve back-compat with existing scheduling code during transition; remove after phase J.
- `DraftBuilder` for Harmony: after deriving, call `store.UpdateProgression(newProgression)` → publishes `ChordProgressionChangedEvent`.
- Event-bus listener in `SessionStore` triggers background re-derivation of Melody / Bass patterns when progression changes.

Acceptance:
- Drawing any stroke in Harmony phase replaces the default progression with a modulated one, still rooted on G–Em–C–D–G–Em–C–D.
- Every pitch in every chord is in G major.
- Playback plays the 8-bar chord bed.
- A second Harmony draw replaces the first; only one Harmony pattern exists per composition.

Tests:
- `HarmonyShapeModulatorTests.AllOutputPitches_InGMajor()`
- `HarmonyShapeModulatorTests.Roots_AlwaysMatchIviIVVLoop()`
- `HarmonyBehaviorTests.CommitsOneProgression_EightChords_EightBars()`
- Manual smoke: Play = hear chords; redraw → chords change.

---

### Phase E — Melody phase rewrite (step 3)

**Scope.** Melody derivation clamped per-bar to the progression. Remove the role-index counter/fill hack.

Changes:
- Rename `MelodyLineBehavior` → `MelodyBehavior`.
- `MelodyDeriver` rewrite:
  - Delete role-index branches (`MelodyDeriver.cs:42-61, 90-95`). No more primary / counter / fill.
  - For each derived step, compute the bar index = `step / 16`, call `HarmonicContextProvider.SetFromProgression(progression, bar)`, and on strong beats (`step % 4 == 0`) snap to `HarmonicContext.NearestChordTone`.
  - On passing beats, snap to `MusicalKeys.QuantizeToKey(midi, "G major")`.
  - Beginner-safety quantization: round note start times to 8th notes unless `sp.speedVariance > 0.65`, in which case 16ths are allowed.
  - Velocity floor 0.28; velocity ceiling 0.88.
  - Bar 8 final note duration = max 4 steps (half note) to mark cadence.
  - Bars 5–8 lift: if `sp.tiltSigned > 0` apply a +2 scale-step transposition to bars 5–6 (the "answer" variation from base-knowledge §5.1).
- Remove `ShapeRoleProvider.Current` reads from melody path.
- `MelodyBehavior.Schedule`: unchanged in structure — still walks the `DerivedSequence.notes`.
- On Melody commit, fire `MelodyCommittedEvent`; Groove / Bass behaviors subscribe so their cached audio invalidates.

Acceptance:
- Melody drawn over the G–Em–C–D progression: every beat-1/3 pitch is in the current bar's chord voicing (or one of its octaves).
- No pitch is outside G major.
- Drawing a second melody replaces the first.
- Redrawing Harmony recomputes melody pitches against the new progression (chord-tone locks still hold).

Tests:
- `MelodyDeriverTests.StrongBeats_AreChordTonesOfCurrentBar()`
- `MelodyDeriverTests.AllPitches_InGMajorScale()`
- `MelodyDeriverTests.Bar8FinalNote_LengthIsHalfNoteOrLonger()`
- Manual: draw wild up/down line → hear melody that follows the chord changes but never clashes.

---

### Phase F — Groove phase (step 4, melody rhythm modulator)

**Scope.** One stroke produces a `GrooveProfile` that mutates the melody's durations/timing/velocities at schedule time. No pitches change.

Changes:
- New `GrooveBehavior` (`PatternType.Groove`).
- New `GrooveShapeMapper` under `Assets/RhythmForge/Core/Sequencing/`:
  - `shapeProfile.pathLength` → `density` (0.5..1.5).
  - `shapeProfile.angularity` → `syncopation` (0..0.5).
  - `shapeProfile.curvatureVariance` → `swing` (0..0.42), shared with percussion.
  - `shapeProfile.verticalSpan` → accent curve amplitude.
  - `shapeProfile.speedVariance` → `quantizeGrid` (8 or 16).
- `GrooveBehavior.Derive`:
  - Does NOT produce MIDI events — produces a `GrooveProfile`, stored on `Composition.groove`.
  - Returns a `DerivedSequence` with `kind = "groove"` and `totalSteps = 0` (placeholder so existing pipeline doesn't break).
- `MelodyBehavior.Schedule` (modified):
  - When `composition.groove` is not null, apply `GrooveProfile` adjustments:
    - Skip notes where `noteIndex % roundedStride != 0` based on `density` (thins out).
    - Shift off-beat notes (`step % 4 != 0`) by `±syncopation * stepDuration`, never past a strong beat.
    - Scale velocities by `accentCurve[step % 4]`.
- `SamplePlayer.InvalidateAll()` is called after Groove changes (same mechanism the existing genre re-derivation uses).
- Beginner hard rule: melody notes on bar-1 beat-1 and bar-5 beat-1 are never thinned out by Groove (phrase anchors).

Acceptance:
- Drawing in the Groove phase without a melody committed: no audible change, but a `GrooveProfile` is stored.
- Drawing in the Groove phase with a melody committed: melody rhythm changes (density, syncopation), pitches identical.
- Redrawing Groove replaces the profile and re-renders.

Tests:
- `GrooveShapeMapperTests.MonotonicDensity_InCurvatureInput()`
- `GrooveBehaviorTests.MelodyPitches_UnchangedAfterGrooveCommit()`
- `MelodyScheduleTests.AnchorNotes_NeverThinned()`

---

### Phase G — Bass phase (step 5)

**Scope.** First-class bass voice with its own behavior and deriver. Locks to progression roots + passing tones.

Changes:
- New `BassBehavior` (`PatternType.Bass`) under `Assets/RhythmForge/Core/PatternBehavior/Behaviors/`.
- New `BassDeriver` under `Assets/RhythmForge/Core/Sequencing/`.
  - Always produces 1 bass event per bar minimum (root on beat 1), clamped via `RegisterPolicy.ClampBass(rootMidi, "electronic")`.
  - Shape-driven pattern:
    - `sp.directionBias > 0.3` → ascending walk into next bar (e.g. bar 4 D → bar 5 G: pass through E → F♯ → G in the last beat).
    - `sp.directionBias < -0.3` → descending walk.
    - `sp.verticalSpan > 0.5` → add the 5th on beat 3 (root + 5th pattern).
    - `sp.horizontalSpan` → note duration: short (staccato quarters) ↔ long (whole notes).
    - `sp.pathLength` → density (roots only vs. root+5 vs. walking eighths).
- `BassBehavior.Schedule`: same shape as melody scheduling; uses `trap-bass` preset by default (already exists in `InstrumentGroup.cs:117` and `GenreRegistry.cs:77`).
- Voice register: `RegisterPolicy.GetBassRange("electronic")` = E1..E3 (28..52).
- Remove the bass-pedal fallback path in the old `HarmonyDeriver` (already gone in phase D).
- Beginner hard rule: bar-1 and bar-5 always start with the root on beat 1.

Acceptance:
- Committing a Bass shape produces a sequence with ≥ 8 events (one per bar, minimum).
- Bar N beat 1 MIDI = `progression.chords[N].rootMidi` transposed into bass range.
- Redrawing Harmony updates bass roots accordingly.

Tests:
- `BassDeriverTests.Beat1OfEveryBar_EqualsProgressionRoot()`
- `BassDeriverTests.AllPitches_InGMajor_OrPassingChromaticAllowedOnBar4Beat4()`
- Manual: play = chords + melody + bass; hear root movement G→E→C→D.

---

### Phase H — Percussion phase (step 6)

**Scope.** Rename + simplify the existing rhythm deriver. Drop the primary/counter/ghost role trick. Keep the good shape-driven pattern logic.

Changes:
- Rename `RhythmLoopBehavior` → `PercussionBehavior`.
- Rename `RhythmDeriver` → `PercussionDeriver`.
- Delete role-index branches (`RhythmDeriver.cs:121-165`). Percussion is always "full pattern".
- Preserve the existing pattern logic (it matches §2.5 of this doc):
  - `kickPattern` selection on `aspectRatio` / `circularity`.
  - `snarePattern` selection on `symmetry`.
  - `hatStride` on `angularity` + `sizeFactor`.
- Add bar-4 and bar-8 fills (new):
  - Bar 4: extra snare on steps 14, 15.
  - Bar 8: extra snare on steps 13, 14, 15 + crash-like accent on bar 5 beat 1 (future genre-specific; for v1 emit as an extra snare hit).
- Consume `GrooveProfile.swing` if present (delayed by `swing * stepDuration` on off-beats).
- Beginner hard rule: step 0 always has a kick; step 8 always has a snare.

Acceptance:
- Every percussion draft has ≥ 1 kick on step 0 and ≥ 1 snare on step 8 regardless of shape extremes.
- Fill events appear on steps 13–15 of bars 4 and 8.
- Playback: full 6-step piece sounds coherent end-to-end.

Tests:
- `PercussionDeriverTests.KickOnStepZero_AlwaysPresent()`
- `PercussionDeriverTests.SnareOnStep8_AlwaysPresent()`
- `PercussionDeriverTests.Bar4AndBar8_ContainFillEvents()`
- Integration: `WalkthroughTests` (see §7).

---

### Phase I — Phase-aware UI + session hygiene

**Scope.** Turn the flow into a polished guided experience.

Changes:
- `PhasePanel` now shows:
  - Each phase button's state: green (filled), yellow (current), gray (empty).
  - A "Play Piece" button that plays the whole composition (regardless of current phase).
  - A "Clear Phase" action per phase.
- `InspectorPanel` when a phase-committed pattern is selected: buttons "Redraw" (enters that phase, clears its shape), "Adjust" (stays in selection, tweak pan/brightness/gain), "Clear" (unfills the phase).
- `CommitCardPanel` for guided mode: after a successful commit, auto-advance `PhaseController.Next()` (toggleable).
- Replace `DemoSession` with `GuidedDemoComposition`:
  - Seeds `GuidedDefaults`.
  - Leaves all 5 phases empty.
  - Sets `currentPhase = Harmony`.
  - No pre-drawn shapes (the user draws from scratch).
- Downstream phase invalidation: changing upstream phase marks downstream phase buttons with a "pending re-derive" badge until background re-render completes.

Acceptance:
- End-to-end walkthrough: fresh session → 5 shapes → play the piece.
- Clicking "Redraw" on the Harmony phase clears its shape and enters the phase; the melody button shows "pending" until re-derivation finishes.

Tests:
- `WalkthroughTests` (see §7) — scripted end-to-end flow.

---

### Phase J — Cleanup

**Scope.** Remove now-unused plumbing from the old free-form pattern model.

Changes:
- Delete `ShapeRoleProvider` and `ShapeRole` (confirm unreferenced).
- Delete legacy `HarmonicContext` single-chord field usage in `SessionStore` (replaced by `Composition.progression`).
- Remove the transitional `rootMidi` / `chord` / `flavor` fields from `DerivedSequence` (superseded by `chordEvents`).
- Delete `DemoSession`'s circle/wave/pad helper methods.
- If "free mode" is confirmed out of scope, delete `ArrangementPanel` and `SceneStripPanel`. If kept, gate cleanly with the `guidedMode` flag and leave them hidden by default.
- Final pass: `StateMigrator` drops the v5 → v6 enum rewrite branch after the release that required it has shipped (defer by one version).

Acceptance:
- No references to deleted classes.
- Build passes; walkthrough test passes.

---

## 7. Testability Contract

Every phase above ends in a state that must pass:

1. **Compile**: full Unity build compiles, no errors in edit-mode tests.
2. **Boot**: `RhythmForgeBootstrapper` brings the scene up; `GuidedDemoComposition` loads.
3. **Phase-level tests**: each phase adds a dedicated test file listed in the Tests block.
4. **Walkthrough test** (lives after phase C, grows through phase H):
   - `WalkthroughTests.Guided5StepFlow_ProducesFullComposition()`
   - Simulates 1 canonical stroke per phase (pre-defined input polylines).
   - Asserts after each phase:
     - Harmony → 8 `ChordSlot`s, all pitches in G major, roots = I-vi-IV-V loop.
     - Melody → ≥ 8 notes, strong beats are chord tones, all pitches in G major.
     - Groove → `Composition.groove` non-null, density in [0.5, 1.5].
     - Bass → ≥ 8 events, beat 1 of each bar matches progression root.
     - Percussion → step 0 has a kick, step 8 has a snare, fills on bars 4 and 8.

5. **Migration tests**: every phase that bumps `AppState.version` adds a test loading a known-good legacy save and asserting no regressions.

---

## 8. Glossary / Legend

From base-knowledge §7 (clip-style labels), adapted to this plan:

| Tag | Meaning |
|---|---|
| `CHORD_HOLD` | Block chord held for the bar (harmony default) |
| `CHORD_COMP_A` | Simple comping (not yet implemented; post-v1) |
| `BASS_ROOT_A` | Root on beat 1 only |
| `BASS_R5_A` | Root + fifth pattern |
| `BASS_WALK_1` | Passing motion into the next chord |
| `MELO_A` | Main phrase, bars 1–4 (question) |
| `MELO_B` | Answering phrase, bars 5–8 (answer) |
| `MELO_END` | Longer held note at bar 8 |
| `KICK_A` | Kick on beats 1 and 3 |
| `SNARE_A` | Snare on beats 2 and 4 |
| `HAT_8_A` | Closed hi-hat 8ths |
| `FILL_1` | Short snare/tom fill end of bar |
| `TURN` | Transition into next loop |

Post-v1 (out of scope for this plan): shaker, tambourine, open hat accents, arp synth, counter-melody, FX risers/impacts. The code must not assume they exist.

---

## 9. Key Tradeoffs the Implementer Should Know

1. **Scenes vs. one-piece model.** Guided mode treats Scene A as the entire piece. Scenes B/C/D exist in the data model but are hidden. A later "variations" feature can let the user duplicate the composition into B/C/D — the Composition model already supports it (the dictionary `phasePatternIds` lives per composition, so multiple compositions are feasible).

2. **Groove as a modulator, not a track.** The Groove phase does not produce MIDI events of its own. It produces a `GrooveProfile` that mutates Melody scheduling. This preserves the base-knowledge principle that "rhythm inside the melody" is distinct from drums. If a future release wants Groove to have its own track, promote the profile into a sequence.

3. **Beginner locks are non-negotiable.** Every derivation must enforce its "hard rules" (chord-tone on strong beats, kick on step 0, etc.). Shape-driven modulation enriches but never overrides these floors. When in doubt, lean safer.

4. **Genre stays Electronic in v1.** Jazz / NewAge derivers remain untouched. Opening guided mode to alternate genres requires verifying their derivers respect the per-bar progression lookup.

5. **Free mode remains addressable.** The `guidedMode` flag is the single switch. All hidden panels and controllers remain functional behind it — a future toggle can re-enable the original free-form experience without code surgery.

---

## 10. Execution Order (summary)

```
A → B → C → D → E → F → G → H → I → J
```

Each arrow is a green build, passing tests, and a playable (partial until phase H) composition. Phases D through H each add one audible musical layer; phases A–C prepare the ground; phases I–J polish and clean.
