using System;
using UnityEngine;

public static class RhythmBuiltInPresets
{
    public static RhythmSoundPreset[] CreateBuiltInPresets()
    {
        return new[]
        {
            CreateElectronic(),
            CreateOrchestra(),
            CreateRock(),
            CreateClassical(),
            CreateDrumAndBass()
        };
    }

    private static RhythmSoundPreset CreateElectronic()
    {
        return Preset(
            "Electronic",
            124f,
            0.88f,
            0.18f,
            Slot("Kick", 1f, 0f, Voice(WaveformType.Sine, 49f, 0.95f, 0.001f, 0.08f, 0f, 0.08f, 0.05f, 0.02f, 0f, 0f, 0f, -0.76f, 0.22f, 0.1f, 2f, 0.25f, -0.35f, 0.3f, 0.01f, 0.08f), 0, 4, 8, 12),
            Slot("Snare", 1f, 0f, Voice(WaveformType.Noise, 190f, 0.74f, 0.001f, 0.05f, 0.02f, 0.05f, 0.08f, 0.94f, 0f, 0f, 0f, -0.2f, 0.05f, 0.18f, 2.4f, 0.65f, -0.2f, 0.45f, 0.018f, 0.22f), 4, 12),
            Slot("Hat", 0.95f, 0f, Voice(WaveformType.Noise, 380f, 0.45f, 0.001f, 0.02f, 0f, 0.02f, 0.02f, 1f, 0f, 0f, 0f, 0f, 0f, 0.3f, 4f, 1f, -0.1f, 0.22f, 0.008f, 0.1f), 2, 6, 10, 14),
            Slot("Bass", 1f, 0f, Voice(WaveformType.Saw, 82.41f, 0.46f, 0.004f, 0.09f, 0.45f, 0.22f, 0.12f, 0.03f, -6f, 0f, 0f, -0.08f, 0.26f, 0.35f, 2f, 0.35f, -0.4f, 0.05f, 0.01f, 0.26f), 0, 3, 8, 11),
            Slot("Lead", 0.95f, 0f, Voice(WaveformType.Square, 329.63f, 0.3f, 0.002f, 0.06f, 0.18f, 0.12f, 0.09f, 0.03f, 5f, 5.4f, 0.008f, 0f, 0.05f, 0.28f, 2f, 0.72f, -0.15f, 0.12f, 0.014f, 0.2f), 0, 5, 7, 12),
            Slot("Pad", 0.9f, 0f, Voice(WaveformType.Triangle, 220f, 0.24f, 0.03f, 0.16f, 0.72f, 0.42f, 0.28f, 0f, -3f, 1.9f, 0.012f, 0f, 0.18f, 0.26f, 1.5f, 0.42f, -0.25f, 0f, 0.02f, 0.05f), 0, 8),
            Slot("Pluck", 0.92f, 0f, Voice(WaveformType.Saw, 440f, 0.22f, 0.001f, 0.08f, 0.05f, 0.08f, 0.08f, 0.03f, 10f, 6.6f, 0.01f, -0.14f, 0.08f, 0.32f, 2f, 0.82f, -0.3f, 0.18f, 0.008f, 0.22f), 1, 5, 9, 13),
            Slot("Arp", 0.9f, 0f, Voice(WaveformType.Sine, 659.25f, 0.19f, 0.002f, 0.04f, 0.18f, 0.08f, 0.08f, 0f, 0f, 7.5f, 0.008f, 0f, 0.02f, 0.2f, 2f, 0.85f, 0.1f, 0.04f, 0.006f, 0.06f), 0, 2, 4, 6, 8, 10, 12, 14),
            Slot("FX", 0.9f, 0f, Voice(WaveformType.Triangle, 523.25f, 0.24f, 0.01f, 0.12f, 0.32f, 0.22f, 0.25f, 0.28f, 13f, 3.2f, 0.03f, 0.45f, 0.12f, 0.3f, 3f, 0.75f, 0.5f, 0.3f, 0.03f, 0.18f), 7, 15));
    }

    private static RhythmSoundPreset CreateOrchestra()
    {
        return Preset(
            "Orchestra",
            96f,
            0.78f,
            0.42f,
            Slot("Timpani", 1f, 0f, Voice(WaveformType.Sine, 73.42f, 0.88f, 0.003f, 0.16f, 0.12f, 0.18f, 0.18f, 0.06f, 0f, 0f, 0f, -0.28f, 0.24f, 0.18f, 1.5f, 0.2f, -0.25f, 0.22f, 0.03f, 0.08f), 0, 4, 8, 12),
            Slot("Snare Drum", 0.92f, 0f, Voice(WaveformType.Noise, 210f, 0.56f, 0.001f, 0.08f, 0.08f, 0.06f, 0.12f, 0.74f, 0f, 0f, 0f, -0.08f, 0.02f, 0.12f, 2f, 0.58f, -0.12f, 0.28f, 0.022f, 0.1f), 4, 12),
            Slot("Triangle", 0.85f, 0f, Voice(WaveformType.Sine, 1244.51f, 0.16f, 0.001f, 0.2f, 0.18f, 0.2f, 0.3f, 0.1f, 0f, 0f, 0f, 0f, 0f, 0.36f, 3f, 1f, 0f, 0.4f, 0.01f, 0.02f), 2, 6, 10, 14),
            Slot("Cello", 0.92f, 0f, Voice(WaveformType.Triangle, 130.81f, 0.34f, 0.02f, 0.14f, 0.62f, 0.36f, 0.22f, 0.01f, -2f, 4.2f, 0.01f, -0.04f, 0.18f, 0.28f, 2f, 0.28f, -0.2f, 0f, 0.03f, 0.06f), 0, 3, 8, 11),
            Slot("Horn", 0.92f, 0f, Voice(WaveformType.Triangle, 220f, 0.28f, 0.03f, 0.18f, 0.55f, 0.34f, 0.24f, 0.01f, 4f, 5f, 0.008f, 0f, 0.14f, 0.3f, 2f, 0.4f, -0.18f, 0.06f, 0.03f, 0.08f), 0, 5, 7, 12),
            Slot("Strings", 0.88f, 0f, Voice(WaveformType.Sine, 261.63f, 0.2f, 0.05f, 0.18f, 0.74f, 0.5f, 0.34f, 0f, 0f, 3.2f, 0.014f, 0f, 0.06f, 0.24f, 2f, 0.46f, -0.1f, 0f, 0.03f, 0.03f), 0, 8),
            Slot("Pizzicato", 0.85f, 0f, Voice(WaveformType.Triangle, 392f, 0.18f, 0.001f, 0.12f, 0.08f, 0.09f, 0.12f, 0.01f, 7f, 4.5f, 0.01f, -0.1f, 0.08f, 0.22f, 2f, 0.62f, -0.25f, 0.12f, 0.012f, 0.1f), 1, 5, 9, 13),
            Slot("Flute", 0.82f, 0f, Voice(WaveformType.Sine, 783.99f, 0.16f, 0.02f, 0.12f, 0.42f, 0.18f, 0.16f, 0f, 0f, 5.8f, 0.012f, 0f, 0f, 0.14f, 2f, 0.94f, 0.05f, 0f, 0.01f, 0.01f), 0, 2, 4, 6, 8, 10, 12, 14),
            Slot("Cymbal Swell", 0.82f, 0f, Voice(WaveformType.Noise, 600f, 0.18f, 0.01f, 0.28f, 0.38f, 0.24f, 0.4f, 0.86f, 0f, 0f, 0f, 0.12f, 0f, 0.2f, 3f, 0.95f, 0.35f, 0.36f, 0.06f, 0.04f), 7, 15));
    }

    private static RhythmSoundPreset CreateRock()
    {
        return Preset(
            "Rock",
            110f,
            0.9f,
            0.16f,
            Slot("Kick", 1f, 0f, Voice(WaveformType.Sine, 58f, 0.92f, 0.001f, 0.09f, 0.02f, 0.08f, 0.06f, 0.03f, 0f, 0f, 0f, -0.38f, 0.16f, 0.14f, 2f, 0.28f, -0.25f, 0.38f, 0.014f, 0.14f), 0, 4, 8, 12),
            Slot("Snare", 1f, 0f, Voice(WaveformType.Noise, 180f, 0.76f, 0.001f, 0.07f, 0.08f, 0.05f, 0.12f, 0.84f, 0f, 0f, 0f, -0.08f, 0.02f, 0.14f, 2f, 0.65f, -0.1f, 0.45f, 0.018f, 0.22f), 4, 12),
            Slot("Closed Hat", 0.95f, 0f, Voice(WaveformType.Noise, 400f, 0.42f, 0.001f, 0.025f, 0f, 0.02f, 0.03f, 1f, 0f, 0f, 0f, 0f, 0f, 0.22f, 4f, 0.95f, 0.1f, 0.24f, 0.008f, 0.1f), 2, 6, 10, 14),
            Slot("Bass Guitar", 1f, 0f, Voice(WaveformType.Saw, 98f, 0.44f, 0.005f, 0.1f, 0.45f, 0.22f, 0.18f, 0.01f, -5f, 4f, 0.008f, -0.02f, 0.18f, 0.24f, 2f, 0.36f, -0.25f, 0.1f, 0.012f, 0.24f), 0, 3, 8, 11),
            Slot("Power Chord", 0.92f, 0f, Voice(WaveformType.Square, 196f, 0.28f, 0.01f, 0.12f, 0.28f, 0.16f, 0.14f, 0.02f, 8f, 5f, 0.009f, 0f, 0.08f, 0.36f, 1.5f, 0.66f, -0.15f, 0.08f, 0.015f, 0.32f), 0, 5, 7, 12),
            Slot("Organ", 0.88f, 0f, Voice(WaveformType.Triangle, 261.63f, 0.22f, 0.03f, 0.16f, 0.68f, 0.38f, 0.26f, 0f, 0f, 2.2f, 0.01f, 0f, 0.18f, 0.22f, 2f, 0.46f, -0.1f, 0f, 0.02f, 0.08f), 0, 8),
            Slot("Guitar Pluck", 0.92f, 0f, Voice(WaveformType.Saw, 392f, 0.2f, 0.001f, 0.09f, 0.1f, 0.1f, 0.11f, 0.01f, 10f, 6.2f, 0.008f, -0.08f, 0.04f, 0.3f, 2f, 0.8f, -0.22f, 0.2f, 0.009f, 0.22f), 1, 5, 9, 13),
            Slot("Lead Guitar", 0.9f, 0f, Voice(WaveformType.Square, 659.25f, 0.19f, 0.01f, 0.09f, 0.32f, 0.12f, 0.12f, 0.01f, 0f, 6.4f, 0.012f, 0f, 0.02f, 0.24f, 2f, 0.9f, 0.05f, 0.04f, 0.01f, 0.18f), 0, 2, 4, 6, 8, 10, 12, 14),
            Slot("Crash", 0.88f, 0f, Voice(WaveformType.Noise, 580f, 0.2f, 0.003f, 0.16f, 0.2f, 0.14f, 0.36f, 0.94f, 0f, 0f, 0f, 0f, 0f, 0.18f, 3f, 0.98f, 0.28f, 0.24f, 0.04f, 0.08f), 7, 15));
    }

    private static RhythmSoundPreset CreateClassical()
    {
        return Preset(
            "Classical",
            88f,
            0.74f,
            0.36f,
            Slot("Bass Drum", 0.95f, 0f, Voice(WaveformType.Sine, 65.41f, 0.8f, 0.004f, 0.18f, 0.14f, 0.16f, 0.18f, 0.04f, 0f, 0f, 0f, -0.18f, 0.16f, 0.12f, 1.5f, 0.22f, -0.18f, 0.2f, 0.02f, 0.04f), 0, 4, 8, 12),
            Slot("Brush Snare", 0.92f, 0f, Voice(WaveformType.Noise, 175f, 0.48f, 0.002f, 0.09f, 0.12f, 0.05f, 0.12f, 0.68f, 0f, 0f, 0f, -0.04f, 0.02f, 0.08f, 2f, 0.52f, -0.08f, 0.14f, 0.02f, 0.04f), 4, 12),
            Slot("Bell", 0.82f, 0f, Voice(WaveformType.Sine, 987.77f, 0.14f, 0.001f, 0.28f, 0.32f, 0.28f, 0.42f, 0.04f, 0f, 0f, 0f, 0f, 0f, 0.48f, 3f, 1f, 0f, 0.18f, 0.01f, 0.02f), 2, 6, 10, 14),
            Slot("Contra Bass", 0.88f, 0f, Voice(WaveformType.Triangle, 98f, 0.26f, 0.02f, 0.14f, 0.58f, 0.28f, 0.24f, 0f, -4f, 3.6f, 0.008f, -0.02f, 0.18f, 0.18f, 2f, 0.26f, -0.16f, 0f, 0.02f, 0.04f), 0, 3, 8, 11),
            Slot("Piano Chord", 0.86f, 0f, Voice(WaveformType.Triangle, 261.63f, 0.2f, 0.002f, 0.18f, 0.18f, 0.16f, 0.18f, 0.02f, 5f, 2.2f, 0.005f, -0.03f, 0.08f, 0.28f, 3f, 0.72f, -0.22f, 0.14f, 0.012f, 0.08f), 0, 5, 7, 12),
            Slot("Strings", 0.84f, 0f, Voice(WaveformType.Sine, 293.66f, 0.18f, 0.06f, 0.18f, 0.78f, 0.5f, 0.34f, 0f, 0f, 2.4f, 0.012f, 0f, 0.08f, 0.18f, 2f, 0.38f, -0.08f, 0f, 0.03f, 0.02f), 0, 8),
            Slot("Harp", 0.84f, 0f, Voice(WaveformType.Sine, 523.25f, 0.15f, 0.001f, 0.12f, 0.08f, 0.09f, 0.15f, 0.01f, 7f, 4.5f, 0.008f, -0.1f, 0.04f, 0.32f, 3f, 0.84f, -0.2f, 0.1f, 0.01f, 0.05f), 1, 5, 9, 13),
            Slot("Oboe", 0.82f, 0f, Voice(WaveformType.Sine, 698.46f, 0.14f, 0.01f, 0.1f, 0.42f, 0.12f, 0.14f, 0f, 0f, 5.8f, 0.012f, 0f, 0.02f, 0.14f, 2f, 0.78f, 0.08f, 0f, 0.01f, 0.02f), 0, 2, 4, 6, 8, 10, 12, 14),
            Slot("Gong", 0.78f, 0f, Voice(WaveformType.Noise, 320f, 0.16f, 0.01f, 0.32f, 0.32f, 0.24f, 0.48f, 0.7f, 0f, 0f, 0f, 0.08f, 0f, 0.4f, 2.6f, 0.85f, 0.25f, 0.18f, 0.04f, 0.03f), 7, 15));
    }

    private static RhythmSoundPreset CreateDrumAndBass()
    {
        return Preset(
            "DrumAndBass",
            172f,
            0.92f,
            0.22f,
            Slot("Sub Kick", 1f, 0f, Voice(WaveformType.Sine, 43.65f, 1f, 0.001f, 0.07f, 0f, 0.08f, 0.05f, 0.02f, 0f, 0f, 0f, -0.82f, 0.34f, 0.1f, 2f, 0.22f, -0.45f, 0.34f, 0.01f, 0.1f), 0, 4, 8, 12),
            Slot("Snare Snap", 1f, 0f, Voice(WaveformType.Noise, 240f, 0.82f, 0.001f, 0.04f, 0.02f, 0.04f, 0.08f, 0.96f, 0f, 0f, 0f, -0.18f, 0.02f, 0.18f, 3f, 0.78f, -0.08f, 0.56f, 0.012f, 0.28f), 4, 12),
            Slot("Hat Tick", 0.96f, 0f, Voice(WaveformType.Noise, 520f, 0.48f, 0.001f, 0.015f, 0f, 0.015f, 0.018f, 1f, 0f, 0f, 0f, 0f, 0f, 0.22f, 4f, 1f, 0f, 0.18f, 0.006f, 0.08f), 2, 6, 10, 14),
            Slot("Reese Bass", 1f, 0f, Voice(WaveformType.Saw, 82.41f, 0.5f, 0.003f, 0.08f, 0.58f, 0.22f, 0.18f, 0.02f, -11f, 0.6f, 0.005f, -0.04f, 0.2f, 0.48f, 1.98f, 0.28f, -0.32f, 0.04f, 0.01f, 0.36f), 0, 3, 8, 11),
            Slot("Wobble Lead", 0.95f, 0f, Voice(WaveformType.Square, 220f, 0.3f, 0.002f, 0.05f, 0.26f, 0.12f, 0.1f, 0.03f, 8f, 7.2f, 0.015f, 0.08f, 0.06f, 0.34f, 2f, 0.72f, 0.18f, 0.12f, 0.014f, 0.22f), 0, 5, 7, 12),
            Slot("Atmos Pad", 0.9f, 0f, Voice(WaveformType.Triangle, 196f, 0.22f, 0.04f, 0.14f, 0.72f, 0.48f, 0.32f, 0.04f, -2f, 1.5f, 0.02f, 0f, 0.12f, 0.24f, 1.5f, 0.4f, -0.12f, 0f, 0.03f, 0.06f), 0, 8),
            Slot("Stab Pluck", 0.94f, 0f, Voice(WaveformType.Saw, 493.88f, 0.24f, 0.001f, 0.07f, 0.08f, 0.08f, 0.08f, 0.02f, 12f, 6.8f, 0.01f, -0.1f, 0.02f, 0.36f, 2.4f, 0.82f, -0.18f, 0.16f, 0.008f, 0.22f), 1, 5, 9, 13),
            Slot("Laser Arp", 0.9f, 0f, Voice(WaveformType.Sine, 783.99f, 0.18f, 0.001f, 0.03f, 0.16f, 0.06f, 0.06f, 0f, 0f, 9f, 0.012f, 0.12f, 0f, 0.28f, 3f, 0.94f, 0.25f, 0.04f, 0.006f, 0.08f), 0, 2, 4, 6, 8, 10, 12, 14),
            Slot("Noise Riser", 0.86f, 0f, Voice(WaveformType.Noise, 700f, 0.18f, 0.01f, 0.18f, 0.42f, 0.22f, 0.28f, 1f, 0f, 0f, 0f, 0.26f, 0f, 0.24f, 3.4f, 1f, 0.5f, 0.2f, 0.05f, 0.06f), 7, 15));
    }

    private static RhythmSoundPreset Preset(string name, float bpm, float volume, float reverb, params RhythmSoundSlot[] slots)
    {
        return new RhythmSoundPreset
        {
            presetName = name,
            defaultBpm = bpm,
            masterVolume = volume,
            defaultReverbAmount = reverb,
            defaultPitchSemitones = 0f,
            slots = slots
        };
    }

    private static RhythmSoundSlot Slot(string name, float volumeMultiplier, float pitchOffset, RhythmVoiceDefinition proceduralVoice, params int[] steps)
    {
        return new RhythmSoundSlot
        {
            name = name,
            volumeMultiplier = volumeMultiplier,
            pitchOffsetSemitones = pitchOffset,
            sampleClips = Array.Empty<AudioClip>(),
            proceduralVoice = proceduralVoice,
            defaultLoopSteps = Pattern(steps)
        };
    }

    private static RhythmVoiceDefinition Voice(
        WaveformType waveform,
        float baseFrequency,
        float gain,
        float attack,
        float decay,
        float sustain,
        float hold,
        float release,
        float noiseMix,
        float detuneCents,
        float vibratoRate,
        float vibratoDepth,
        float pitchDecayAmount,
        float subMix,
        float overtoneMix,
        float overtoneRatio,
        float brightness,
        float brightnessDecay,
        float transientMix,
        float transientDecay,
        float saturation)
    {
        return new RhythmVoiceDefinition
        {
            waveform = waveform,
            baseFrequency = baseFrequency,
            gain = gain,
            attackSeconds = attack,
            decaySeconds = decay,
            sustainLevel = sustain,
            holdSeconds = hold,
            releaseSeconds = release,
            noiseMix = noiseMix,
            detuneCents = detuneCents,
            vibratoRate = vibratoRate,
            vibratoDepth = vibratoDepth,
            pitchDecayAmount = pitchDecayAmount,
            subOscillatorMix = subMix,
            overtoneMix = overtoneMix,
            overtoneRatio = overtoneRatio,
            brightness = brightness,
            brightnessDecay = brightnessDecay,
            transientMix = transientMix,
            transientDecaySeconds = transientDecay,
            saturation = saturation,
            defaultLoopSteps = new bool[16]
        };
    }

    private static bool[] Pattern(params int[] activeSteps)
    {
        var steps = new bool[16];
        if (activeSteps == null)
        {
            return steps;
        }

        for (int i = 0; i < activeSteps.Length; i++)
        {
            int index = activeSteps[i];
            if (index >= 0 && index < steps.Length)
            {
                steps[index] = true;
            }
        }

        return steps;
    }
}
