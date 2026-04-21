# Phase B Handover Plan

## Purpose

This document is the single source of truth for what was implemented for Phase B of the phased music creation rollout in RhythmForgeVR.

Phase B is a foundation phase. It does not introduce guided-mode UI, phase navigation, or new musical derivation behavior yet. It adds the guided composition data model, default seeded composition state, and the bar-aware harmonic-context plumbing that later phases will consume.

## Locked Scope

Implemented in Phase B:

- guided composition data types
- seeded guided defaults for new empty sessions
- migration from Phase A baseline to the new Phase B baseline
- `SessionStore` composition APIs
- `ChordProgressionChangedEvent`
- progression-aware harmonic-context helpers
- focused EditMode test coverage for the new model
- Phase B documentation

Not implemented in Phase B:

- no `PhaseController`
- no phase panel or phase buttons
- no hidden or read-only UI changes yet
- no rewrite of Harmony, Melody, Groove, Bass, or Percussion derivation
- no one-shape-per-phase enforcement
- no sequencing behavior driven by `Composition` yet

## What Was Implemented

### 1. New Guided Data Model

Added under `Assets/RhythmForge/Core/Data/`:

- `CompositionPhase.cs`
- `ChordProgression.cs`
- `GrooveProfile.cs`
- `Composition.cs`
- `GuidedDefaults.cs`

The new model includes:

- `CompositionPhase` enum with `Harmony`, `Melody`, `Groove`, `Bass`, `Percussion`
- `ChordSlot` and `ChordProgression`
- `GrooveProfile`
- `Composition`
- `CompositionPhasePatternRef`

Important implementation detail:

- `Composition.phasePatternIds` is a `List<CompositionPhasePatternRef>`, not a dictionary.
- This is intentional because the project persists session state through Unity `JsonUtility`, and raw dictionaries are not a safe storage choice here.

### 2. Guided Defaults

`GuidedDefaults.Create()` now seeds a composition with:

- tempo `100f`
- key `G major`
- bars `8`
- current phase `Harmony`
- no groove profile
- empty phase refs
- progression `G, Em, C, D, G, Em, C, D`

Default chord voicings are generated with:

- `MusicalKeys.BuildScaleChord(rootMidi, "G major", new[] { 0, 2, 4 })`

Flavors:

- `major` for G, C, and D
- `minor` for Em

### 3. AppState and Factory Baseline

`AppState` was advanced from Phase A baseline `6` to Phase B baseline `7`.

New fields:

- `bool guidedMode = true`
- `Composition composition`

`AppStateFactory.CreateEmpty()` now creates a guided-ready empty session:

- `guidedMode = true`
- `composition = GuidedDefaults.Create()`
- top-level `tempo` and `key` mirror the composition defaults
- top-level `activeGenreId = "electronic"`
- top-level `harmonicContext` is seeded from bar 0 of the default progression

Compatibility retained:

- top-level `tempo`, `key`, and `harmonicContext` still exist and are still usable
- `activeGroupId` remains the legacy compatibility field

### 4. State Migration

`StateMigrator.NormalizeState()` now:

- bumps `state.version` to `7`
- backfills `composition` when missing
- backfills `composition.progression` when missing
- backfills `composition.phasePatternIds` when missing
- sets `guidedMode = true` when loading a pre-Phase-B save
- preserves legacy top-level `tempo`, `key`, `activeGenreId`, and `harmonicContext`
- keeps all existing Phase A draw-mode normalization intact

This means a migrated older save now has both:

- its original top-level musical state preserved
- a new guided composition model available for future phases

### 5. SessionStore Composition APIs

`SessionStore` now exposes:

- `GetComposition()`
- `SetComposition(Composition)`
- `UpdateProgression(ChordProgression)`
- `UpdateGroove(GrooveProfile)`
- `GetHarmonicContextForBar(int barIndex)`

Behavior details:

- `GetComposition()` lazy-heals missing `composition`, `progression`, and phase refs
- `SetComposition()` is the authoritative composition mutator and mirrors top-level `tempo` and `key` from the supplied composition
- `UpdateProgression()` stores the progression, refreshes top-level `harmonicContext` from bar 0, publishes `ChordProgressionChangedEvent`, and notifies listeners
- `UpdateGroove()` stores the groove profile and notifies listeners
- legacy `GetHarmonicContext()` and `SetHarmonicContext()` remain in place as Phase B compatibility shims

### 6. Event Bus and Harmonic Context Bridge

Added to `RhythmForgeEventBus`:

- `ChordProgressionChangedEvent`

Added to `HarmonicContextProvider`:

- `FromProgression(ChordProgression progression, int barIndex)`
- `SetFromProgression(ChordProgression progression, int barIndex)`

Behavior:

- `barIndex` is zero-based
- out-of-range indices wrap into the progression range
- returned context is built from the requested `ChordSlot`

### 7. Demo Session Behavior

`DemoSession.CreateDemoState()` now guarantees `composition` is non-null.

Repo-specific compatibility guard:

- `state.guidedMode` is explicitly set to `false` for the demo session

Reason:

- the demo session is still the legacy authored demo
- Phase B does not yet have the guided UI flow
- this prevents the demo from being treated as a real guided composition session once later phases begin gating UI on `guidedMode`

The demo session's audible content was intentionally left unchanged.

## Repo-Specific Deviations From The Master Plan

These are intentional and should be treated as the correct Phase B behavior for this repo:

- state version is `7`, not `6`, because Phase A already claimed `6`
- composition phase refs use a serializable list, not a dictionary
- migrated legacy top-level `tempo`, `key`, `activeGenreId`, and `harmonicContext` are preserved
- the legacy demo session now forces `guidedMode = false` so future guided UI does not misclassify it

## Tests Added Or Updated

### Updated

- `Assets/RhythmForge/Editor/StateMigratorTests.cs`
- `Assets/RhythmForge/Editor/ShapeSizeBehaviorTests.cs`

### Added

- `Assets/RhythmForge/Editor/GuidedDefaultsTests.cs`
- `Assets/RhythmForge/Editor/HarmonicContextProviderTests.cs`
- `Assets/RhythmForge/Editor/SessionStoreCompositionTests.cs`

Coverage added:

- guided default composition seeding
- default progression root order
- default voicing pitches staying inside G major
- bar-specific harmonic-context lookup
- wrapped bar-index lookup
- empty-session composition seeding
- progression updates refreshing bar-0 harmonic context
- progression-change event publishing
- groove updates storing without chord-progression events
- migration backfilling composition while preserving legacy top-level state

## Verification Status

Implemented and source-audited:

- yes

Automated verification executed successfully in this environment:

- no

Reason:

- Unity batchmode test execution was blocked because another Unity instance already had the project open
- this environment also does not have `dotnet`, `msbuild`, or `mono` installed for a secondary compile path

Important implication:

- the code changes were completed carefully and cross-checked at source level, but the next agent or local developer should run Unity EditMode tests once the project is not open in another editor instance

## Known Limitations Carried Into Phase C

- `Composition` exists, but current musical behaviors still read the legacy session model
- the current UI still exposes the free-form flow
- the transport panel still shows and uses top-level `tempo` and `key`
- no phase UI state is surfaced yet
- no derivation currently reads `composition.progression` directly
- `harmonicContext` is still the live compatibility bridge for older code paths

## Handover Guidance For Phase C

Phase C should treat these as stable assumptions:

- `Composition` is now the long-term guided source of truth
- top-level `tempo`, `key`, and `harmonicContext` still exist only for compatibility
- `guidedMode` is now the intended feature gate for guided UI
- the default empty session is guided-ready
- the default demo session is intentionally not guided

Recommended Phase C rules:

- use `SessionStore.GetComposition()` rather than reading `State.composition` directly when possible
- drive phase UI from `composition.currentPhase`
- keep bar lookups progression-based through `GetHarmonicContextForBar()` or `HarmonicContextProvider.SetFromProgression()`
- do not remove top-level `harmonicContext` yet; existing derivers still depend on it
- do not convert `phasePatternIds` to a dictionary unless persistence changes away from `JsonUtility`

## Files Touched In Phase B

Primary runtime files:

- `Assets/RhythmForge/Core/Data/AppState.cs`
- `Assets/RhythmForge/Core/Session/StateMigrator.cs`
- `Assets/RhythmForge/Core/Session/SessionStore.cs`
- `Assets/RhythmForge/Core/Events/RhythmForgeEventBus.cs`
- `Assets/RhythmForge/Core/Sequencing/HarmonicContextProvider.cs`
- `Assets/RhythmForge/Core/Session/DemoSession.cs`

New runtime files:

- `Assets/RhythmForge/Core/Data/CompositionPhase.cs`
- `Assets/RhythmForge/Core/Data/ChordProgression.cs`
- `Assets/RhythmForge/Core/Data/GrooveProfile.cs`
- `Assets/RhythmForge/Core/Data/Composition.cs`
- `Assets/RhythmForge/Core/Data/GuidedDefaults.cs`
