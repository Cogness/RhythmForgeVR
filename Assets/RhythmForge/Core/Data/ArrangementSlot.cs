using System;

namespace RhythmForge.Core.Data
{
    [Serializable]
    public class ArrangementSlot
    {
        public string id;
        public string sceneId;
        public int bars;

        public ArrangementSlot() { }

        public ArrangementSlot(string id, int bars = 4)
        {
            this.id = id;
            this.sceneId = null;
            this.bars = bars;
        }

        public bool IsPopulated => !string.IsNullOrEmpty(sceneId);
    }
}
