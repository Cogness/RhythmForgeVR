# Phase H Handover Plan

## Purpose

This document is the single source of truth for what was implemented for Phase H of the phased music creation rollout in RhythmForgeVR.

Phase H replaces the old guided rhythm-loop path with a first-class guided Percussion phase. One committed Percussion stroke now derives a full 8-bar drum pattern, redraw-replaces the previous Percussion pattern in guided mode, adds the required bar-4 and bar-8 fills, and can inherit Groove swing as delayed off-beat drum timing at schedule time.

## Locked Scope

Implemented in Phase H:

- new first-class `PercussionBehavior`
- new first-class `PercussionDeriver`
- legacy `RhythmLoopBehavior` and `RhythmDeriver` kept only as compatibility wrappers
- guided percussion now derives a full 8-bar loop instead of the old 2-bar or 4-bar rhythm loop sizing rule
- old role-index full/counter/ghost branching removed from the main guided percussion path
- bar-4 and bar-8 guided fill events added
- groove swing now affects percussion scheduling on off-beats
- guided percussion redraw now replaces the previous phase-owned Percussion pattern
- focused Phase H edit-mode tests
- Phase H handoff and manual-test documentation

Not implemented in Phase H:

- Jazz and New Age rhythm derivers were not rewritten; the guided flow is still effectively scoped to the Electronic path for v1
- no new `PercussionCommittedEvent` was added
- visual grammar assets were not renamed from `RhythmLoop` to `Percussion`; the new behavior intentionally reuses the existing rhythm visual grammar profile
- no local Unity or C# compile/test pass was possible from the terminal because no CLI toolchain was installed

## What Was Implemented

### 1. Percussion is now a first-class guided behavior

Added:

- `Assets/RhythmForge/Core/PatternBehavior/Behaviors/PercussionBehavior.cs`

Updated:

- `Assets/RhythmForge/Core/PatternBehavior/PatternBehaviorRegistry.cs`
- `Assets/RhythmForge/Core/PatternBehavior/Behaviors/RhythmLoopBehavior.cs`

What changed:

- `PatternBehaviorRegistry` now registers `PercussionBehavior` as the real runtime behavior for `PatternType.Percussion`
- `PercussionBehavior` owns the guided percussion scheduling path
- `RhythmLoopBehavior` is now only a thin legacy wrapper that forwards to `PercussionBehavior`
- auto-generated draft names for the phase now use the `Percussion` prefix instead of the older `Beat` naming

Why the wrapper was kept:

- there are still legacy references and older code paths using `RhythmLoopBehavior`
- keeping the wrapper reduces migration risk while making the guided architecture truthful for the next agent

### 2. New `PercussionDeriver` owns the guided drum pattern generation

Added:

- `Assets/RhythmForge/Core/Sequencing/PercussionDeriver.cs`

Updated:

- `Assets/RhythmForge/Core/Sequencing/RhythmDeriver.cs`
- `Assets/RhythmForge/Core/Sequencing/Electronic/ElectronicRhythmDeriver.cs`
- `Assets/RhythmForge/Editor/AlgorithmTest.cs`

What the new deriver does:

- always derives `GuidedDefaults.Bars` bars, which is the guided 8-bar loop
- preserves the useful shape-driven mapping from the old rhythm logic:
  - `aspectRatio` / `circularity` select the kick pattern
  - `symmetry` changes the snare pattern
  - `angularity` and size increase hat density
  - dense or unstable shapes can still add extra ghost-style percussion hits
- removes the old `ShapeRoleProvider.Current` full/counter/ghost branching from the main guided percussion logic
- always emits a full pattern for the guided phase

Percussion invariants that now hold:

- step `0` always contains a `kick`
- step `8` always contains a `snare`
- bars 4 and 8 now contain explicit end-of-bar snare fills
- the loop is always 8 bars long in the guided flow

Fill details that landed:

- bar 4: extra snare hits on steps `14` and `15`, plus a transition accent on bar 5 beat 1
- bar 8: extra snare hits on steps `13`, `14`, and `15`

Compatibility decision:

- `RhythmDeriver` still exists, but it is now only a forwarding wrapper to `PercussionDeriver`
- this keeps older references working while making the new implementation name match the phase language

### 3. Groove swing now reaches percussion scheduling

Updated:

- `Assets/RhythmForge/Core/PatternBehavior/Behaviors/PercussionBehavior.cs`
- `Assets/RhythmForge/Audio/IAudioDispatcher.cs`
- `Assets/RhythmForge/Audio/AudioEngine.cs`
- `Assets/RhythmForge/Audio/SamplePlayer.cs`

What changed:

- `IAudioDispatcher.PlayDrum(...)` now accepts an optional `startDelay`
- `AudioEngine` and `SamplePlayer` now pass that delay through to the clip playback path
- `PercussionBehavior.Schedule(...)` reads `appState.composition.groove.swing` when guided mode is active
- off-beat percussion events now play later by `groove.swing * stepDuration`
- positive per-event `microShift` is also preserved as additional delay

Important limitation:

- only positive delay is currently honored in scheduling; early drum hits are still not scheduled ahead of the clock
- this is acceptable for the current guided swing requirement because the requirement is specifically delayed off-beats

### 4. Guided Percussion redraw now follows the one-shape-per-phase rule

Updated:

- `Assets/RhythmForge/Core/Session/PatternRepository.cs`
- `Assets/RhythmForge/Editor/PatternRepositoryTests.cs`

What changed:

- `PatternRepository.ShouldReplaceGuidedPhasePattern(...)` now includes `PatternType.Percussion`
- committing a new Percussion draft removes the previous guided Percussion pattern, its instances, and scene references first
- `composition.phasePatternIds[CompositionPhase.Percussion]` now behaves the same way as Harmony, Melody, Groove, and Bass

Why this matters:

- guided mode is now consistent across all five drawing phases
- the next agent can assume there is exactly one committed Percussion pattern in guided mode

### 5. Phase H tests were added

Added:

- `Assets/RhythmForge/Editor/PercussionDeriverTests.cs`

Updated:

- `Assets/RhythmForge/Editor/PatternRepositoryTests.cs`

Coverage that landed:

- `PercussionDeriverTests.KickOnStepZero_AlwaysPresent()`
- `PercussionDeriverTests.SnareOnStep8_AlwaysPresent()`
- `PercussionDeriverTests.Bar4AndBar8_ContainFillEvents()`
- `PatternRepositoryTests.CommitDraft_InGuidedMode_ReplacesPreviousPercussionPattern()`

## Files Changed

Core runtime:

- `Assets/RhythmForge/Core/PatternBehavior/Behaviors/PercussionBehavior.cs`
- `Assets/RhythmForge/Core/PatternBehavior/Behaviors/RhythmLoopBehavior.cs`
- `Assets/RhythmForge/Core/PatternBehavior/PatternBehaviorRegistry.cs`
- `Assets/RhythmForge/Core/Sequencing/PercussionDeriver.cs`
- `Assets/RhythmForge/Core/Sequencing/RhythmDeriver.cs`
- `Assets/RhythmForge/Core/Sequencing/Electronic/ElectronicRhythmDeriver.cs`
- `Assets/RhythmForge/Core/Session/PatternRepository.cs`
- `Assets/RhythmForge/Audio/IAudioDispatcher.cs`
- `Assets/RhythmForge/Audio/AudioEngine.cs`
- `Assets/RhythmForge/Audio/SamplePlayer.cs`

Tests:

- `Assets/RhythmForge/Editor/PercussionDeriverTests.cs`
- `Assets/RhythmForge/Editor/PatternRepositoryTests.cs`
- `Assets/RhythmForge/Editor/AlgorithmTest.cs`

Docs:

- `_reference-docs/20260421-creating-music-in-phases.md/Phase-H-Handover-plan.md`
- `_reference-docs/20260421-creating-music-in-phases.md/Phase-H-User-instructions.md`

## Verification Status

Code review completed:

- main guided percussion runtime path checked
- guided replacement behavior checked
- groove-to-percussion swing propagation checked
- focused edit-mode tests added for the new musical invariants

Local compilation status:

- `dotnet`, `msbuild`, `mono`, `mcs`, `csc`, and `Unity` CLI were all unavailable in this workspace
- because of that, I could not run a real local compile or Unity edit-mode batch run from the terminal

## Constraints The Next Agent Should Preserve

- keep `PercussionBehavior` as the real guided behavior
- keep `PercussionDeriver` as the real guided drum derivation path
- keep the legacy `RhythmLoopBehavior` / `RhythmDeriver` wrappers only as compatibility shims unless a deliberate cleanup pass removes them everywhere
- keep guided Percussion as a single-slot phase in `PatternRepository`
- keep step `0` kick and step `8` snare invariants
- keep bar-4 and bar-8 fills intact unless product direction changes
- keep percussion swing sourced from `Composition.groove.swing` rather than inventing a second swing source

## Recommended Next-Phase Notes

For Phase I:

- the phase panel can now safely treat all five phases as single-slot guided phases
- pending / complete badges can use:
  - Harmony: `composition.GetPatternId(CompositionPhase.Harmony)`
  - Melody: `composition.GetPatternId(CompositionPhase.Melody)`
  - Groove: `composition.groove != null`
  - Bass: `composition.GetPatternId(CompositionPhase.Bass)`
  - Percussion: `composition.GetPatternId(CompositionPhase.Percussion)`
- if Phase I adds phase auto-advance, Percussion should be treated as the terminal drawing phase before any future “review/playback complete” state

Potential cleanup later:

- rename the remaining `Rhythm*` compatibility interfaces, profile fields, and helper names if the product fully commits to the phase vocabulary everywhere
- add explicit edit-mode coverage for percussion swing scheduling if that timing nuance becomes regression-prone
- consider a future genre-policy layer if guided mode is reopened to Jazz or New Age instead of staying effectively Electronic-first
