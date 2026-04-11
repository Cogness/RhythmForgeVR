using System;

namespace RhythmForge.Core.Data
{
    [Serializable]
    public class SoundProfile
    {
        public float brightness;
        public float resonance;
        public float drive;
        public float attackBias;
        public float releaseBias;
        public float detune;
        public float modDepth;
        public float stereoSpread;
        public float grooveInstability;
        public float delayBias;
        public float reverbBias;
        public float waveMorph;
        public float filterMotion;
        public float transientSharpness;
        public float body;

        public SoundProfile Clone()
        {
            return new SoundProfile
            {
                brightness = brightness,
                resonance = resonance,
                drive = drive,
                attackBias = attackBias,
                releaseBias = releaseBias,
                detune = detune,
                modDepth = modDepth,
                stereoSpread = stereoSpread,
                grooveInstability = grooveInstability,
                delayBias = delayBias,
                reverbBias = reverbBias,
                waveMorph = waveMorph,
                filterMotion = filterMotion,
                transientSharpness = transientSharpness,
                body = body
            };
        }
    }
}
