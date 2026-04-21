# Phase C Handover Plan

## Purpose

This document is the single source of truth for what was implemented for Phase C of the phased music creation rollout in RhythmForgeVR.

Phase C introduces guided-mode phase navigation as a real runtime workflow layer. It does not yet rewrite harmony, melody, groove, bass, or percussion derivation. It makes guided mode visible and operable, while keeping the existing musical generation paths intact underneath.

## Locked Scope

Implemented in Phase C:

- `CompositionPhase -> PatternType` mapping helpers
- `PhaseController`
- `PhaseChangedEvent`
- `PhasePanel`
- startup flow now defaults to a fresh guided session
- guided-mode UI gating in `RhythmForgeManager`
- guided-mode transport mode-lock behavior
- guided-mode panel visibility changes
- phase tracking in `SessionStore`
- latest-committed-pattern tracking per phase for guided sessions
- focused EditMode coverage for phase navigation and guided phase fill tracking
- Phase C documentation

Not implemented in Phase C:

- no Harmony phase rewrite yet
- no Melody phase rewrite yet
- no Groove profile scheduling yet
- no Bass deriver yet
- no Percussion rewrite yet
- no one-shape-per-phase replacement enforcement yet
- no auto-advance after commit yet
- no guided demo replacement yet

## What Was Implemented

### 1. Composition Phase Mapping Helpers

Added:

- `Assets/RhythmForge/Core/Data/CompositionPhaseExtensions.cs`

This file is now the central bridge between guided phases and runtime pattern types.

Implemented helpers:

- `CompositionPhase.ToPatternType()`
- `PatternType.ToCompositionPhase()`
- ordered phase list via `CompositionPhaseExtensions.All`

The canonical guided phase order is:

1. `Harmony`
2. `Melody`
3. `Groove`
4. `Bass`
5. `Percussion`

This order is used by `PhaseController.Next()` and `Prev()`.

### 2. Phase Event

Added to `Assets/RhythmForge/Core/Events/RhythmForgeEventBus.cs`:

- `PhaseChangedEvent`

This is published by `PhaseController` whenever guided phase navigation changes phase through the controller.

Important detail:

- `SessionStateChangedEvent` still remains the broader refresh trigger
- `PhaseChangedEvent` is a more specific UI signal for phase-aware elements

### 3. SessionStore Phase APIs

Added to `Assets/RhythmForge/Core/Session/SessionStore.cs`:

- `GetCurrentPhase()`
- `SetCurrentPhase(CompositionPhase phase)`

Behavior:

- `GetCurrentPhase()` reads from `composition.currentPhase`
- `SetCurrentPhase()` creates guided-mode intent by forcing `guidedMode = true`
- `SetCurrentPhase()` notifies state listeners only when the phase actually changes, unless the session was previously non-guided

Important implementation note:

- top-level `drawMode` is still the compatibility field used by older systems
- `composition.currentPhase` is now the guided source of truth for the active creation step
- `RhythmForgeManager` keeps them synchronized when guided mode is active

### 4. Guided Commit Tracking For Filled Phase State

Updated:

- `Assets/RhythmForge/Core/Session/PatternRepository.cs`

When a draft is committed in a guided session:

- the new pattern is inserted as before
- `state.composition.SetPatternId(pattern.type.ToCompositionPhase(), pattern.id)` is now called

This gives Phase C a real filled/empty signal for the `PhasePanel`.

Important limitation:

- this tracks the latest committed pattern for that phase
- it does not remove older patterns
- it does not yet enforce “exactly one committed pattern per phase”
- that stricter replacement behavior remains future work

This is intentional for Phase C because the master plan only requires the UI and navigation layer here.

### 5. Draw Mode Stability Improvement

Updated:

- `Assets/RhythmForge/Interaction/DrawModeController.cs`

`SetMode()` now canonicalizes the requested mode and returns early if the mode is already active.

Why this matters:

- Phase C synchronizes guided phase and draw mode more often than the old free-form flow
- without the guard, redundant event publication would create unnecessary state churn

### 6. New PhaseController

Added:

- `Assets/RhythmForge/Interaction/PhaseController.cs`

Exposed behavior:

- `CurrentPhase`
- `GoToPhase(phase)`
- `Next()`
- `Prev()`
- `SyncFromStore()`

Behavior:

- reads and persists `composition.currentPhase` through `SessionStore`
- drives `DrawModeController.SetMode(phase.ToPatternType())`
- publishes `PhaseChangedEvent`
- wraps correctly from `Percussion -> Harmony` and `Harmony -> Percussion`

Important runtime detail:

- the controller is a MonoBehaviour created by the bootstrapper
- it is initialized by `RhythmForgeManager`
- it is now the intended entry point for guided phase navigation

### 7. New PhasePanel

Added:

- `Assets/RhythmForge/UI/Panels/PhasePanel.cs`

Built at runtime by:

- `Assets/RhythmForge/Bootstrap/RhythmForgeBootstrapper.cs`

The panel includes:

- a current-phase banner
- five phase buttons
- button states driven by guided composition data

Button state semantics:

- yellow = current phase
- green = filled phase
- gray = empty phase

Label semantics:

- each button shows both phase name and state label such as `Harmony / Current`, `Bass / Filled`, or `Groove / Empty`

State source:

- current phase comes from `composition.currentPhase`
- filled state comes from `composition.phasePatternIds`, validated against the actual pattern repository

### 8. Bootstrap Wiring

Updated:

- `Assets/RhythmForge/Bootstrap/RhythmForgeBootstrapper.cs`

New runtime-built components:

- `PhaseController`
- `PhasePanel`

New wiring:

- `PhaseController` added to `ManagerSubsystems`
- `PhasePanel` added to `ManagerPanels`
- `BuildUIPanels()` now builds the Phase panel

Panel placement:

- `PhasePanel` is positioned where `SceneStripPanel` normally sits
- only one of them is visible at a time, depending on `guidedMode`

This avoids introducing a new panel layout conflict in the current runtime UI.

### 9. Manager Guided-Mode Gating

Updated:

- `Assets/RhythmForge/RhythmForgeManager.cs`

New manager responsibilities:

- initialize `PhaseController`
- initialize `PhasePanel`
- synchronize guided phase and legacy draw mode on startup/load/reset
- show or hide guided/free-mode panels based on `guidedMode`
- disable scene thumbstick switching while guided mode is active
- expose `LoadFreshGuidedSession()` for startup and future guided-entry flows

Guided-mode UI behavior implemented:

- `PhasePanel` visible
- `SceneStripPanel` hidden
- `ArrangementPanel` hidden
- transport mode cycling disabled through the transport panel

Free-mode behavior retained:

- `SceneStripPanel` visible
- `ArrangementPanel` visible
- `PhasePanel` hidden
- transport mode cycling still works

Important synchronization detail:

- when guided mode is active, the manager now forces `State.drawMode` to match `composition.currentPhase.ToPatternType()`
- this preserves compatibility with systems that still read the legacy draw-mode field

### 10. Startup Flow Now Enters Guided Mode By Default

Updated:

- `Assets/RhythmForge/Bootstrap/RhythmForgeBootstrapper.cs`
- `Assets/RhythmForge/RhythmForgeManager.cs`

New startup behavior:

- Play Mode now calls `RhythmForgeManager.LoadFreshGuidedSession()` by default
- the old bootstrap-time demo load is no longer the default startup path

Behavior details:

- startup now creates a fresh guided session through `SessionStore.Reset()`
- the guided UI becomes visible immediately on normal launch
- the method intentionally does not delete persistence files; it only resets the live session state for the current launch

Why this repo-specific change was made:

- the user asked for Phase C to be visible immediately on normal app launch
- the previous demo-first startup path kept launching into `guidedMode = false`, which hid the new Phase C UI

### 11. Transport Panel Guided Lock

Updated:

- `Assets/RhythmForge/UI/Panels/TransportPanel.cs`

New guided-mode behavior:

- mode button no longer cycles pattern types
- mode button label changes to `Phase Locked`
- mode button becomes non-interactable
- BPM and key read from `composition` when guided mode is active

Important detail:

- the transport panel was already label-only for BPM and key in this repo
- Phase C therefore expresses “read-only” as a locked display sourced from guided composition state, not as a newly disabled input widget

### 11. Dock Panel Guided Tweaks

Updated:

- `Assets/RhythmForge/UI/Panels/DockPanel.cs`

Repo-specific guided UX improvements added in Phase C:

- scenes tab is hidden while guided mode is active
- dock header label shows `Current phase: ...` instead of the free-form draw mode label

This is a repo-specific extension beyond the minimum master-plan requirement and should be treated as intentional.

### 12. New EditMode Tests

Added:

- `Assets/RhythmForge/Editor/PhaseControllerTests.cs`

Coverage:

- `GoToPhase_UpdatesDrawMode()`
- `NextPrev_Wrap()`

Updated:

- `Assets/RhythmForge/Editor/PatternRepositoryTests.cs`

Added coverage:

- `CommitDraft_InGuidedMode_TracksLatestPatternForPhase()`

## Repo-Specific Deviations From The Master Plan

These are intentional and should be treated as the correct Phase C behavior for this repo:

- the transport panel already exposed BPM and key as labels, so guided “read-only” behavior is implemented by locking mode cycling and sourcing BPM/key from `composition`
- `DockPanel` now hides the scenes tab in guided mode, even though the master plan only required hiding `SceneStripPanel` and `ArrangementPanel`
- guided phase fill state is implemented as “latest committed pattern id per phase”, not strict one-instance replacement yet
- thumbstick-driven scene switching is disabled in guided mode to match the hidden scene UI and avoid invisible scene changes

## Verification Status

Implemented and source-audited:

- yes

Automated verification executed successfully in this environment:

- no

Reason:

- this environment does not have `Unity`, `unity`, `dotnet`, `mono`, or `msbuild` available
- because of that, no compile or EditMode run could be executed from the terminal here

Important implication:

- the Phase C code and tests were completed carefully and reviewed at source level
- the next agent or local developer should run Unity EditMode tests in the project editor

## Expected Runtime Outcome

### Fresh guided session

In a fresh guided session, the expected UI behavior is now:

- `PhasePanel` is visible
- current phase starts at `Harmony`
- `SceneStripPanel` is hidden
- `ArrangementPanel` is hidden
- transport mode cycling is locked
- BPM should show `100 BPM`
- key should show `G major`

Drawing behavior in guided mode at Phase C:

- drawing still works in every phase
- `Harmony`, `Melody`, and `Percussion` still use the current pre-rewrite behavior
- `Bass` and `Groove` still use the Phase A placeholders
- committing a draft marks that phase as filled on the `PhasePanel`

### Legacy demo session

The legacy demo session still behaves as free mode because `DemoSession` still forces:

- `guidedMode = false`

That means:

- the demo can still be loaded manually for regression checking
- when manually loaded, the new Phase panel will be hidden and the free-form UI will appear

This is expected and should not be treated as a bug.

## Known Limitations Carried Into Phase D

- guided mode navigation exists, but the actual harmony derivation is still the legacy implementation
- phase fill state tracks the latest committed pattern id, but older phase patterns are not cleaned up automatically
- there is no one-shape-per-phase enforcement yet
- `Composition.phasePatternIds` is currently UI/state metadata only; deeper phase-specific playback ownership is still future work
- the demo session is still the legacy seeded demo, not the future guided empty demo
- startup now enters a fresh guided session by default, but the legacy demo path still exists as a separate manual load path
- the transport panel mode button is locked in guided mode, but there is no phase auto-advance after commit yet
- scenes still exist in data and save state; they are only hidden in guided mode

## Handover Guidance For Phase D

Phase D should treat these as stable assumptions:

- `composition.currentPhase` is now the guided active-step source of truth
- `CompositionPhaseExtensions.ToPatternType()` is the canonical mapping bridge
- `PhaseController` is now the correct place to move guided phase selection
- `PhasePanel` already exists and can be extended instead of replaced
- guided-mode UI gating is already centralized in `RhythmForgeManager`
- `composition.phasePatternIds` already gives the next agent a way to know which phase currently owns the latest committed pattern

Recommended Phase D rules:

- keep using `SessionStore.GetComposition()` and `GetCurrentPhase()` instead of reading `State.composition` directly where practical
- if Harmony phase starts replacing prior phase-owned content, update `composition.phasePatternIds` as the authoritative UI ownership record
- do not remove the top-level `drawMode` field yet; it is still a compatibility seam for current systems
- preserve the `DemoSession.guidedMode = false` behavior until the guided demo replacement phase is intentionally implemented
- treat the current guided phase fill state as metadata only; do not assume Phase C already guarantees uniqueness

## Files Touched In Phase C

Primary runtime files updated:

- `Assets/RhythmForge/RhythmForgeManager.cs`
- `Assets/RhythmForge/Bootstrap/RhythmForgeBootstrapper.cs`
- `Assets/RhythmForge/Core/Events/RhythmForgeEventBus.cs`
- `Assets/RhythmForge/Core/Session/SessionStore.cs`
- `Assets/RhythmForge/Core/Session/PatternRepository.cs`
- `Assets/RhythmForge/Interaction/DrawModeController.cs`
- `Assets/RhythmForge/UI/Panels/TransportPanel.cs`
- `Assets/RhythmForge/UI/Panels/DockPanel.cs`

New runtime files:

- `Assets/RhythmForge/Core/Data/CompositionPhaseExtensions.cs`
- `Assets/RhythmForge/Interaction/PhaseController.cs`
- `Assets/RhythmForge/UI/Panels/PhasePanel.cs`

Test files:

- `Assets/RhythmForge/Editor/PhaseControllerTests.cs`
- `Assets/RhythmForge/Editor/PatternRepositoryTests.cs`

## Suggested Next-Agent Checklist

Before starting Phase D, the next agent should:

1. Run Unity EditMode tests.
2. Manually verify a fresh guided session shows the Phase panel.
3. Confirm the legacy demo still stays in free mode.
4. Use this file, not the older Phase B handoff, as the current source of truth for guided navigation behavior.
