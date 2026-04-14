#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using RhythmForge.Core.Data;
using RhythmForge.Core.Session;

namespace RhythmForge.Editor
{
    public class SoundProfileResolverTests
    {
        [Test]
        public void GetEffectivePresetId_PrefersInstanceOverride()
        {
            var resolver = new SoundProfileResolver(InstrumentPresets.Get);
            var pattern = new PatternDefinition { presetId = "lofi-piano" };
            var instance = new PatternInstance("pattern-1", "scene-a", Vector3.zero, 0.3f)
            {
                presetOverrideId = "trap-bass"
            };

            string presetId = resolver.GetEffectivePresetId(instance, pattern);

            Assert.That(presetId, Is.EqualTo("trap-bass"));
        }

        [Test]
        public void GetEffectiveSoundProfile_BlendsGeometryWithPresetBias()
        {
            var resolver = new SoundProfileResolver(InstrumentPresets.Get);
            var geometry = new SoundProfile
            {
                brightness = 0.18f,
                resonance = 0.16f,
                drive = 0.12f,
                attackBias = 0.14f,
                releaseBias = 0.2f,
                detune = 0.1f,
                modDepth = 0.12f,
                stereoSpread = 0.14f,
                grooveInstability = 0.1f,
                delayBias = 0.1f,
                reverbBias = 0.12f,
                waveMorph = 0.1f,
                filterMotion = 0.1f,
                transientSharpness = 0.14f,
                body = 0.12f
            };

            var pattern = new PatternDefinition
            {
                type = PatternType.MelodyLine,
                presetId = "lofi-piano",
                shapeProfile = new ShapeProfile(),
                soundProfile = geometry
            };
            var instance = new PatternInstance("pattern-1", "scene-a", Vector3.zero, 0.3f)
            {
                presetOverrideId = "trap-bass"
            };

            var effective = resolver.GetEffectiveSoundProfile(instance, pattern);

            Assert.That(effective.body, Is.GreaterThan(geometry.body));
            Assert.That(effective.drive, Is.GreaterThan(geometry.drive));
            Assert.That(effective.transientSharpness, Is.GreaterThan(geometry.transientSharpness));
            Assert.That(effective.brightness, Is.GreaterThan(geometry.brightness));
        }
    }
}
#endif
