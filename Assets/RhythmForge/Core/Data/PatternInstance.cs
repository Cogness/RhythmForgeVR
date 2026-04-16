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
        public float reverbSend;
        public float delaySend;
        public float gainTrim;

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
            RecalculateMixFromPosition();
        }

        public void RecalculateMixFromPosition()
        {
            // Legacy stereo mapping (still used by preview pool / SamplePlayer fallback)
            pan = Mathf.Clamp(position.x * 2f - 1f, -1f, 1f);

            // Spatial mix: brightness driven by height relative to a typical head height (~1.4m)
            float headHeight = 1.4f;
            brightness = Mathf.Clamp01(0.35f + (position.y - headHeight) * 0.6f);

            // Depth now derives from actual world-space distance from origin, not just the depth slider
            float distanceFromOrigin = position.magnitude;
            reverbSend = Mathf.Clamp01(depth * 0.55f + distanceFromOrigin * 0.1f);
            delaySend  = Mathf.Clamp01(depth * 0.35f);
            gainTrim   = Mathf.Clamp01(1.05f - depth * 0.25f);

            // Legacy gain kept for backward compat
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
                reverbSend = reverbSend,
                delaySend = delaySend,
                gainTrim = gainTrim
            };
        }
    }
}
