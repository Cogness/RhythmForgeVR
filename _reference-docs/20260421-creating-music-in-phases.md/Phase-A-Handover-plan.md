# Phase A Handover Plan

## Purpose

This document is the single source of truth for what was implemented for Phase A of the phased music creation plan on top of the current RhythmForgeVR architecture.

Phase A was implemented as a compatibility bridge, not as a guided-composition rollout. The goal was to expand the runtime pattern domain from three modes to five without breaking existing save data, existing authored assets, or the current free-form interaction model.

## Locked Strategy

Phase A uses the **Alias Bridge** strategy.

That means:

- The runtime now exposes five canonical pattern types: `Percussion`, `Melody`, `Harmony`, `Bass`, and `Groove`.
- Legacy names are preserved as enum aliases so existing code and serialized enum ordinals continue to work.
- Existing content and behavior classes for rhythm, melody, and harmony remain in place for compatibility.
- `Bass` and `Groove` are registered and usable, but they are temporary melody-backed placeholders in this phase.

## Implemented Scope

The following work is implemented in code.

### 1. Pattern Type Expansion

`PatternType` was expanded to the canonical five-type runtime surface:

- `Percussion = 0`
- `Melody = 1`
- `Harmony = 2`
- `Bass = 3`
- `Groove = 4`

Legacy aliases were kept:

- `RhythmLoop = Percussion`
- `MelodyLine = Melody`
- `HarmonyPad = Harmony`

This preserves ordinal compatibility for persistence and keeps older call sites compiling during the bridge phase.

### 2. Canonicalization Helper

A new compatibility helper was added:

- [`Assets/RhythmForge/Core/Data/PatternTypeCompatibility.cs`](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Core/Data/PatternTypeCompatibility.cs)

It centralizes:

- canonicalization of alias values
- display label normalization
- percussion checks
- melody-family checks
- harmony checks

This helper is the main guardrail that keeps the phase coherent while both old and new names coexist.

### 3. App State and Migration Update

State versioning and draw-mode defaults were moved to the Phase A baseline:

- `AppState.version` is now `6`
- default `drawMode` is now `Percussion`

`DraftCounters` was extended safely with explicit fields:

- existing fields retained: `rhythm`, `melody`, `harmony`
- new fields added: `bass`, `groove`

This was intentionally done with fields, not a dictionary, because the project currently relies on Unity `JsonUtility`, which does not provide a safe path for the dictionary-based version of this migration.

`StateMigrator` now normalizes legacy draw mode strings:

- `RhythmLoop` and `Rhythm` -> `Percussion`
- `MelodyLine` -> `Melody`
- `HarmonyPad` -> `Harmony`

Canonical names already in save data remain valid:

- `Percussion`
- `Melody`
- `Harmony`
- `Bass`
- `Groove`

### 4. Behavior Registration for Five Types

The pattern behavior registry now resolves all five canonical types.

Existing behaviors remain intact:

- `RhythmLoopBehavior`
- `MelodyLineBehavior`
- `HarmonyPadBehavior`

Temporary Phase A behaviors were added:

- [`Assets/RhythmForge/Core/PatternBehavior/Behaviors/BassBehavior.cs`](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Core/PatternBehavior/Behaviors/BassBehavior.cs)
- [`Assets/RhythmForge/Core/PatternBehavior/Behaviors/GrooveBehavior.cs`](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Core/PatternBehavior/Behaviors/GrooveBehavior.cs)

These two new behaviors:

- expose their own type identity
- expose their own display names
- expose their own draft name prefixes
- delegate derivation, scheduling, voice selection, and visual behavior to `MelodyLineBehavior`

This keeps the app stable while allowing the five-mode domain to be exercised end to end.

### 5. Compatibility Updates Across Type-Switched Systems

The following systems were updated so `Bass` and `Groove` do not fall into invalid or unhandled branches:

- type colors
- visual grammar color lookup
- genre preset lookup
- sound mapping profile lookup
- instrument group default preset lookup
- shape profile sizing
- register policy
- preset bias summary helpers
- parameter summary helpers
- inspector labels and inspector metric logic
- session draw-mode parsing and storage
- derivation routing
- draw-mode controller fallbacks
- draft builder harmony checks
- bootstrap defaults and placeholder UI labels

In all of those places, Phase A intentionally treats `Bass` and `Groove` as melody-family placeholders unless a subsystem only needs a distinct label or a distinct color.

## Files Changed

The main implementation touched these files:

- [`Assets/RhythmForge/Core/Data/PatternType.cs`](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Core/Data/PatternType.cs)
- [`Assets/RhythmForge/Core/Data/PatternTypeCompatibility.cs`](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Core/Data/PatternTypeCompatibility.cs)
- [`Assets/RhythmForge/Core/Data/AppState.cs`](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Core/Data/AppState.cs)
- [`Assets/RhythmForge/Core/Session/StateMigrator.cs`](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Core/Session/StateMigrator.cs)
- [`Assets/RhythmForge/Core/PatternBehavior/PatternBehaviorRegistry.cs`](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Core/PatternBehavior/PatternBehaviorRegistry.cs)
- [`Assets/RhythmForge/Core/PatternBehavior/Behaviors/BassBehavior.cs`](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Core/PatternBehavior/Behaviors/BassBehavior.cs)
- [`Assets/RhythmForge/Core/PatternBehavior/Behaviors/GrooveBehavior.cs`](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Core/PatternBehavior/Behaviors/GrooveBehavior.cs)
- [`Assets/RhythmForge/Core/Data/TypeColors.cs`](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Core/Data/TypeColors.cs)
- [`Assets/RhythmForge/Core/Data/VisualGrammarProfileAsset.cs`](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Core/Data/VisualGrammarProfileAsset.cs)
- [`Assets/RhythmForge/Core/Data/InstrumentGroup.cs`](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Core/Data/InstrumentGroup.cs)
- [`Assets/RhythmForge/Core/Data/GenreProfile.cs`](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Core/Data/GenreProfile.cs)
- [`Assets/RhythmForge/Core/Data/SoundMappingProfileAsset.cs`](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Core/Data/SoundMappingProfileAsset.cs)
- [`Assets/RhythmForge/Core/Analysis/ShapeProfileCalculator.cs`](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Core/Analysis/ShapeProfileCalculator.cs)
- [`Assets/RhythmForge/Core/Sequencing/RegisterPolicy.cs`](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Core/Sequencing/RegisterPolicy.cs)
- [`Assets/RhythmForge/Core/Analysis/PresetBiasResolver.cs`](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Core/Analysis/PresetBiasResolver.cs)
- [`Assets/RhythmForge/UI/PatternParameterHelper.cs`](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/UI/PatternParameterHelper.cs)
- [`Assets/RhythmForge/UI/Panels/InspectorPanel.cs`](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/UI/Panels/InspectorPanel.cs)
- [`Assets/RhythmForge/Interaction/DrawModeController.cs`](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Interaction/DrawModeController.cs)
- [`Assets/RhythmForge/Core/Session/SessionStore.cs`](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Core/Session/SessionStore.cs)
- [`Assets/RhythmForge/Interaction/StrokeCapture.cs`](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Interaction/StrokeCapture.cs)
- [`Assets/RhythmForge/Sequencer/Sequencer.cs`](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Sequencer/Sequencer.cs)
- [`Assets/RhythmForge/Sequencer/PlaybackVisualTracker.cs`](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Sequencer/PlaybackVisualTracker.cs)
- [`Assets/RhythmForge/UI/Panels/TransportPanel.cs`](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/UI/Panels/TransportPanel.cs)
- [`Assets/RhythmForge/Core/Session/DraftBuilder.cs`](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Core/Session/DraftBuilder.cs)
- [`Assets/RhythmForge/Core/PatternBehavior/Behaviors/RhythmLoopBehavior.cs`](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Core/PatternBehavior/Behaviors/RhythmLoopBehavior.cs)
- [`Assets/RhythmForge/Bootstrap/RhythmForgeBootstrapper.cs`](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Bootstrap/RhythmForgeBootstrapper.cs)

## Tests Added or Updated

### Updated

- [`Assets/RhythmForge/Editor/StateMigratorTests.cs`](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Editor/StateMigratorTests.cs)

Changes:

- expected migrated state version updated to `6`
- expected default draw mode updated to `Percussion`
- added coverage for normalization of legacy and canonical draw mode values

### Added

- [`Assets/RhythmForge/Editor/PatternBehaviorRegistryTests.cs`](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Editor/PatternBehaviorRegistryTests.cs)

Coverage:

- registry returns all five canonical types
- each canonical type resolves to a behavior
- resolved behavior type matches the requested canonical type

## Verification Performed

### Compile Verification

The project was recompiled by Unity successfully after the Phase A edits.

### Test Verification

The following focused Unity EditMode runs completed successfully:

1. `RhythmForge.Editor.StateMigratorTests`
2. `RhythmForge.Editor.PatternBehaviorRegistryTests`

One earlier combined filter attempt matched no tests because Unity interpreted the filter unexpectedly. That run was not used as evidence. The test fixtures were rerun separately and those focused runs completed with exit code `0`.

## Manual Behavior Outcome

Based on the implementation, the expected Phase A app behavior is:

- legacy sessions still load
- the visible runtime naming uses `Percussion`, `Melody`, `Harmony`, `Bass`, and `Groove`
- existing `Percussion`, `Melody`, and `Harmony` behavior remains functionally unchanged
- `Bass` and `Groove` are reachable in mode cycling and should work through draw, commit, playback, and inspector flows
- `Bass` and `Groove` currently sound and behave like melody-family patterns by design

## Intentional Non-Goals for Phase A

The following were not implemented in this phase:

- no guided composition UX
- no phase-based session flow
- no replacement of old asset field names such as `rhythmLoop`, `melodyLine`, or `harmonyPad`
- no musically distinct bass derivation pipeline
- no musically distinct groove derivation pipeline
- no class or file renaming cleanup for the old rhythm/melody/harmony behavior classes

## Known Limitations

These limitations are intentional and should not be treated as regressions for Phase A:

- `Bass` and `Groove` are placeholders, not distinct musical generators yet
- some internal code still references legacy names through enum aliases for compatibility
- some asset-backed fields still use old naming because changing them now would add migration risk without Phase A product value
- colors and labels distinguish the five types, but the deeper musical logic only distinguishes percussion, melody-family, and harmony at this stage

## Handover Guidance for the Next Agent

The next agent should treat this implementation as the stable starting point for Phase B or later work.

### Safe Assumptions

- canonical runtime naming is now `Percussion`, `Melody`, `Harmony`, `Bass`, `Groove`
- alias compatibility must remain intact until a dedicated cleanup/migration phase is planned
- save-state version `6` is now the Phase A baseline
- any new type-branching code should canonicalize before switching on `PatternType`
- `Bass` and `Groove` are currently melody-family placeholders everywhere that matters musically

### Replace Later, Not Now

The next agent will likely need to replace these temporary Phase A decisions when the product is ready:

- `BassBehavior` delegating to `MelodyLineBehavior`
- `GrooveBehavior` delegating to `MelodyLineBehavior`
- melody-family fallback in preset lookup, sound mapping, parameter summaries, register policy, and derivation routing

### Recommended Rules for Future Work

- keep `PatternTypeCompatibility` as the first place to extend canonicalization behavior
- avoid removing enum aliases until migration and asset cleanup are explicitly planned
- avoid renaming serialized asset fields without a concrete migration strategy
- if a future phase introduces true bass or groove derivation, update the registry-backed behaviors first, then remove melody-family fallbacks subsystem by subsystem

## Phase B Starting Assumptions

If the next phase introduces guided composition or musically distinct layers, it can assume:

- the app can already cycle and resolve all five pattern types
- save data can survive the naming transition
- inspector, draw mode, and registry infrastructure already recognize the five-type domain
- the remaining work is primarily product behavior, musical specialization, and UX flow, not core compatibility plumbing
