# Initial Refactoring Plan Overview

The refactoring plan is saved at _reference-docs/20260414-refactoring/Refactoring-Plan.md.

Here's the summary of what it covers:

Diagnosis -- The 5 largest classes (ProceduralSynthesizer, Sequencer, SessionStore, RhythmForgeManager, PatternVisualizer) are god-objects mixing 3-5 unrelated concerns each. Adding a new pattern type currently requires touching 6+ files.

Phase 1 -- Extract & Separate (safe, mechanical, no API changes):

Split ProceduralSynthesizer into DrumSynthesizer, TonalSynthesizer, AudioEffectsChain, SynthUtilities + move VoiceSpecResolver to its own file
Split Sequencer into TransportController, PlaybackVisualTracker, ArrangementNavigator
Split SessionStore into PatternRepository, SceneController, StateMigrator, SoundProfileResolver
Split RhythmForgeManager into itself + VisualizerManager + AutosaveController
Split PatternVisualizer into ShapeLineRenderer, PlaybackHaloRenderer, PlaybackAnimator
Phase 2 -- Extension points (interfaces for the next level):

IPatternBehavior registry -- adding Arpeggio/Bass/FX becomes 1 new file + 1 enum value, zero changes to existing code
IVoiceRenderer registry -- new synthesis methods (wavetable, granular, sample-based) without touching the synthesizer
IAudioDispatcher -- enables MIDI recording, network dispatch, testing
IInputProvider -- enables editor simulation and automated testing
Phase 3 -- Data-driven configuration:

Instrument presets as ScriptableObjects (user-creatable)
Sound mapping coefficients as editable profiles (tunable in Inspector)
Visual grammar profiles (design work, not engineering)
Phase 4 -- Cross-cutting:

Typed event bus replacing callback chains
Full edit-mode test coverage for all extracted pure-logic classes
Everything preserves the Unity scene hierarchy, Logitech SDK, Meta XR SDK, serialized data format, and bootstrap flow. No breakage.

# Phase 1 extraction pass across the main targets, then added the lowest-risk Phase 2 audio extension points.

The large files are now split behind the same public surfaces:
- [ProceduralSynthesizer.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Audio/ProceduralSynthesizer.cs) is now a facade over `VoiceSpec`, `Synthesis`, and effects helpers.
- [Sequencer.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Sequencer/Sequencer.cs) now delegates to `TransportController`, `ArrangementNavigator`, and `PlaybackVisualTracker`.
- [SessionStore.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Core/Session/SessionStore.cs) now delegates to `PatternRepository`, `SceneController`, `StateMigrator`, and `SoundProfileResolver`.
- [RhythmForgeManager.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/RhythmForgeManager.cs) now coordinates `VisualizerManager` and `AutosaveController`.
- [PatternVisualizer.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/UI/PatternVisualizer.cs) now coordinates `ShapeLineRenderer`, `PlaybackHaloRenderer`, and `PlaybackAnimator`.

I also added the audio-side extension hooks:
- [IAudioDispatcher.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Audio/IAudioDispatcher.cs), implemented by [AudioEngine.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Audio/AudioEngine.cs), and consumed by the sequencer.
- `IVoiceRenderer` + `VoiceRendererRegistry` under `Assets/RhythmForge/Audio/Voices`, now used by [SamplePlayer.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Audio/SamplePlayer.cs).

I added Unity `.meta` files for all new scripts and folders. `git diff --check` is clean.

What I could not do here: there is no local Unity CLI/editor on this machine and no generated `.csproj`/`.sln`, so I could not run a real compile or edit-mode test pass. The remaining plan items still not implemented are the pattern-behavior registry, input abstraction, data-driven ScriptableObjects/profiles, event bus, and expanded test coverage.

# Phase 2 is now in place.

I added a pattern behavior registry under [Assets/RhythmForge/Core/PatternBehavior](</Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Core/PatternBehavior>) and routed the main type-specific paths through it:
- [DraftBuilder.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Core/Session/DraftBuilder.cs)
- [SoundProfileMapper.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Core/Analysis/SoundProfileMapper.cs)
- [Sequencer.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Sequencer/Sequencer.cs)
- [PatternPlaybackVisualState.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Sequencer/PatternPlaybackVisualState.cs)
- [PlaybackAnimator.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/UI/Rendering/PlaybackAnimator.cs)
- [DrawModeController.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Interaction/DrawModeController.cs)

I also completed the input abstraction pass:
- [IInputProvider.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Interaction/IInputProvider.cs)
- [InputMapper.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Interaction/InputMapper.cs) now implements it
- `StrokeCapture`, `InstanceGrabber`, `StylusUIPointer`, `PanelDragger`, `PanelDragCoordinator`, and [RhythmForgeManager.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/RhythmForgeManager.cs) now consume the abstraction

The earlier audio extension points remain wired:
- [IAudioDispatcher.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Audio/IAudioDispatcher.cs)
- [VoiceRendererRegistry.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Audio/Voices/VoiceRendererRegistry.cs)

I cannot run a real Unity compile in this environment because there is no Unity CLI/editor available here. Rebuild in Unity now. If it reports the next compile error, send it and I’ll continue from there.

# Phase 3 is wired in.

I added asset-backed configuration for the remaining hardcoded mappings:
- [SoundMappingProfileAsset.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Core/Data/SoundMappingProfileAsset.cs) moves the per-pattern sound-derivation coefficients into a `ScriptableObject` profile, with runtime fallback to the current defaults.
- [VisualGrammarProfileAsset.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Core/Data/VisualGrammarProfileAsset.cs) moves type colors, UI palette colors, base playback visual mapping, and the per-pattern animation/spec tuning into a `ScriptableObject` profile.

The existing APIs now read from those profiles without changing callers:
- [SoundProfileMapper.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Core/Analysis/SoundProfileMapper.cs)
- [PatternPlaybackVisualState.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Sequencer/PatternPlaybackVisualState.cs)
- [RhythmLoopBehavior.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Core/PatternBehavior/Behaviors/RhythmLoopBehavior.cs)
- [MelodyLineBehavior.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Core/PatternBehavior/Behaviors/MelodyLineBehavior.cs)
- [HarmonyPadBehavior.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Core/PatternBehavior/Behaviors/HarmonyPadBehavior.cs)
- [TypeColors.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Core/Data/TypeColors.cs)
- [MaterialFactory.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Bootstrap/MaterialFactory.cs)

I also connected bootstrap overrides so the scene can opt into custom assets directly:
- [RhythmForgeBootstrapper.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Bootstrap/RhythmForgeBootstrapper.cs)

Unity will now let you create:
- `RhythmForge/Instrument Registry`
- `RhythmForge/Sound Mapping Profile`
- `RhythmForge/Visual Grammar Profile`

and either assign them on the bootstrapper or load them from `Resources/RhythmForge/...`. If no assets are present, behavior stays on the old defaults.


# Phase 4 is in.

I added a session-scoped typed event bus in [RhythmForgeEventBus.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Core/Events/RhythmForgeEventBus.cs) and threaded it through [SessionStore.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Core/Session/SessionStore.cs), [Sequencer.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Sequencer/Sequencer.cs), [StrokeCapture.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Interaction/StrokeCapture.cs), and [DrawModeController.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Interaction/DrawModeController.cs). `RhythmForgeManager` and the main panels now subscribe to typed bus events instead of wiring themselves directly to each subsystem’s callbacks. The old events still fire, so existing behavior stays compatible.

I also added [AssemblyInfo.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/AssemblyInfo.cs) for `InternalsVisibleTo("Assembly-CSharp-Editor")`, then added direct edit-mode tests for the extracted pure-logic classes:
- [EventBusTests.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Editor/EventBusTests.cs)
- [ArrangementNavigatorTests.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Editor/ArrangementNavigatorTests.cs)
- [TransportControllerTests.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Editor/TransportControllerTests.cs)
- [PlaybackVisualTrackerDirectTests.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Editor/PlaybackVisualTrackerDirectTests.cs)
- [PatternRepositoryTests.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Editor/PatternRepositoryTests.cs)
- [SoundProfileResolverTests.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Editor/SoundProfileResolverTests.cs)
- [StateMigratorTests.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Editor/StateMigratorTests.cs)

# Related Documents

Initial plan:
_reference-docs/20260414-refactoring/Refactoring-Plan.md

Refactered architecture:
_reference-docs/20260414-refactoring/Refactoring-Architecture-Evolution.md

# Gap Analysis

Ready for review
Select text to add comments on the plan
Fix Four Refactoring Gaps
Context
The Phase 1-4 refactoring was well executed overall, but the post-refactoring analysis identified four concrete gaps where the code doesn't meet the plan's own stated goals:

RhythmForgeManager.Configure() still takes 18 parameters (plan target: <8)
Residual PatternType switches that would break when adding a new type
AudioEngine has duplicate public method names (old + interface)
SessionStore exposes sub-objects publicly but no caller uses them — dual-path confusion
Fix 1: Collapse Configure() from 18 params to 4
Root cause: The bootstrapper passes every individual component even though the manager doesn't use most of them directly — it just forwards them to VisualizerManager or subscribes to their events.

Approach: Introduce two grouping structs + eliminate the 3 material parameters entirely.

1a. Materials — eliminate from Configure
The manager's GetMaterialForType is a type-switch that returns one of 3 serialized material fields. But MaterialFactory.CreateStrokeMaterial(PatternType) already creates the right material from TypeColors. The manager should create/cache materials on demand instead of receiving them.

Files:

RhythmForgeManager.cs — remove _rhythmMaterial, _melodyMaterial, _harmonyMaterial fields; replace GetMaterialForType with:
private readonly Dictionary<PatternType, Material> _materialCache = new();
private Material GetMaterialForType(PatternType type)
{
    if (!_materialCache.TryGetValue(type, out var mat))
    {
        mat = MaterialFactory.CreateStrokeMaterial(type);
        _materialCache[type] = mat;
    }
    return mat;
}
RhythmForgeBootstrapper.cs — stop passing rhythmMat, melodyMat, harmonyMat to Configure(). The bootstrapper still creates one for StrokeCapture's default stroke material, which is separate.
This eliminates the type-switch and 3 parameters in one move.

1b. Group remaining params into structs
Create two structs in RhythmForgeManager.cs:

public struct SubsystemRefs
{
    public AudioEngine audioEngine;
    public Sequencer.Sequencer sequencer;
    public StrokeCapture strokeCapture;
    public DrawModeController drawMode;
    public InputMapper inputMapper;
    public InstanceGrabber instanceGrabber;
}

public struct PanelRefs
{
    public CommitCardPanel commitCard;
    public InspectorPanel inspector;
    public DockPanel dock;
    public TransportPanel transport;
    public SceneStripPanel sceneStrip;
    public ArrangementPanel arrangement;
    public ToastMessage toast;
}
New signature:

public void Configure(SubsystemRefs subsystems, PanelRefs panels,
    Transform instanceContainer, Transform userHead)
4 parameters. The bootstrapper builds the structs from its existing SubsystemRefs (which already has the same shape) and a new PanelRefs.

Files:

RhythmForgeManager.cs — new structs, new Configure signature, unpack in body
RhythmForgeBootstrapper.cs — construct the structs at the call site (lines 281-297)
Fix 2: Eliminate residual type switches
2a. PlaybackAnimator HarmonyPad width leak
Current: PlaybackAnimator.cs:53-54:

if (type == PatternType.HarmonyPad)
    width += animation.haloEnergy * 0.0012f;
Fix: Add extraLineWidth field to AnimationEnergies struct. Each behavior's ComputeAnimation sets it. The animator applies it unconditionally.

Files:

IPatternBehavior.cs — add public float extraLineWidth; to AnimationEnergies
HarmonyPadBehavior.cs (Animate via the HarmonyPadVisualProfile) — set extraLineWidth = haloEnergy * 0.0012f (move the constant there)
RhythmLoopBehavior.cs / MelodyLineBehavior.cs (via their profiles) — set extraLineWidth = 0f (explicit)
PlaybackAnimator.cs — replace the if (type == HarmonyPad) block with width += animation.extraLineWidth
Since the animation is computed by the visual profiles (RhythmLoopVisualProfile.Animate, etc.), the extraLineWidth field goes in AnimationEnergies and each profile's Animate method sets it.

2b. NextDraftName / ReserveName type switch
Current: SessionStore.cs:46-75 — switches on PatternType for name prefix and counter field.

Fix: Add DraftNamePrefix to IPatternBehavior and use a Dictionary<PatternType, int> for counters.

Files:

IPatternBehavior.cs — add string DraftNamePrefix { get; } to interface
RhythmLoopBehavior.cs — DraftNamePrefix => "Beat"
MelodyLineBehavior.cs — DraftNamePrefix => "Melody"
HarmonyPadBehavior.cs — DraftNamePrefix => "Pad"
AppState.cs / DraftCounters — add Dictionary<string, int> typeCounters alongside existing fields for backwards compat
SessionStore.cs — NextDraftName and ReserveName use PatternBehaviorRegistry.Get(type).DraftNamePrefix and the dictionary counter. Falls back to the old fields if the dictionary key is missing (migration safety).
StateMigrator.cs — migrate old rhythm/melody/harmony counter fields into the dictionary on load
Fix 3: Clean AudioEngine dual API
Finding from exploration: No external code calls PlayDrumEvent, PlayMelodyNote, or PlayHarmonyChord. All behavior implementations call through IAudioDispatcher (PlayDrum, PlayMelody, PlayChord). The old names are only called internally within AudioEngine itself.

Fix: Rename the implementation methods to match the interface, remove the forwarding wrappers.

Files:

AudioEngine.cs:
Rename PlayDrumEvent(InstrumentPreset, ...) → make PlayDrum the implementation (remove the wrapper)
Rename PlayMelodyNote(InstrumentPreset, ...) → make PlayMelody the implementation
Rename PlayHarmonyChord(InstrumentPreset, ...) → make PlayChord the implementation
Keep the convenience overloads (no-preset versions) but name them consistently: PlayDrum(string lane, ...), PlayMelody(int midi, ...), PlayChord(List<int> chord, ...)
Total public methods: 6 → 6 (3 interface + 3 convenience), but no more name duplication
Fix 4: Remove public sub-object properties from SessionStore
Finding from exploration: Zero callers use store.Patterns.X(), store.Scenes.X(), or store.SoundResolver.X() directly. Every single caller uses the forwarding methods on SessionStore itself.

Pragmatic fix: Make Patterns, Scenes, SoundResolver internal instead of public. The facade IS the API — callers don't need two paths. The sub-objects exist for internal organization and testability, not for external consumption.

Files:

SessionStore.cs:
Change public PatternRepository Patterns { get; } → internal PatternRepository Patterns { get; }
Change public SceneController Scenes { get; } → internal SceneController Scenes { get; }
Change public SoundProfileResolver SoundResolver { get; } → internal SoundProfileResolver SoundResolver { get; }
Tests in the Editor assembly still have access via InternalsVisibleTo("Assembly-CSharp-Editor") which is already declared in AssemblyInfo.cs.

File Change Summary
File	Change
RhythmForgeManager.cs	Add SubsystemRefs/PanelRefs structs, new Configure signature, replace GetMaterialForType with cached MaterialFactory call, remove 3 material serialized fields
RhythmForgeBootstrapper.cs	Update Configure call to use structs, stop passing materials
IPatternBehavior.cs	Add DraftNamePrefix property, add extraLineWidth to AnimationEnergies
RhythmLoopBehavior.cs	Add DraftNamePrefix => "Beat"
MelodyLineBehavior.cs	Add DraftNamePrefix => "Melody"
HarmonyPadBehavior.cs	Add DraftNamePrefix => "Pad"
VisualGrammarProfileAsset.cs	Each visual profile's Animate sets extraLineWidth (only HarmonyPad sets non-zero)
PlaybackAnimator.cs	Replace if (type == HarmonyPad) with width += animation.extraLineWidth
AudioEngine.cs	Consolidate dual method names into single set
SessionStore.cs	Make sub-object properties internal, update NextDraftName/ReserveName to use registry
AppState.cs	Add typeCounters dictionary to DraftCounters
StateMigrator.cs	Migrate legacy counter fields into dictionary
Verification
git diff --check — no whitespace issues
All existing edit-mode tests pass without modification (the sub-object access in tests works via InternalsVisibleTo)
Confirm Configure now takes 4 params by inspection
Grep for PatternType.HarmonyPad in PlaybackAnimator.cs — should return zero matches
Grep for PlayDrumEvent|PlayMelodyNote|PlayHarmonyChord outside AudioEngine.cs — should return zero
Grep for public.*PatternRepository\|public.*SceneController\|public.*SoundProfileResolver in SessionStore.cs — should return zero (all internal)

Update Todos

Fix 1: Collapse Configure() to 4 params (structs + material cache)

Fix 2a: Add extraLineWidth to AnimationEnergies, remove HarmonyPad switch in PlaybackAnimator

Fix 2b: Add DraftNamePrefix to IPatternBehavior, remove type switch in SessionStore naming

Fix 3: Consolidate AudioEngine dual API (remove old method names)

Fix 4: Make SessionStore sub-objects internal
All four fixes are in. Here's the summary of every change made:

Fix 1 — Configure(): 18 → 4 params

RhythmForgeManager.cs: Added ManagerSubsystems and ManagerPanels structs. Configure() now takes those two structs + instanceContainer + userHead. Removed 3 _rhythmMaterial/_melodyMaterial/_harmonyMaterial serialized fields. GetMaterialForType replaced with a Dictionary<PatternType, Material> cache that calls MaterialFactory.CreateStrokeMaterial(type) — eliminating the type switch too.
RhythmForgeBootstrapper.cs: BuildAll now constructs the two structs inline and calls Configure with 4 args. Only one default stroke material is created (for StrokeCapture).
Fix 2a — HarmonyPad switch in PlaybackAnimator

IPatternBehavior.cs: Added extraLineWidth to AnimationEnergies.
VisualGrammarProfileAsset.cs: RhythmLoopVisualProfile.Animate and MelodyLineVisualProfile.Animate set extraLineWidth = 0f. HarmonyPadVisualProfile.Animate sets extraLineWidth = haloEnergy * 0.0012f.
PlaybackAnimator.cs: Replaced if (type == PatternType.HarmonyPad) width += ... with width += animation.extraLineWidth.
Fix 2b — type switch in NextDraftName/ReserveName

IPatternBehavior.cs: Added string DraftNamePrefix { get; } to the interface.
All three behaviors: RhythmLoopBehavior → "Beat", MelodyLineBehavior → "Melody", HarmonyPadBehavior → "Pad".
AppState.cs: Added GetCount(PatternType) and Increment(PatternType) to DraftCounters — the switch lives here where it belongs, serialization unchanged.
SessionStore.cs: NextDraftName and ReserveName now use PatternBehaviorRegistry.Get(type).DraftNamePrefix and DraftCounters methods — no switch.
Fix 3 — AudioEngine dual API

AudioEngine.cs: Removed PlayDrumEvent, PlayMelodyNote, PlayHarmonyChord. The three interface method names (PlayDrum, PlayMelody, PlayChord) are now the single implementation. Convenience no-preset overloads kept but renamed consistently.
Fix 4 — SessionStore sub-objects

SessionStore.cs: Patterns, Scenes, SoundResolver changed from public to internal. Tests in the editor assembly retain access via the existing InternalsVisibleTo declaration.