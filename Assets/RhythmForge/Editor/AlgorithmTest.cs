#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using RhythmForge.Core.Data;
using RhythmForge.Core.Analysis;
using RhythmForge.Core.Sequencing;
using RhythmForge.Core.Session;
using RhythmForge.Audio;

namespace RhythmForge.Editor
{
    public static class AlgorithmTest
    {
        [MenuItem("RhythmForge/Run Algorithm Tests")]
        public static void RunAll()
        {
            Debug.Log("═══════════════════════════════════════");
            Debug.Log("[RhythmForge] Running algorithm tests…");
            Debug.Log("═══════════════════════════════════════");

            TestRhythmPipeline();
            TestMelodyPipeline();
            TestHarmonyPipeline();
            TestDemoSession();
            TestProceduralAudio();

            Debug.Log("═══════════════════════════════════════");
            Debug.Log("[RhythmForge] ✅ All tests complete.");
            Debug.Log("═══════════════════════════════════════");
        }

        // ──────────────────────────── RHYTHM ────────────────────────────

        static void TestRhythmPipeline()
        {
            Debug.Log("── Rhythm Pipeline ──");

            var pts = GenerateCircle(24, 0.14f);

            var metrics = StrokeAnalyzer.Analyze(pts);
            Debug.Log($"  StrokeMetrics: closed={metrics.closed}, length={metrics.length:F3}" +
                      $", wobble={metrics.wobble:F3}, avgSize={metrics.averageSize:F3}");

            AssertTrue(metrics.closed, "Circle should be detected as closed");
            AssertTrue(metrics.length > 0.1f, "Circle path length should be > 0.1m");

            var norm = StrokeAnalyzer.NormalizePoints(pts, metrics);
            var sp   = ShapeProfileCalculator.Derive(norm, metrics, PatternType.RhythmLoop);
            Debug.Log($"  ShapeProfile: circ={sp.circularity:F2}, ang={sp.angularity:F2}" +
                      $", sym={sp.symmetry:F2}, asp={sp.aspectRatio:F2}");

            AssertRange(sp.circularity, 0.5f, 1.0f, "Circularity should be high for a circle");

            var sound = SoundProfileMapper.Derive(PatternType.RhythmLoop, sp);
            Debug.Log($"  SoundProfile: body={sound.body:F2}, bright={sound.brightness:F2}" +
                      $", drive={sound.drive:F2}, transient={sound.transientSharpness:F2}");

            AssertRange(sound.body, 0f, 1f, "body in [0,1]");
            AssertRange(sound.brightness, 0f, 1f, "brightness in [0,1]");

            var result = RhythmDeriver.Derive(pts, metrics, "lofi", sp, sound);
            Debug.Log($"  RhythmSeq: bars={result.bars}, events={result.derivedSequence.events.Count}" +
                      $", swing={result.derivedSequence.swing:F3}");
            Debug.Log($"  Summary: {result.summary}");

            AssertTrue(result.bars == 2 || result.bars == 4, "Bars must be 2 or 4");
            AssertTrue(result.derivedSequence.events.Count > 0, "Must have events");
        }

        // ──────────────────────────── MELODY ────────────────────────────

        static void TestMelodyPipeline()
        {
            Debug.Log("── Melody Pipeline ──");

            var pts = GenerateWave(32, 0.22f, 0.09f);
            var metrics = StrokeAnalyzer.Analyze(pts);
            var norm = StrokeAnalyzer.NormalizePoints(pts, metrics);
            var sp   = ShapeProfileCalculator.Derive(norm, metrics, PatternType.MelodyLine);
            var sound = SoundProfileMapper.Derive(PatternType.MelodyLine, sp);

            var result = MelodyDeriver.Derive(pts, metrics, "A minor", "lofi", sp, sound);
            Debug.Log($"  MelodySeq: bars={result.bars}, notes={result.derivedSequence.notes.Count}");
            Debug.Log($"  Summary: {result.summary}");

            AssertTrue(result.derivedSequence.notes.Count > 0, "Melody must have notes");

            foreach (var note in result.derivedSequence.notes)
            {
                AssertTrue(note.midi >= 36 && note.midi <= 108, $"MIDI {note.midi} out of sane range");
                AssertRange(note.velocity, 0f, 1f, "velocity in [0,1]");
            }
        }

        // ──────────────────────────── HARMONY ────────────────────────────

        static void TestHarmonyPipeline()
        {
            Debug.Log("── Harmony Pipeline ──");

            var pts = GeneratePadStroke(20, 0.18f, 0.06f);
            var metrics = StrokeAnalyzer.Analyze(pts);
            var norm = StrokeAnalyzer.NormalizePoints(pts, metrics);
            var sp   = ShapeProfileCalculator.Derive(norm, metrics, PatternType.HarmonyPad);
            var sound = SoundProfileMapper.Derive(PatternType.HarmonyPad, sp);

            var result = HarmonyDeriver.Derive(pts, metrics, "A minor", "lofi", sp, sound);
            Debug.Log($"  HarmonySeq: bars={result.bars}, flavor={result.derivedSequence.flavor}" +
                      $", chord={string.Join(",", result.derivedSequence.chord)}");
            Debug.Log($"  Summary: {result.summary}");

            AssertTrue(result.derivedSequence.chord != null && result.derivedSequence.chord.Count >= 3,
                "Chord must have at least 3 notes");
            AssertTrue(!string.IsNullOrEmpty(result.derivedSequence.flavor), "Flavor must be set");
        }

        // ──────────────────────────── DEMO SESSION ────────────────────────────

        static void TestDemoSession()
        {
            Debug.Log("── Demo Session ──");

            var store = new SessionStore();
            var state = DemoSession.CreateDemoState(store);

            Debug.Log($"  Patterns: {state.patterns.Count}");
            Debug.Log($"  Instances: {state.instances.Count}");
            Debug.Log($"  Active scene: {state.activeSceneId}");

            AssertTrue(state.patterns.Count == 3, "Demo should create 3 patterns");
            AssertTrue(state.instances.Count == 3, "Demo should create 3 instances");
            AssertTrue(!string.IsNullOrEmpty(state.activeSceneId), "Active scene must be set");

            foreach (var p in state.patterns)
            {
                AssertTrue(!string.IsNullOrEmpty(p.id), "Pattern id must be set");
                AssertTrue(p.derivedSequence != null, $"Pattern {p.name} must have sequence");
                AssertTrue(!string.IsNullOrEmpty(p.shapeSummary), $"Pattern {p.name} must have shapeSummary");
            }
        }

        // ──────────────────────────── PROCEDURAL AUDIO ────────────────────────────

        static void TestProceduralAudio()
        {
            Debug.Log("── Procedural Audio Synthesis ──");

            var kick  = ProceduralSynthesizer.GenerateKick();
            var snare = ProceduralSynthesizer.GenerateSnare();
            var hat   = ProceduralSynthesizer.GenerateHat();
            var perc  = ProceduralSynthesizer.GeneratePerc();
            var tone  = ProceduralSynthesizer.GenerateTone();

            AssertTrue(kick  != null && kick.length  > 0.1f, "Kick clip valid");
            AssertTrue(snare != null && snare.length > 0.1f, "Snare clip valid");
            AssertTrue(hat   != null && hat.length   > 0.03f, "Hat clip valid");
            AssertTrue(perc  != null && perc.length  > 0.05f, "Perc clip valid");
            AssertTrue(tone  != null && tone.length  > 1.0f,  "Tone clip valid");

            Debug.Log($"  kick={kick.length:F2}s, snare={snare.length:F2}s, hat={hat.length:F2}s" +
                      $", perc={perc.length:F2}s, tone={tone.length:F2}s");
        }

        // ──────────────────────────── SHAPE GENERATORS ────────────────────────────

        static List<Vector2> GenerateCircle(int n, float r)
        {
            var pts = new List<Vector2>(n + 1);
            for (int i = 0; i <= n; i++)
            {
                float a = (float)i / n * Mathf.PI * 2f;
                pts.Add(new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r));
            }
            return pts;
        }

        static List<Vector2> GenerateWave(int n, float width, float amp)
        {
            var pts = new List<Vector2>(n);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / (n - 1);
                pts.Add(new Vector2((t - 0.5f) * width, Mathf.Sin(t * Mathf.PI * 3f) * amp));
            }
            return pts;
        }

        static List<Vector2> GeneratePadStroke(int n, float width, float height)
        {
            var pts = new List<Vector2>(n);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / (n - 1);
                pts.Add(new Vector2((t - 0.5f) * width, (t - 0.5f) * height));
            }
            return pts;
        }

        // ──────────────────────────── ASSERTION HELPERS ────────────────────────────

        static void AssertTrue(bool condition, string msg)
        {
            if (!condition)
                Debug.LogError($"  [FAIL] {msg}");
            else
                Debug.Log($"  [pass] {msg}");
        }

        static void AssertRange(float value, float min, float max, string msg)
        {
            AssertTrue(value >= min && value <= max, $"{msg} (got {value:F3}, expected [{min},{max}])");
        }
    }
}
#endif
