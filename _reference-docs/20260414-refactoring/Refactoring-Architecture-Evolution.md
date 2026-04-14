# RhythmForge VR Architecture Evolution

Companion to `Refactoring-Plan.md`.

This note summarizes:
- how responsibilities were concentrated in the original architecture,
- how they are distributed in the current refactored architecture,
- which extension points now exist,
- why the new structure is easier to extend, test, and maintain.

## 1. Initial Architecture: Responsibility Concentration

The original codebase centered a large amount of behavior in a few high-pressure classes. Each class mixed multiple unrelated responsibilities, which increased change surface and made new features expensive.

| Original class | Approx. size from plan | Initial responsibility mix |
|---|---:|---|
| `ProceduralSynthesizer.cs` | ~600+ lines | voice spec resolution, drum generation, tonal generation, effects, waveform/filter utilities |
| `Sequencer.cs` | ~580 lines | transport FSM, lookahead scheduling, arrangement navigation, playback visual tracking |
| `SessionStore.cs` | ~515 lines | app state, CRUD, scene mutation, state migration, sound resolution |
| `RhythmForgeManager.cs` | ~390 lines | lifecycle wiring, visualizer lifecycle, scene switching input, autosave, playback dispatch |
| `PatternVisualizer.cs` | ~440 lines | line rendering, playback halo, marker, collider, labels, per-pattern visual grammar |

### Initial structure

```mermaid
flowchart TB
    Boot["RhythmForgeBootstrapper<br/>runtime setup + UI creation + panel layout"] --> Manager["RhythmForgeManager<br/>event wiring + visualizer lifecycle + autosave + input polling"]

    Manager --> Store["SessionStore<br/>state + CRUD + scenes + migration + sound resolution"]
    Manager --> Seq["Sequencer<br/>transport + scheduling + arrangement + playback visuals"]
    Manager --> Viz["PatternVisualizer<br/>shape + halo + marker + collider + labels + type-specific animation"]
    Seq --> Synth["ProceduralSynthesizer<br/>voice spec + drums + tonal + effects + utilities"]

    Synth --> SynthA["Voice spec resolution"]
    Synth --> SynthB["Drum rendering"]
    Synth --> SynthC["Tonal rendering"]
    Synth --> SynthD["Effects chain"]
    Synth --> SynthE["DSP utilities"]

    Seq --> SeqA["Transport FSM"]
    Seq --> SeqB["Step scheduling"]
    Seq --> SeqC["Arrangement navigation"]
    Seq --> SeqD["Pulse / sustain / phase tracking"]

    Store --> StoreA["Pattern / instance CRUD"]
    Store --> StoreB["Scene / arrangement mutation"]
    Store --> StoreC["State migration"]
    Store --> StoreD["Effective sound / preset resolution"]

    Viz --> VizA["Shape rendering"]
    Viz --> VizB["Halo rendering"]
    Viz --> VizC["Playback animation"]
    Viz --> VizD["Interaction bounds"]
    Viz --> VizE["Parameter label"]
```

### Initial pain points

- Adding a new pattern type meant changing multiple places that switched on `PatternType`.
- Audio behavior and DSP logic were hard to test independently from the full rendering path.
- UI panels and manager wiring depended on direct callback chains.
- Sound and visual formulas were embedded in code instead of being tunable in data.

## 2. Evolution Map: Old Large Classes to New Focused Units

```mermaid
flowchart LR
    PS["ProceduralSynthesizer.cs"] --> RV["ResolvedVoiceSpec.cs"]
    PS --> VSR["VoiceSpecResolver.cs"]
    PS --> DS["DrumSynthesizer.cs"]
    PS --> TS["TonalSynthesizer.cs"]
    PS --> FX["AudioEffectsChain.cs"]
    PS --> SU["SynthUtilities.cs"]
    PS --> PF["ProceduralSynthesizer.cs<br/>thin facade"]

    SQ["Sequencer.cs"] --> TC["TransportController.cs"]
    SQ --> AN["ArrangementNavigator.cs"]
    SQ --> PVT["PlaybackVisualTracker.cs"]
    SQ --> SQF["Sequencer.cs<br/>orchestrator"]

    SS["SessionStore.cs"] --> PR["PatternRepository.cs"]
    SS --> SC["SceneController.cs"]
    SS --> SM["StateMigrator.cs"]
    SS --> SR["SoundProfileResolver.cs"]
    SS --> SSF["SessionStore.cs<br/>facade"]

    MGR["RhythmForgeManager.cs"] --> VM["VisualizerManager.cs"]
    MGR --> AC["AutosaveController.cs"]
    MGR --> MGF["RhythmForgeManager.cs<br/>top-level coordinator"]

    PV["PatternVisualizer.cs"] --> SLR["ShapeLineRenderer.cs"]
    PV --> PHR["PlaybackHaloRenderer.cs"]
    PV --> PA["PlaybackAnimator.cs"]
    PV --> PVF["PatternVisualizer.cs<br/>coordinator"]
```

## 3. Current Refactored Architecture

The refactored codebase is now organized around facades, collaborators, registries, data profiles, and a typed event bus.

```mermaid
flowchart TB
    Boot["RhythmForgeBootstrapper"] --> Manager["RhythmForgeManager"]
    Boot --> DataProfiles["Profile assets<br/>InstrumentRegistryAsset<br/>SoundMappingProfileAsset<br/>VisualGrammarProfileAsset"]

    Manager --> Bus["RhythmForgeEventBus"]
    Manager --> Store["SessionStore facade"]
    Manager --> Seq["Sequencer orchestrator"]
    Manager --> VizMgr["VisualizerManager"]
    Manager --> Auto["AutosaveController"]
    Manager --> Panels["UI Panels"]

    Store --> Repo["PatternRepository"]
    Store --> SceneCtl["SceneController"]
    Store --> Migrator["StateMigrator"]
    Store --> SoundRes["SoundProfileResolver"]

    Seq --> TransportCtl["TransportController"]
    Seq --> ArrNav["ArrangementNavigator"]
    Seq --> VisualTrack["PlaybackVisualTracker"]
    Seq --> PatternReg["PatternBehaviorRegistry"]
    Seq --> AudioDispatch["IAudioDispatcher"]

    PatternReg --> RhythmBehavior["RhythmLoopBehavior"]
    PatternReg --> MelodyBehavior["MelodyLineBehavior"]
    PatternReg --> HarmonyBehavior["HarmonyPadBehavior"]

    AudioDispatch --> AudioEngine["AudioEngine"]
    AudioEngine --> SamplePlayer["SamplePlayer"]
    SamplePlayer --> VoiceReg["VoiceRendererRegistry"]
    VoiceReg --> DrumRenderer["ProceduralDrumRenderer"]
    VoiceReg --> TonalRenderer["ProceduralTonalRenderer"]
    DrumRenderer --> ProcSynth["ProceduralSynthesizer facade"]
    TonalRenderer --> ProcSynth
    ProcSynth --> VoiceSpec["VoiceSpecResolver + ResolvedVoiceSpec"]
    ProcSynth --> DrumSynth["DrumSynthesizer"]
    ProcSynth --> TonalSynth["TonalSynthesizer"]
    ProcSynth --> FxChain["AudioEffectsChain"]
    ProcSynth --> DSP["SynthUtilities"]

    VizMgr --> PatternViz["PatternVisualizer coordinator"]
    PatternViz --> ShapeRenderer["ShapeLineRenderer"]
    PatternViz --> HaloRenderer["PlaybackHaloRenderer"]
    PatternViz --> PlaybackAnim["PlaybackAnimator"]

    DataProfiles --> Store
    DataProfiles --> PatternReg
    DataProfiles --> PatternViz
    DataProfiles --> TypeColors["TypeColors / MaterialFactory"]

    Bus --> Panels
    Bus --> Manager
```

## 4. What Changed Functionally

| Concern | Before | Now |
|---|---|---|
| Audio synthesis | one large static file | facade + voice spec + drum synth + tonal synth + effects + utilities |
| Sequencing | one class owning transport, arrangement, scheduling, and visuals | orchestrator composed from transport, navigation, and visual tracking units |
| Session management | one class handling state, repository, migration, and resolution | `SessionStore` facade with focused session collaborators |
| Pattern behavior | type switches across multiple systems | centralized behavior registry with per-pattern behavior classes |
| Voice rendering | synthesis choice embedded in flow | `IVoiceRenderer` registry under `Audio/Voices` |
| Input | direct hardware assumptions | `IInputProvider` abstraction for runtime and tests |
| Configuration | hardcoded lists and formulas | asset-backed instrument, sound mapping, and visual grammar profiles |
| Event propagation | direct callback chains | typed `RhythmForgeEventBus` with legacy events preserved |
| Testing | mostly top-level tests | direct edit-mode coverage for extracted pure-logic classes |

## 5. Extensibility Paths

### Current extension routes

```mermaid
flowchart LR
    NewPattern["New pattern family"] --> Enum["Add enum value"]
    NewPattern --> Behavior["Add IPatternBehavior implementation"]
    Behavior --> PatternReg["Register in PatternBehaviorRegistry"]
    Behavior --> Derivation["DraftBuilder uses registry"]
    Behavior --> Scheduling["Sequencer uses registry"]
    Behavior --> Visuals["PatternPlaybackVisualState / PlaybackAnimator use registry"]
    Behavior --> Profiles["Optional tuning via SoundMappingProfileAsset and VisualGrammarProfileAsset"]

    NewRenderer["New synthesis backend"] --> VoiceImpl["Add IVoiceRenderer implementation"]
    VoiceImpl --> VoiceReg["Register in VoiceRendererRegistry"]

    NewDispatcher["New playback target"] --> Dispatcher["Implement IAudioDispatcher"]
    Dispatcher --> Seq["Sequencer consumes dispatcher abstraction"]

    NewInput["Editor simulation / automation"] --> InputImpl["Implement IInputProvider"]
    InputImpl --> Capture["StrokeCapture"]
    InputImpl --> Pointer["StylusUIPointer"]
    InputImpl --> Drag["PanelDragCoordinator / InstanceGrabber"]
```

### Practical impact

- A new pattern type is no longer spread across scheduler, visualizer, draft builder, sound mapper, and UI mode logic. The change is concentrated around the behavior class, registration point, and optional profile tuning.
- A new synthesis strategy can be introduced without rewriting the sequencer or sample player.
- Input simulation and automated interaction tests can target the abstraction rather than headset-only hardware paths.
- New UI or runtime listeners can subscribe to typed events without editing `RhythmForgeManager`.

## 6. Architecture Advantages

### Lower coupling

- The manager is no longer the only place where subsystem communication can happen.
- UI panels no longer need to bind themselves directly to every subsystem callback they care about.
- Pattern-type-specific logic is centralized behind behavior interfaces instead of duplicated conditionals.

### Better testability

- `ArrangementNavigator`, `TransportController`, `PlaybackVisualTracker`, `PatternRepository`, `StateMigrator`, and `SoundProfileResolver` can be exercised directly in edit-mode tests.
- Audio generation and behavior logic are isolated enough to validate without the full VR runtime.
- Event flow can be tested independently from scene setup.

### Better data ownership

- Instrument sets can be authored as assets instead of static lists.
- Sound mapping coefficients can be tuned without code edits.
- Visual grammar can be adjusted from a profile instead of hardcoded constants.

### Better change isolation

- The original god-objects were change hotspots.
- The new structure localizes edits to a smaller module boundary.
- Serialization format, scene hierarchy, bootstrap flow, Logitech SDK, and Meta XR integration remain preserved while the logic layer becomes easier to evolve.

## 7. Summary

The original architecture concentrated most functional complexity inside a few large classes. The current architecture distributes that complexity into:

- facades for stable entry points,
- focused collaborators for single responsibilities,
- registries for replaceable behavior,
- data profiles for tunable configuration,
- an event bus for cross-system communication,
- direct edit-mode tests around the extracted pure logic.

That combination is what changes RhythmForge from a hard-to-extend feature cluster into a system that can grow in pattern types, renderers, dispatch targets, tuning assets, and tooling support with much lower risk.
