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
            SoundProfile soundProfile,
            string instanceId = null);

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
            string instanceId = null);

        void PlayChord(
            InstrumentPreset preset,
            List<int> chord,
            float velocity,
            float duration,
            float pan,
            float brightness,
            float depth,
            float fxSend,
            SoundProfile soundProfile,
            string instanceId = null);
    }
}
