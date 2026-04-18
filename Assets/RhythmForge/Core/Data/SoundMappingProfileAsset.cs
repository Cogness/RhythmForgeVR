using System;
using UnityEngine;
using RhythmForge.Core.Analysis;

namespace RhythmForge.Core.Data
{
    [CreateAssetMenu(menuName = "RhythmForge/Sound Mapping Profile")]
    public class SoundMappingProfileAsset : ScriptableObject
    {
        public PatternSoundMappingProfile rhythmLoop = PatternSoundMappingProfile.CreateRhythmDefaults();
        public PatternSoundMappingProfile melodyLine = PatternSoundMappingProfile.CreateMelodyDefaults();
        public PatternSoundMappingProfile harmonyPad = PatternSoundMappingProfile.CreateHarmonyDefaults();
    }

    [Serializable]
    public class PatternSoundMappingProfile
    {
        public SoundMetricWeights brightness = new SoundMetricWeights();
        public SoundMetricWeights resonance = new SoundMetricWeights();
        public SoundMetricWeights drive = new SoundMetricWeights();
        public SoundMetricWeights attackBias = new SoundMetricWeights();
        public SoundMetricWeights releaseBias = new SoundMetricWeights();
        public SoundMetricWeights detune = new SoundMetricWeights();
        public SoundMetricWeights modDepth = new SoundMetricWeights();
        public SoundMetricWeights stereoSpread = new SoundMetricWeights();
        public SoundMetricWeights grooveInstability = new SoundMetricWeights();
        public SoundMetricWeights delayBias = new SoundMetricWeights();
        public SoundMetricWeights reverbBias = new SoundMetricWeights();
        public SoundMetricWeights waveMorph = new SoundMetricWeights();
        public SoundMetricWeights filterMotion = new SoundMetricWeights();
        public SoundMetricWeights transientSharpness = new SoundMetricWeights();
        public SoundMetricWeights body = new SoundMetricWeights();

        public SoundProfile Evaluate(PatternType type, ShapeProfile shapeProfile)
        {
            var inputs = SoundMappingInputs.Create(type, shapeProfile);

            return new SoundProfile
            {
                brightness = Mathf.Clamp01(brightness.Evaluate(shapeProfile, inputs)),
                resonance = Mathf.Clamp01(resonance.Evaluate(shapeProfile, inputs)),
                drive = Mathf.Clamp01(drive.Evaluate(shapeProfile, inputs)),
                attackBias = Mathf.Clamp01(attackBias.Evaluate(shapeProfile, inputs)),
                releaseBias = Mathf.Clamp01(releaseBias.Evaluate(shapeProfile, inputs)),
                detune = Mathf.Clamp01(detune.Evaluate(shapeProfile, inputs)),
                modDepth = Mathf.Clamp01(modDepth.Evaluate(shapeProfile, inputs)),
                stereoSpread = Mathf.Clamp01(stereoSpread.Evaluate(shapeProfile, inputs)),
                grooveInstability = Mathf.Clamp01(grooveInstability.Evaluate(shapeProfile, inputs)),
                delayBias = Mathf.Clamp01(delayBias.Evaluate(shapeProfile, inputs)),
                reverbBias = Mathf.Clamp01(reverbBias.Evaluate(shapeProfile, inputs)),
                waveMorph = Mathf.Clamp01(waveMorph.Evaluate(shapeProfile, inputs)),
                filterMotion = Mathf.Clamp01(filterMotion.Evaluate(shapeProfile, inputs)),
                transientSharpness = Mathf.Clamp01(transientSharpness.Evaluate(shapeProfile, inputs)),
                body = Mathf.Clamp01(body.Evaluate(shapeProfile, inputs))
            };
        }

        public static PatternSoundMappingProfile CreateRhythmDefaults()
        {
            return new PatternSoundMappingProfile
            {
                brightness = new SoundMetricWeights { constant = 0.16f, angularity = 0.4f, instability = 0.14f, compactness = 0.24f },
                resonance = new SoundMetricWeights { constant = 0.14f, angularity = 0.26f, curvatureVariance = 0.18f, sizeFactor = 0.12f },
                drive = new SoundMetricWeights { constant = 0.1f, angularity = 0.52f, symmetryInverse = 0.18f, sizeFactor = 0.12f, pressureMean = 0.18f },
                attackBias = new SoundMetricWeights { constant = 0.26f, angularity = 0.54f, compactness = 0.24f, circularityInverse = 0.08f, speedMean = 0.20f },
                releaseBias = new SoundMetricWeights { constant = 0.1f, circularity = 0.34f, symmetry = 0.08f, sizeFactor = 0.54f },
                detune = new SoundMetricWeights { constant = 0.04f, instability = 0.22f, sizeFactor = 0.08f },
                modDepth = new SoundMetricWeights { constant = 0.06f, instability = 0.34f, sizeFactor = 0.18f },
                stereoSpread = new SoundMetricWeights { constant = 0.08f, aspectRatioInverse = 0.2f, symmetryInverse = 0.12f, sizeFactor = 0.26f },
                grooveInstability = new SoundMetricWeights { instability = 0.78f, sizeFactor = 0.26f },
                delayBias = new SoundMetricWeights { constant = 0.03f, instability = 0.14f, sizeFactor = 0.16f },
                reverbBias = new SoundMetricWeights { constant = 0.04f, circularity = 0.12f, symmetryInverse = 0.08f, sizeFactor = 0.48f },
                waveMorph = new SoundMetricWeights { constant = 0.18f, angularity = 0.58f, compactness = 0.08f },
                filterMotion = new SoundMetricWeights { constant = 0.08f, instability = 0.26f, sizeFactor = 0.18f },
                transientSharpness = new SoundMetricWeights { constant = 0.26f, angularity = 0.58f, compactness = 0.22f, pressurePeak = 0.14f },
                body = new SoundMetricWeights { constant = 0.12f, circularity = 0.28f, symmetry = 0.08f, sizeFactor = 0.62f }
            };
        }

        public static PatternSoundMappingProfile CreateMelodyDefaults()
        {
            return new PatternSoundMappingProfile
            {
                brightness = new SoundMetricWeights { constant = 0.18f, angularity = 0.28f, centroidHeight = 0.12f, verticalSpan = 0.08f, compactness = 0.32f },
                resonance = new SoundMetricWeights { constant = 0.14f, curvatureMean = 0.3f, curvatureVariance = 0.14f, compactness = 0.08f },
                drive = new SoundMetricWeights { constant = 0.06f, angularity = 0.28f, speedVariance = 0.12f, compactness = 0.12f, pressureMean = 0.14f },
                attackBias = new SoundMetricWeights { constant = 0.14f, speedVariance = 0.34f, angularity = 0.14f, compactness = 0.24f, speedMean = 0.16f },
                releaseBias = new SoundMetricWeights { constant = 0.1f, smoothness = 0.24f, horizontalSpan = 0.06f, sizeFactor = 0.56f, speedTailOff = 0.18f },
                detune = new SoundMetricWeights { constant = 0.06f, curvatureVariance = 0.28f, symmetryInverse = 0.14f, sizeFactor = 0.14f },
                modDepth = new SoundMetricWeights { constant = 0.08f, curvatureMean = 0.2f, contourPull = 0.18f, sizeFactor = 0.36f, tiltVariance = 0.24f },
                stereoSpread = new SoundMetricWeights { constant = 0.08f, horizontalSpan = 0.18f, contourPull = 0.08f, sizeFactor = 0.46f },
                grooveInstability = new SoundMetricWeights { constant = 0.06f, speedVariance = 0.22f, compactness = 0.06f },
                delayBias = new SoundMetricWeights { constant = 0.06f, contourPull = 0.18f, sizeFactor = 0.24f },
                reverbBias = new SoundMetricWeights { constant = 0.08f, smoothness = 0.16f, verticalSpan = 0.08f, sizeFactor = 0.4f },
                waveMorph = new SoundMetricWeights { constant = 0.14f, angularity = 0.5f, compactness = 0.08f },
                filterMotion = new SoundMetricWeights { constant = 0.1f, contourPull = 0.26f, curvatureVariance = 0.12f, sizeFactor = 0.34f, tiltMean = 0.22f },
                transientSharpness = new SoundMetricWeights { constant = 0.16f, angularity = 0.38f, speedVariance = 0.12f, compactness = 0.28f },
                body = new SoundMetricWeights { constant = 0.1f, smoothness = 0.2f, verticalSpan = 0.08f, sizeFactor = 0.58f }
            };
        }

        public static PatternSoundMappingProfile CreateHarmonyDefaults()
        {
            return new PatternSoundMappingProfile
            {
                brightness = new SoundMetricWeights { constant = 0.12f, centroidHeight = 0.18f, angularity = 0.12f, tiltSignedAbs = 0.12f, compactness = 0.26f },
                resonance = new SoundMetricWeights { constant = 0.08f, tiltSignedAbs = 0.3f, symmetryInverse = 0.14f, compactness = 0.08f, pressureMean = 0.10f },
                drive = new SoundMetricWeights { constant = 0.04f, angularity = 0.16f, symmetryInverse = 0.08f, compactness = 0.06f },
                attackBias = new SoundMetricWeights { constant = 0.1f, angularity = 0.24f, symmetryInverse = 0.08f, compactness = 0.22f },
                releaseBias = new SoundMetricWeights { constant = 0.16f, pathLength = 0.08f, smoothness = 0.16f, sizeFactor = 0.58f, speedTailOff = 0.22f },
                detune = new SoundMetricWeights { constant = 0.08f, symmetryInverse = 0.26f, horizontalSpan = 0.08f, sizeFactor = 0.42f },
                modDepth = new SoundMetricWeights { constant = 0.12f, tiltSignedAbs = 0.22f, symmetryInverse = 0.16f, sizeFactor = 0.36f, tiltVariance = 0.20f },
                stereoSpread = new SoundMetricWeights { constant = 0.14f, horizontalSpan = 0.16f, sizeFactor = 0.56f },
                grooveInstability = new SoundMetricWeights { constant = 0.02f, curvatureVariance = 0.12f },
                delayBias = new SoundMetricWeights { constant = 0.06f, tiltSignedAbs = 0.1f, sizeFactor = 0.16f },
                reverbBias = new SoundMetricWeights { constant = 0.12f, pathLength = 0.08f, smoothness = 0.12f, sizeFactor = 0.58f },
                waveMorph = new SoundMetricWeights { constant = 0.18f, angularity = 0.22f, tiltSignedAbs = 0.1f, sizeFactor = 0.08f },
                filterMotion = new SoundMetricWeights { constant = 0.16f, tiltSignedAbs = 0.3f, symmetryInverse = 0.12f, sizeFactor = 0.4f, tiltMean = 0.28f },
                transientSharpness = new SoundMetricWeights { constant = 0.06f, angularity = 0.22f, compactness = 0.12f },
                body = new SoundMetricWeights { constant = 0.14f, smoothness = 0.18f, verticalSpan = 0.06f, sizeFactor = 0.66f }
            };
        }
    }

    [Serializable]
    public class SoundMetricWeights
    {
        public float constant;
        public float closedness;
        public float circularity;
        public float circularityInverse;
        public float aspectRatio;
        public float aspectRatioInverse;
        public float angularity;
        public float symmetry;
        public float symmetryInverse;
        public float verticalSpan;
        public float horizontalSpan;
        public float pathLength;
        public float speedVariance;
        public float curvatureMean;
        public float curvatureVariance;
        public float centroidHeight;
        public float directionBias;
        public float directionBiasCentered;
        public float tilt;
        public float tiltSignedAbs;
        public float wobble;
        public float sizeFactor;
        public float compactness;
        public float instability;
        public float smoothness;
        public float contourPull;

        // Kinematic weights (Phase 1 — Pen-as-Instrument)
        public float pressureMean;
        public float pressurePeak;
        public float tiltMean;
        public float tiltVariance;
        public float speedMean;
        public float speedTailOff;

        public float Evaluate(ShapeProfile shapeProfile, SoundMappingInputs inputs)
        {
            shapeProfile = shapeProfile ?? new ShapeProfile();

            return constant
                + closedness * shapeProfile.closedness
                + circularity * shapeProfile.circularity
                + circularityInverse * inputs.circularityInverse
                + aspectRatio * shapeProfile.aspectRatio
                + aspectRatioInverse * inputs.aspectRatioInverse
                + angularity * shapeProfile.angularity
                + symmetry * shapeProfile.symmetry
                + symmetryInverse * inputs.symmetryInverse
                + verticalSpan * shapeProfile.verticalSpan
                + horizontalSpan * shapeProfile.horizontalSpan
                + pathLength * shapeProfile.pathLength
                + speedVariance * shapeProfile.speedVariance
                + curvatureMean * shapeProfile.curvatureMean
                + curvatureVariance * shapeProfile.curvatureVariance
                + centroidHeight * shapeProfile.centroidHeight
                + directionBias * shapeProfile.directionBias
                + directionBiasCentered * inputs.directionBiasCentered
                + tilt * shapeProfile.tilt
                + tiltSignedAbs * inputs.tiltSignedAbs
                + wobble * shapeProfile.wobble
                + sizeFactor * inputs.sizeFactor
                + compactness * inputs.compactness
                + instability * inputs.instability
                + smoothness * inputs.smoothness
                + contourPull * inputs.contourPull
                // Kinematic contributions (Phase 1 — Pen-as-Instrument)
                + pressureMean * shapeProfile.pressureMean
                + pressurePeak * shapeProfile.pressurePeak
                + tiltMean * shapeProfile.tiltMean
                + tiltVariance * shapeProfile.tiltVariance
                + speedMean * shapeProfile.speedMean
                + speedTailOff * shapeProfile.speedTailOff;
        }
    }

    public static class SoundMappingProfiles
    {
        public static void SetActiveProfile(SoundMappingProfileAsset profile)
        {
            SoundMappingProfileRuntime.SetActiveProfile(profile);
        }

        public static PatternSoundMappingProfile Get(PatternType type)
        {
            // If an asset override is set, use it; otherwise delegate to active genre
            var fromRuntime = SoundMappingProfileRuntime.GetFromAssetOnly(type);
            if (fromRuntime != null)
                return fromRuntime;
            return GenreRegistry.GetActive().GetSoundMapping(type);
        }
    }

    internal static class SoundMappingProfileRuntime
    {
        private const string ResourcePath = "RhythmForge/SoundMappingProfile";

        private static readonly PatternSoundMappingProfile DefaultRhythm = PatternSoundMappingProfile.CreateRhythmDefaults();
        private static readonly PatternSoundMappingProfile DefaultMelody = PatternSoundMappingProfile.CreateMelodyDefaults();
        private static readonly PatternSoundMappingProfile DefaultHarmony = PatternSoundMappingProfile.CreateHarmonyDefaults();

        private static SoundMappingProfileAsset _activeProfile;
        private static SoundMappingProfileAsset _resourceProfile;

        public static void SetActiveProfile(SoundMappingProfileAsset profile)
        {
            _activeProfile = profile;
            _resourceProfile = null;
        }

        public static PatternSoundMappingProfile Get(PatternType type)
        {
            var profile = ResolveProfile();

            switch (type)
            {
                case PatternType.MelodyLine:
                    return profile?.melodyLine ?? DefaultMelody;

                case PatternType.HarmonyPad:
                    return profile?.harmonyPad ?? DefaultHarmony;

                default:
                    return profile?.rhythmLoop ?? DefaultRhythm;
            }
        }

        /// <summary>Returns a mapping from the loaded asset only, or null if no asset is active.</summary>
        public static PatternSoundMappingProfile GetFromAssetOnly(PatternType type)
        {
            var profile = ResolveProfile();
            if (profile == null) return null;

            switch (type)
            {
                case PatternType.MelodyLine: return profile.melodyLine;
                case PatternType.HarmonyPad: return profile.harmonyPad;
                default:                     return profile.rhythmLoop;
            }
        }

        private static SoundMappingProfileAsset ResolveProfile()
        {
            if (_activeProfile != null)
                return _activeProfile;

            if (_resourceProfile == null)
                _resourceProfile = Resources.Load<SoundMappingProfileAsset>(ResourcePath);

            return _resourceProfile;
        }
    }

    public struct SoundMappingInputs
    {
        public float aspectRatioInverse;
        public float circularityInverse;
        public float symmetryInverse;
        public float directionBiasCentered;
        public float tiltSignedAbs;
        public float sizeFactor;
        public float compactness;
        public float instability;
        public float smoothness;
        public float contourPull;

        public static SoundMappingInputs Create(PatternType type, ShapeProfile shapeProfile)
        {
            shapeProfile = shapeProfile ?? new ShapeProfile();

            float symmetryInverse = 1f - shapeProfile.symmetry;
            float sizeFactor = ShapeProfileSizing.GetSizeFactor(type, shapeProfile);

            return new SoundMappingInputs
            {
                aspectRatioInverse = 1f - shapeProfile.aspectRatio,
                circularityInverse = 1f - shapeProfile.circularity,
                symmetryInverse = symmetryInverse,
                directionBiasCentered = Mathf.Abs(shapeProfile.directionBias - 0.5f) * 2f,
                tiltSignedAbs = Mathf.Abs(shapeProfile.tiltSigned),
                sizeFactor = sizeFactor,
                compactness = 1f - sizeFactor,
                instability = Mathf.Clamp01(shapeProfile.wobble * 0.7f + symmetryInverse * 0.55f + shapeProfile.curvatureVariance * 0.35f),
                smoothness = 1f - shapeProfile.angularity,
                contourPull = Mathf.Abs(shapeProfile.directionBias - 0.5f) * 2f
            };
        }
    }
}
