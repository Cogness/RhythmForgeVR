# Section 2 Handover Plan

## Summary

This pass implemented the technical-gap work from Gap Analysis Section 2 while preserving the current product split:

- guided mode remains the primary runtime path
- free-mode and Jazz/NewAge genre support remain available
- `SceneStripPanel` and `ArrangementPanel` stay hidden in guided mode rather than deleted

The main outcome is that guided/runtime harmony now has a single source of truth (`chordEvents`), the old role-index plumbing is gone, phase invalidation is explicit and phase-scoped, and Groove/Bass now have dedicated visual/audio seams instead of piggybacking on Melody.

## Implemented Changes

### 1. Harmony payload cleanup

- Removed legacy harmony payload fields from `DerivedSequence`:
  - `flavor`
  - `rootMidi`
  - `chord`
- Removed legacy `HarmonySequence`.
- Updated runtime harmony consumers to use `DerivedSequence.chordEvents` only.
- Updated Jazz/NewAge harmony derivers to emit `chordEvents` for every derived bar.
  - Current legacy-genre behavior is a repeated per-bar progression using the derived free-mode voicing.
  - This preserves the old harmonic color while removing single-chord fallback branching.

### 2. Role-index plumbing removal

- Deleted:
  - `ShapeRoleProvider.cs`
  - `RhythmDeriver.cs`
  - `RhythmLoopBehavior.cs`
- Simplified `PatternContextScope.Push(...)` to carry only harmonic context + optional progression.
- Removed role-aware branching from:
  - `MelodyDeriver.DeriveLegacy`
  - `JazzHarmonyDeriver`
  - `JazzMelodyDeriver`
  - `JazzRhythmDeriver`
  - `NewAgeHarmonyDeriver`
  - `NewAgeMelodyDeriver`
  - `NewAgeRhythmDeriver`
- Removed role-based stereo-width logic from `VoiceSpecResolver.ResolveHarmony`.

### 3. Dedicated invalidation model

- Added `PhaseInvalidationKind` in `RhythmForgeEventBus.cs`:
  - `AsyncRederive`
  - `ScheduleDirty`
- Added `PhaseInvalidationChangedEvent`.
- Replaced the old count-based `_pendingPhaseCounts` in `SessionStore` with phase invalidation flags.
- Added `SessionStore.GetPhaseInvalidation(CompositionPhase)`.
- Guided invalidation behavior now works as follows:
  - Harmony change -> Melody + Bass marked `AsyncRederive`
  - Groove change -> Melody + Percussion marked `ScheduleDirty`
  - Async re-derive clears on background completion
  - Schedule-dirty clears when the affected phase is recommitted

### 4. Groove/Bass dedicated seams

- Added `IAudioDispatcher.PlayBass(...)`.
- Added `AudioEngine.PlayBass(...)`.
- Routed `BassBehavior.Schedule(...)` through `PlayBass(...)` instead of `PlayMelody(...)`.
- Added `VoiceSpecResolver.ResolveBass(...)`.
- Added dedicated visual grammar profiles:
  - `GrooveVisualProfile`
  - `BassVisualProfile`
- Added accessors:
  - `VisualGrammarProfiles.GetGroove()`
  - `VisualGrammarProfiles.GetBass()`
- Updated:
  - `GrooveBehavior` to use Groove visuals
  - `BassBehavior` to use Bass visuals

### 5. UI wiring

- `PhasePanel` now subscribes to `PhaseInvalidationChangedEvent`.
- Phase button labels now distinguish:
  - `Pending` for `AsyncRederive`
  - `Stale` for `ScheduleDirty`
  - `Pending/Stale` if both flags are set

### 6. Migration/versioning

- Bumped `AppState.version` from `7` to `8`.
- Updated `StateMigrator` to normalize old saves to version `8`.
- The existing guided-mode migration policy was preserved:
  - any save older than version `8` is normalized to `guidedMode = true`
- Added best-effort harmony payload normalization for older saves:
  - if a Harmony pattern is missing `derivedSequence.chordEvents`, the migrator rebuilds them from `composition.progression`
  - if progression data is also missing, it falls back to `state.harmonicContext`
  - if progression has no chord slots but a Harmony pattern is successfully rebuilt, the migrator seeds `composition.progression` from that rebuilt payload

## Files Touched

High-signal implementation files:

- `Assets/RhythmForge/Core/Data/SequenceData.cs`
- `Assets/RhythmForge/Core/Session/SessionStore.cs`
- `Assets/RhythmForge/Core/Events/RhythmForgeEventBus.cs`
- `Assets/RhythmForge/Core/Data/VisualGrammarProfileAsset.cs`
- `Assets/RhythmForge/Audio/IAudioDispatcher.cs`
- `Assets/RhythmForge/Audio/AudioEngine.cs`
- `Assets/RhythmForge/Audio/VoiceSpec/VoiceSpecResolver.cs`
- `Assets/RhythmForge/Core/PatternBehavior/Behaviors/BassBehavior.cs`
- `Assets/RhythmForge/Core/PatternBehavior/Behaviors/GrooveBehavior.cs`
- `Assets/RhythmForge/UI/Panels/PhasePanel.cs`
- `Assets/RhythmForge/Core/Sequencing/MelodyDeriver.cs`
- `Assets/RhythmForge/Core/Sequencing/Jazz/*`
- `Assets/RhythmForge/Core/Sequencing/NewAge/*`

Deleted legacy compatibility files:

- `Assets/RhythmForge/Core/Sequencing/ShapeRoleProvider.cs`
- `Assets/RhythmForge/Core/Sequencing/RhythmDeriver.cs`
- `Assets/RhythmForge/Core/PatternBehavior/Behaviors/RhythmLoopBehavior.cs`

## Tests Added / Updated

### Added

- `Assets/RhythmForge/Editor/WalkthroughTests.cs`
  - `Guided5StepFlow_ProducesFullComposition`
  - `HarmonyRedraw_InvalidatesAndRederivesDependentPhases`
  - `GuidedMode_HidesSceneAndArrangementPanels`

- `Assets/RhythmForge/Editor/BehaviorSeamTests.cs`
  - `BassBehavior_Schedule_UsesPlayBass`
  - `GrooveBehavior_AdjustVisualSpec_UsesDedicatedGrooveProfile`
  - `BassBehavior_AdjustVisualSpec_UsesDedicatedBassProfile`

### Updated

- `SessionStoreCompositionTests`
  - schedule-dirty coverage for Groove -> Melody/Percussion
  - async invalidation event coverage for Harmony -> Melody/Bass
- `MusicalCoherenceRegressionTests`
  - legacy genre harmony now asserts `chordEvents`
  - harmonic context thread-local behavior updated to the remaining provider
  - genre re-derive test now validates harmony-context propagation without role tags
- `StateMigratorTests`
  - version `8` assertions
  - version `7 -> 8` normalization coverage
  - missing harmony `chordEvents` rebuilt from legacy harmonic context
- `MelodyDeriverTests`
  - updated `PatternContextScope.Push(...)` signature usage
- `BassDeriverTests`
  - updated `PatternContextScope.Push(...)` signature usage
- `ShapeSizeBehaviorTests`
  - version `8` assertion update

## Verification Status

### Unity batch run attempted

Command attempted:

```bash
"/Applications/Unity/Hub/Editor/6000.4.3f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -quit \
  -projectPath "/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR" \
  -runTests -testPlatform editmode \
  -testResults "/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Logs/EditModeResults.xml"
```

Result:

- blocked because another Unity instance already had the project open
- no full EditMode run could complete from terminal in this session

### Local fallback verification limitations

- `dotnet`, `msbuild`, `mono`, `mcs`, and `csc` are not installed in this environment
- source-level review was completed on the highest-risk files, but a real Unity compile/test pass is still required

## Required Next Verification Step

Before starting Section 3 work, the next engineer/agent should:

1. Close the currently open Unity editor instance or work from that editor directly.
2. Run the full EditMode suite.
3. Pay special attention to:
   - `WalkthroughTests`
   - `BehaviorSeamTests`
   - `SessionStoreCompositionTests`
   - `MusicalCoherenceRegressionTests`

## Known Residual Risks

- Because the Unity test runner was blocked, there may still be compile/runtime issues that only Unity will surface.
- Legacy free-mode behavior no longer differentiates multiple same-type Jazz/NewAge patterns by ensemble role.
  - This is intentional for Section 2.
  - The capability preserved is “free mode still derives and plays,” not “free mode still generates primary/counter/fill by draw order.”
- Existing ScriptableObject assets may need Unity to serialize new `groove` / `bass` visual profile fields on import.
- `SessionStore.BuildRederivationSnapshot(string genreId)` still carries an unused parameter; harmless, but worth cleaning if that file is touched again.

## Section 3+ Handoff Guidance

This document should be the single source of truth for the next implementation slice.

Important follow-on seams for the next agent:

- Section 3 musical work should build on the new invalidation model, not add new broad “pending” state.
- Harmony work must continue using `chordEvents` only.
- If future UI work needs richer status language, extend `PhaseInvalidationKind` rather than reintroducing separate ad-hoc booleans.
- If free mode ever needs multi-pattern ensemble differentiation again, it should be reintroduced with explicit domain modeling, not hidden thread-static role indexing.
- Groove and Bass visual/audio behaviors now have dedicated seams and should be extended there rather than through Melody delegates.

## Recommended Manual Sanity Pass

After Unity compiles:

1. Start a fresh guided session.
2. Commit Harmony, Melody, Groove, Bass, and Percussion in order.
3. Confirm:
   - Harmony, Melody, Groove, Bass, and Percussion all occupy one guided slot each
   - Groove makes Melody show `Stale`
   - Harmony redraw makes Melody and Bass show `Pending`
   - Guided mode still hides Scene Strip and Arrangement panels
   - Bass still plays, but through the new Bass dispatch path
