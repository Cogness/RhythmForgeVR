# Phase E Handover Plan

## Purpose

This document is the single source of truth for what was implemented for Phase E of the phased music creation rollout in RhythmForgeVR.

Phase E rewrites the guided Melody phase so one committed Melody stroke now derives an 8-bar lead line against the committed Harmony progression bar-by-bar, instead of using the older electronic role-index primary/counter/fill hack.

This handoff describes what actually landed in the codebase, the compatibility decisions that were made to keep the project runnable, and the constraints the next agent should preserve.

## Locked Scope

Implemented in Phase E:

- guided Melody derivation now reads the full committed `Composition.progression`
- strong-beat melody notes now lock to the current bar's chord tones
- passing-beat melody notes now quantize to `G major`
- guided Melody now always derives as an 8-bar sequence
- positive-tilt Melody shapes lift the answer phrase in bars 5 and 6
- Melody redraw in guided mode now replaces the previously committed Melody pattern
- new `MelodyCommittedEvent` published on Melody commit
- background re-derivation plumbing now carries full progression context instead of only bar-0 harmonic context
- focused Phase E edit-mode tests
- Phase E documentation

Not implemented in Phase E:

- Groove phase rewrite is still not done
- Bass phase rewrite is still not done
- Percussion rewrite is still not done
- one-pattern-per-phase replacement is still not generalized beyond Harmony and Melody
- auto-advance after commit is still not done
- pending downstream badges are still not done
- guided demo replacement is still not done

## What Was Implemented

### 1. Guided progression context now reaches Melody derivation

Updated:

- `Assets/RhythmForge/Core/Sequencing/HarmonicContextProvider.cs`
- `Assets/RhythmForge/Core/Sequencing/PatternContextScope.cs`
- `Assets/RhythmForge/Core/Session/DraftBuilder.cs`
- `Assets/RhythmForge/Core/Session/SessionStore.cs`

New runtime behavior:

- `HarmonicContextProvider` now carries two thread-static values:
  - the current per-bar `HarmonicContext`
  - the current full `ChordProgression`
- `PatternContextScope.Push(...)` and `PatternContextScope.ForPattern(...)` now optionally pass progression context
- `DraftBuilder` only injects the progression when `state.guidedMode == true`
- `SessionStore` background re-derivation now passes `ChordProgression`, not just bar-0 chord context

Why this matters:

- Phase D already stored Harmony as `DerivedSequence.chordEvents`
- before Phase E, Melody re-derivation only had bar-0 harmony available
- after this change, guided Melody can query the right chord for bar 1, 2, 3, and so on across the full 8-bar loop

Compatibility decision:

- progression context is only injected for guided mode
- this preserves the older free-mode / legacy-fallback behavior path when guided progression data is not present

### 2. `MelodyLineBehavior` runtime class is now `MelodyBehavior`

Updated:

- `Assets/RhythmForge/Core/PatternBehavior/Behaviors/MelodyLineBehavior.cs`
- `Assets/RhythmForge/Core/PatternBehavior/PatternBehaviorRegistry.cs`
- `Assets/RhythmForge/Core/PatternBehavior/Behaviors/BassBehavior.cs`
- `Assets/RhythmForge/Core/PatternBehavior/Behaviors/GrooveBehavior.cs`

Important detail:

- the file path is still `MelodyLineBehavior.cs`
- the class inside is now `MelodyBehavior`
- this mirrors the Phase D Harmony approach and avoids unnecessary Unity asset churn

Behavior changes:

- `Type` is now `PatternType.Melody`
- sound mapping now resolves through `PatternType.Melody`
- `PatternBehaviorRegistry` registers `MelodyBehavior`
- the temporary Bass/Groove stub delegates now point at `MelodyBehavior`

### 3. Guided electronic Melody derivation was rewritten

Updated:

- `Assets/RhythmForge/Core/Sequencing/MelodyDeriver.cs`

Phase E guided path:

- `MelodyDeriver.Derive(...)` now branches:
  - if a guided progression is present in `HarmonicContextProvider`, use the new guided derivation path
  - otherwise fall back to the older legacy derivation path

Guided derivation rules that landed:

- derives `8` bars / `128` total steps
- resamples the stroke into a guided note set sized from shape complexity
- quantizes note starts to:
  - 8th-note grid by default (`2` step spacing)
  - 16th-note grid only when `sp.speedVariance > 0.65`
- for every note:
  - bar index = `step / 16`
  - current harmonic context = `progression.ToHarmonicContext(barIndex)`
- strong beats:
  - `step % 4 == 0`
  - snapped to `HarmonicContext.NearestChordTone(...)`
- passing beats:
  - snapped with `MusicalKeys.QuantizeToKey(..., "G major")`
- all guided melody notes are clamped to the electronic Melody register via:
  - `RegisterPolicy.Clamp(midi, PatternType.Melody, "electronic")`
- velocities are clamped into:
  - floor `0.28`
  - ceiling `0.88`
- positive tilt:
  - `sp.tiltSigned > 0`
  - transposes bars 5 and 6 upward by `+2` scale degrees before chord/key snapping
- cadence handling:
  - the final note is forced into bar 8
  - the final note is held for at least a half note (`8` steps)
  - conflicting very-late notes in bar 8 are removed so the cadence note can actually ring

Important implementation interpretation:

- the phase plan text contained a mismatch:
  - one line said the bar-8 note should be "max 4 steps (half note)"
  - the test block said "half note or longer"
- the landed implementation treats a half note correctly as `8` 16th-note steps
- this keeps the cadence audibly clear and aligns with the musical meaning of "half note"

Important compatibility detail:

- the old role-based electronic Melody logic was retained as a private legacy fallback path for non-guided contexts
- guided mode no longer uses primary/counter/fill role branching
- this was an intentional safety choice so hidden free-mode behavior would not be broken mid-rollout

### 4. Guided Melody redraw now replaces the previous Melody pattern

Updated:

- `Assets/RhythmForge/Core/Session/PatternRepository.cs`

New guided replacement behavior:

- before committing a new guided Melody pattern, the repository removes the previously tracked Melody pattern for that phase
- all scene instances tied to that old Melody pattern are removed
- then the new Melody pattern is inserted and tracked in `composition.phasePatternIds`

Current replacement coverage:

- `Harmony`
- `Melody`

Still not covered:

- `Groove`
- `Bass`
- `Percussion`

### 5. Melody commit now emits a dedicated event

Updated:

- `Assets/RhythmForge/Core/Events/RhythmForgeEventBus.cs`
- `Assets/RhythmForge/Core/Session/SessionStore.cs`

Added:

- `MelodyCommittedEvent`

Published from:

- `SessionStore.CommitDraft(...)`

When:

- after a successful Melody commit

Payload:

- committed Melody `patternId`

Why it landed now:

- Phase E needed a formal invalidation point for future Groove/Bass follow-up work
- Phase F and Phase G can subscribe to this event instead of piggybacking on generic state-change wiring

Current limitation:

- no new downstream subscribers landed in Phase E yet
- the event is now available as a stable seam for the next phases

### 6. Background re-derivation now respects Harmony progression changes properly

Updated:

- `Assets/RhythmForge/Core/Session/SessionStore.cs`

Before Phase E:

- background re-derive only carried a cloned bar-0 `HarmonicContext`
- that meant Harmony redraw could trigger Melody re-derive, but the Melody path still lacked real bar-by-bar progression knowledge

After Phase E:

- `HandleChordProgressionChanged(...)` passes the committed `ChordProgression`
- generic background re-derive also carries progression when guided mode is active
- if Harmony is re-derived during a broader pass, `SessionStore` now refreshes the active progression from `derivedSequence.chordEvents`

Practical result:

- redrawing Harmony in guided mode now gives Melody access to the new progression across all 8 bars
- strong-beat chord-tone locks remain meaningful after Harmony redraws

## Files Changed

Core implementation:

- `Assets/RhythmForge/Core/Events/RhythmForgeEventBus.cs`
- `Assets/RhythmForge/Core/PatternBehavior/Behaviors/MelodyLineBehavior.cs`
- `Assets/RhythmForge/Core/PatternBehavior/Behaviors/BassBehavior.cs`
- `Assets/RhythmForge/Core/PatternBehavior/Behaviors/GrooveBehavior.cs`
- `Assets/RhythmForge/Core/PatternBehavior/PatternBehaviorRegistry.cs`
- `Assets/RhythmForge/Core/Sequencing/HarmonicContextProvider.cs`
- `Assets/RhythmForge/Core/Sequencing/MelodyDeriver.cs`
- `Assets/RhythmForge/Core/Sequencing/PatternContextScope.cs`
- `Assets/RhythmForge/Core/Session/DraftBuilder.cs`
- `Assets/RhythmForge/Core/Session/PatternRepository.cs`
- `Assets/RhythmForge/Core/Session/SessionStore.cs`

Tests:

- `Assets/RhythmForge/Editor/MelodyDeriverTests.cs`
- `Assets/RhythmForge/Editor/PatternRepositoryTests.cs`
- `Assets/RhythmForge/Editor/SessionStoreCompositionTests.cs`

Docs:

- `_reference-docs/20260421-creating-music-in-phases.md/Phase-E-Handover-plan.md`
- `_reference-docs/20260421-creating-music-in-phases.md/Phase-E-User-instructions.md`

## Validation

### Targeted Phase E coverage added

Added tests:

- `MelodyDeriverTests.StrongBeats_AreChordTonesOfCurrentBar`
- `MelodyDeriverTests.AllPitches_InGMajorScale`
- `MelodyDeriverTests.Bar8FinalNote_LengthIsHalfNoteOrLonger`
- `PatternRepositoryTests.CommitDraft_InGuidedMode_ReplacesPreviousMelodyPattern`
- `SessionStoreCompositionTests.CommitDraft_ForMelody_PublishesMelodyCommittedEvent`

### Run status

Could not run automated compilation or the Unity test suite in this workspace because:

- `dotnet` is not installed
- `msbuild` is not installed
- no local C# compiler (`csc` / `mcs`) is available either

Validation for this phase was therefore limited to:

- code-path inspection
- diff review
- targeted consistency checks against the existing architecture and Phase D handoff

## Known Gaps And Follow-Up Notes

### 1. Guided Melody rewrite is electronic-guided only

The new Phase E guided rewrite lives in `MelodyDeriver`, which is used by the electronic melody path.

Jazz and NewAge melody derivers still keep their older role-driven behavior when explicitly used.

That matches the rollout assumption that guided mode remains electronically voiced for v1.

### 2. Legacy fallback still exists intentionally

The electronic Melody role hack was removed from the guided path, but a private legacy fallback still exists in `MelodyDeriver` for non-guided calls.

Do not remove that fallback casually unless:

- free mode has been formally retired, or
- its remaining callers have been migrated and validated

### 3. Replacement hygiene is still partial

Guided redraw replacement now covers:

- Harmony
- Melody

It still does not cover:

- Groove
- Bass
- Percussion

If a later phase wants strict one-pattern-per-phase hygiene everywhere, continue the same repository pattern rather than inventing a second ownership mechanism.

### 4. `MelodyCommittedEvent` is a seam, not yet a full workflow

The event is now published, but no new invalidation consumers were added in Phase E.

Phase F should use this event when Groove starts mutating Melody scheduling.

### 5. Bar-5/6 lift was implemented exactly, not widened

The broader theory notes talk about phrase lift in bars 5–8.

The actual Phase E landed behavior lifts only bars 5 and 6 when `tiltSigned > 0`.

This follows the explicit Phase E implementation block and keeps bars 7 and 8 more stable for cadence resolution.

## What The Next Agent Should Do In Phase F

Phase F should treat this Phase E handoff as its runtime baseline.

The next agent should:

1. Add a first-class `GrooveBehavior` and `GrooveShapeMapper`.
2. Store `Composition.groove` as the source of truth instead of pretending Groove is a melody-like note sequence.
3. Subscribe Groove-driven invalidation to `MelodyCommittedEvent` instead of using ad-hoc store polling.
4. Update Melody scheduling, not Melody derivation, so Groove changes rhythm/dynamics without changing pitch.
5. Preserve the guided Melody guarantees from Phase E:
   - strong beats stay chord-safe
   - passing notes stay in key
   - cadence hold remains intact

## Recommended Assumptions To Preserve

- guided mode remains locked to:
  - `G major`
  - `100 bpm`
  - `8 bars`
  - electronic genre defaults
- Harmony remains the upstream musical source of truth for guided Melody
- `DerivedSequence.chordEvents` remains the Harmony progression source of truth
- guided progression updates still happen on commit, not on draft creation
- melody redraw replacement should stay phase-owned through `composition.phasePatternIds`
