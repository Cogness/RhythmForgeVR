#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using RhythmForge.Core.Analysis;
using RhythmForge.Core.Data;
using RhythmForge.Core.PatternBehavior;
using RhythmForge.Core.Session;

namespace RhythmForge.Editor
{
    public class HarmonyBehaviorTests
    {
        [Test]
        public void CommitHarmony_StoresEightChordProgressionAcrossEightBars()
        {
            var store = new SessionStore();
            var points = new List<Vector2>
            {
                new Vector2(-0.4f, -0.15f),
                new Vector2(-0.1f, -0.05f),
                new Vector2(0.15f, 0.08f),
                new Vector2(0.4f, 0.2f)
            };
            var draft = DraftBuilder.BuildFromStroke(
                PatternType.Harmony,
                points,
                new Vector3(0.5f, 0.5f, 0.4f),
                Quaternion.identity,
                store.State,
                store);

            Assert.That(draft.success, Is.True);
            Assert.That(draft.derivedSequence, Is.Not.Null);
            Assert.That(draft.derivedSequence.chordEvents, Is.Not.Null);
            Assert.That(draft.derivedSequence.chordEvents, Has.Count.EqualTo(8));
            Assert.That(draft.bars, Is.EqualTo(8));

            store.CommitDraft(draft, duplicate: false);

            var harmonyPatternId = store.GetComposition().GetPatternId(CompositionPhase.Harmony);
            var pattern = store.GetPattern(harmonyPatternId);

            Assert.That(pattern, Is.Not.Null);
            Assert.That(pattern.bars, Is.EqualTo(8));
            Assert.That(pattern.derivedSequence.chordEvents, Has.Count.EqualTo(8));
            Assert.That(store.GetComposition().progression.chords, Has.Count.EqualTo(8));
        }
    }
}
#endif
