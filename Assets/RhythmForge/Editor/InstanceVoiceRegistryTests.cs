#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using RhythmForge.Audio;

namespace RhythmForge.Editor
{
    public class InstanceVoiceRegistryTests
    {
        [Test]
        public void RegisterLookupAndUnregister_WorkAsExpected()
        {
            var registry = new InstanceVoiceRegistry();
            var root = new GameObject("InstanceVoiceRegistryTestRoot");
            try
            {
                var pool = new InstanceVoicePool(root.transform, "TestPool");

                registry.Register("instance-1", pool, 3);

                Assert.That(registry.TryGetPool("instance-1", out var resolved), Is.True);
                Assert.That(resolved, Is.SameAs(pool));
                Assert.That(registry.ActiveVoiceCount, Is.EqualTo(3));

                registry.Unregister("instance-1");

                Assert.That(registry.TryGetPool("instance-1", out _), Is.False);
                Assert.That(registry.ActiveVoiceCount, Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
#endif
