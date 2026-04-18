using System;
using System.Collections.Generic;
using UnityEngine;

namespace RhythmForge.Core.Data
{
    /// <summary>
    /// A named spherical region in world space. Patterns inside the zone receive
    /// additional reverb/delay/filter sends on top of their position-derived mix.
    /// </summary>
    [Serializable]
    public class SpatialZone
    {
        public string id;
        public string displayName;
        public Vector3 center;
        public float radius;
        public Color color;
        public ZoneBusFxProfile busFx;

        /// <summary>Which pattern type defaults to spawning in this zone.</summary>
        public PatternType defaultType;
    }

    [Serializable]
    public struct ZoneBusFxProfile
    {
        /// <summary>Additional reverb send (0–1) added when inside this zone.</summary>
        public float reverbAdd;
        /// <summary>Additional delay send (0–1) added when inside this zone.</summary>
        public float delayAdd;
        /// <summary>Filter-cutoff brightness offset (−1 to +1) applied to the brightness param.</summary>
        public float filterCutoffOffset;
    }

    /// <summary>Factory that creates the five default spatial zones.</summary>
    public static class SpatialZoneFactory
    {
        /// <summary>
        /// Returns the five default zones positioned relative to a stage origin.
        /// The origin is typically the user's standing position at session start.
        /// </summary>
        public static List<SpatialZone> CreateDefaults(Vector3 stageOrigin)
        {
            return new List<SpatialZone>
            {
                new SpatialZone
                {
                    id          = "drums-floor",
                    displayName = "Drums Floor",
                    center      = stageOrigin + new Vector3(0f, -0.3f, 0.5f),
                    radius      = 0.9f,
                    color       = new Color(0.9f, 0.3f, 0.2f, 0.15f),  // warm red
                    busFx       = new ZoneBusFxProfile { reverbAdd = 0.08f, delayAdd = 0.04f, filterCutoffOffset =  0.05f },
                    defaultType = PatternType.RhythmLoop
                },
                new SpatialZone
                {
                    id          = "bass-low",
                    displayName = "Bass Low",
                    center      = stageOrigin + new Vector3(0.8f, 0.2f, 0.4f),
                    radius      = 0.8f,
                    color       = new Color(0.5f, 0.2f, 0.9f, 0.15f),  // deep purple
                    busFx       = new ZoneBusFxProfile { reverbAdd = 0.10f, delayAdd = 0.05f, filterCutoffOffset = -0.10f },
                    defaultType = PatternType.RhythmLoop
                },
                new SpatialZone
                {
                    id          = "melody-front",
                    displayName = "Melody Front",
                    center      = stageOrigin + new Vector3(0f, 1.2f, 1.5f),
                    radius      = 0.85f,
                    color       = new Color(0.2f, 0.8f, 0.4f, 0.15f),  // bright green
                    busFx       = new ZoneBusFxProfile { reverbAdd = 0.06f, delayAdd = 0.12f, filterCutoffOffset =  0.08f },
                    defaultType = PatternType.MelodyLine
                },
                new SpatialZone
                {
                    id          = "harmony-behind",
                    displayName = "Harmony Behind",
                    center      = stageOrigin + new Vector3(0f, 1.3f, -1.2f),
                    radius      = 1.1f,
                    color       = new Color(0.2f, 0.4f, 0.9f, 0.15f),  // cool blue
                    busFx       = new ZoneBusFxProfile { reverbAdd = 0.22f, delayAdd = 0.08f, filterCutoffOffset =  0.04f },
                    defaultType = PatternType.HarmonyPad
                },
                new SpatialZone
                {
                    id          = "accents-overhead",
                    displayName = "Accents Overhead",
                    center      = stageOrigin + new Vector3(0f, 2.2f, 0.3f),
                    radius      = 0.7f,
                    color       = new Color(1.0f, 0.85f, 0.2f, 0.15f), // bright yellow
                    busFx       = new ZoneBusFxProfile { reverbAdd = 0.05f, delayAdd = 0.18f, filterCutoffOffset =  0.12f },
                    defaultType = PatternType.MelodyLine
                }
            };
        }
    }
}
