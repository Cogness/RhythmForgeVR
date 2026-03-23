using System.Collections.Generic;
using UnityEngine;

public enum ShapeVoiceType
{
    Plucked,
    Percussive,
    Drone,
    Arpeggio
}

public readonly struct MusicalPulse
{
    public readonly float TimeSeconds;
    public readonly float DurationSeconds;
    public readonly float Frequency;
    public readonly float Amplitude;

    public MusicalPulse(float timeSeconds, float durationSeconds, float frequency, float amplitude)
    {
        TimeSeconds = timeSeconds;
        DurationSeconds = durationSeconds;
        Frequency = frequency;
        Amplitude = amplitude;
    }
}

public sealed class MusicalLoopDefinition
{
    public ShapeVoiceType VoiceType;
    public float LoopDurationSeconds;
    public float BaseFrequency;
    public float Volume;
    public Color VisualColor;
    public float ContinuousDroneMix;
    public readonly List<MusicalPulse> Pulses = new();

    public static MusicalLoopDefinition FromDescriptor(ShapeDescriptor descriptor)
    {
        MusicalLoopDefinition definition = new();
        definition.BaseFrequency = QuantizeHeightToFrequency(descriptor.Height01);
        definition.Volume = Mathf.Lerp(0.22f, 0.48f, descriptor.AveragePressure);
        definition.VisualColor = descriptor.Classification switch
        {
            ShapeClassification.CircleLoop => new Color(0.38f, 0.86f, 0.86f, 1f),
            ShapeClassification.ZigZag => new Color(0.95f, 0.72f, 0.35f, 1f),
            ShapeClassification.Arc => new Color(0.62f, 0.84f, 1f, 1f),
            _ => new Color(0.72f, 0.93f, 0.74f, 1f)
        };

        switch (descriptor.Classification)
        {
            case ShapeClassification.CircleLoop:
                definition.VoiceType = ShapeVoiceType.Plucked;
                definition.LoopDurationSeconds = Mathf.Lerp(1.4f, 3.4f, descriptor.Size01);
                BuildCirclePattern(definition, descriptor);
                break;
            case ShapeClassification.ZigZag:
                definition.VoiceType = ShapeVoiceType.Percussive;
                definition.LoopDurationSeconds = Mathf.Lerp(1.1f, 2.4f, descriptor.Size01);
                BuildZigZagPattern(definition, descriptor);
                break;
            case ShapeClassification.Arc:
                definition.VoiceType = ShapeVoiceType.Arpeggio;
                definition.LoopDurationSeconds = Mathf.Lerp(1.6f, 3.2f, descriptor.Size01);
                BuildArcPattern(definition, descriptor);
                break;
            default:
                definition.VoiceType = ShapeVoiceType.Drone;
                definition.LoopDurationSeconds = Mathf.Lerp(1.8f, 3.8f, descriptor.Size01);
                BuildLinePattern(definition, descriptor);
                break;
        }

        return definition;
    }

    private static void BuildCirclePattern(MusicalLoopDefinition definition, ShapeDescriptor descriptor)
    {
        int steps = descriptor.Size01 > 0.55f ? 8 : 6;
        float[] pentatonic = { 0f, 2f, 4f, 7f, 9f, 12f };
        for (int i = 0; i < steps; i++)
        {
            float time = (definition.LoopDurationSeconds / steps) * i;
            float semitone = pentatonic[i % pentatonic.Length];
            float frequency = definition.BaseFrequency * Mathf.Pow(2f, semitone / 12f);
            definition.Pulses.Add(new MusicalPulse(time, 0.18f, frequency, 0.72f));
        }
    }

    private static void BuildZigZagPattern(MusicalLoopDefinition definition, ShapeDescriptor descriptor)
    {
        int hits = Mathf.Clamp(4 + descriptor.DirectionChanges, 6, 12);
        for (int i = 0; i < hits; i++)
        {
            float time = (definition.LoopDurationSeconds / hits) * i;
            float frequency = definition.BaseFrequency * (i % 3 == 0 ? 0.5f : 1f + ((i % 4) * 0.05f));
            float amplitude = i % 2 == 0 ? 0.92f : 0.65f;
            definition.Pulses.Add(new MusicalPulse(time, 0.08f, frequency, amplitude));
        }
    }

    private static void BuildArcPattern(MusicalLoopDefinition definition, ShapeDescriptor descriptor)
    {
        int notes = Mathf.Clamp(4 + Mathf.RoundToInt(Mathf.Abs(descriptor.RotationDegrees) / 80f), 4, 7);
        for (int i = 0; i < notes; i++)
        {
            float time = (definition.LoopDurationSeconds / notes) * i;
            float semitone = i * 3f;
            float frequency = definition.BaseFrequency * Mathf.Pow(2f, semitone / 12f);
            definition.Pulses.Add(new MusicalPulse(time, 0.15f, frequency, 0.68f));
        }
    }

    private static void BuildLinePattern(MusicalLoopDefinition definition, ShapeDescriptor descriptor)
    {
        definition.ContinuousDroneMix = Mathf.Lerp(0.38f, 0.68f, 1f - descriptor.Jaggedness);
        definition.Pulses.Add(new MusicalPulse(0f, 0.22f, definition.BaseFrequency, 0.55f));
        definition.Pulses.Add(new MusicalPulse(definition.LoopDurationSeconds * 0.5f, 0.18f, definition.BaseFrequency * 1.5f, 0.34f));
    }

    private static float QuantizeHeightToFrequency(float height01)
    {
        int[] pentatonic = { 0, 2, 4, 7, 9 };
        int octave = Mathf.Clamp(Mathf.FloorToInt(height01 * 3.6f), 0, 3);
        float fractional = Mathf.Repeat(height01 * pentatonic.Length * 3f, pentatonic.Length);
        int scaleIndex = Mathf.Clamp(Mathf.RoundToInt(fractional), 0, pentatonic.Length - 1);
        int midi = 45 + (octave * 12) + pentatonic[scaleIndex];
        return 440f * Mathf.Pow(2f, (midi - 69) / 12f);
    }
}
