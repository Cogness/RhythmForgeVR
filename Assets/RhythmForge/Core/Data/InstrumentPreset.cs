using System;

namespace RhythmForge.Core.Data
{
    [Serializable]
    public class InstrumentPreset
    {
        public string id;
        public string label;
        public string voiceType;
        public string groupId;
        public float fxSend;
        public PresetEnvelope envelope;

        public InstrumentPreset() { }

        public InstrumentPreset(string id, string label, string voiceType, string groupId,
            float fxSend, float attack, float release)
        {
            this.id = id;
            this.label = label;
            this.voiceType = voiceType;
            this.groupId = groupId;
            this.fxSend = fxSend;
            this.envelope = new PresetEnvelope { attack = attack, release = release };
        }
    }

    [Serializable]
    public class PresetEnvelope
    {
        public float attack;
        public float release;
    }
}
