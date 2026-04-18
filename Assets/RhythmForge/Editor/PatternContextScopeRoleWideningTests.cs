#if UNITY_EDITOR
using NUnit.Framework;
using RhythmForge.Core.Data;
using RhythmForge.Core.Sequencing;

namespace RhythmForge.Editor
{
    /// <summary>
    /// Phase C verification: <see cref="PatternContextScope.ResolveRole"/> is
    /// scene-wide — a mixed scene of rhythm + melody + harmony patterns
    /// produces indices 0..N-1 across ALL patterns, not 0..M-1 per type. This
    /// prevents the "two role-0 primaries" collision across facets that made
    /// unified-shape voices double up in the legacy per-type-indexed world.
    /// </summary>
    public class PatternContextScopeRoleWideningTests
    {
        [Test]
        public void MixedScene_ResolveRole_IndexesSceneWide()
        {
            var state = AppStateFactory.CreateEmpty();
            // 2 rhythm + 1 melody + 1 harmony = 4 shapes total.
            var r1 = new PatternDefinition { id = "r1", type = PatternType.RhythmLoop };
            var m1 = new PatternDefinition { id = "m1", type = PatternType.MelodyLine };
            var r2 = new PatternDefinition { id = "r2", type = PatternType.RhythmLoop };
            var h1 = new PatternDefinition { id = "h1", type = PatternType.HarmonyPad };
            state.patterns.Add(r1);
            state.patterns.Add(m1);
            state.patterns.Add(r2);
            state.patterns.Add(h1);

            var role_r1 = PatternContextScope.ResolveRole(state, r1);
            var role_m1 = PatternContextScope.ResolveRole(state, m1);
            var role_r2 = PatternContextScope.ResolveRole(state, r2);
            var role_h1 = PatternContextScope.ResolveRole(state, h1);

            Assert.That(role_r1.count, Is.EqualTo(4));
            Assert.That(role_m1.count, Is.EqualTo(4));
            Assert.That(role_r2.count, Is.EqualTo(4));
            Assert.That(role_h1.count, Is.EqualTo(4));

            Assert.That(role_r1.index, Is.EqualTo(0));
            Assert.That(role_m1.index, Is.EqualTo(1));
            Assert.That(role_r2.index, Is.EqualTo(2));
            Assert.That(role_h1.index, Is.EqualTo(3));
        }

        [Test]
        public void EmptyScene_ResolveRole_FallsBackToPrimary()
        {
            var state = AppStateFactory.CreateEmpty();
            var pattern = new PatternDefinition { id = "orphan", type = PatternType.RhythmLoop };

            var role = PatternContextScope.ResolveRole(state, pattern);

            // No entries in state.patterns → defaults to primary.
            Assert.That(role.index, Is.EqualTo(ShapeRole.Primary.index));
            Assert.That(role.count, Is.EqualTo(ShapeRole.Primary.count));
        }

        [Test]
        public void UnregisteredPattern_InNonEmptyScene_ReturnsIndex0WithTotalCount()
        {
            var state = AppStateFactory.CreateEmpty();
            state.patterns.Add(new PatternDefinition { id = "a", type = PatternType.RhythmLoop });
            state.patterns.Add(new PatternDefinition { id = "b", type = PatternType.MelodyLine });

            var orphan = new PatternDefinition { id = "orphan", type = PatternType.HarmonyPad };
            var role = PatternContextScope.ResolveRole(state, orphan);

            // Pattern isn't in the list → index 0, count = total patterns (2).
            Assert.That(role.index, Is.EqualTo(0));
            Assert.That(role.count, Is.EqualTo(2));
        }

        [Test]
        public void ForShape_MatchesForPattern()
        {
            // ForShape is a Phase C alias for ForPattern; both must push the
            // same scene-wide role.
            var state = AppStateFactory.CreateEmpty();
            var p1 = new PatternDefinition { id = "p1", type = PatternType.RhythmLoop };
            var p2 = new PatternDefinition { id = "p2", type = PatternType.MelodyLine };
            state.patterns.Add(p1);
            state.patterns.Add(p2);

            var a = PatternContextScope.ResolveRole(state, p2);
            // ForShape opens a scope; we validate by reading role back out of
            // ShapeRoleProvider.
            using (PatternContextScope.ForShape(state, p2))
            {
                var observed = ShapeRoleProvider.Current;
                Assert.That(observed.index, Is.EqualTo(a.index));
                Assert.That(observed.count, Is.EqualTo(a.count));
            }
        }
    }
}
#endif
