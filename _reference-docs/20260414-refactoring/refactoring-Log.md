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