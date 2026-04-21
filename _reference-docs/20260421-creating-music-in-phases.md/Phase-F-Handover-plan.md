# Phase F Handover Plan

## Purpose

This document is the single source of truth for what was implemented for Phase F of the phased music creation rollout in RhythmForgeVR.

Phase F rewrites the guided Groove phase so one committed Groove stroke now stores a first-class `Composition.groove` profile and applies that profile to Melody at schedule time. The Groove phase now changes Melody rhythm, accents, and timing without recalculating Melody pitches.

This handoff describes what actually landed in the codebase, which compatibility choices were made to keep the current architecture stable, and what the next agent should preserve.

## Locked Scope

Implemented in Phase F:

- guided Groove now derives a first-class `GrooveProfile`
- Groove data is stored on `Composition.groove` and committed through `SessionStore`
- guided Groove redraw now replaces the previously committed Groove pattern
- Melody scheduling now reads the committed Groove profile in guided mode
- Groove affects Melody timing, thinning, accents, and cadence-safe durations at schedule time
- Melody pitch content remains unchanged by Groove
- optional micro-delay support was added to the audio dispatcher so syncopation can be heard without rewriting Melody derivation
- dedicated `GrooveCommittedEvent` published on Groove commit
- sample cache invalidation now happens when Groove changes, and also when Melody changes while Groove is active
- focused Phase F edit-mode tests
- Phase F documentation

Not implemented in Phase F:

- Bass phase rewrite is still not done
- Percussion rewrite is still not done
- auto-advance after commit is still not done
- pending downstream badges are still not done
- guided demo replacement is still not done

## What Was Implemented

### 1. Groove is now a real guided phase, not a Melody alias

Updated:

- `Assets/RhythmForge/Core/PatternBehavior/Behaviors/GrooveBehavior.cs`
- `Assets/RhythmForge/Core/Session/SessionStore.cs`
- `Assets/RhythmForge/Core/Data/SequenceData.cs`

New runtime behavior:

- `GrooveBehavior` no longer delegates derivation or scheduling to `MelodyBehavior`
- Groove derivation now creates a `GrooveProfile`
- the committed Groove draft still stores a placeholder `DerivedSequence`
- that placeholder now carries `derivedSequence.grooveProfile`
- the actual source of truth is `Composition.groove`

Important implementation detail:

- the placeholder `DerivedSequence` is still used so the current draft/commit pipeline stays intact
- the placeholder uses:
  - `kind = "groove"`
  - `totalSteps = 0`
  - `grooveProfile = ...`

Compatibility decision:

- Groove keeps the same general visual grammar path as Melody for now
- Groove does not emit audio directly
- this avoids a deeper visual rewrite during the Phase F slice

### 2. New `GrooveShapeMapper` converts shape metrics into Groove data

Added:

- `Assets/RhythmForge/Core/Sequencing/GrooveShapeMapper.cs`

Mapping that landed:

- `shapeProfile.pathLength` -> `density` in the range `0.5 .. 1.5`
- `shapeProfile.angularity` -> `syncopation` in the range `0 .. 0.5`
- `shapeProfile.curvatureVariance` -> `swing` in the range `0 .. 0.42`
- `shapeProfile.speedVariance` -> `quantizeGrid`
  - `8` by default
  - `16` when `speedVariance > 0.65`
- `shapeProfile.verticalSpan` -> accent curve amplitude around the base curve:
  - `[1.0, 0.7, 0.85, 0.7]`

What this means in practice:

- longer shapes tend toward denser Groove profiles
- more angular shapes shift Melody timing more strongly
- more unstable curvature increases groove looseness
- taller shapes exaggerate beat-1 / beat-3 style accent contrast more

### 3. Groove commit now updates `Composition.groove`

Updated:

- `Assets/RhythmForge/Core/Session/SessionStore.cs`
- `Assets/RhythmForge/Core/Events/RhythmForgeEventBus.cs`

New runtime behavior:

- when a guided Groove draft is committed, `SessionStore.CommitDraft(...)` now:
  - reads `draft.derivedSequence.grooveProfile`
  - writes it into `Composition.groove`
  - publishes `GrooveCommittedEvent`

Current event behavior:

- `MelodyCommittedEvent` still publishes when Melody is committed
- `GrooveCommittedEvent` now publishes when Groove is committed

Why this matters:

- Melody schedule-time mutation now reads a stable Groove profile owned by the composition
- later phases do not need to scrape Groove state from pattern details or ad-hoc store scans

### 4. Guided Groove redraw now replaces the previous Groove pattern

Updated:

- `Assets/RhythmForge/Core/Session/PatternRepository.cs`
- `Assets/RhythmForge/Editor/PatternRepositoryTests.cs`

New guided replacement behavior:

- before committing a new guided Groove pattern, the repository removes the previously tracked Groove pattern for that phase
- all scene instances tied to that old Groove pattern are removed
- the new Groove pattern is then inserted and tracked in `composition.phasePatternIds`

Current replacement coverage:

- `Harmony`
- `Melody`
- `Groove`

Still not covered:

- `Bass`
- `Percussion`

### 5. Melody schedule-time playback now applies Groove

Updated:

- `Assets/RhythmForge/Core/PatternBehavior/Behaviors/MelodyLineBehavior.cs`
- `Assets/RhythmForge/Core/Sequencing/MelodyGrooveApplier.cs`
- `Assets/RhythmForge/Audio/IAudioDispatcher.cs`
- `Assets/RhythmForge/Audio/AudioEngine.cs`

Added:

- `Assets/RhythmForge/Core/Sequencing/MelodyGrooveApplier.cs`

New scheduling behavior:

- Melody only reads Groove when `appState.guidedMode == true`
- Groove is applied in both:
  - `MelodyBehavior.Schedule(...)`
  - `MelodyBehavior.CollectVoiceSpecs(...)`

Schedule-time Groove rules that landed:

- sparse Groove profiles thin Melody notes by stride
- dense Groove profiles can add safe retriggers of already-derived Melody notes
- notes are optionally re-quantized to the Groove grid:
  - `8th` grid -> 2-step spacing
  - `16th` grid -> 1-step spacing
- off-beat notes receive micro timing shifts from `syncopation`
- note velocity is multiplied by the Groove accent curve
- cadence handling is preserved so the end of bar 8 still holds properly

Important safety rules preserved:

- Melody MIDI pitches are not recomputed by Groove
- bar-1 beat-1 and bar-5 beat-1 anchors are never thinned out
- cadence hold in bar 8 is still protected

Important implementation interpretation:

- Groove still does not create new Melody pitches
- when density rises above `1.0`, the schedule-time path can now add extra retriggers using already-derived Melody pitches
- this makes busy Groove shapes audibly clearer without moving pitch derivation out of Phase E

### 6. Syncopation is audible through micro-delay, not note re-derivation

Updated:

- `Assets/RhythmForge/Audio/IAudioDispatcher.cs`
- `Assets/RhythmForge/Audio/AudioEngine.cs`

Compatibility decision:

- instead of rewriting Melody notes themselves, Phase F extends `PlayMelody(...)` with an optional `startDelay`
- this is used only by the guided Melody scheduling path after Groove is applied
- existing callers stay valid because the new parameter is optional

Why this matters:

- Groove can now audibly push or pull off-beat Melody notes
- Melody derivation remains the Phase E source of truth for pitch

### 7. Sample cache invalidation now respects Groove

Updated:

- `Assets/RhythmForge/RhythmForgeManager.cs`

New runtime behavior:

- on `GrooveCommittedEvent`:
  - `SamplePlayer.InvalidateAll()`
  - `Sequencer.ResetWarmBar()`
  - if no Melody is committed yet, the user now gets a toast explaining that Groove will become audible after Melody is added
- on `MelodyCommittedEvent`, if a Groove profile is currently active:
  - `SamplePlayer.InvalidateAll()`
  - `Sequencer.ResetWarmBar()`

Why this landed:

- Groove now changes the effective scheduled Melody playback
- cache warming should not keep stale assumptions after Groove or Melody changes

This is the Phase F use of `MelodyCommittedEvent` requested by the Phase E handoff.

### 8. Background re-derivation now understands Groove placeholders

Updated:

- `Assets/RhythmForge/Core/Session/SessionStore.cs`

New background behavior:

- the background re-derivation switch no longer routes Groove through the Melody deriver
- Groove now rebuilds its placeholder `DerivedSequence` through `GrooveShapeMapper`
- if the re-derived Groove pattern is the composition-owned Groove pattern, `Composition.groove` is refreshed from the result

Why this matters:

- genre re-derivation and other full-pattern refresh paths no longer silently turn Groove back into Melody-like data

## Files Changed

Core implementation:

- `Assets/RhythmForge/Audio/AudioEngine.cs`
- `Assets/RhythmForge/Audio/IAudioDispatcher.cs`
- `Assets/RhythmForge/Core/Data/SequenceData.cs`
- `Assets/RhythmForge/Core/Events/RhythmForgeEventBus.cs`
- `Assets/RhythmForge/Core/PatternBehavior/Behaviors/GrooveBehavior.cs`
- `Assets/RhythmForge/Core/PatternBehavior/Behaviors/MelodyLineBehavior.cs`
- `Assets/RhythmForge/Core/Sequencing/GrooveShapeMapper.cs`
- `Assets/RhythmForge/Core/Sequencing/MelodyGrooveApplier.cs`
- `Assets/RhythmForge/Core/Session/PatternRepository.cs`
- `Assets/RhythmForge/Core/Session/SessionStore.cs`
- `Assets/RhythmForge/RhythmForgeManager.cs`

Tests:

- `Assets/RhythmForge/Editor/GrooveShapeMapperTests.cs`
- `Assets/RhythmForge/Editor/MelodyGrooveApplierTests.cs`
- `Assets/RhythmForge/Editor/PatternRepositoryTests.cs`
- `Assets/RhythmForge/Editor/SessionStoreCompositionTests.cs`

Docs:

- `_reference-docs/20260421-creating-music-in-phases.md/Phase-F-Handover-plan.md`
- `_reference-docs/20260421-creating-music-in-phases.md/Phase-F-User-instructions.md`

## Validation

### Targeted Phase F coverage added

Added tests:

- `GrooveShapeMapperTests.MonotonicDensity_InPathLengthInput`
- `MelodyGrooveApplierTests.HighDensityGroove_KeepsMelodyPitchesIntact`
- `MelodyGrooveApplierTests.AnchorNotes_NeverThinned`
- `PatternRepositoryTests.CommitDraft_InGuidedMode_ReplacesPreviousGroovePattern`
- `SessionStoreCompositionTests.CommitDraft_ForGroove_StoresProfile_AndPublishesGrooveCommittedEvent`

### Run status

Could not run automated compilation or the Unity test suite in this workspace because:

- `dotnet` is not installed
- `msbuild` is not installed
- no local C# compiler (`csc` / `mcs`) is available either

Validation for this phase was therefore limited to:

- code-path inspection
- diff review
- targeted consistency checks against the existing architecture and Phase E handoff

## Known Gaps And Follow-Up Notes

### 1. Busy Groove now works through retriggers, but should still be monitored in VR

The current implementation now supports:

- thinning when density drops below `1.0`
- safe retriggers of existing Melody notes when density rises above `1.0`

If a later agent wants even stronger "busy groove" articulation, the clean extension point is still:

- `Assets/RhythmForge/Core/Sequencing/MelodyGrooveApplier.cs`

Do not move this back into Melody derivation unless free mode is being redesigned intentionally.

### 2. Groove is guided-mode only

`MelodyBehavior` now explicitly applies Groove only when `appState.guidedMode == true`.

That was intentional.

It preserves the older free-mode / legacy-fallback behavior path.

### 3. Groove owns timing, not pitch

Groove does not alter Melody MIDI values.

This is a key Phase F invariant and should remain true unless the product direction changes.

If a later phase wants more dramatic rhythmic articulation, prefer:

- note splitting
- retriggering
- duration reshaping
- off-beat rescheduling

before touching pitch derivation.

### 4. Groove visual grammar is still a temporary reuse

`GrooveBehavior` still reuses Melody-style visual adjustment and animation.

That kept the phase isolated and low-risk.

If Groove later needs a more distinctive visual identity, update:

- `GrooveBehavior.AdjustVisualSpec(...)`
- `GrooveBehavior.ComputeAnimation(...)`
- any matching visual grammar profile assets

### 5. Replacement hygiene is still partial

Guided redraw replacement now covers:

- `Harmony`
- `Melody`
- `Groove`

It still does not cover:

- `Bass`
- `Percussion`

If the later rollout wants strict one-pattern-per-phase behavior everywhere, continue this repository-owned pattern instead of introducing a second ownership system.

## What The Next Agent Should Do In Phase G

Phase G should treat this Phase F handoff as its runtime baseline.

The next agent should:

1. Add a first-class `BassBehavior` + `BassDeriver`.
2. Lock Bass beat-1 notes to the committed progression roots bar-by-bar.
3. Preserve the current Groove contract:
   - Groove mutates Melody timing only
   - Groove does not own pitch derivation
4. Keep using composition-owned phase state:
   - `Composition.progression`
   - `Composition.groove`
   - `composition.phasePatternIds`
5. Continue guided replacement hygiene through `PatternRepository` for Bass.

## Recommended Assumptions To Preserve

- guided mode remains locked to:
  - `G major`
  - `100 bpm`
  - `8 bars`
  - electronic genre defaults
- Harmony remains the upstream musical source of truth for guided Melody and Bass
- Groove remains a schedule-time Melody modulator, not a pitch deriver
- `MelodyCommittedEvent` remains the seam for Groove-sensitive cache invalidation
- groove commits still publish `GrooveCommittedEvent`
- guided progression updates still happen on commit, not on draft creation
