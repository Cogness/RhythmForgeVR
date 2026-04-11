# Comprehensive Plan: RhythmForge VR — Pilot → Unity 3D Adaptation

## Overview

This plan adapts the **RhythmForge Pilot** (2D web implementation) to a **Meta Quest 3 VR experience** using the **Logitech MX Ink** stylus. The core concept is preserved: **geometry-driven music composition** where drawn shapes become reusable, expressive musical patterns.

---

## I. Core Concept Preservation

### Must Preserve (Non-Negotiable)

| Concept | Pilot (2D) | VR Target |
|---------|----------|-----------|
| **Pattern-first composition** | Mouse-drawn strokes | MX Ink stylus strokes in 3D space |
| **3 Pattern Types** | RhythmLoop (closed), MelodyLine (open curve), HarmonyPad (broad stroke) | Same types, drawn in 3D air |
| **Master + Instance model** | Patterns are masters; scenes contain instances | Identical data model |
| **Geometry = DNA** | Shape analysis → sound profile | Same math, world-space thresholds |
| **Scene-based arrangement** | Scenes A-D + 8 arrangement slots | Identical system |
| **Instrument Groups** | Lo-Fi Kit, Trap Kit, Dream Ensemble | Same groups + presets |

### VR-Specific Adaptations

| Pilot (2D Desktop) | VR Target |
|---|---|
| Mouse/touchpad draw → | **MX Ink stylus draw** (pressure = line width + velocity) |
| 2D canvas (x,y) → | **3D volume** (x,y,z) — drawing in air |
| Drag cards on screen → | **Grab 3D objects** (left hand direct manipulation) |
| Depth slider → | **Z-position** in 3D space (depth = distance from user) |
| Left dock panel → | **Wrist/floating radial menu** (left hand) |
| Right inspector panel → | **Floating panel attached to object** or **wrist menu page** |
| Bottom scene strip → | **Floating horizontal strip** in front of user |

---

## II. VR Interaction Model

### Input Mapping

| Hand | Device | Primary Function |
|------|--------|-----------------|
| **Right** | **MX Ink Stylus** | **Drawing tool** — the musical instrument |
| **Left** | **Quest Controller** | **UI/Navigation** — menus, grab, scene switching |

### MX Ink Specific Integration

The existing [VrStylusHandler.cs](cci:7://file:///Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/Logitech/Scripts/VrStylusHandler.cs:0:0-0:0) already exposes:

```csharp
// From existing Logitech SDK integration
public struct StylusInputs {
    public float tip_value;           // PRESSURE (0-1) → line width, note velocity
    public bool cluster_front_value;  // Button → preset switch / commit confirm
    public float cluster_middle_value; // Middle pressure (alternate input)
    public bool cluster_back_value;   // Button → undo / mode switch
    public Pose inkingPose;           // Position + rotation in 3D space
    public bool cluster_back_double_tap_value; // Double-tap → clear all
}
```

**Drawing Mechanics:**
- `tip_value > threshold` → Start/end stroke
- Pressure → Line width (thicker = more pressure)
- Pressure → Note velocity on commit (louder = harder press)
- Button press while drawing → **Mode toggle** (RhythmLoop → MelodyLine → HarmonyPad)

### Left Hand (Quest Controller) Functions

| Button/Gesture | Action |
|----------------|--------|
| **Thumbstick Left/Right** | Switch active Scene (A→B→C→D) |
| **Thumbstick Up** | Open **Instrument Group** radial menu |
| **Thumbstick Down** | Open **Pattern Library** radial menu |
| **Trigger (hold)** | **Grab mode** — raycast to select/drag pattern instances |
| **Trigger (click on empty space)** | Summon commit card at that location |
| **A/B buttons** | Play/Stop transport |

### 3D Spatial Interactions

| Concept | Implementation |
|---------|----------------|
| **Drawing volume** | 2m × 1.5m × 1m box centered at user chest height |
| **Pattern instances** | Floating 3D wireframe objects that pulse with playback |
| **Instance position** | X/Y = stereo pan; Z (depth) = volume + reverb |
| **Grabbing** | Raycast from left controller, pull trigger to grab, move in 3D |
| **Selection** | Point left controller at instance → highlight → click to inspect |

---

## III. Data Model (Preserve Exactly from Pilot)

The C# structs mirror the TypeScript interfaces:

```csharp
public enum PatternType { RhythmLoop, MelodyLine, HarmonyPad }

[System.Serializable]
public class PatternDefinition {
    public string id;
    public PatternType type;
    public string name;
    public int bars;
    public float tempoBase;
    public string key;
    public string groupId;
    public string presetId;
    public List<Vector2> points;              // Normalized 0-1 coordinates
    public RhythmSeq derivedRhythm;           // or MelodySeq, HarmonySeq
    public List<string> tags;
    public Color color;
    public ShapeProfile shapeProfile;
    public SoundProfile soundProfile;
    public string shapeSummary;
}

[System.Serializable]
public class PatternInstance {
    public string id;
    public string patternId;        // Reference to master
    public string sceneId;
    public Vector3 position;        // 3D world position
    public float depth;             // 0-1 normalized from user
    public string presetOverrideId;
    public bool muted;
    public float gain;
    public float pan;
    public float brightness;
}

[System.Serializable]
public class Scene {
    public string id;
    public string name;
    public List<string> instanceIds;
    public float? tempoOverride;
    public string keyOverride;
}

[System.Serializable]
public class ArrangementSlot {
    public string id;
    public string sceneId;
    public int bars;  // 4, 8, or 16
}
```

---

## IV. Geometry & Shape Analysis (Algorithm Port)

All shape analysis algorithms from the Pilot **must be preserved exactly**. The math is deterministic and geometry-driven.

### World-Space Thresholds (Replace Pixel Values)

| Pilot (pixels) | VR (meters) |
|----------------|-------------|
| `averageSize > 240` (4 bars rhythm) | `0.3f` world units |
| `strokeLength > 880` (4 bars melody) | `0.8f` world units |
| `strokeLength > 1200` (8 bars harmony) | `1.2f` world units |
| `distance(first,last) < 64` (closed check) | `0.08f` world units |

### ShapeProfile (17 metrics) — Direct Port

```csharp
public class ShapeProfile {
    public float closedness;
    public float circularity;
    public float aspectRatio;
    public float angularity;
    public float symmetry;
    public float verticalSpan;
    public float horizontalSpan;
    public float pathLength;
    public float speedVariance;
    public float curvatureMean;
    public float curvatureVariance;
    public float centroidHeight;
    public float directionBias;
    public float tilt;
    public float tiltSigned;
    public float wobble;
}
```

### SoundProfile (16 params) — Direct Port

```csharp
public class SoundProfile {
    public float brightness;
    public float resonance;
    public float drive;
    public float attackBias;
    public float releaseBias;
    public float detune;
    public float modDepth;
    public float stereoSpread;
    public float grooveInstability;
    public float delayBias;
    public float reverbBias;
    public float waveMorph;
    public float filterMotion;
    public float transientSharpness;
    public float body;
}
```

### Sequence Derivation (Port Algorithms)

**RhythmLoop → RhythmSeq:**
```csharp
int density = Mathf.RoundToInt(Mathf.Clamp(6 + angularity*4 + (1-symmetry)*3 + wobble*3, 6, 14));
float swing = Mathf.Clamp(grooveInstability * 0.34f + wobble * 0.08f, 0, 0.42f);
// Kick/snare/hat patterns from aspectRatio, circularity, symmetry, angularity
```

**MelodyLine → MelodySeq:**
```csharp
int sliceCount = (strokeLength > 0.8f || pathLength > 0.68f) ? 16 : 8;
// Resample stroke to slices
// Map Y-height to scale pitch
// Speed → note duration, curvature → velocity, slope → glide
```

**HarmonyPad → HarmonySeq:**
```csharp
// Root from centroid height
// Chord flavor from tiltSigned: maj7 (>0.28), sus (<-0.22), minor (else)
// Voicing spread from horizontalSpan
```

---

## V. Audio Engine (Unity Implementation)

Replace Web Audio API with **Unity AudioSource + Custom DSP** or **FMOD**.

### Recommended: Unity DSP Graph Approach

```csharp
public class Voice : MonoBehaviour {
    public AudioSource output;
    
    // Parameters driven by SoundProfile + Preset
    public void PlayRhythmEvent(RhythmEvent evt, SoundProfile profile, Preset preset) {
        // Synthesize kick/snare/hat based on profile
        // Filter freq = base + brightness * range
        // Drive = tanh waveshaper
        // Pan = instance.pan
        // Reverb/Delay send = profile.reverbBias * depth
    }
    
    public void PlayMelodyNote(MelodyNote note, SoundProfile profile, Preset preset) {
        // Dual oscillator: waveA + waveB (detuned)
        // Glide from startFreq to targetFreq
        // Filter LFO if filterMotion > 0.08
        // Vibrato if modDepth > 0.1
    }
    
    public void PlayHarmonyChord(HarmonyChord chord, SoundProfile profile, Preset preset) {
        // Sustained chord with attack/release from envelope
        // Filter sweep if filterMotion high
        // Wide stereo spread based on profile
    }
}
```

### Global Mix

- **Master bus**: Standard Unity AudioMixer
- **Reverb**: Reverb Zone or Convolution Reverb
- **Delay**: Simple delay line with feedback

---

## VI. VR UI Architecture

### 1. Left Hand — Radial Menu (Wrist-Mounted)

**Default State:** Menu collapsed, only "puck" visible

**Thumbstick Up → Expand Instrument Menu:**
```
         [Lo-Fi Kit]
            ↑
[Trap Kit] ←●→ [Dream Ens.]
            ↓
      [Custom...]
```

**Thumbstick Down → Expand Pattern Library:**
- List of saved pattern masters
- Scroll with thumbstick
- Click to spawn instance in current scene

### 2. Drawing Commit Flow

```
1. User draws stroke with MX Ink (visual line follows stylus)
2. On release (tip_value < threshold):
   - Stroke analyzed immediately
   - Draft Pattern created with ShapeProfile/SoundProfile
   - Floating "Commit Card" appears at stroke center
3. Commit Card shows:
   - Auto-name ("Beat-03")
   - Shape summary ("spiky balanced loop with heavy body")
   - Type icon
   - [Save] [Save+Dup] [Discard] buttons
4. User looks at card + MX Ink front button to confirm
```

### 3. Right Hand (MX Ink) — Inspector Interaction

When an instance is selected:
- **Front button click**: Cycle through preset overrides
- **Back button click**: Delete instance
- **Double-tap back**: Mute/unmute

### 4. Scene Strip (Floating Horizontal)

Position: 1.5m in front, 0.5m below eye level

```
[Scene A ▶] [Scene B] [Scene C] [Scene D]
   ↑ playing (glows)
```

- Thumbstick left/right to switch
- Trigger click to queue switch (quantized to bar)

### 5. Arrangement Strip (Below Scene Strip)

```
[Slot 1: A/8] [Slot 2: A/8] [Slot 3: B/8] [Slot 4: B/8] [5: --] [6: --] ...
```

- Left hand raycast to select slot
- Opens radial: pick scene (A/B/C/D) and bars (4/8/16)

---

## VII. Visual Design (3D Aesthetic)

### Pattern Instance Rendering

| Type | Visual Style |
|------|-------------|
| **RhythmLoop** | Glowing ring (loop) with pulsing trigger points |
| **MelodyLine** | Curved neon tube with traveling playback marker |
| **HarmonyPad** | Soft aurora ellipse with rotating gradient |

All instances:
- Wireframe glow matching their color (`#51d7ff` rhythm, `#f7c975` melody, `#62f3d3` harmony)
- Pulse on beat (RhythmLoop), pulse on note (MelodyLine), breathe (HarmonyPad)
- Selected = brighter + floating inspector panel attached

### Drawing Feedback

- **Stroke**: Particle trail following MX Ink position
- **Pressure**: Line thickness varies in real-time
- **Closed detection**: Stroke auto-closes if near start point (RhythmLoop)
- **Commit card**: Holographic floating panel at stroke center

---

## VIII. Implementation Phases

### Phase 1: Foundation (Week 1-2)
- [ ] Port data models (Pattern, Instance, Scene, etc.)
- [ ] Port ShapeProfile/SoundProfile algorithms
- [ ] Port sequence derivation (Rhythm, Melody, Harmony)
- [ ] Integrate MX Ink drawing (pressure-sensitive LineRenderer)
- [ ] Basic 3D stroke capture and normalization

### Phase 2: Audio (Week 2-3)
- [ ] Implement Voice classes (Kick, Snare, Hat, Melody, Harmony)
- [ ] Port synthesis parameters from SoundProfile
- [ ] Implement sequencer with lookahead scheduling
- [ ] Scene playback and arrangement playback

### Phase 3: VR UI (Week 3-4)
- [ ] Left hand radial menu system
- [ ] Floating commit cards
- [ ] Instance grabbing and positioning
- [ ] Scene strip and arrangement strip
- [ ] Inspector panels

### Phase 4: Polish (Week 4)
- [ ] Visual feedback (glows, pulses, particles)
- [ ] Haptic feedback on MX Ink (drawing resistance, commit confirm)
- [ ] Persistence (save/load JSON)
- [ ] Demo session preset

---

## IX. File Structure (Unity)

```
Assets/
├── RhythmForge/
│   ├── Core/
│   │   ├── Models/           # PatternDefinition, Instance, etc.
│   │   ├── ShapeAnalysis/    # Geometry algorithms
│   │   ├── Sequencer/        # Transport, scheduling
│   │   └── Persistence/      # JSON save/load
│   ├── Audio/
│   │   ├── Voices/           # Synthesis classes
│   │   ├── Synth/            # Oscillators, filters
│   │   └── AudioEngine.cs    # Main audio controller
│   ├── VR/
│   │   ├── Drawing/          # Stroke capture, line rendering
│   │   ├── UI/               # Radial menus, floating panels
│   │   ├── Interaction/      # Grab, select, move
│   │   └── RhythmForgeVR.cs  # Main controller
│   └── Resources/
│       └── Presets.json      # Instrument groups & presets
├── Logitech/                 # Existing MX Ink SDK
└── Scenes/
    └── RhythmForgeMain.unity
```

---

## X. Key Algorithm Files to Port

| Pilot (index.html line) | Unity Target |
|------------------------|--------------|
| [analyzeStroke()](cci:1://file:///Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/_reference-docs/Pilot/index.html:1335:6-1371:7) [1336] | `ShapeAnalyzer.cs` |
| [deriveShapeProfile()](cci:1://file:///Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/_reference-docs/Pilot/index.html:1463:6-1542:7) [1464] | `ShapeProfileCalculator.cs` |
| [deriveSoundProfile()](cci:1://file:///Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/_reference-docs/Pilot/index.html:1544:6-1608:7) [1545] | `SoundProfileMapper.cs` |
| Rhythm derivation [1664] | `RhythmSequencer.cs` |
| Melody derivation [1738] | `MelodySequencer.cs` |
| Harmony derivation [1783] | `HarmonySequencer.cs` |
| Audio playback [3232] | `Voice.cs`, `DrumVoice.cs`, `MelodyVoice.cs` |

---

This plan ensures the **geometry-driven music composition** concept is fully preserved while adapting interactions for **intuitive VR/MX Ink** use. The core innovation — that **shapes determine both what is played and how it sounds** — remains intact.