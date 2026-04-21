# Phase I Handover Plan

## Purpose

This document is the single source of truth for what was implemented for Phase I of the phased music creation rollout in RhythmForgeVR.

Phase I turns the already-functional phased composition flow from phases A-H into a more guided and usable end-to-end VR experience. The work in this phase is mostly UI and session hygiene, not new music derivation.

## Locked Scope

Implemented in Phase I:

- guided startup now loads an empty guided composition instead of pre-drawn demo content
- `SessionStore` now exposes guided phase clearing and phase pending-state queries
- `PhasePanel` now supports:
  - per-phase clear actions
  - a `Play Piece` / `Stop Piece` action
  - pending badges for background re-derives
- `InspectorPanel` now supports guided `Redraw`, `Adjust`, and `Clear` actions for committed phase-owned patterns
- `CommitCardPanel` now supports guided auto-advance with an on/off toggle
- focused edit-mode test coverage for guided starter state and clear-phase behavior
- Phase I handoff and manual-test documentation

Not implemented in Phase I:

- no new dedicated walkthrough integration test was added
- no dedicated `PhaseInvalidationChangedEvent` was introduced; the phase UI still refreshes from `SessionStateChangedEvent`
- no generic pending badge system was added for all conceptual dependencies; the pending badge only tracks real asynchronous re-derives currently in the codebase
- no local Unity compile or edit-mode test execution was possible from the terminal because no Unity or C# CLI tooling was installed

## What Was Implemented

### 1. Guided starter session now matches the phased product

Added:

- `Assets/RhythmForge/Core/Session/GuidedDemoComposition.cs`

Updated:

- `Assets/RhythmForge/RhythmForgeManager.cs`
- `Assets/RhythmForge/Core/Session/DemoSession.cs`
- `Assets/RhythmForge/Editor/AlgorithmTest.cs`

What changed:

- `GuidedDemoComposition.CreateDemoState(...)` now creates the intended guided starter state:
  - `guidedMode = true`
  - `Composition` seeded from `GuidedDefaults`
  - `currentPhase = Harmony`
  - no pre-drawn patterns
  - no pre-spawned instances
- `RhythmForgeManager.LoadDemoSession()` now loads the guided starter composition instead of the old pre-populated demo
- `RhythmForgeManager.LoadFreshGuidedSession()` now also uses `GuidedDemoComposition`
- `DemoSession` was kept as a thin compatibility wrapper that forwards to `GuidedDemoComposition`

Why the wrapper was kept:

- some code and editor utility paths still reference `DemoSession`
- keeping the wrapper avoids unnecessary churn before Phase J cleanup

### 2. Guided phase clearing now lives in the session layer

Updated:

- `Assets/RhythmForge/Core/Session/SessionStore.cs`
- `Assets/RhythmForge/Core/Session/PatternRepository.cs`

What changed:

- `PatternRepository` now exposes `RemovePatternAndInstances(...)` so guided UI can clear a committed phase-owned pattern without duplicating repository logic
- `SessionStore` now exposes:
  - `ClearPhase(CompositionPhase phase)`
  - `HasCommittedPhase(CompositionPhase phase)`
  - `IsPhasePending(CompositionPhase phase)`

Clear-phase behavior by phase:

- `Harmony`
  - removes the committed Harmony pattern and its instances
  - clears the Harmony phase slot
  - resets the progression back to `GuidedDefaults.CreateDefaultProgression()`
  - republishes `ChordProgressionChangedEvent`, which triggers background Melody/Bass re-derivation if those phases exist
- `Melody`
  - removes the committed Melody pattern and its instances
  - clears the Melody phase slot
  - publishes `MelodyCommittedEvent(null)` so warmed playback state can refresh
- `Groove`
  - removes the committed Groove pattern and its instances
  - clears the Groove phase slot
  - clears `Composition.groove`
  - publishes `GrooveCommittedEvent(null)` so warmed playback state can refresh
- `Bass` and `Percussion`
  - remove the committed pattern and its instances
  - clear the phase slot

Important behavior:

- Groove completion state now effectively comes from `Composition.groove != null` through `SessionStore.HasCommittedPhase(CompositionPhase.Groove)`
- guided phase clearing does not rely on instance-only deletion; it removes both the pattern definition and its spawned scene instances

### 3. Pending re-derive tracking now exists for Harmony-driven downstream work

Updated:

- `Assets/RhythmForge/Core/Session/SessionStore.cs`

What changed:

- when a `ChordProgressionChangedEvent` fires, `SessionStore` now:
  - inspects which dependent phase patterns need real background re-derivation
  - marks those phases as pending before scheduling the task
  - clears the pending state when the re-derivation results are applied on the main thread
- pending state uses per-phase counters instead of a simple bool, so overlapping re-derive tasks do not immediately clear each other

What is currently considered pending:

- `Melody`
- `Bass`

Why only those two:

- those are the actual asynchronous progression-dependent re-derives in the current architecture
- Groove and Percussion currently depend on schedule-time state rather than their own async regeneration when Harmony changes

### 4. `PhasePanel` is now phase-aware beyond simple navigation

Updated:

- `Assets/RhythmForge/UI/Panels/PhasePanel.cs`
- `Assets/RhythmForge/Bootstrap/RhythmForgeBootstrapper.cs`
- `Assets/RhythmForge/RhythmForgeManager.cs`

What changed:

- the Phase panel canvas was expanded to fit guided controls
- each phase column now has:
  - the main phase button
  - a small `Clear` action button
- the panel now includes a `Play Piece` button that toggles playback for the composition
- the main phase buttons still show:
  - yellow for current
  - green for filled
  - gray for empty
- when a phase is pending async re-derive, the button label appends `Pending`

How playback works:

- the `Play Piece` button simply toggles `Sequencer` playback
- the label updates between `Play Piece` and `Stop Piece`
- this is intentionally independent from the current selected drawing phase

### 5. `InspectorPanel` now has guided actions for committed phase patterns

Updated:

- `Assets/RhythmForge/UI/Panels/InspectorPanel.cs`

What changed:

- when the selected instance belongs to the committed guided pattern for its phase, the existing bottom-row action buttons are repurposed as:
  - `Redraw`
  - `Adjust`
  - `Clear`

Exact behavior:

- `Redraw`
  - clears the phase via `SessionStore.ClearPhase(...)`
  - sets the current phase back to that phase
  - intended flow: remove old committed shape, then immediately return the user to drawing mode for that phase
- `Adjust`
  - intentionally keeps the pattern selected
  - leaves the user in the existing adjustment workflow:
    - move the instance spatially
    - use the depth slider
    - inspect pan / gain / brightness readouts
    - optionally change the preset override
- `Clear`
  - clears the phase without forcing a phase switch

Important limitation:

- `Adjust` is intentionally lightweight; it does not open a second UI mode
- the existing spatial/mix controls are still the actual adjustment surface

### 6. `CommitCardPanel` now supports guided auto-advance

Updated:

- `Assets/RhythmForge/UI/Panels/CommitCardPanel.cs`
- `Assets/RhythmForge/RhythmForgeManager.cs`

What changed:

- `CommitCardPanel.Initialize(...)` now receives `SessionStore` and `PhaseController`
- in guided mode, the center button is no longer `Save+Dup`
- that button now toggles:
  - `Auto Next ON`
  - `Auto Next OFF`
- pressing `Save` in guided mode:
  - commits the pattern without duplicating
  - optionally advances to the next phase if auto-next is enabled

Free-mode compatibility:

- outside guided mode, the center button still behaves as `Save+Dup`

### 7. Small runtime polish for clear actions

Updated:

- `Assets/RhythmForge/RhythmForgeManager.cs`

What changed:

- `HandleGrooveCommitted(...)` now ignores toast messaging when the event carries `null`
- this prevents a misleading “Groove saved” toast when Groove was actually cleared

## Files Changed

Core runtime:

- `Assets/RhythmForge/Core/Session/GuidedDemoComposition.cs`
- `Assets/RhythmForge/Core/Session/GuidedDemoComposition.cs.meta`
- `Assets/RhythmForge/Core/Session/DemoSession.cs`
- `Assets/RhythmForge/Core/Session/PatternRepository.cs`
- `Assets/RhythmForge/Core/Session/SessionStore.cs`
- `Assets/RhythmForge/RhythmForgeManager.cs`
- `Assets/RhythmForge/UI/Panels/PhasePanel.cs`
- `Assets/RhythmForge/UI/Panels/CommitCardPanel.cs`
- `Assets/RhythmForge/UI/Panels/InspectorPanel.cs`
- `Assets/RhythmForge/Bootstrap/RhythmForgeBootstrapper.cs`

Tests:

- `Assets/RhythmForge/Editor/SessionStoreCompositionTests.cs`
- `Assets/RhythmForge/Editor/AlgorithmTest.cs`

Docs:

- `_reference-docs/20260421-creating-music-in-phases.md/Phase-I-Handover-plan.md`
- `_reference-docs/20260421-creating-music-in-phases.md/Phase-I-User-instructions.md`

## Verification Status

Code review completed:

- guided starter session path reviewed
- phase clear flow reviewed
- Harmony-to-Melody/Bass pending re-derive tracking reviewed
- guided Phase panel, inspector actions, and commit card behavior reviewed
- `git diff --check` passed

Local compilation status:

- `dotnet` not installed
- `msbuild` not installed
- `csc` not installed
- `Unity` CLI not installed

Because of that, no real local compile or Unity edit-mode batch run was possible from the terminal.

## Constraints The Next Agent Should Preserve

- keep `GuidedDemoComposition` as the real guided starter state
- keep `DemoSession` only as a compatibility shim unless a deliberate cleanup pass removes the remaining references
- keep phase clearing centralized in `SessionStore` / `PatternRepository`, not scattered through UI panels
- keep Groove completion tied to `Composition.groove`, not only to the presence of a pattern id
- keep Harmony clear resetting the progression to guided defaults
- keep phase pending tracking count-based, not a single global bool
- keep guided `Save` from duplicating patterns

## Known Limitations

- pending badges currently reflect only real async re-derivation work:
  - Harmony change -> Melody pending
  - Harmony change -> Bass pending
- pending badges do not currently light up for schedule-time dependencies like:
  - Groove affecting Melody timing
  - Groove affecting Percussion swing
- no dedicated walkthrough integration test exists yet
- no dedicated “phase cleared” event exists; clear flows reuse existing session/committed event plumbing where needed

## Recommended Next-Phase Notes

For Phase J cleanup:

- remove the `DemoSession` compatibility wrapper once all references have moved to `GuidedDemoComposition`
- consider introducing explicit clear / invalidation events if guided UI behavior grows more complex
- consider whether `Adjust` should become a more explicit mode or remain a lightweight “stay selected” affordance
- if product wants stronger dependency badging, decide whether schedule-time dependencies should also become visible pending states
- add a real scripted walkthrough test when Unity batch test execution is available
