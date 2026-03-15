# RhythmForge VR Sound Engine V3

The engine now supports real audio samples. Each preset is sample-first: if clips are found for a preset slot, the engine plays those clips. If not, it falls back to the procedural sound for that slot.

## Built-In Presets

- `Electronic`
- `Orchestra`
- `Rock`
- `Classical`
- `DrumAndBass`

## How Sample Loading Works

The engine looks for clips in Unity `Resources` folders using the preset name.

Expected folder layout:

```text
Assets/Resources/AudioPresets/Orchestra/
Assets/Resources/AudioPresets/Rock/
Assets/Resources/AudioPresets/Classical/
Assets/Resources/AudioPresets/DrumAndBass/
Assets/Resources/AudioPresets/Electronic/
```

The engine assigns clips to the 9 sound slots by the number at the start of the clip file name.

Examples:

```text
Assets/Resources/AudioPresets/Orchestra/1_Timpani.wav
Assets/Resources/AudioPresets/Orchestra/1_Timpani_Alt.wav
Assets/Resources/AudioPresets/Orchestra/2_Snare.wav
Assets/Resources/AudioPresets/Orchestra/4_CelloShort.wav
Assets/Resources/AudioPresets/Rock/5_PowerChord_A.wav
Assets/Resources/AudioPresets/Rock/5_PowerChord_B.wav
Assets/Resources/AudioPresets/DrumAndBass/4_ReeseBass.wav
```

Rules:

- File names must start with `1` to `9`
- Multiple clips with the same starting number become variations for that slot
- When a slot has multiple samples, the engine picks one variation at random on each trigger
- If no sample exists for a slot, that slot still works using its procedural fallback

## Keyboard Controls

- `1-9`: trigger sound slots 1 through 9
- `Shift + 1-9`: toggle the loop pattern for that sound slot
- `,`: previous preset
- `.`: next preset
- `-` / `=`: decrease / increase BPM
- `[` / `]`: decrease / increase global pitch in semitones
- `;` / `'`: decrease / increase reverb amount
- `Backspace`: stop all loops
- `R`: reset BPM, pitch, reverb, and loop toggles to the current preset defaults

## Public API

```csharp
engine.TriggerSound(soundIndex);
engine.ToggleLoop(soundIndex);
engine.SetPreset(2);
engine.SetPreset("Orchestra");
engine.NextPreset();
engine.PreviousPreset();
engine.SetBpm(128f);
engine.AdjustBpm(5f);
engine.SetPitchSemitones(3f);
engine.AdjustPitchSemitones(-1f);
engine.SetReverb(0.35f);
engine.AdjustReverb(0.05f);
engine.StopAllLoops();
RhythmEngineState state = engine.GetEngineState();
string[] presetNames = engine.GetPresetNames();
```

## What To Do Next

1. Import real drum/instrument one-shots into the `Resources/AudioPresets/<PresetName>/` folders.
2. Name each file with a leading slot number from `1` to `9`.
3. Enter Play Mode, switch presets with `,` and `.`, and test slots `1-9`.
4. Replace only the slots you care about first; missing slots will still use procedural fallback.

## Notes

- This project currently does not contain a real sample library, so adding proper source clips is still required for non-synthetic sound.
- The current runtime architecture is now ready for those clips and does not need another redesign just to support sample playback.
