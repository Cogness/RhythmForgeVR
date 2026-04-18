using System;
using UnityEngine;

namespace RhythmForge.Core.Data
{
    [Serializable]
    public class PatternInstance
    {
        public string id;
        public string patternId;
        public string sceneId;
        public Vector3 position;
        public float depth;
        public string presetOverrideId;
        public bool muted;
        public float gain;
        public float pan;
        public float brightness;
        public int ensembleRoleIndex;
        public int progressionBarIndex;

        public PatternInstance() { }

        public PatternInstance(string patternId, string sceneId, Vector3 position, float depth)
        {
            id = Guid.NewGuid().ToString("N").Substring(0, 12);
            this.patternId = patternId;
            this.sceneId = sceneId;
            this.position = position;
            this.depth = depth;
            presetOverrideId = null;
            muted = false;
            ensembleRoleIndex = 0;
            progressionBarIndex = 0;
            RecalculateMixFromPosition();
        }

        public void RecalculateMixFromPosition()
        {
            pan = Mathf.Clamp(position.x * 2f - 1f, -1f, 1f);
            brightness = Mathf.Clamp01(1f - position.y);
            gain = Mathf.Clamp01(1.08f - depth * 0.58f);
        }

        public PatternInstance Clone()
        {
            return new PatternInstance
            {
                id = Guid.NewGuid().ToString("N").Substring(0, 12),
                patternId = patternId,
                sceneId = sceneId,
                position = position,
                depth = depth,
                presetOverrideId = presetOverrideId,
                muted = muted,
                gain = gain,
                pan = pan,
                brightness = brightness,
                ensembleRoleIndex = ensembleRoleIndex,
                progressionBarIndex = progressionBarIndex
            };
        }
    }
}
