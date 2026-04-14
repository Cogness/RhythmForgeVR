using System;
using UnityEngine;
using RhythmForge.Core.PatternBehavior;
using RhythmForge.Sequencer;

namespace RhythmForge.Core.Data
{
    [CreateAssetMenu(menuName = "RhythmForge/Visual Grammar Profile")]
    public class VisualGrammarProfileAsset : ScriptableObject
    {
        public PatternColorPalette typeColors = PatternColorPalette.CreateDefaults();
        public UIPalette ui = UIPalette.CreateDefaults();
        public PlaybackVisualBaseProfile playbackBase = PlaybackVisualBaseProfile.CreateDefaults();
        public RhythmLoopVisualProfile rhythmLoop = RhythmLoopVisualProfile.CreateDefaults();
        public MelodyLineVisualProfile melodyLine = MelodyLineVisualProfile.CreateDefaults();
        public HarmonyPadVisualProfile harmonyPad = HarmonyPadVisualProfile.CreateDefaults();
    }

    [Serializable]
    public class PatternColorPalette
    {
        public Color rhythmLoop = ProfileColorUtility.HexColor("#51d7ff");
        public Color melodyLine = ProfileColorUtility.HexColor("#f7c975");
        public Color harmonyPad = ProfileColorUtility.HexColor("#62f3d3");

        public Color Get(PatternType type)
        {
            switch (type)
            {
                case PatternType.MelodyLine: return melodyLine;
                case PatternType.HarmonyPad: return harmonyPad;
                default: return rhythmLoop;
            }
        }

        public static PatternColorPalette CreateDefaults()
        {
            return new PatternColorPalette();
        }
    }

    [Serializable]
    public class UIPalette
    {
        public Color panelBg = new Color(0.08f, 0.08f, 0.12f, 0.88f);
        public Color buttonDefault = new Color(0.18f, 0.18f, 0.24f, 1f);
        public Color buttonActive = new Color(0.24f, 0.72f, 0.96f, 1f);
        public Color buttonDanger = new Color(0.80f, 0.22f, 0.22f, 1f);

        public static UIPalette CreateDefaults()
        {
            return new UIPalette();
        }
    }

    [Serializable]
    public class PlaybackVisualBaseProfile
    {
        public float brightnessBase = 0.18f;
        public float brightnessWeight = 0.82f;
        public float thicknessBase = 0.18f;
        public float thicknessWeight = 0.82f;
        public float decayMin = 0.16f;
        public float decayMax = 1.28f;
        public float motionAmplitudeModDepth = 0.55f;
        public float motionAmplitudeFilterMotion = 0.45f;
        public float motionSpeedMin = 0.35f;
        public float motionSpeedMax = 1.6f;
        public float motionSpeedFilterMotionWeight = 0.5f;
        public float motionSpeedModDepthWeight = 0.5f;
        public float phaseJitterGrooveWeight = 0.7f;
        public float phaseJitterFilterMotionWeight = 0.3f;
        public float markerScaleBase = 0.24f;
        public float markerScaleBodyWeight = 0.34f;
        public float markerScaleBrightnessWeight = 0.18f;
        public float markerScaleTransientWeight = 0.24f;
        public float haloStrengthStereoWeight = 0.38f;
        public float haloStrengthReverbWeight = 0.36f;
        public float haloStrengthBodyWeight = 0.26f;
        public float secondaryStrengthModDepthWeight = 0.32f;
        public float secondaryStrengthFilterMotionWeight = 0.24f;
        public float secondaryStrengthReverbWeight = 0.18f;
        public float secondaryStrengthReleaseWeight = 0.26f;
        public float sharpnessBase = 0.14f;
        public float sharpnessTransientWeight = 0.86f;

        public PlaybackVisualSpec Build(PatternType type, SoundProfile soundProfile)
        {
            soundProfile = soundProfile ?? new SoundProfile();

            return new PlaybackVisualSpec
            {
                type = type,
                brightness = Mathf.Clamp01(brightnessBase + soundProfile.brightness * brightnessWeight),
                thickness = Mathf.Clamp01(thicknessBase + soundProfile.body * thicknessWeight),
                decaySeconds = Mathf.Lerp(decayMin, decayMax, soundProfile.releaseBias),
                motionAmplitude = Mathf.Clamp01(soundProfile.modDepth * motionAmplitudeModDepth + soundProfile.filterMotion * motionAmplitudeFilterMotion),
                motionSpeed = Mathf.Lerp(
                    motionSpeedMin,
                    motionSpeedMax,
                    soundProfile.filterMotion * motionSpeedFilterMotionWeight + soundProfile.modDepth * motionSpeedModDepthWeight),
                phaseJitter = Mathf.Clamp01(soundProfile.grooveInstability * phaseJitterGrooveWeight + soundProfile.filterMotion * phaseJitterFilterMotionWeight),
                markerScale = Mathf.Clamp01(
                    markerScaleBase
                    + soundProfile.body * markerScaleBodyWeight
                    + soundProfile.brightness * markerScaleBrightnessWeight
                    + soundProfile.transientSharpness * markerScaleTransientWeight),
                haloStrength = Mathf.Clamp01(
                    soundProfile.stereoSpread * haloStrengthStereoWeight
                    + soundProfile.reverbBias * haloStrengthReverbWeight
                    + soundProfile.body * haloStrengthBodyWeight),
                secondaryStrength = Mathf.Clamp01(
                    soundProfile.modDepth * secondaryStrengthModDepthWeight
                    + soundProfile.filterMotion * secondaryStrengthFilterMotionWeight
                    + soundProfile.reverbBias * secondaryStrengthReverbWeight
                    + soundProfile.releaseBias * secondaryStrengthReleaseWeight),
                sharpness = Mathf.Clamp01(sharpnessBase + soundProfile.transientSharpness * sharpnessTransientWeight)
            };
        }

        public static PlaybackVisualBaseProfile CreateDefaults()
        {
            return new PlaybackVisualBaseProfile();
        }
    }

    [Serializable]
    public class RhythmLoopVisualProfile
    {
        public float markerScaleAdd = 0.12f;
        public float haloStrengthBaseScale = 0.55f;
        public float haloStrengthBodyWeight = 0.08f;
        public float secondaryStrengthBaseScale = 0.42f;
        public float secondaryStrengthGrooveWeight = 0.28f;
        public float phaseJitterGrooveWeight = 0.22f;
        public float motionSpeedMin = 0.85f;
        public float motionSpeedMax = 1.9f;
        public float motionSpeedGrooveWeight = 0.45f;
        public float motionSpeedTransientWeight = 0.55f;
        public float lineEnergyPulseWeight = 0.72f;
        public float lineEnergySustainWeight = 0.22f;
        public float haloEnergyPulseWeight = 0.55f;
        public float haloEnergySustainWeight = 0.18f;
        public float markerMinimum = 0.15f;
        public float markerSustainWeight = 0.3f;
        public float markerPhaseBaseSpeed = 1.6f;
        public float markerPhaseMotionSpeedWeight = 0.8f;
        public float markerPhaseJitterScale = 0.015f;
        public float markerScaleBase = 0.72f;
        public float markerScalePulseWeight = 0.84f;
        public float markerScaleSustainWeight = 0.2f;
        public float normalOffsetBaseSpeed = 2.2f;
        public float normalOffsetMotionSpeedWeight = 1f;
        public float normalOffsetHeightWeight = 0.02f;

        public PlaybackVisualSpec Apply(PlaybackVisualSpec spec, SoundProfile soundProfile)
        {
            soundProfile = soundProfile ?? new SoundProfile();
            spec.markerScale = Mathf.Clamp01(spec.markerScale + markerScaleAdd);
            spec.haloStrength = Mathf.Clamp01(spec.haloStrength * haloStrengthBaseScale + soundProfile.body * haloStrengthBodyWeight);
            spec.secondaryStrength = Mathf.Clamp01(spec.secondaryStrength * secondaryStrengthBaseScale + soundProfile.grooveInstability * secondaryStrengthGrooveWeight);
            spec.phaseJitter = Mathf.Clamp01(spec.phaseJitter + soundProfile.grooveInstability * phaseJitterGrooveWeight);
            spec.motionSpeed = Mathf.Lerp(
                motionSpeedMin,
                motionSpeedMax,
                soundProfile.grooveInstability * motionSpeedGrooveWeight + soundProfile.transientSharpness * motionSpeedTransientWeight);
            return spec;
        }

        public AnimationEnergies Animate(PatternPlaybackVisualState state, float pulse, float sustain, float renderedHeight, float timeSeconds)
        {
            return new AnimationEnergies
            {
                lineEnergy = pulse * lineEnergyPulseWeight + sustain * lineEnergySustainWeight,
                haloEnergy = pulse * haloEnergyPulseWeight + sustain * haloEnergySustainWeight,
                markerEnergy = state.phase >= 0f ? Mathf.Max(markerMinimum, pulse, sustain * markerSustainWeight) : 0f,
                markerPhase = state.phase >= 0f
                    ? Mathf.Repeat(
                        state.phase + Mathf.Sin(timeSeconds * (markerPhaseBaseSpeed + state.visualSpec.motionSpeed * markerPhaseMotionSpeedWeight)) * state.visualSpec.phaseJitter * markerPhaseJitterScale,
                        1f)
                    : -1f,
                markerScale = state.visualSpec.markerScale * (markerScaleBase + pulse * markerScalePulseWeight + sustain * markerScaleSustainWeight),
                normalOffset = Mathf.Sin(timeSeconds * (normalOffsetBaseSpeed + state.visualSpec.motionSpeed * normalOffsetMotionSpeedWeight))
                    * renderedHeight
                    * normalOffsetHeightWeight
                    * state.visualSpec.secondaryStrength,
                haloBreath = 1f,
                extraLineWidth = 0f
            };
        }

        public static RhythmLoopVisualProfile CreateDefaults()
        {
            return new RhythmLoopVisualProfile();
        }
    }

    [Serializable]
    public class MelodyLineVisualProfile
    {
        public float markerScaleAdd = 0.04f;
        public float haloStrengthBaseScale = 0.72f;
        public float haloStrengthReleaseWeight = 0.16f;
        public float secondaryStrengthModWeight = 0.2f;
        public float motionSpeedMin = 0.55f;
        public float motionSpeedMax = 1.3f;
        public float motionSpeedFilterWeight = 0.4f;
        public float motionSpeedModWeight = 0.6f;
        public float lineEnergyPulseWeight = 0.36f;
        public float lineEnergySustainWeight = 0.46f;
        public float haloEnergyPulseWeight = 0.3f;
        public float haloEnergySustainWeight = 0.54f;
        public float markerPulseWeight = 0.95f;
        public float markerSustainWeight = 0.85f;
        public float markerScaleBase = 0.6f;
        public float markerScalePulseWeight = 0.56f;
        public float markerScaleSustainWeight = 0.34f;
        public float normalOffsetBaseSpeed = 1.2f;
        public float normalOffsetMotionSpeedWeight = 2.4f;
        public float normalOffsetHeightWeight = 0.12f;
        public float haloBreathBase = 1f;
        public float haloBreathBaseSpeed = 1.1f;
        public float haloBreathMotionSpeedWeight = 1.4f;
        public float haloBreathAmplitude = 0.06f;

        public PlaybackVisualSpec Apply(PlaybackVisualSpec spec, SoundProfile soundProfile)
        {
            soundProfile = soundProfile ?? new SoundProfile();
            spec.markerScale = Mathf.Clamp01(spec.markerScale + markerScaleAdd);
            spec.haloStrength = Mathf.Clamp01(spec.haloStrength * haloStrengthBaseScale + soundProfile.releaseBias * haloStrengthReleaseWeight);
            spec.secondaryStrength = Mathf.Clamp01(spec.secondaryStrength + soundProfile.modDepth * secondaryStrengthModWeight);
            spec.motionSpeed = Mathf.Lerp(
                motionSpeedMin,
                motionSpeedMax,
                soundProfile.filterMotion * motionSpeedFilterWeight + soundProfile.modDepth * motionSpeedModWeight);
            return spec;
        }

        public AnimationEnergies Animate(PatternPlaybackVisualState state, float pulse, float sustain, float renderedHeight, float timeSeconds)
        {
            return new AnimationEnergies
            {
                lineEnergy = pulse * lineEnergyPulseWeight + sustain * lineEnergySustainWeight,
                haloEnergy = pulse * haloEnergyPulseWeight + sustain * haloEnergySustainWeight,
                markerEnergy = Mathf.Max(pulse * markerPulseWeight, sustain * markerSustainWeight),
                markerPhase = state.phase,
                markerScale = state.visualSpec.markerScale * (markerScaleBase + pulse * markerScalePulseWeight + sustain * markerScaleSustainWeight),
                normalOffset = Mathf.Sin(timeSeconds * (normalOffsetBaseSpeed + state.visualSpec.motionSpeed * normalOffsetMotionSpeedWeight))
                    * renderedHeight
                    * normalOffsetHeightWeight
                    * state.visualSpec.motionAmplitude,
                haloBreath = haloBreathBase + Mathf.Sin(timeSeconds * (haloBreathBaseSpeed + state.visualSpec.motionSpeed * haloBreathMotionSpeedWeight))
                    * haloBreathAmplitude
                    * state.visualSpec.secondaryStrength,
                extraLineWidth = 0f
            };
        }

        public static MelodyLineVisualProfile CreateDefaults()
        {
            return new MelodyLineVisualProfile();
        }
    }

    [Serializable]
    public class HarmonyPadVisualProfile
    {
        public float markerScaleBaseScale = 0.72f;
        public float markerScaleBodyWeight = 0.08f;
        public float haloStrengthStereoWeight = 0.2f;
        public float haloStrengthReverbWeight = 0.18f;
        public float secondaryStrengthReleaseWeight = 0.2f;
        public float secondaryStrengthModWeight = 0.16f;
        public float motionSpeedMin = 0.2f;
        public float motionSpeedMax = 0.72f;
        public float motionSpeedFilterWeight = 0.45f;
        public float motionSpeedModWeight = 0.55f;
        public float phaseJitterScale = 0.35f;
        public float lineEnergyPulseWeight = 0.16f;
        public float lineEnergySustainWeight = 0.5f;
        public float haloEnergySustainWeight = 0.92f;
        public float haloEnergyActiveMinimum = 0.18f;
        public float haloEnergyPulseWeight = 0.14f;
        public float markerSustainWeight = 0.42f;
        public float markerPulseWeight = 0.18f;
        public float markerPhaseScale = 0.35f;
        public float markerPhaseBaseSpeed = 0.04f;
        public float markerPhaseMotionSpeedWeight = 0.08f;
        public float markerScaleBase = 0.52f;
        public float markerScaleSustainWeight = 0.42f;
        public float normalOffsetBaseSpeed = 0.8f;
        public float normalOffsetMotionSpeedWeight = 1.2f;
        public float normalOffsetHeightWeight = 0.08f;
        public float normalOffsetAmplitudeBase = 0.3f;
        public float normalOffsetAmplitudeMotionWeight = 0.7f;
        public float haloBreathBase = 1f;
        public float haloBreathBaseSpeed = 0.9f;
        public float haloBreathMotionSpeedWeight = 0.6f;
        public float haloBreathAmplitude = 0.12f;

        public PlaybackVisualSpec Apply(PlaybackVisualSpec spec, SoundProfile soundProfile)
        {
            soundProfile = soundProfile ?? new SoundProfile();
            spec.markerScale = Mathf.Clamp01(spec.markerScale * markerScaleBaseScale + soundProfile.body * markerScaleBodyWeight);
            spec.haloStrength = Mathf.Clamp01(spec.haloStrength + soundProfile.stereoSpread * haloStrengthStereoWeight + soundProfile.reverbBias * haloStrengthReverbWeight);
            spec.secondaryStrength = Mathf.Clamp01(spec.secondaryStrength + soundProfile.releaseBias * secondaryStrengthReleaseWeight + soundProfile.modDepth * secondaryStrengthModWeight);
            spec.motionSpeed = Mathf.Lerp(
                motionSpeedMin,
                motionSpeedMax,
                soundProfile.filterMotion * motionSpeedFilterWeight + soundProfile.modDepth * motionSpeedModWeight);
            spec.phaseJitter = Mathf.Clamp01(spec.phaseJitter * phaseJitterScale);
            return spec;
        }

        public AnimationEnergies Animate(PatternPlaybackVisualState state, float pulse, float sustain, float renderedHeight, float timeSeconds)
        {
            float haloEnergy = Mathf.Max(sustain * haloEnergySustainWeight, state.isActive ? haloEnergyActiveMinimum : 0f) + pulse * haloEnergyPulseWeight;
            return new AnimationEnergies
            {
                lineEnergy = pulse * lineEnergyPulseWeight + sustain * lineEnergySustainWeight,
                haloEnergy = haloEnergy,
                markerEnergy = Mathf.Max(sustain * markerSustainWeight, pulse * markerPulseWeight),
                markerPhase = state.phase >= 0f
                    ? Mathf.Repeat(state.phase * markerPhaseScale + timeSeconds * (markerPhaseBaseSpeed + state.visualSpec.motionSpeed * markerPhaseMotionSpeedWeight), 1f)
                    : -1f,
                markerScale = state.visualSpec.markerScale * (markerScaleBase + sustain * markerScaleSustainWeight),
                normalOffset = Mathf.Sin(timeSeconds * (normalOffsetBaseSpeed + state.visualSpec.motionSpeed * normalOffsetMotionSpeedWeight))
                    * renderedHeight
                    * normalOffsetHeightWeight
                    * (normalOffsetAmplitudeBase + state.visualSpec.motionAmplitude * normalOffsetAmplitudeMotionWeight),
                haloBreath = haloBreathBase + Mathf.Sin(timeSeconds * (haloBreathBaseSpeed + state.visualSpec.motionSpeed * haloBreathMotionSpeedWeight))
                    * haloBreathAmplitude
                    * state.visualSpec.motionAmplitude,
                extraLineWidth = haloEnergy * 0.0012f
            };
        }

        public static HarmonyPadVisualProfile CreateDefaults()
        {
            return new HarmonyPadVisualProfile();
        }
    }

    public static class VisualGrammarProfiles
    {
        public static void SetActiveProfile(VisualGrammarProfileAsset profile)
        {
            VisualGrammarProfileRuntime.SetActiveProfile(profile);
        }

        public static Color GetTypeColor(PatternType type)
        {
            return VisualGrammarProfileRuntime.GetTypeColors().Get(type);
        }

        public static UIPalette GetUI()
        {
            return VisualGrammarProfileRuntime.GetUI();
        }

        public static PlaybackVisualBaseProfile GetPlaybackBase()
        {
            return VisualGrammarProfileRuntime.GetPlaybackBase();
        }

        public static RhythmLoopVisualProfile GetRhythmLoop()
        {
            return VisualGrammarProfileRuntime.GetRhythmLoop();
        }

        public static MelodyLineVisualProfile GetMelodyLine()
        {
            return VisualGrammarProfileRuntime.GetMelodyLine();
        }

        public static HarmonyPadVisualProfile GetHarmonyPad()
        {
            return VisualGrammarProfileRuntime.GetHarmonyPad();
        }
    }

    internal static class VisualGrammarProfileRuntime
    {
        private const string ResourcePath = "RhythmForge/VisualGrammarProfile";

        private static readonly PatternColorPalette DefaultTypeColors = PatternColorPalette.CreateDefaults();
        private static readonly UIPalette DefaultUI = UIPalette.CreateDefaults();
        private static readonly PlaybackVisualBaseProfile DefaultPlaybackBase = PlaybackVisualBaseProfile.CreateDefaults();
        private static readonly RhythmLoopVisualProfile DefaultRhythmLoop = RhythmLoopVisualProfile.CreateDefaults();
        private static readonly MelodyLineVisualProfile DefaultMelodyLine = MelodyLineVisualProfile.CreateDefaults();
        private static readonly HarmonyPadVisualProfile DefaultHarmonyPad = HarmonyPadVisualProfile.CreateDefaults();

        private static VisualGrammarProfileAsset _activeProfile;
        private static VisualGrammarProfileAsset _resourceProfile;

        public static void SetActiveProfile(VisualGrammarProfileAsset profile)
        {
            _activeProfile = profile;
            _resourceProfile = null;
        }

        public static PatternColorPalette GetTypeColors()
        {
            // If a ScriptableObject override is set, use it; otherwise use active genre colors
            var profile = ResolveProfile();
            if (profile != null)
                return profile.typeColors;
            return GenreRegistry.GetActive().ColorPalette;
        }

        public static UIPalette GetUI()
        {
            return ResolveProfile()?.ui ?? DefaultUI;
        }

        public static PlaybackVisualBaseProfile GetPlaybackBase()
        {
            return ResolveProfile()?.playbackBase ?? DefaultPlaybackBase;
        }

        public static RhythmLoopVisualProfile GetRhythmLoop()
        {
            return ResolveProfile()?.rhythmLoop ?? DefaultRhythmLoop;
        }

        public static MelodyLineVisualProfile GetMelodyLine()
        {
            return ResolveProfile()?.melodyLine ?? DefaultMelodyLine;
        }

        public static HarmonyPadVisualProfile GetHarmonyPad()
        {
            return ResolveProfile()?.harmonyPad ?? DefaultHarmonyPad;
        }

        private static VisualGrammarProfileAsset ResolveProfile()
        {
            if (_activeProfile != null)
                return _activeProfile;

            if (_resourceProfile == null)
                _resourceProfile = Resources.Load<VisualGrammarProfileAsset>(ResourcePath);

            return _resourceProfile;
        }
    }

    internal static class ProfileColorUtility
    {
        public static Color HexColor(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var color);
            return color;
        }
    }
}
