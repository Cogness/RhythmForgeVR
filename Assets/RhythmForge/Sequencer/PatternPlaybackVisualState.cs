using System;
using UnityEngine;
using RhythmForge.Core.Data;
using RhythmForge.Core.PatternBehavior;

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
            var spec = VisualGrammarProfiles.GetPlaybackBase().Build(type, sound);
            return PatternBehaviorRegistry.Get(type).AdjustVisualSpec(spec, sound);
        }
    }

    [Serializable]
    public struct PatternPlaybackVisualState
    {
        public float phase;
        public float pulse;
        public bool isActive;
        public float sustainAmount;
        public float rhythmPulse;
        public float melodyMotion;
        public float harmonySustain;
        public bool isShapeNative;
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
                rhythmPulse = 0f,
                melodyMotion = 0f,
                harmonySustain = 0f,
                isShapeNative = false,
                playbackSceneId = playbackSceneId,
                visualSpec = PlaybackVisualSpec.FromSoundProfile(type, sound)
            };
        }
    }
}
