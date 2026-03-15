# RhythmForge VR Sound Engine V1

This folder contains the first-pass procedural sound engine for RhythmForge VR. It is built to be usable from keyboard input now and callable from drawing or VR interactions later.

## What It Does

- Generates 9 procedural sounds in code with no imported audio clips required
- Lets you trigger sounds directly or toggle beat-synced loops
- Supports runtime control over BPM, global pitch, and reverb
- Boots automatically in Play Mode through `RhythmAudioBootstrap`

## Files

- `RhythmSoundEngine.cs`: the reusable audio core and public API
- `KeyboardSoundEngineDriver.cs`: temporary desktop input adapter
- `RhythmAudioBootstrap.cs`: creates the runtime audio rig automatically
- `RhythmVoiceDefinition.cs`: serializable voice preset structure
- `RhythmEngineState.cs`: snapshot of current engine state
- `WaveformType.cs`: waveform enum used by the voice presets

## Default Controls

- `1-9`: trigger sound slots 1 through 9
- `Shift + 1-9`: toggle the loop pattern for that sound slot
- `-` / `=`: decrease / increase BPM
- `[` / `]`: decrease / increase global pitch in semitones
- `;` / `'`: decrease / increase reverb amount
- `Backspace`: stop all loops
- `R`: reset BPM, pitch, reverb, and loop toggles to defaults

## Public API

`RhythmSoundEngine` is the part that later drawing code should call.

```csharp
engine.TriggerSound(soundIndex);
engine.ToggleLoop(soundIndex);
engine.SetBpm(128f);
engine.AdjustBpm(5f);
engine.SetPitchSemitones(3f);
engine.AdjustPitchSemitones(-1f);
engine.SetReverb(0.35f);
engine.AdjustReverb(0.05f);
engine.StopAllLoops();
RhythmEngineState state = engine.GetEngineState();
```

`soundIndex` is zero-based in code:

- `0`: Kick
- `1`: Snare
- `2`: HiHat
- `3`: Bass Pulse
- `4`: Lead Stab
- `5`: Pad Hit
- `6`: Pluck
- `7`: Arp Tone
- `8`: FX Sweep

## How To Hook It To Drawing Later

When your loop-drawing system is ready, do not call keyboard code. Get a reference to `RhythmSoundEngine` and call the public methods directly.

Examples:

- A tap gesture can call `TriggerSound(4)`
- Closing a loop shape can call `ToggleLoop(3)`
- Resizing a ring can call `SetBpm(...)`
- Stylus pressure or distance can call `SetReverb(...)` or `SetPitchSemitones(...)`

## Tuning Notes

- Default sound presets are created automatically if no custom voice definitions are assigned.
- You can edit the serialized voice settings on the `RhythmSoundEngine` component later in the Inspector.
- The reverb is a lightweight built-in DSP effect intended for prototyping, not final mixing.
