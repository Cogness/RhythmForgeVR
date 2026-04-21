# Phase G Handover Plan

## Purpose

This document is the single source of truth for what was implemented for Phase G of the phased music creation rollout in RhythmForgeVR.

Phase G replaces the temporary Bass stub with a first-class guided Bass phase. One committed Bass stroke now derives its own bass-note sequence, locks beat 1 of every bar to the active progression root, redraw-replaces the previously committed Bass pattern, and re-derives against Harmony changes.

## Locked Scope

Implemented in Phase G:

- guided Bass now has its own real behavior and deriver
- Bass no longer aliases Melody derivation
- Bass commit redraw now replaces the previously committed Bass pattern in guided mode
- background re-derivation after Harmony changes now rebuilds Bass through the new bass logic
- Bass uses the `trap-bass` preset by default
- Bass pitch output is clamped to the guided electronic bass register
- focused Phase G edit-mode tests
- Phase G handoff and manual-test documentation

Not implemented in Phase G:

- no new `BassCommittedEvent` was added
- Bass does not have a dedicated visual grammar profile yet; it reuses the Melody visual grammar path
- Bass does not introduce a new audio-dispatcher method; it reuses the existing tonal playback path with the bass preset
- Percussion phase rewrite is still not done
- Phase I auto-advance, pending badges, and guided demo replacement are still not done

## What Was Implemented

### 1. Bass is now a real guided behavior

Updated:

- `Assets/RhythmForge/Core/PatternBehavior/Behaviors/BassBehavior.cs`

New runtime behavior:

- `BassBehavior` no longer delegates to `MelodyBehavior`
- `BassBehavior.Derive(...)` now routes through `BassDeriver`
- scheduling and prewarm collection read Bass notes directly from `derivedSequence.notes`
- Bass defaults to the `trap-bass` preset produced by derivation

Compatibility decision:

- Bass still uses the existing tonal voice path through `PlayMelody(...)` and `VoiceSpecResolver.ResolveMelody(...)`
- this is intentional because the preset itself carries the "bass" identity and the audio stack already treats bass presets differently through `ResolvedVoiceSpec.isBass`
- Bass visuals still use the Melody visual grammar profile for now, while the phase color remains the Bass color already introduced in earlier phases

### 2. New `BassDeriver` maps one stroke into a first-class bass line

Added:

- `Assets/RhythmForge/Core/Sequencing/BassDeriver.cs`

What the deriver does:

- reads the active guided `ChordProgression` from `HarmonicContextProvider.CurrentProgression`
- falls back to a repeated harmonic-context progression if the progression bridge is missing
- always emits at least one bass note per bar on beat 1
- clamps every root to `RegisterPolicy.ClampBass(..., "electronic")`
- emits a `DerivedSequence` with:
  - `kind = "bass"`
  - `totalSteps = 128` for the guided 8-bar loop
  - `notes = List<MelodyNote>` used as bass note events

Shape-to-bass mapping that landed:

- `shapeProfile.directionBias`
  - converted to signed motion using `(directionBias - 0.5f) * 2`
  - `> 0.3` enables ascending end-of-bar walk behavior
  - `< -0.3` enables descending end-of-bar walk behavior
- `shapeProfile.verticalSpan > 0.5`
  - adds the fifth on beat 3
- `shapeProfile.horizontalSpan`
  - controls how long sustained bass notes are before duration fitting
  - wider shapes hold longer
- `shapeProfile.pathLength`
  - opens density tiers:
  - sparse: root holds
  - medium: root plus fifth
  - busy: beat-2 reinforcement plus end-of-bar walk notes

Important musical rules preserved:

- every bar starts with the chord root on beat 1
- bar 1 and bar 5 are therefore guaranteed root anchors automatically
- all normal bass notes stay in G major
- one optional chromatic leading tone is allowed only on the final eighth of bar 4 or bar 8, and only for strongly ascending, angular shapes

Implementation detail:

- busy ascending shapes may create step-12 and step-14 approach notes near the bar boundary
- if an octave-perfect target walk would exceed the guided bass range, the deriver resolves to the nearest in-range in-key ceiling/floor note instead of breaking the bass register contract

### 3. Guided Bass redraw now replaces the previous Bass pattern

Updated:

- `Assets/RhythmForge/Core/Session/PatternRepository.cs`
- `Assets/RhythmForge/Editor/PatternRepositoryTests.cs`

New guided replacement behavior:

- `PatternRepository.ShouldReplaceGuidedPhasePattern(...)` now includes `PatternType.Bass`
- committing a new Bass draft removes the previous guided Bass pattern, its instances, and its scene references before inserting the new one
- `composition.phasePatternIds` now tracks the latest Bass pattern as a true single-slot phase, just like Harmony, Melody, and Groove

Why this matters:

- Bass now matches the "one shape per phase per piece" contract
- the next agent can safely assume there is only one committed Bass pattern in guided mode

### 4. Progression-driven background re-derivation now uses Bass logic

Updated:

- `Assets/RhythmForge/Core/Session/SessionStore.cs`

New background behavior:

- the existing Harmony-change background re-derivation snapshot already included Bass patterns
- Phase G changes the actual re-derivation branch so `PatternType.Bass` now rebuilds through `BassDeriver` instead of `genre.MelodyDeriver`
- when Harmony changes, Bass beat-1 roots follow the updated progression instead of recomputing as melody-like notes

Why this matters:

- Bass is now progression-aware in the same way the Phase G plan required
- the next agent does not need extra store plumbing before Phase H; the progression invalidation path is already compatible

### 5. Phase G tests were added and updated

Added:

- `Assets/RhythmForge/Editor/BassDeriverTests.cs`

Updated:

- `Assets/RhythmForge/Editor/PatternRepositoryTests.cs`

Coverage that landed:

- `BassDeriverTests.Beat1OfEveryBar_EqualsProgressionRoot()`
- `BassDeriverTests.AllPitches_InGMajor_OrPassingChromaticAllowedOnBar4Beat4()`
- `PatternRepositoryTests.CommitDraft_InGuidedMode_ReplacesPreviousBassPattern()`

## Files Changed

Core runtime:

- `Assets/RhythmForge/Core/PatternBehavior/Behaviors/BassBehavior.cs`
- `Assets/RhythmForge/Core/Sequencing/BassDeriver.cs`
- `Assets/RhythmForge/Core/Session/PatternRepository.cs`
- `Assets/RhythmForge/Core/Session/SessionStore.cs`

Tests:

- `Assets/RhythmForge/Editor/BassDeriverTests.cs`
- `Assets/RhythmForge/Editor/PatternRepositoryTests.cs`

Docs:

- `_reference-docs/20260421-creating-music-in-phases.md/Phase-G-Handover-plan.md`
- `_reference-docs/20260421-creating-music-in-phases.md/Phase-G-User-instructions.md`

## Verification Status

Code review completed:

- Bass stub removal path checked
- guided replacement logic checked
- Harmony-change re-derivation branch checked
- new Bass tests added to cover the critical musical invariants

Local compilation status:

- no CLI C# toolchain is installed in this workspace
- `dotnet`, `msbuild`, `mono`, `mcs`, and `csc` were all unavailable
- because of that, I could not run an actual local compile or Unity edit-mode test pass from the terminal

## Constraints The Next Agent Should Preserve

- keep Bass first-class; do not route it back through Melody derivation
- keep beat 1 of every bar locked to the progression root
- keep Bass redraw as single-slot replacement in guided mode
- keep background Bass re-derivation progression-aware
- keep the guided Bass register clamped to the electronic bass range unless the product decision explicitly broadens guided-mode genre support

## Recommended Next-Phase Notes

For Phase H:

- Percussion can now assume Harmony, Melody, Groove, and Bass all exist as phase-owned artifacts
- if Phase H wants to consume Groove swing, it does not need Bass changes first
- no extra repository or composition-model work is needed before Percussion replacement logic; Bass already proved the single-slot phase replacement path

Potential cleanup later:

- add a dedicated Bass visual grammar profile if the current Melody-shaped playback visualization feels too "lead-like" in VR
- consider a `VoiceSpecResolver.ResolveBass(...)` or `IAudioDispatcher.PlayBass(...)` only if later phases need bass-specific DSP beyond what the preset-based path already gives
- if guided mode becomes truly genre-switchable, move the hardcoded electronic Bass assumptions into a guided-mode policy layer
