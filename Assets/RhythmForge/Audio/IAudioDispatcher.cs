using System.Collections.Generic;
using RhythmForge.Core.Data;

namespace RhythmForge.Audio
{
    public interface IAudioDispatcher
    {
        void PlayDrum(
            InstrumentPreset preset,
            string lane,
            float velocity,
            float pan,
            float brightness,
            float depth,
            float fxSend,
            SoundProfile soundProfile);

        void PlayMelody(
            InstrumentPreset preset,
            int midi,
            float velocity,
            float duration,
            float pan,
            float brightness,
            float depth,
            float fxSend,
            SoundProfile soundProfile,
            float glide = 0f,
            float startDelay = 0f);

        void PlayChord(
            InstrumentPreset preset,
            List<int> chord,
            float velocity,
            float duration,
            float pan,
            float brightness,
            float depth,
            float fxSend,
            SoundProfile soundProfile);
    }
}
