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
        public float brightness;
        public float reverbSend;
        public float delaySend;
        public float gainTrim;
        public int ensembleRoleIndex;
        public int progressionBarIndex;
        [NonSerialized] public string currentZoneId;

        [Obsolete("Spatial direction is now driven by the instance transform. Retained for one migration window.")]
        public float pan;

        [Obsolete("Use gainTrim instead. Retained for one migration window.")]
        public float gain;

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
            brightness = Mathf.Clamp01(1f - position.y);
            reverbSend = Mathf.Clamp01(depth * 0.55f);
            delaySend = Mathf.Clamp01(depth * 0.35f);
            gainTrim = Mathf.Clamp01(1.05f - depth * 0.15f);

            // Keep legacy serialized fields populated for one version while callers migrate.
            pan = Mathf.Clamp(position.x * 2f - 1f, -1f, 1f);
            gain = gainTrim;
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
                brightness = brightness,
                reverbSend = reverbSend,
                delaySend = delaySend,
                gainTrim = gainTrim,
                ensembleRoleIndex = ensembleRoleIndex,
                progressionBarIndex = progressionBarIndex,
                currentZoneId = currentZoneId,
                pan = pan,
                gain = gain
            };
        }
    }
}
