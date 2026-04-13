using System;
using UnityEngine;
using RhythmForge.Core.Data;

namespace RhythmForge.Sequencer
{
    [Serializable]
    public struct PlaybackVisualSpec
    {
        public PatternType type;
        public float brightness;
        public float thickness;
        public float decaySeconds;
        public float motionAmplitude;
        public float motionSpeed;
        public float phaseJitter;
        public float markerScale;
        public float haloStrength;
        public float secondaryStrength;
        public float sharpness;

        public static PlaybackVisualSpec FromSoundProfile(PatternType type, SoundProfile sound)
        {
            sound = sound ?? new SoundProfile();

            var spec = new PlaybackVisualSpec
            {
                type = type,
                brightness = Mathf.Clamp01(0.18f + sound.brightness * 0.82f),
                thickness = Mathf.Clamp01(0.18f + sound.body * 0.82f),
                decaySeconds = Mathf.Lerp(0.16f, 1.28f, sound.releaseBias),
                motionAmplitude = Mathf.Clamp01(sound.modDepth * 0.55f + sound.filterMotion * 0.45f),
                motionSpeed = Mathf.Lerp(0.35f, 1.6f, sound.filterMotion * 0.5f + sound.modDepth * 0.5f),
                phaseJitter = Mathf.Clamp01(sound.grooveInstability * 0.7f + sound.filterMotion * 0.3f),
                markerScale = Mathf.Clamp01(0.24f + sound.body * 0.34f + sound.brightness * 0.18f + sound.transientSharpness * 0.24f),
                haloStrength = Mathf.Clamp01(sound.stereoSpread * 0.38f + sound.reverbBias * 0.36f + sound.body * 0.26f),
                secondaryStrength = Mathf.Clamp01(sound.modDepth * 0.32f + sound.filterMotion * 0.24f + sound.reverbBias * 0.18f + sound.releaseBias * 0.26f),
                sharpness = Mathf.Clamp01(0.14f + sound.transientSharpness * 0.86f)
            };

            switch (type)
            {
                case PatternType.RhythmLoop:
                    spec.markerScale = Mathf.Clamp01(spec.markerScale + 0.12f);
                    spec.haloStrength = Mathf.Clamp01(spec.haloStrength * 0.55f + sound.body * 0.08f);
                    spec.secondaryStrength = Mathf.Clamp01(spec.secondaryStrength * 0.42f + sound.grooveInstability * 0.28f);
                    spec.phaseJitter = Mathf.Clamp01(spec.phaseJitter + sound.grooveInstability * 0.22f);
                    spec.motionSpeed = Mathf.Lerp(0.85f, 1.9f, sound.grooveInstability * 0.45f + sound.transientSharpness * 0.55f);
                    break;
                case PatternType.MelodyLine:
                    spec.markerScale = Mathf.Clamp01(spec.markerScale + 0.04f);
                    spec.haloStrength = Mathf.Clamp01(spec.haloStrength * 0.72f + sound.releaseBias * 0.16f);
                    spec.secondaryStrength = Mathf.Clamp01(spec.secondaryStrength + sound.modDepth * 0.2f);
                    spec.motionSpeed = Mathf.Lerp(0.55f, 1.3f, sound.filterMotion * 0.4f + sound.modDepth * 0.6f);
                    break;
                case PatternType.HarmonyPad:
                    spec.markerScale = Mathf.Clamp01(spec.markerScale * 0.72f + sound.body * 0.08f);
                    spec.haloStrength = Mathf.Clamp01(spec.haloStrength + sound.stereoSpread * 0.2f + sound.reverbBias * 0.18f);
                    spec.secondaryStrength = Mathf.Clamp01(spec.secondaryStrength + sound.releaseBias * 0.2f + sound.modDepth * 0.16f);
                    spec.motionSpeed = Mathf.Lerp(0.2f, 0.72f, sound.filterMotion * 0.45f + sound.modDepth * 0.55f);
                    spec.phaseJitter = Mathf.Clamp01(spec.phaseJitter * 0.35f);
                    break;
            }

            return spec;
        }
    }

    [Serializable]
    public struct PatternPlaybackVisualState
    {
        public float phase;
        public float pulse;
        public bool isActive;
        public float sustainAmount;
        public string playbackSceneId;
        public PlaybackVisualSpec visualSpec;

        public static PatternPlaybackVisualState CreateInactive(PatternType type, SoundProfile sound = null, string playbackSceneId = null)
        {
            return new PatternPlaybackVisualState
            {
                phase = -1f,
                pulse = 0f,
                isActive = false,
                sustainAmount = 0f,
                playbackSceneId = playbackSceneId,
                visualSpec = PlaybackVisualSpec.FromSoundProfile(type, sound)
            };
        }
    }
}
