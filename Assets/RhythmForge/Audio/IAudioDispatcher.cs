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
            float gainTrim,
            float brightness,
            float reverbSend,
            float delaySend,
            SoundProfile soundProfile,
            string instanceId = null);

        void PlayMelody(
            InstrumentPreset preset,
            int midi,
            float velocity,
            float duration,
            float gainTrim,
            float brightness,
            float reverbSend,
            float delaySend,
            SoundProfile soundProfile,
            float glide = 0f,
            string instanceId = null);

        void PlayChord(
            InstrumentPreset preset,
            List<int> chord,
            float velocity,
            float duration,
            float gainTrim,
            float brightness,
            float reverbSend,
            float delaySend,
            SoundProfile soundProfile,
            string instanceId = null);
    }
}
