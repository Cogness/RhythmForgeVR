# RhythmForge VR Architecture Refactoring Plan

**Date:** 2026-04-14
**Branch:** `feature/from-2d-engine`
**Scope:** Logic-layer and class-structure refactoring. No Unity scene/prefab breakage, no Logitech SDK changes, no Meta XR API changes.

---

## Executive Summary

The current RhythmForge codebase works and ships, but several core classes have grown into god-objects that mix unrelated concerns. This makes it hard to:

- Add new pattern types (Arpeggio, Bass, FX)
- Swap or layer synthesis methods
- Test sound logic without Unity
- Extend the sequencer with per-pattern-type scheduling
- Add new visual modes or interaction paradigms

This plan decomposes the five largest classes into focused, single-responsibility modules, introduces clean extension points through interfaces, and moves hardcoded configuration into data. Every phase is incremental, testable in isolation, and backwards-compatible with the existing Unity integration.

---

## Current Architecture Diagnosis

### Class Size & Responsibility Map

| Class | Lines | Responsibilities (mixed) |
|---|---|---|
| `ProceduralSynthesizer.cs` | ~600+ | Voice spec resolution, drum rendering (4 lane types), tonal rendering, filter/drive/stereo/ambience effects, waveform utilities, envelope math |
| `Sequencer.cs` | ~580 | Transport FSM, lookahead scheduling, per-type event dispatch, arrangement navigation, playback visual state tracking (pulse/sustain/phase), scheduled step history |
| `SessionStore.cs` | ~515 | State container, CRUD for patterns/instances/scenes, draft naming, sound profile resolution, state migration/normalization, arrangement mutation, scene membership cleanup |
| `RhythmForgeManager.cs` | ~390 | Event wiring, visualizer lifecycle, input polling (thumbstick/Y-button), autosave timer, scene switching, playback visual dispatch |
| `RhythmForgeBootstrapper.cs` | ~400+ | VR rig discovery, subsystem instantiation, UI panel creation, panel positioning, drag coordinator setup, manager configuration (18-param Configure call) |
| `PatternVisualizer.cs` | ~440 | Shape line rendering, playback halo, playback marker, interaction collider, parameter label, selection state, mute state, 3 pattern-type visual grammars |

### Core Structural Issues

1. **God classes.** `ProceduralSynthesizer` renders 4 drum types + tonal voices + effects chain + utilities in one static class. `Sequencer` mixes transport state, scheduling, visual tracking, and arrangement navigation.

2. **Switch-on-type everywhere.** `DraftBuilder.BuildFromStroke`, `Sequencer.ScheduleCurrentStep`, `AudioEngine.Play*`, `PatternVisualizer.UpdateAppearance`, and `SoundProfileMapper.Derive` all switch on `PatternType`. Adding a new type (e.g., `Arpeggio`) requires touching 6+ files.

3. **Static classes for core logic.** `ProceduralSynthesizer`, `SoundProfileMapper`, `ShapeProfileCalculator`, `PresetBiasResolver`, `DraftBuilder` are all static. This prevents injection, mocking, and runtime strategy swapping.

4. **Hardcoded instrument data.** `InstrumentGroups` and `InstrumentPresets` are coded as static initializer lists. No way for users to add presets or for the system to load them from files.

5. **Opaque coefficient formulas.** `SoundProfileMapper` has ~200 magic numbers embedded in arithmetic expressions. The musical intent behind `0.16f + sp.angularity * 0.4f + instability * 0.14f + compactness * 0.24f` is not discoverable.

6. **Tight callback coupling.** `RhythmForgeManager` manually wires `+=` event handlers between 8+ subsystems. Adding a new subsystem means modifying the manager.

7. **Visual concerns in the sequencer.** `Sequencer` tracks `_lastTriggerAt`, `_playbackActivity`, `_scheduledTransportSteps`, and computes pulse/sustain/phase. These are visual concerns, not audio scheduling concerns.

---

## Refactoring Phases

### Phase 1: Extract & Separate (Safe Mechanical Refactoring)

**Goal:** Break god classes into focused files. No new abstractions, no API changes. Each extraction is a single commit that moves code without changing behavior.

**Risk:** Minimal. Every extraction is a code move + delegation.

#### 1.1 Audio Pipeline Decomposition

**Current:** `ProceduralSynthesizer.cs` contains `VoiceSpecResolver` (static class), `ResolvedVoiceSpec` (struct), and `ProceduralSynthesizer` (static class) — all in one 600+ line file.

**Target structure:**

```
Assets/RhythmForge/Audio/
  VoiceSpec/
    ResolvedVoiceSpec.cs        -- the struct (already exists, extract to own file)
    VoiceSpecResolver.cs        -- the resolver (already a class, move to own file)
  Synthesis/
    DrumSynthesizer.cs          -- RenderKick, RenderSnare, RenderHat, RenderPercussion
    TonalSynthesizer.cs         -- RenderTone (melody + harmony oscillator loop)
    AudioEffectsChain.cs        -- ApplyVoiceChain, ApplyAmbience, NormalizeStereo
    SynthUtilities.cs           -- SampleWave, AdvancePhase, MidiToFrequency, EnvelopeAtTime, ProcessFilter, SvfState, BuildClip
  ProceduralSynthesizer.cs      -- becomes a thin facade: RenderDrum/RenderTone delegate to the above
  AudioEngine.cs                -- unchanged
  SamplePlayer.cs               -- unchanged
```

**Extraction steps:**

1. Move `ResolvedVoiceSpec` and `VoiceWaveform`/`VoiceFilterMode` enums to `VoiceSpec/ResolvedVoiceSpec.cs`.
2. Move `VoiceSpecResolver` to `VoiceSpec/VoiceSpecResolver.cs`.
3. Extract `SynthUtilities` — all the `private static` helpers: `SampleWave`, `AdvancePhase`, `MidiToFrequency`, `EnvelopeAtTime`, `EnvelopeDecay`, `ProcessFilter`, `SvfState`, `BuildClip`, `SecondsToSamples`, `ComputeSeed`, `ExponentialLerp`, `MixStereo`, `GetAmbienceTail`.
4. Extract `DrumSynthesizer` — `RenderKick`, `RenderSnare`, `RenderHat`, `RenderPercussion`, plus the convenience generators `GenerateKick`, `GenerateSnare`, etc.
5. Extract `TonalSynthesizer` — the `RenderTone` method and `GenerateTone`/`GeneratePad`/`GeneratePerc`.
6. Extract `AudioEffectsChain` — `ApplyVoiceChain`, `ApplyDrive`, `ApplyFilter`, `ApplyAmbience`, `NormalizeStereo`.
7. `ProceduralSynthesizer` becomes:

```csharp
public static class ProceduralSynthesizer
{
    public static AudioClip RenderDrum(ResolvedVoiceSpec spec) => DrumSynthesizer.Render(spec);
    public static AudioClip RenderTone(ResolvedVoiceSpec spec) => TonalSynthesizer.Render(spec);
}
```

**Zero callers change.** `SamplePlayer.GetOrCreateClip` still calls `ProceduralSynthesizer.RenderDrum/RenderTone`.

#### 1.2 Sequencer Decomposition

**Current:** `Sequencer.cs` owns transport state, scheduling, visual tracking, and arrangement navigation.

**Target structure:**

```
Assets/RhythmForge/Sequencer/
  Transport.cs                       -- unchanged (already exists)
  TransportController.cs             -- Play, Stop, AdvanceTransport, SetPlaybackScene
  ArrangementNavigator.cs            -- HasArrangement, FindFirstPopulatedSlot, FindNextPopulatedSlot
  PlaybackVisualTracker.cs           -- RecordTrigger, GetPulse, GetSustainAmount, TryGetPlaybackVisualState, scheduled step tracking
  PatternPlaybackVisualState.cs      -- unchanged
  SequencerClock.cs                  -- unchanged
  Sequencer.cs                       -- orchestrator that composes the above three
```

**Extraction steps:**

1. Extract `ArrangementNavigator` — static or instance helper for slot navigation.
2. Extract `PlaybackVisualTracker` — owns `_lastTriggerAt`, `_playbackActivity`, `_scheduledTransportSteps`, and all pulse/sustain/phase computation.
3. Extract `TransportController` — owns the `Transport` instance, `Play`/`Stop`/`AdvanceTransport`, and scene-switching logic.
4. `Sequencer` keeps `Update()` and `ScheduleCurrentStep()` as the glue, delegates to the three extracted classes.

**Key benefit:** `PlaybackVisualTracker` becomes independently testable. Transport logic is isolated from visual state.

#### 1.3 SessionStore Decomposition

**Current:** Single class with ~30 public methods spanning state container, repository, resolver, and migrator roles.

**Target structure:**

```
Assets/RhythmForge/Core/Session/
  SessionStore.cs                    -- thin facade, owns AppState, delegates to:
  PatternRepository.cs               -- GetPattern, GetInstance, GetSceneInstances, CommitDraft, SpawnPattern, ClonePattern, RemoveInstance, DuplicateInstance, UpdateInstance
  SceneController.cs                 -- SetActiveScene, QueueScene, CopyScene, ClearArrangementScene, UpdateArrangement
  StateMigrator.cs                   -- NormalizeState, NormalizePatternShapeData, NormalizePatternOrientations, CleanupSceneMembership, RefreshPatternDescriptors
  SoundProfileResolver.cs            -- GetEffectiveSoundProfile, GetEffectivePresetId (currently ~10 lines, but this is the extension point for adding more resolution strategies)
  DraftBuilder.cs                    -- unchanged
  SessionPersistence.cs              -- unchanged
  DemoSession.cs                     -- unchanged
```

**Extraction steps:**

1. Extract `StateMigrator` — all normalization/backfill logic. Called once on load.
2. Extract `SoundProfileResolver` — blending geometry + preset bias.
3. Extract `PatternRepository` — all CRUD operations on patterns and instances.
4. Extract `SceneController` — scene/arrangement mutation.
5. `SessionStore` becomes:

```csharp
public class SessionStore
{
    public AppState State { get; private set; }
    public PatternRepository Patterns { get; }
    public SceneController Scenes { get; }
    public SoundProfileResolver SoundResolver { get; }
    public event Action OnStateChanged;
    // ... thin delegation
}
```

**Key benefit:** `StateMigrator` can be tested with synthetic AppState objects without SessionStore. Pattern CRUD is isolated from scene logic.

#### 1.4 RhythmForgeManager Decomposition

**Current:** Manager handles visual lifecycle, input polling, autosave, and event dispatch.

**Target structure:**

```
Assets/RhythmForge/
  RhythmForgeManager.cs              -- event wiring and dispatch only
  VisualizerManager.cs               -- create/update/destroy PatternVisualizer instances
  AutosaveController.cs              -- timer + SessionPersistence.Save calls
```

**Extraction steps:**

1. Extract `VisualizerManager` — owns `_visualizers` dictionary, `RebuildInstanceVisuals()`, `UpdatePlaybackVisuals()`, `ResolveVisibleSceneId()`.
2. Extract `AutosaveController` — owns save timer, can be configured for interval.
3. Move thumbstick scene-switching into a small `InputActionHandler` or keep inline (it's only ~10 lines).
4. `RhythmForgeManager` shrinks to ~120 lines of event wiring.

#### 1.5 PatternVisualizer Decomposition

**Current:** Single MonoBehaviour handles shape rendering, playback animation, halo, marker, collider, and parameter label — 440 lines with three pattern-type visual branches.

**Target structure:**

```
Assets/RhythmForge/UI/
  PatternVisualizer.cs               -- coordinator component
  Rendering/
    ShapeLineRenderer.cs             -- LineRenderer setup, RenderPoints, scale from ShapeProfile
    PlaybackHaloRenderer.cs          -- halo line renderer, UpdateHalo
    PlaybackAnimator.cs              -- UpdateAppearance, per-type energy/phase computation
  InteractionTarget.cs               -- SphereCollider management
  PatternParameterLabel.cs           -- parameter label display (rename from ShapeParameterLabel)
  PlaybackMarker.cs                  -- unchanged
```

**Key benefit:** Each visual sub-concern is independently tunable. Adding a new visual grammar for a new pattern type means implementing one animation branch, not modifying the whole visualizer.

---

### Phase 2: Introduce Extension Points (Interface-Based Design)

**Goal:** Enable adding new pattern types, synthesis methods, and visual modes without modifying existing classes.

#### 2.1 Pattern Type System

**Problem:** Adding a new pattern type (e.g., `Arpeggio`, `BassLine`, `SoundFX`) currently requires modifying 6+ files that switch on `PatternType`.

**Solution:** Introduce a `IPatternBehavior` interface that encapsulates all type-specific logic:

```csharp
// Assets/RhythmForge/Core/PatternBehavior/IPatternBehavior.cs
public interface IPatternBehavior
{
    PatternType Type { get; }

    // Derivation: stroke -> sequence
    DerivationResult Derive(List<Vector2> points, StrokeMetrics metrics,
        string keyName, string groupId, ShapeProfile sp, SoundProfile sound);

    // Sound mapping: shape -> sound profile
    SoundProfile DeriveSoundProfile(ShapeProfile sp);

    // Scheduling: pattern + step -> audio events
    void Schedule(PatternDefinition pattern, PatternInstance instance,
        int localStep, float stepDur, double scheduledTime,
        SoundProfile sound, InstrumentPreset preset, InstrumentGroup group,
        IAudioDispatcher audio);

    // Visual: sound profile -> playback visual spec adjustments
    PlaybackVisualSpec AdjustVisualSpec(PlaybackVisualSpec baseSpec, SoundProfile sound);

    // Visual: compute animation energies for this type
    AnimationEnergies ComputeAnimation(PatternPlaybackVisualState state,
        PlaybackVisualSpec spec, float renderedWidth, float renderedHeight);
}
```

**Registry:**

```csharp
// Assets/RhythmForge/Core/PatternBehavior/PatternBehaviorRegistry.cs
public static class PatternBehaviorRegistry
{
    private static readonly Dictionary<PatternType, IPatternBehavior> _behaviors = new();

    static PatternBehaviorRegistry()
    {
        Register(new RhythmLoopBehavior());
        Register(new MelodyLineBehavior());
        Register(new HarmonyPadBehavior());
    }

    public static void Register(IPatternBehavior behavior) => _behaviors[behavior.Type] = behavior;
    public static IPatternBehavior Get(PatternType type) => _behaviors[type];
}
```

**Implementation per existing type:**

```
Assets/RhythmForge/Core/PatternBehavior/
  IPatternBehavior.cs
  PatternBehaviorRegistry.cs
  AnimationEnergies.cs               -- struct with lineEnergy, haloEnergy, markerEnergy, etc.
  Behaviors/
    RhythmLoopBehavior.cs            -- wraps RhythmDeriver + rhythm scheduling + rhythm visual grammar
    MelodyLineBehavior.cs            -- wraps MelodyDeriver + melody scheduling + melody visual grammar
    HarmonyPadBehavior.cs            -- wraps HarmonyDeriver + harmony scheduling + harmony visual grammar
```

**Impact:** After this, adding `ArpeggioPattern` means:
1. Add `Arpeggio` to `PatternType` enum.
2. Create `ArpeggioBehavior : IPatternBehavior`.
3. Register it in `PatternBehaviorRegistry`.
4. **Zero changes to Sequencer, DraftBuilder, PatternVisualizer, or SoundProfileMapper.**

#### 2.2 Audio Voice System

**Problem:** Adding a new synthesis method (e.g., wavetable, granular, sample-based) requires modifying `ProceduralSynthesizer`.

**Solution:** Introduce `IVoiceRenderer`:

```csharp
// Assets/RhythmForge/Audio/Voices/IVoiceRenderer.cs
public interface IVoiceRenderer
{
    string VoiceId { get; }
    bool CanRender(ResolvedVoiceSpec spec);
    AudioClip Render(ResolvedVoiceSpec spec);
}
```

**Implementations:**

```
Assets/RhythmForge/Audio/Voices/
  IVoiceRenderer.cs
  VoiceRendererRegistry.cs          -- ordered list of renderers, first match wins
  ProceduralDrumRenderer.cs         -- wraps DrumSynthesizer
  ProceduralTonalRenderer.cs        -- wraps TonalSynthesizer
  // Future:
  WavetableRenderer.cs
  SampleBasedRenderer.cs
  GranularRenderer.cs
```

**`SamplePlayer.GetOrCreateClip` becomes:**

```csharp
private AudioClip GetOrCreateClip(ResolvedVoiceSpec spec)
{
    // ... cache lookup ...
    AudioClip clip = VoiceRendererRegistry.Render(spec);
    // ... cache store ...
}
```

#### 2.3 Audio Dispatch Interface

**Problem:** `Sequencer` directly calls `AudioEngine` methods. Adding a new event type requires modifying both.

**Solution:**

```csharp
// Assets/RhythmForge/Audio/IAudioDispatcher.cs
public interface IAudioDispatcher
{
    void PlayDrum(InstrumentPreset preset, string lane, float velocity,
        float pan, float brightness, float depth, float fxSend, SoundProfile sound);

    void PlayNote(InstrumentPreset preset, int midi, float velocity, float duration,
        float pan, float brightness, float depth, float fxSend,
        SoundProfile sound, PatternType type, float glide = 0f);

    void PlayChord(InstrumentPreset preset, List<int> chord, float velocity, float duration,
        float pan, float brightness, float depth, float fxSend, SoundProfile sound);
}
```

`AudioEngine` implements `IAudioDispatcher`. Sequencer depends on the interface, not the concrete class. This enables:
- Recording dispatchers (capture events to MIDI)
- Network dispatchers (collaborative sessions)
- Null dispatchers (testing)

#### 2.4 Input Abstraction

**Problem:** `InputMapper` directly calls `OVRInput` and `StylusHandler`. Editor testing requires on-device hardware.

**Solution:**

```csharp
// Assets/RhythmForge/Interaction/IInputProvider.cs
public interface IInputProvider
{
    float DrawPressure { get; }
    bool IsDrawing { get; }
    bool FrontButtonDown { get; }
    bool BackButtonDown { get; }
    bool BackDoubleTap { get; }
    Pose StylusPose { get; }
    Vector2 LeftThumbstick { get; }
    bool ButtonTwo { get; }
    bool FrontButtonConsumed { get; set; }
    bool BackButton { get; }
}
```

`InputMapper` implements `IInputProvider`. Editor gets `EditorInputProvider` (mouse/keyboard simulation). Test gets `MockInputProvider`.

**All interaction classes** (`StrokeCapture`, `InstanceGrabber`, `StylusUIPointer`) take `IInputProvider` instead of `InputMapper`.

---

### Phase 3: Data-Driven Configuration

**Goal:** Move hardcoded values into loadable, editable data. Enable preset creation and sound tuning without code changes.

#### 3.1 Instrument Registry as ScriptableObjects

**Current:** `InstrumentGroups` and `InstrumentPresets` are static C# lists.

**Target:**

```
Assets/RhythmForge/Data/
  InstrumentGroups/
    LoFi.asset                       -- ScriptableObject
    Trap.asset
    Dream.asset
  InstrumentPresets/
    LoFi-Drums.asset
    LoFi-Piano.asset
    ...
  InstrumentRegistry.asset           -- references all groups + presets
```

```csharp
[CreateAssetMenu(menuName = "RhythmForge/Instrument Group")]
public class InstrumentGroupAsset : ScriptableObject
{
    public string groupId;
    public string displayName;
    public GroupDefaultPresets defaultPresets;
    public GroupBusFx busFx;
    public Color[] swatches;
}
```

**Bootstrap loads the registry at startup. Runtime API stays the same** — `InstrumentGroups.Get(groupId)` now reads from loaded assets instead of a hardcoded list.

**Key benefit:** Artists/designers can create new instrument groups without touching C#. User-created presets become possible through persistence.

#### 3.2 Sound Mapping Profiles

**Current:** `SoundProfileMapper` has ~200 magic coefficients inline.

**Target:** Extract coefficients into a `SoundMappingProfile` data structure:

```csharp
[Serializable]
public class SoundMappingCoefficients
{
    [Header("Brightness")]
    public float brightnessBase = 0.16f;
    public float brightnessAngularity = 0.4f;
    public float brightnessInstability = 0.14f;
    public float brightnessCompactness = 0.24f;

    [Header("Body")]
    public float bodyBase = 0.12f;
    public float bodyCircularity = 0.28f;
    public float bodySymmetry = 0.08f;
    public float bodySizeFactor = 0.62f;
    // ... all 15 parameters x ~4 coefficients each
}

[CreateAssetMenu(menuName = "RhythmForge/Sound Mapping Profile")]
public class SoundMappingProfile : ScriptableObject
{
    public SoundMappingCoefficients rhythm;
    public SoundMappingCoefficients melody;
    public SoundMappingCoefficients harmony;
}
```

**`SoundProfileMapper.Derive` reads from the active profile** instead of hardcoded formulas. Multiple profiles can exist for different musical aesthetics (e.g., "Aggressive", "Ambient", "Acoustic").

#### 3.3 Visual Grammar Profiles

**Current:** `PatternVisualizer.UpdateAppearance` has inline constants for per-type visual behavior.

**Target:**

```csharp
[CreateAssetMenu(menuName = "RhythmForge/Visual Grammar")]
public class VisualGrammarProfile : ScriptableObject
{
    [Header("Rhythm")]
    public float rhythmPulseWeight = 0.72f;
    public float rhythmSustainWeight = 0.22f;
    public float rhythmMarkerBaseScale = 0.72f;
    // ...

    [Header("Melody")]
    public float melodyPulseWeight = 0.36f;
    public float melodySustainWeight = 0.46f;
    // ...

    [Header("Harmony")]
    // ...
}
```

**Key benefit:** Visual tuning becomes a design task, not an engineering task. Different visual themes become possible.

---

### Phase 4: Cross-Cutting Improvements

#### 4.1 Event Bus

**Problem:** `RhythmForgeManager` manually wires `+=` event handlers. Adding a new subsystem means modifying the manager.

**Solution:** Lightweight typed event bus:

```csharp
// Assets/RhythmForge/Core/Events/EventBus.cs
public static class EventBus
{
    public static void Publish<T>(T evt) where T : struct { ... }
    public static void Subscribe<T>(Action<T> handler) where T : struct { ... }
    public static void Unsubscribe<T>(Action<T> handler) where T : struct { ... }
}

// Event types
public struct StateChangedEvent { }
public struct DraftCommittedEvent { public DraftResult draft; }
public struct TransportChangedEvent { public Transport transport; }
public struct SceneChangedEvent { public string sceneId; }
public struct PatternSelectedEvent { public string instanceId; }
```

**Migration:** Initially, `SessionStore.OnStateChanged` still fires, and the manager publishes to the bus in its handler. Over time, subsystems subscribe directly.

#### 4.2 Testable Core

**Goal:** All analysis, derivation, sound mapping, and scheduling logic should be testable in edit-mode tests without MonoBehaviour dependencies.

**Current testable classes:** `ShapeProfileCalculator`, `SoundProfileMapper`, `RhythmDeriver`, `MelodyDeriver`, `HarmonyDeriver`, `PresetBiasResolver` — all static, already testable.

**Newly testable after refactoring:**
- `TransportController` — pure state machine, no MonoBehaviour needed
- `PlaybackVisualTracker` — injectable time source, no MonoBehaviour needed
- `ArrangementNavigator` — pure logic on slot data
- `StateMigrator` — pure logic on AppState
- `PatternRepository` — pure CRUD on lists
- `DrumSynthesizer` / `TonalSynthesizer` — pure sample generation
- `AudioEffectsChain` — pure sample processing
- All `IPatternBehavior` implementations — pure logic

**Test organization:**

```
Assets/RhythmForge/Editor/
  Audio/
    DrumSynthesizerTests.cs
    TonalSynthesizerTests.cs
    AudioEffectsChainTests.cs
    VoiceSpecResolverTests.cs
  Sequencer/
    TransportControllerTests.cs
    PlaybackVisualTrackerTests.cs
    ArrangementNavigatorTests.cs
  Session/
    PatternRepositoryTests.cs
    StateMigratorTests.cs
    SoundProfileResolverTests.cs
  PatternBehavior/
    RhythmLoopBehaviorTests.cs
    MelodyLineBehaviorTests.cs
    HarmonyPadBehaviorTests.cs
```

---

## Dependency Flow (After Refactoring)

```
┌─────────────────────────────────────────────────────────────────┐
│                    RhythmForgeBootstrapper                       │
│    (composition root — wires everything, then steps back)       │
└──────────┬──────────────────────────────────────────────────────┘
           │ creates & configures
           ▼
┌──────────────────────┐    ┌──────────────────────┐
│  RhythmForgeManager  │◄───│      EventBus        │
│  (event dispatch)    │    │  (typed pub/sub)      │
└──────┬───────────────┘    └───────────────────────┘
       │ delegates to
       ▼
┌──────────────┐  ┌───────────────────┐  ┌──────────────────┐
│ Visualizer   │  │     Sequencer     │  │   SessionStore   │
│ Manager      │  │  (orchestrator)   │  │   (thin facade)  │
└──────┬───────┘  └───┬───┬───┬───────┘  └───┬───┬───┬──────┘
       │              │   │   │              │   │   │
       ▼              ▼   ▼   ▼              ▼   ▼   ▼
  PatternVis    Transport  Playback  Arrangement   Pattern  Scene   State
  components    Controller  Visual    Navigator    Repo     Ctrl    Migrator
                    │       Tracker
                    ▼
             ┌─────────────────┐
             │ IAudioDispatcher │
             │ (AudioEngine)    │
             └────────┬────────┘
                      ▼
             ┌─────────────────┐
             │  SamplePlayer   │
             └────────┬────────┘
                      ▼
             ┌─────────────────┐
             │ VoiceRenderer   │
             │ Registry        │
             │ (IVoiceRenderer)│
             └────────┬────────┘
                      ▼
             ┌────────┴────────┐
             │ DrumSynth       │  TonalSynth
             │ + Effects Chain │  + Effects Chain
             └─────────────────┘

Pattern Type extensibility:
             ┌─────────────────────────┐
             │ PatternBehaviorRegistry │
             └────────┬────────────────┘
                      ▼
             ┌────────┴─────────┬──────────────────┐
             │ RhythmLoopBehav  │ MelodyLineBehav   │ HarmonyPadBehav
             │ (derives+sched+  │ (derives+sched+   │ (derives+sched+
             │  visual grammar) │  visual grammar)  │  visual grammar)
             └──────────────────┴──────────────────┘
```

---

## Implementation Order & Dependencies

```
Phase 1.1  Audio decomposition         ← no dependencies, start here
Phase 1.2  Sequencer decomposition     ← no dependencies, parallel with 1.1
Phase 1.3  SessionStore decomposition  ← no dependencies, parallel with 1.1
Phase 1.4  Manager decomposition       ← depends on 1.2, 1.3 (uses their new types)
Phase 1.5  Visualizer decomposition    ← no dependencies, parallel with 1.1

Phase 2.1  PatternBehavior interfaces  ← depends on 1.1, 1.2, 1.5 (wraps extracted classes)
Phase 2.2  Voice renderer interfaces   ← depends on 1.1
Phase 2.3  Audio dispatch interface    ← depends on 1.2
Phase 2.4  Input abstraction           ← no dependencies, any time

Phase 3.1  ScriptableObject presets    ← depends on 2.1 (registry pattern established)
Phase 3.2  Sound mapping profiles      ← depends on 2.1
Phase 3.3  Visual grammar profiles     ← depends on 1.5, 2.1

Phase 4.1  Event bus                   ← depends on 1.4
Phase 4.2  Test coverage               ← continuous, after each phase
```

**Recommended parallel tracks:**

```
Track A (Audio):     1.1 → 2.2 → 3.2
Track B (Sequencer): 1.2 → 2.1 → 2.3
Track C (State):     1.3 → 4.1
Track D (Visual):    1.5 → 3.3
Track E (Input):     2.4 (any time)
Track F (Data):      3.1 (after 2.1)
```

---

## What NOT to Change

1. **Unity scene hierarchy.** The bootstrapper creates GameObjects at runtime. That pattern stays.
2. **Logitech SDK integration.** `StylusHandler`, `VrStylusHandler`, `LineDrawing` in `Assets/Logitech` are untouched.
3. **Meta XR SDK integration.** `OVRCameraRig`, `OVRInput`, controller/anchor discovery stay as-is.
4. **Serialized data format.** `AppState`, `PatternDefinition`, `PatternInstance`, `ShapeProfile`, `SoundProfile` keep their current field shapes. No migration needed.
5. **Bootstrap flow.** `RhythmForgeBootstrapper` still composes everything at runtime. It just delegates to smaller builders.
6. **Namespace structure.** Keep `RhythmForge.Audio`, `RhythmForge.Core`, etc. Add sub-namespaces where needed.

---

## File-by-File Change Summary

### Files to Extract From (modify in place, shrink)

| File | Current Lines | Target Lines | What Moves Out |
|---|---|---|---|
| `ProceduralSynthesizer.cs` | ~600+ | ~20 (facade) | Everything moves to `Synthesis/`, `VoiceSpec/` |
| `Sequencer.cs` | ~580 | ~150 | Visual tracking, transport FSM, arrangement nav |
| `SessionStore.cs` | ~515 | ~100 | CRUD, scene ops, migration, sound resolution |
| `RhythmForgeManager.cs` | ~390 | ~120 | Visualizer lifecycle, autosave |
| `PatternVisualizer.cs` | ~440 | ~120 | Rendering, halo, animation branches |

### New Files to Create

| File | Purpose | Lines (est) |
|---|---|---|
| `VoiceSpec/ResolvedVoiceSpec.cs` | Struct + enums | ~100 |
| `VoiceSpec/VoiceSpecResolver.cs` | Preset → spec resolution | ~180 |
| `Synthesis/DrumSynthesizer.cs` | 4 drum lane renderers | ~200 |
| `Synthesis/TonalSynthesizer.cs` | Oscillator + envelope rendering | ~120 |
| `Synthesis/AudioEffectsChain.cs` | Filter, drive, ambience, normalize | ~140 |
| `Synthesis/SynthUtilities.cs` | Waveform, phase, envelope, filter math | ~120 |
| `TransportController.cs` | Play/stop/advance state machine | ~100 |
| `PlaybackVisualTracker.cs` | Pulse, sustain, phase tracking | ~180 |
| `ArrangementNavigator.cs` | Slot navigation | ~40 |
| `PatternRepository.cs` | CRUD on patterns/instances | ~160 |
| `SceneController.cs` | Scene/arrangement mutation | ~100 |
| `StateMigrator.cs` | Version normalization | ~120 |
| `SoundProfileResolver.cs` | Effective profile blending | ~30 |
| `VisualizerManager.cs` | Visual lifecycle management | ~140 |
| `AutosaveController.cs` | Timer + save | ~30 |
| `IPatternBehavior.cs` | Extension interface | ~40 |
| `PatternBehaviorRegistry.cs` | Type → behavior lookup | ~30 |
| `RhythmLoopBehavior.cs` | Rhythm type implementation | ~80 |
| `MelodyLineBehavior.cs` | Melody type implementation | ~80 |
| `HarmonyPadBehavior.cs` | Harmony type implementation | ~80 |
| `IVoiceRenderer.cs` | Synthesis extension interface | ~20 |
| `VoiceRendererRegistry.cs` | Renderer lookup | ~40 |
| `IAudioDispatcher.cs` | Audio event interface | ~20 |
| `IInputProvider.cs` | Input abstraction | ~20 |
| `EventBus.cs` | Typed pub/sub | ~60 |
| `Rendering/ShapeLineRenderer.cs` | Line rendering component | ~80 |
| `Rendering/PlaybackHaloRenderer.cs` | Halo component | ~60 |
| `Rendering/PlaybackAnimator.cs` | Animation computation | ~100 |

### Files That Stay Unchanged

- `AudioEngine.cs` (implements `IAudioDispatcher`, code stays same)
- `SamplePlayer.cs`
- `InputMapper.cs` (implements `IInputProvider`)
- `StrokeCapture.cs`
- `InstanceGrabber.cs`
- `StylusUIPointer.cs`
- `PanelDragCoordinator.cs` / `PanelDragger.cs`
- `DrawModeController.cs`
- `SessionPersistence.cs`
- `DemoSession.cs`
- All data classes (`AppState`, `PatternDefinition`, `PatternInstance`, `ShapeProfile`, `SoundProfile`, `SequenceData`, etc.)
- All UI panels (`TransportPanel`, `SceneStripPanel`, `ArrangementPanel`, `InspectorPanel`, `CommitCardPanel`, `DockPanel`)
- `ShapeProfileCalculator.cs`
- `SoundProfileMapper.cs` (content stays, reads from profile in Phase 3)
- `PresetBiasResolver.cs`
- `RhythmDeriver.cs`, `MelodyDeriver.cs`, `HarmonyDeriver.cs`
- `Transport.cs`, `SequencerClock.cs`
- `PatternPlaybackVisualState.cs`
- `PlaybackMarker.cs`
- `ToastMessage.cs`
- All test files (extended, not modified)
- All Logitech scripts
- All Meta XR / OVR scripts

---

## Validation Criteria Per Phase

### After Phase 1 (Extract & Separate)

- [ ] All existing edit-mode tests pass without modification.
- [ ] On-device behavior is identical (draw, commit, play, arrange, autosave).
- [ ] No new public API surface — all new classes are `internal` or used only by their parent facade.
- [ ] Each extracted class has zero Unity MonoBehaviour dependency (except `VisualizerManager` and `AutosaveController`).

### After Phase 2 (Interfaces)

- [ ] Can create a `NullAudioDispatcher` and run the sequencer silently.
- [ ] Can create a `MockInputProvider` and drive StrokeCapture from test code.
- [ ] Can add a `PatternType.Test` with a custom `IPatternBehavior` and have it schedule, render, and animate without touching existing code.
- [ ] Existing tests still pass.

### After Phase 3 (Data-Driven)

- [ ] Instrument presets load from ScriptableObjects.
- [ ] Sound mapping coefficients come from a `SoundMappingProfile` asset.
- [ ] Changing coefficients in the Inspector immediately affects new patterns.
- [ ] A new preset added via ScriptableObject appears in the InspectorPanel preset list.

### After Phase 4 (Cross-Cutting)

- [ ] Event bus carries all session/transport events.
- [ ] New subsystem can subscribe to `DraftCommittedEvent` without touching manager.
- [ ] Edit-mode test coverage for all extracted pure-logic classes.

---

## Risk Assessment

| Risk | Likelihood | Mitigation |
|---|---|---|
| Behavioral regression during extraction | Medium | Each extraction is a single commit. Existing tests run after each. On-device smoke test after each phase. |
| Performance impact from indirection | Low | All new interfaces are on hot-but-not-frame-critical paths (scheduling runs once per step, not per frame). Synthesis is already cached. |
| Scope creep into Unity/VR layer | Low | Explicitly excluded from scope. Bootstrapper/scene/prefab changes are banned. |
| Serialization breakage | Low | No data model changes. New classes are runtime-only. |
| MonoBehaviour lifecycle surprises | Medium | Only 2 new MonoBehaviours (`VisualizerManager`, `AutosaveController`). Everything else is plain C#. |

---

## Success Metrics

After full completion:

1. **No class exceeds 200 lines** (current max: 600+).
2. **Adding a new PatternType requires exactly 1 new file** (the `IPatternBehavior` implementation) + 1 enum value.
3. **Adding a new synthesis method requires exactly 1 new file** (the `IVoiceRenderer` implementation).
4. **Sound mapping is tunable from the Unity Inspector** without code changes.
5. **Edit-mode test coverage doubles** from current ~5 test files to ~15+.
6. **`RhythmForgeManager.Configure()` parameter count drops from 18 to <8** through composed subsystems.
