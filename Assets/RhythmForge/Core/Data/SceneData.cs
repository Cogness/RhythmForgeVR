using System;
using System.Collections.Generic;

namespace RhythmForge.Core.Data
{
    [Serializable]
    public class SceneData
    {
        public string id;
        public string name;
        public List<string> instanceIds = new List<string>();
        public float tempoOverride;
        public string keyOverride;
        public bool hasTempoOverride;
        public bool hasKeyOverride;

        public SceneData() { }

        public SceneData(string id, string name)
        {
            this.id = id;
            this.name = name;
            instanceIds = new List<string>();
            hasTempoOverride = false;
            hasKeyOverride = false;
        }
    }
}
