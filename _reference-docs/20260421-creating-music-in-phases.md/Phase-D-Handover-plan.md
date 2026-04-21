# Phase D Handover Plan

## Purpose

This document is the single source of truth for what was implemented for Phase D of the phased music creation rollout in RhythmForgeVR.

Phase D is the first phase that changes the musical behavior itself. It rewrites the Harmony phase so one committed Harmony stroke now produces and schedules an 8-bar guided chord progression instead of the legacy single static pad chord with role-based fake bass branches.

This document describes the implementation as it actually landed in the current codebase, not just the original plan.

## Locked Scope

Implemented in Phase D:

- progression-based Harmony derivation for guided mode
- new `HarmonyShapeModulator`
- `DerivedSequence.chordEvents` transition field
- Harmony scheduling changed from one long static chord to one bar-long chord per bar
- Harmony commit now updates `Composition.progression` only on commit, not on draft creation
- Harmony redraw now replaces the previous Harmony pattern in guided mode
- `ChordProgressionChangedEvent` now triggers background Melody/Bass re-derivation
- focused Phase D edit-mode tests
- Phase D documentation

Not implemented in Phase D:

- Melody phase rewrite is still not done
- Groove profile scheduling is still not done
- Bass phase rewrite is still not done
- Percussion phase rewrite is still not done
- one-pattern-per-phase replacement is not generalized to all phases yet
- auto-advance after commit is still not done
- pending downstream badges are still not done
- guided demo replacement is still not done

## What Was Implemented

### 1. New progression modulation layer

Added:

- `Assets/RhythmForge/Core/Sequencing/HarmonyShapeModulator.cs`

This is the new Phase D core.

Input:

- the Harmony stroke metrics
- the Harmony `ShapeProfile`
- the guided default 8-bar progression
- key name
- genre/register target

Output:

- a new `ChordProgression` with the same root loop:
  - `G`
  - `Em`
  - `C`
  - `D`
  - `G`
  - `Em`
  - `C`
  - `D`

but with shape-driven voicing changes per bar.

Implemented modulation rules:

- `tiltSigned > 0.28` promotes the progression to `maj7`-style voicings
- `tiltSigned < -0.22` promotes the progression to `sus2`-style voicings
- `horizontalSpan` picks one shared inversion across the whole loop:
  - low span = root position
  - medium span = 1st inversion
  - high span = 2nd inversion
- `verticalSpan > 0.5` adds an upper-octave copy layer for a wider pad
- `centroidHeight` shifts the whole voicing into a higher or lower harmony register
- `symmetry < 0.45` strengthens bars 4 and 8 with cadence-lift voicings that add the 7th

Hard invariants:

- roots never leave the `I-vi-IV-V-I-vi-IV-V` loop
- every voiced pitch is quantized back into `G major`
- every voiced pitch is clamped into the electronic Harmony register

Important implementation note:

- the master plan described the negative-tilt flavor as `sus2` but listed degree steps that do not actually form a sus2
- the landed implementation uses a true in-key sus2 shape:
  - scale-degree steps `{0, 1, 4}` = root, 2nd, 5th
- this was chosen because it matches the plan’s label and beginner-safe intent better than the tuple printed in the plan

### 2. Harmony derivation now outputs an 8-bar sequence

Updated:

- `Assets/RhythmForge/Core/Sequencing/HarmonyDeriver.cs`

Behavior changes:

- Harmony derivation now always produces `8` bars for the guided Harmony phase
- the old role-based branches were removed from the electronic Harmony path
- the old single static chord output is still populated for backward compatibility
- the new source of truth is `derivedSequence.chordEvents`

New `DerivedSequence` contract:

- `kind = "harmony"`
- `totalSteps = 128`
- `chordEvents = List<ChordSlot>` with one entry per bar

Transition compatibility kept:

- `rootMidi`
- `flavor`
- `chord`

These are still filled from bar 1 so old code paths and tests do not immediately break. They should remain transitional until later cleanup.

### 3. Harmony behavior class now schedules one chord per bar

Updated:

- `Assets/RhythmForge/Core/PatternBehavior/Behaviors/HarmonyPadBehavior.cs`
- `Assets/RhythmForge/Core/PatternBehavior/PatternBehaviorRegistry.cs`

Important detail:

- the file path is still `HarmonyPadBehavior.cs`
- the class inside is now `HarmonyBehavior`
- this was done to avoid unnecessary Unity asset churn while still moving the runtime behavior to the new name

Scheduling changes:

- if `derivedSequence.chordEvents` exists, Harmony playback now schedules one chord every 16 steps
- each bar plays only the chord for that bar
- warm-up/precache voice collection also walks bar-by-bar
- if `chordEvents` is absent, the old single-chord fallback path still works for older patterns

Practical result:

- playback now sounds like an 8-bar bed
- redraw changes the harmonic contour of the whole loop, not just a single held chord

### 4. Harmony no longer mutates shared harmony state at draft-creation time

Updated:

- `Assets/RhythmForge/Core/Session/DraftBuilder.cs`

Previous behavior:

- simply drawing a Harmony stroke updated shared harmonic context before commit

New behavior:

- drawing creates a draft only
- `Composition.progression` and `AppState.harmonicContext` are not updated until the draft is committed

Why this matters:

- discarding a Harmony draft no longer mutates the guided composition
- downstream systems now react to committed Harmony only

### 5. Harmony commit now updates the composition progression

Updated:

- `Assets/RhythmForge/Core/Session/SessionStore.cs`

New commit behavior:

- after a successful Harmony commit, `SessionStore.CommitDraft()` extracts `draft.derivedSequence.chordEvents`
- it builds a new `ChordProgression`
- it calls `UpdateProgression()`

That means:

- `composition.progression` becomes the newly committed progression
- `harmonicContext` is refreshed from bar 0
- `ChordProgressionChangedEvent` is published

This is the commit-time equivalent of what the master plan wanted, but moved to the safer lifecycle point.

### 6. Guided Harmony redraw now replaces the previous Harmony pattern

Updated:

- `Assets/RhythmForge/Core/Session/PatternRepository.cs`

New guided replacement behavior:

- before committing a new Harmony pattern in guided mode, the repository removes the previously committed Harmony pattern for that phase
- all scene instances tied to that old Harmony pattern are also removed
- then the new Harmony pattern is inserted and tracked

What this guarantees:

- only one committed Harmony pattern exists per composition after a redraw

What it does not yet guarantee:

- this replacement rule is not yet generalized to Melody, Groove, Bass, or Percussion
- `Save+Dup` can still create two scene instances of the same newly committed Harmony pattern, because the duplicate button behavior is unchanged

### 7. Progression changes now trigger downstream background re-derivation hooks

Updated:

- `Assets/RhythmForge/Core/Session/SessionStore.cs`

New event-bus wiring:

- `SessionStore` now subscribes to `ChordProgressionChangedEvent`
- when the progression changes, it builds a snapshot of committed Melody and Bass patterns
- those patterns are re-derived on a background task
- results are applied back on the main-thread queue

Important limitation for the next agent:

- this hook is intentionally only a scaffold for later phases
- today’s Melody and Bass paths still use the pre-Phase-E derivation logic
- because Melody has not yet been rewritten to read the progression bar-by-bar during derivation, this background re-derive only gives partial musical adaptation
- the wiring is ready; the real payoff comes when Phase E and Phase G land

## Files Changed

Core implementation:

- `Assets/RhythmForge/Core/Sequencing/HarmonyShapeModulator.cs`
- `Assets/RhythmForge/Core/Sequencing/HarmonyDeriver.cs`
- `Assets/RhythmForge/Core/Data/SequenceData.cs`
- `Assets/RhythmForge/Core/PatternBehavior/Behaviors/HarmonyPadBehavior.cs`
- `Assets/RhythmForge/Core/PatternBehavior/PatternBehaviorRegistry.cs`
- `Assets/RhythmForge/Core/Session/DraftBuilder.cs`
- `Assets/RhythmForge/Core/Session/PatternRepository.cs`
- `Assets/RhythmForge/Core/Session/SessionStore.cs`

Tests:

- `Assets/RhythmForge/Editor/HarmonyShapeModulatorTests.cs`
- `Assets/RhythmForge/Editor/HarmonyBehaviorTests.cs`
- `Assets/RhythmForge/Editor/PatternRepositoryTests.cs`
- `Assets/RhythmForge/Editor/MusicalCoherenceRegressionTests.cs`

Docs:

- `_reference-docs/20260421-creating-music-in-phases.md/Phase-D-Handover-plan.md`
- `_reference-docs/20260421-creating-music-in-phases.md/Phase-D-User-instructions.md`

## Validation

### Targeted Phase D coverage added

Passed in Unity batch mode on a temp project copy:

- `HarmonyShapeModulatorTests.AllOutputPitches_InGMajor`
- `HarmonyShapeModulatorTests.Roots_AlwaysMatchIviIVVLoop`
- `HarmonyBehaviorTests.CommitHarmony_StoresEightChordProgressionAcrossEightBars`
- `PatternRepositoryTests.CommitDraft_InGuidedMode_ReplacesPreviousHarmonyPattern`
- `MusicalCoherenceRegressionTests.GuidedElectronicHarmony_UsesProgressionRootsAndHarmonyRegister`

### Full EditMode run status

Full edit-mode suite run on a temp copy completed with:

- `73` total tests
- `58` passed
- `15` failed

The remaining failures are outside the new Phase D slice and were not addressed here:

- `ArrangementSequencerTests`:
  - `Arrangement_BarsValueControlsSlotDuration`
  - `Arrangement_LoopsAcrossPopulatedSlots`
  - `Arrangement_SkipsEmptySlots_WhenAdvancing`
  - `SceneMode_QueuedSceneSwitch_StillUpdatesPlaybackScene`
  - `TransportChanged_FiresOnPlay_AndDisplayedBarChanges`
- `HarmonicContextProviderTests`:
  - `FromProgression_BarIndex2_ReturnsCMajorChordTones`
  - `FromProgression_BarIndex3_ReturnsDMajorChordTones`
- `PlaybackAnimationTests`:
  - `HarmonyVisualState_RemainsActive_ForChordSustainWindow`
  - `PatternVisualizer_SetPlaybackState_ActivatesPlaybackChildren`
- `ProceduralAudioRendererTests`:
  - `BrighterMelodySpec_ProducesBrighterOutput`
  - `HigherBodyAndReleaseBias_IncreaseDrumWeightAndTail("kick")`
  - `HigherBodyAndReleaseBias_IncreaseDrumWeightAndTail("snare")`
- `ShapeSizeBehaviorTests`:
  - `LargerMelodyLine_ProducesWiderAndMoreSustainedSoundProfile`
  - `LargerRhythmLoop_ProducesFullerAndLooserSoundProfile`
  - `PatternVisualizer_UsesWorldSize_ForRenderColliderAndLabelOffset`

Important note:

- the temp-copy run was necessary because the main project was already open in another Unity instance
- `dotnet`/`msbuild` were not available in this workspace

## Known Gaps And Follow-Up Notes

### 1. Harmony replacement is phase-specific, not global yet

Only Harmony redraw enforces replacement right now. The next agent should not assume the same hygiene exists for Melody, Groove, Bass, or Percussion yet.

### 2. Guided mode still uses the old duplicate/save UX

Because `CommitCardPanel` and guided session hygiene are Phase I work, `Save+Dup` still exists. In guided Harmony, that can create multiple instances of the same Harmony pattern even though the pattern itself is singular.

### 3. Melody/Bass re-derive wiring is ahead of the actual musical rewrite

The `ChordProgressionChangedEvent` listener is now present, but the musical value of that listener is limited until:

- Phase E rewrites Melody against per-bar harmonic context
- Phase G rewrites Bass as its own first-class voice

### 4. Harmony remains effectively electronic-guided for the new path

The new progression-based Harmony rewrite landed in the electronic guided Harmony path. Legacy jazz/new age Harmony derivers still keep their old role behavior when explicitly invoked.

### 5. `chordEvents` is transitional data

The code now carries both:

- old Harmony fields: `rootMidi`, `flavor`, `chord`
- new Harmony field: `chordEvents`

Future cleanup should remove the old single-chord Harmony fields only after all consumers have moved.

## What The Next Agent Should Do In Phase E

Phase E should treat this Phase D handoff as its runtime baseline.

The next agent should:

1. Rewrite Melody derivation to read `composition.progression` bar-by-bar, not just the bar-0 harmonic context.
2. Keep `ChordProgressionChangedEvent` as the invalidation trigger for Harmony redraws.
3. Reuse `DerivedSequence.chordEvents` as the harmony source of truth instead of falling back to `chord`.
4. Decide whether Melody replacement should mirror the new Harmony replacement behavior immediately or wait for a broader per-phase hygiene pass.
5. Preserve the Phase D assumption that progression updates happen on commit, not on draft creation.

## Recommended Assumptions To Preserve

- Guided mode remains locked to:
  - `G major`
  - `100 bpm`
  - `8 bars`
  - electronic genre defaults
- Harmony roots remain fixed to the guided `I-vi-IV-V` loop in Phase D and Phase E
- the Harmony phase is the first real guided musical source of truth for downstream derivation

