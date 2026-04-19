#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using RhythmForge.Core.Data;
using RhythmForge.Core.Session;
using RhythmForge.Interaction;

namespace RhythmForge.Editor
{
    public class ConductorGestureRecognizerTests
    {
        [Test]
        public void Sway_Detected_OnSustainedPeakToPeak()
        {
            WithRecognizer((recognizer, _, _) =>
            {
                ConductorGestureEvent? detected = null;
                double dt = 1d / 60d;
                for (int i = 0; i < 80; i++)
                {
                    double time = i * dt;
                    float x = 0.30f * Mathf.Sin((float)(time * Mathf.PI * 4d));
                    detected = recognizer.PushSyntheticSample(new Vector3(x, 0f, 1f), false, false, time) ?? detected;
                }

                Assert.That(detected.HasValue, Is.True);
                Assert.That(detected.Value.kind, Is.EqualTo(ConductorGestureKind.Sway));
            });
        }

        [Test]
        public void Sway_NotDetected_BelowAmplitudeThreshold()
        {
            WithRecognizer((recognizer, _, _) =>
            {
                ConductorGestureEvent? detected = null;
                double dt = 1d / 60d;
                for (int i = 0; i < 80; i++)
                {
                    double time = i * dt;
                    float x = 0.18f * Mathf.Sin((float)(time * Mathf.PI * 4d));
                    detected = recognizer.PushSyntheticSample(new Vector3(x, 0f, 1f), false, false, time) ?? detected;
                }

                Assert.That(detected.HasValue, Is.False);
            });
        }

        [Test]
        public void LiftTendu_Detected_FastVerticalRise()
        {
            WithRecognizer((recognizer, _, _) =>
            {
                ConductorGestureEvent? detected = null;
                for (int i = 0; i < 20; i++)
                {
                    double time = i / 60d;
                    float y = i / 19f * 0.25f;
                    detected = recognizer.PushSyntheticSample(new Vector3(0f, y, 1f), false, false, time) ?? detected;
                }

                Assert.That(detected.HasValue, Is.True);
                Assert.That(detected.Value.kind, Is.EqualTo(ConductorGestureKind.LiftTendu));
            });
        }

        [Test]
        public void LiftTendu_RejectedWhenHorizontalDriftPresent()
        {
            WithRecognizer((recognizer, _, _) =>
            {
                ConductorGestureEvent? detected = null;
                for (int i = 0; i < 20; i++)
                {
                    double time = i / 60d;
                    float y = i / 19f * 0.25f;
                    float x = i / 19f * 0.20f;
                    detected = recognizer.PushSyntheticSample(new Vector3(x, y, 1f), false, false, time) ?? detected;
                }

                Assert.That(detected.HasValue, Is.False);
            });
        }

        [Test]
        public void FadePlie_Detected_FastVerticalDrop()
        {
            WithRecognizer((recognizer, _, _) =>
            {
                ConductorGestureEvent? detected = null;
                for (int i = 0; i < 20; i++)
                {
                    double time = i / 60d;
                    float y = 0.25f - i / 19f * 0.25f;
                    detected = recognizer.PushSyntheticSample(new Vector3(0f, y, 1f), false, false, time) ?? detected;
                }

                Assert.That(detected.HasValue, Is.True);
                Assert.That(detected.Value.kind, Is.EqualTo(ConductorGestureKind.FadePlie));
            });
        }

        [Test]
        public void CutOff_RequiresLeftGrip()
        {
            WithRecognizer((recognizer, _, _) =>
            {
                ConductorGestureEvent? withoutGrip = null;
                ConductorGestureEvent? withGrip = null;
                for (int i = 0; i < 10; i++)
                {
                    double time = i / 120d;
                    float x = i / 9f * 0.35f;
                    withoutGrip = recognizer.PushSyntheticSample(new Vector3(x, 0f, 1f), false, false, time) ?? withoutGrip;
                }

                recognizer.SetConductingMode(true);
                for (int i = 0; i < 10; i++)
                {
                    double time = 1d + i / 120d;
                    float x = i / 9f * 0.35f;
                    withGrip = recognizer.PushSyntheticSample(new Vector3(x, 0f, 1f), true, false, time) ?? withGrip;
                }

                Assert.That(withoutGrip.HasValue, Is.False);
                Assert.That(withGrip.HasValue, Is.True);
                Assert.That(withGrip.Value.kind, Is.EqualTo(ConductorGestureKind.CutOff));
            });
        }

        [Test]
        public void Recognizer_DisabledWhileDrawing()
        {
            WithRecognizer((recognizer, _, _) =>
            {
                ConductorGestureEvent? detected = null;
                for (int i = 0; i < 20; i++)
                {
                    double time = i / 60d;
                    float y = i / 19f * 0.25f;
                    detected = recognizer.PushSyntheticSample(new Vector3(0f, y, 1f), false, true, time) ?? detected;
                }

                Assert.That(detected.HasValue, Is.False);
            });
        }

        [Test]
        public void Recognizer_DisabledWhenConductingModeOff()
        {
            WithRecognizer((recognizer, _, _) =>
            {
                recognizer.SetConductingMode(false);
                ConductorGestureEvent? detected = null;
                for (int i = 0; i < 20; i++)
                {
                    double time = i / 60d;
                    float y = i / 19f * 0.25f;
                    detected = recognizer.PushSyntheticSample(new Vector3(0f, y, 1f), false, false, time) ?? detected;
                }

                Assert.That(detected.HasValue, Is.False);
            });
        }

        [Test]
        public void Refractory_Window_Prevents_Double_Fire()
        {
            WithRecognizer((recognizer, _, _) =>
            {
                int fireCount = 0;
                for (int pass = 0; pass < 2; pass++)
                {
                    for (int i = 0; i < 20; i++)
                    {
                        double time = pass * 0.35d + i / 60d;
                        float y = i / 19f * 0.25f;
                        var detected = recognizer.PushSyntheticSample(new Vector3(0f, y, 1f), false, false, time);
                        if (detected.HasValue)
                            fireCount++;
                    }
                }

                Assert.That(fireCount, Is.EqualTo(1));
            });
        }

        private static void WithRecognizer(System.Action<ConductorGestureRecognizer, SpatialZoneController, SessionStore> test)
        {
            var controllerGo = new GameObject("zones");
            var recognizerGo = new GameObject("recognizer");
            try
            {
                var store = new SessionStore();
                var controller = controllerGo.AddComponent<SpatialZoneController>();
                controller.Initialize(store, SpatialZoneLayout.CreateDefault(), null, null);

                var recognizer = recognizerGo.AddComponent<ConductorGestureRecognizer>();
                recognizer.Initialize(store, store.EventBus, null, null, controller);
                recognizer.SetConductingMode(true);

                test(recognizer, controller, store);
            }
            finally
            {
                Object.DestroyImmediate(recognizerGo);
                Object.DestroyImmediate(controllerGo);
            }
        }
    }
}
#endif
