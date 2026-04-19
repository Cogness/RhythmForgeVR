using System.Collections.Generic;
using UnityEngine;

namespace RhythmForge.Core.Data
{
    [CreateAssetMenu(menuName = "RhythmForge/Spatial Zone Layout")]
    public class SpatialZoneLayout : ScriptableObject
    {
        [SerializeField] private List<SpatialZone> _zones = new List<SpatialZone>();

        public IReadOnlyList<SpatialZone> Zones => _zones;

        public static SpatialZoneLayout CreateDefault()
        {
            var layout = CreateInstance<SpatialZoneLayout>();
            layout._zones = new List<SpatialZone>
            {
                new SpatialZone
                {
                    id = "DrumsFloor",
                    targetRole = "Rhythm",
                    center = new Vector3(0f, -0.6f, 0.9f),
                    radius = 0.8f,
                    reverbBias = 0f,
                    delayBias = 0.05f,
                    gainBias = 1.05f
                },
                new SpatialZone
                {
                    id = "MelodyFront",
                    targetRole = "Melody",
                    center = new Vector3(0f, 0f, 1f),
                    radius = 0.7f,
                    reverbBias = 0.05f,
                    delayBias = 0.1f,
                    gainBias = 1.08f
                },
                new SpatialZone
                {
                    id = "HarmonyBehind",
                    targetRole = "Harmony",
                    center = new Vector3(0f, 0.1f, -0.9f),
                    radius = 1.1f,
                    reverbBias = 0.25f,
                    delayBias = 0.2f,
                    gainBias = 0.92f
                },
                new SpatialZone
                {
                    id = "PadsFar",
                    targetRole = "Harmony",
                    center = new Vector3(0f, 0.3f, 2.4f),
                    radius = 1.2f,
                    reverbBias = 0.35f,
                    delayBias = 0.15f,
                    gainBias = 0.84f,
                    depthZThreshold = 1.6f
                },
                new SpatialZone
                {
                    id = "AccentsOverhead",
                    targetRole = "Ornament",
                    center = new Vector3(0f, 1f, 0.6f),
                    radius = 0.8f,
                    reverbBias = 0.1f,
                    delayBias = 0.3f,
                    gainBias = 0.96f
                }
            };
            return layout;
        }
    }
}
