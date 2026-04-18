**User Experience**

From the user’s point of view, the main change is that each pattern is now a sound object in the room, not just a lane in a stereo mix.

- When a pattern is playing, its sound now comes from that pattern’s actual world position. If you walk around it or turn your head, the sound should stay anchored to the shape instead of staying “inside the mix.” That routing is created per visualizer in [PatternVisualizer.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/UI/PatternVisualizer.cs:62) and [PatternVisualizer.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/UI/PatternVisualizer.cs:260), and each pool uses fully spatialized `AudioSource`s in [InstanceVoicePool.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Audio/InstanceVoicePool.cs:134).
- Moving a pattern while it plays should move the sound with it immediately, because the emitter lives under that pattern’s transform and the engine routes playback by `instanceId` instead of by a shared center pool [AudioEngine.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Audio/AudioEngine.cs:143), [AudioEngine.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Audio/AudioEngine.cs:158).
- Turning your head in-headset should now matter much more than before, because the listener is forced onto the center eye and duplicate listeners are removed at boot [RhythmForgeBootstrapper.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Bootstrap/RhythmForgeBootstrapper.cs:352).
- While drawing, you now hear a quiet continuous sidetone. More pressure makes it louder, and changing stylus height changes pitch. It starts on stroke start and stops when the stroke ends or is cleared [StrokeCapture.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Interaction/StrokeCapture.cs:105), [StrokeCapture.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Interaction/StrokeCapture.cs:396).

There is also a second layer beyond plain 3D position: each instance now has `brightness`, `reverbSend`, `delaySend`, and `gainTrim` instead of the old `pan/gain` model [PatternInstance.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Core/Data/PatternInstance.cs:45). In practice that means:
- Higher/lower placement still changes tone brightness.
- “Depth” is now meant to mean wetter/drier and slightly farther/closer in mix feel, not just fake volume + fake pan.
- The inspector reflects that shift by showing a listener-relative direction label instead of a raw pan number [InspectorPanel.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/UI/Panels/InspectorPanel.cs:172).

**What Feels Different From Before**

Before this change:
- Most playback effectively came from a shared non-spatial source pool.
- Left/right placement was synthesized with stereo pan.
- Near/far was faked mostly with scalar gain.
- Moving a visual pattern did not make the room behave like the sound source had physically moved.

After this change:
- Live instance playback is routed to per-instance spatial pools, while the old shared pool is only fallback for non-instance playback such as preview-style cases [AudioEngine.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Audio/AudioEngine.cs:146), [SamplePlayer.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Audio/SamplePlayer.cs:284).
- Drum, melody, and harmony scheduling all now pass the instance id and the spatial-native mix fields into playback [MusicalShapeBehavior.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Core/PatternBehavior/Behaviors/MusicalShapeBehavior.cs:117), [RhythmLoopBehavior.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Core/PatternBehavior/Behaviors/RhythmLoopBehavior.cs:58).
- The spatial position is truly listener-relative now, so “left” means left of your head, not left on a UI plane.
- Old saves are auto-migrated to the new mix model on load [StateMigrator.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Core/Session/StateMigrator.cs:11).

Two important behavioral nuances:
- Position changes are immediate for localization and distance attenuation, because the `AudioSource` physically moves with the object.
- Brightness/reverb/delay are still clip-render parameters, so they mainly affect new hits/notes/chords after the move, not the timbre already baked into a note that is currently sustaining. That behavior comes from the clip render/effects path in [AudioEffectsChain.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Audio/Synthesis/AudioEffectsChain.cs:90).

One implementation nuance worth knowing as you test:
- The grab push/pull control definitely moves the source farther/closer in space [InstanceGrabber.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Interaction/InstanceGrabber.cs:140).
- But the extra authored wet/dry layer is still stored in `PatternInstance.depth`, and `UpdateInstance(position: ...)` does not currently rewrite `depth` [PatternRepository.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Core/Session/PatternRepository.cs:189).
- So as implemented today, thumbstick push/pull gives you real spatial distance change immediately, but the `reverbSend/delaySend/gainTrim` part changes when `depth` changes through its own path, such as the inspector slider, not automatically from grab distance.

**How To Test**

Use headphones for all of this.

1. Basic spatial anchor test.
Create 2-3 patterns and place one clearly left, one right, one forward. Start playback. You should hear distinct anchored positions instead of a flat stereo spread.

2. Head-turn test on device.
Stand still and turn 90 degrees. A source that was in front-left should now feel more side/behind relative to your head. This is the most important “room sounds like a room” check.

3. Move-while-playing test.
Grab a playing pattern and move it left/right/up/down. The sound location should follow smoothly with no duplicate center copy and no audible pop.

4. Push/pull distance test.
While holding a pattern, use left thumbstick Y. The object should move farther/closer along the controller ray [InstanceGrabber.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Interaction/InstanceGrabber.cs:145). Expect immediate spatial distance change. For the extra wet/dry mix layer, also test the inspector depth slider separately.

5. Inspector depth test.
Select a pattern and move the depth slider. On subsequent notes/hits, it should sound drier/clearer when closer and wetter/less present when farther, because `reverbSend`, `delaySend`, and `gainTrim` are recalculated from depth [PatternInstance.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Core/Data/PatternInstance.cs:45).

6. Brightness test.
Move a pattern vertically and listen to the next triggered notes. Higher/lower placement should change tonal brightness. This is not a live filter on an already-playing note; it affects newly triggered audio.

7. Sidetone test.
Begin drawing. You should hear a quiet continuous tone immediately. Press harder and it gets louder. Raise/lower the stylus and the pitch changes. Release pressure and it stops [StrokeCapture.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Interaction/StrokeCapture.cs:415).

8. Save/load migration test.
Load an older session. It should open without errors and still sound coherent, but the perceived left/right image is now head-relative instead of the old baked pan model [StateMigrator.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Core/Session/StateMigrator.cs:115).

9. Stress test.
Create many active patterns. If you get dense enough, some new instances will degrade to a single spatial voice instead of full overlap rather than blowing up voice count; that is intentional protection [InstanceVoiceRegistry.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Audio/InstanceVoiceRegistry.cs:12), [InstanceVoiceRegistry.cs](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Audio/InstanceVoiceRegistry.cs:51).

Automated verification also passed: `86/86` EditMode tests in Unity on a temp clone of the project, since the main project was already open in another Unity instance.

If you want, I can turn this into a tighter QA checklist specifically for Quest device testing, or I can point out the one follow-up patch needed to make grab push/pull also drive the authored `depth` wet/dry layer.