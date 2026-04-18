# Music Creation from 3D Shapes - Deep Dive

## Overview

RhythmForgeVR converts **3D stylus strokes** into **musical patterns** through a multi-stage pipeline that analyzes geometry, derives musical structure, and generates procedural audio. Here's the complete flow:

---

## Stage 1: Stroke Capture

### Input Stream

When you draw in VR with the MX Ink stylus, the system captures a stream of **StrokeSample** structs:

```csharp
struct StrokeSample {
    Vector3 worldPos;      // 3D position in world space
    float pressure;        // Pen pressure (0-1)
    Quaternion stylusRot;  // Stylus rotation
    double timestamp;      // Time since stroke start
}
```

### StrokeCurve Construction

The raw samples are wrapped into a **StrokeCurve** carrier that provides two representations:

1. **3D samples** - Original point cloud with pressure/rotation/time
2. **2D projection** - Arc-length-preserving projection onto the stroke's best-fit plane

```
Raw 3D Samples → StrokeCurve
                    ├── samples (3D with pressure, rotation, time)
                    └── projected (2D on best-fit plane)
```

---

## Stage 2: Geometric Analysis

### 2.1 2D Shape Metrics (StrokeAnalyzer)

The 2D projection is analyzed for geometric properties:

| Metric | Calculation | Musical Meaning |
|--------|-------------|-----------------|
| `closed` | Start-end distance < threshold | Rhythm loop viability |
| `width`, `height` | Bounding box dimensions | Pattern scale |
| `length` | Sum of segment distances | Pattern duration |
| `wobble` | Radius variance from centroid | Groove looseness |
| `tilt` | Angle from start to end | Chord voicing direction |

### 2.2 Shape DNA (ShapeProfileCalculator)

From the metrics, a **ShapeProfile** is derived with 20+ normalized features:

```csharp
ShapeProfile {
    // Form characteristics
    closedness      // 0=open, 1=closed loop
    circularity     // 0=linear, 1=perfect circle
    aspectRatio     // 0=elongated, 1=square
    angularity      // 0=smooth, 1=sharp corners
    symmetry        // 0=asymmetric, 1=mirrored
    
    // Spatial properties
    verticalSpan    // Height in normalized space
    horizontalSpan  // Width in normalized space
    pathLength      // Total stroke length (0-1)
    centroidHeight  // Vertical center position
    
    // Motion characteristics
    directionBias   // Left-to-right vs right-to-left
    tilt            // Diagonal tilt (0=horizontal, 1=vertical)
    tiltSigned      // -1=down, 0=level, +1=up
    wobble          // Irregularity measure
    curvatureMean   // Average corner sharpness
    curvatureVariance // Corner consistency
    
    // World-space metrics (Phase G+)
    worldWidth, worldHeight, worldLength  // Actual size in meters
    worldMaxDimension  // Largest dimension (for scale preservation)
}
```

### 2.3 3D Shape Profile (ShapeProfile3DCalculator)

From the raw 3D samples, additional spatial characteristics are computed:

```csharp
ShapeProfile3D {
    // Pressure characteristics
    thicknessMean       // Average pen pressure
    thicknessVariance   // Pressure dynamics
    
    // Orientation characteristics
    tiltMean            // Average stylus tilt angle
    tiltVariance        // Tilt consistency
    
    // 3D structure (via PCA - Principal Component Analysis)
    elongation3D        // How linear vs blob-like
    planarity           // How flat vs volumetric
    depthSpan           // Depth relative to size
    
    // Spatial positioning
    centroidDepth       // Position in depth space
    helicity            // Spiral/helix tendency (-1 to +1)
    
    // Temporal characteristics
    temporalEvenness    // Drawing speed consistency
    passCount           // Self-intersection count
}
```

**PCA Decomposition:**
- Computes covariance matrix of 3D points
- Extracts eigenvectors (principal axes) and eigenvalues (variances)
- **Major axis** = direction of greatest spread
- **Mid axis** = secondary spread direction
- **Minor axis** = thickness dimension

---

## Stage 3: Sound Profile Mapping

### Pattern Behavior Routing

Each pattern type (RhythmLoop, MelodyLine, HarmonyPad) has a **PatternBehavior** that maps shape features to sound characteristics:

```csharp
SoundProfile {
    // Tonal characteristics
    body;           // 0=thin, 1=full
    brightness;     // 0=dark, 1=bright
    transientSharpness; // 0=soft attack, 1=punchy
    
    // Modulation characteristics
    modDepth;       // 0=static, 1=animated
    modRate;        // LFO speed
    filterMotion;   // Filter envelope amount
    
    // Spatial characteristics
    reverbBias;     // 0=dry, 1=wet
    stereoWidth;    // 0=mono, 1=wide
    
    // Performance characteristics
    grooveInstability; // 0=tight, 1=loose
    drive;          // 0=clean, 1=distorted
}
```

### Example: RhythmLoop Sound Derivation

```csharp
// From PatternBehavior for RhythmLoop
SoundProfile DeriveRhythm(ShapeProfile sp) {
    return new SoundProfile {
        body = sp.circularity * 0.7f + sp.closedness * 0.3f,
        brightness = sp.angularity,
        transientSharpness = sp.angularity * 0.8f + (1 - sp.symmetry) * 0.2f,
        grooveInstability = sp.wobble,
        drive = sp.speedVariance
    };
}
```

---

## Stage 4: Unified Music Derivation

### Phase G: Three-Facet Architecture

A single stroke now produces **three musical facets** simultaneously:

```
                    ┌─────────────────┐
                    │   ShapeProfile  │
                    │  ShapeProfile3D │
                    │   SoundProfile  │
                    └────────┬────────┘
                             │
                    ┌────────▼────────┐
                    │ BondStrengthResolver │
                    │  (derives weights)    │
                    └────────┬────────┘
                             │
         ┌───────────────────┼───────────────────┐
         │                   │                   │
         ▼                   ▼                   ▼
┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐
│  Rhythm Facet   │ │  Melody Facet   │ │ Harmony Facet   │
│  (drums/perc)   │ │  (lead line)    │ │  (chords/pads)  │
└────────┬────────┘ └────────┬────────┘ └────────┬────────┘
         │                   │                   │
         └───────────────────┼───────────────────┘
                             │
                    ┌────────▼────────┐
                    │  MusicalShape   │
                    │  (unified entity)│
                    └─────────────────┘
```

### Bond Strength Calculation

The **BondStrengthResolver** determines how much each facet contributes:

```csharp
Vector3 Resolve(ShapeProfile profile, ShapeProfile3D profile3D) {
    // Rhythm weight: angularity + pressure variance + non-planarity
    float rhythm = angularity + thicknessVar + (1 - planarity);
    
    // Melody weight: elongation + open paths + length
    float melody = elongation3D + (1 - circularity) + pathLenNorm;
    
    // Harmony weight: circularity + closed loops + depth
    float harmony = circularity + closedness + depthSpan + planarity;
    
    // Normalize to weights summing to 1.0
    return new Vector3(rhythm, melody, harmony).normalized;
}
```

**Facet Modes:**
- **Free Mode**: All three facets audible, weighted by bond strength
- **SoloRhythm**: Only rhythm facet (bondStrength = [1,0,0])
- **SoloMelody**: Only melody facet (bondStrength = [0,1,0])
- **SoloHarmony**: Only harmony facet (bondStrength = [0,0,1])

---

## Stage 5: Sequence Generation

### 5.1 Harmony Derivation (First - Foundation)

**Purpose:** Establish chord progression foundation

**Key Mappings:**

| Shape Feature | Musical Parameter |
|---------------|-------------------|
| `tiltSigned` | Chord quality (maj7/sus/minor) |
| `centroidHeight` | Root pitch (low/high) |
| `horizontalSpan` | Voicing width (tight/spread) |
| `pathLength` | Chord duration (bars) |
| `roleIndex` | Voicing complexity |

**Chord Construction:**
```csharp
// Tilt determines chord family
if (tiltSigned > 0.28f)      chord = [root, 3rd, 5th, 7th]  // maj7
else if (tiltSigned < -0.22f) chord = [root, 4th, 5th, 7th]  // sus4
else                         chord = [root, b3rd, 5th, b7th] // minor

// Role-based voicing
Role 0 (primary): Full 4-5 note voicing
Role 1 (counter): Root + 5th only, octave lower
Role 2+ (fill): Bass pedal (root only)
```

### 5.2 Melody Derivation (Second - Counterpoint)

**Purpose:** Generate melodic contour from stroke geometry

**Process:**

1. **Resample stroke** to 8-16 equidistant points along the curve
2. **Map Y position to pitch** via relative height within bounding box
3. **Quantize to key** using MusicalKeys.QuantizeToKey()
4. **Snap to chord tones** on strong beats (steps 0, 4, 8, 12)
5. **Apply register clamping** to keep notes in playable range

**Key Mappings:**

| Shape Feature | Musical Parameter |
|---------------|-------------------|
| `verticalSpan` | Pitch range (narrow/wide intervals) |
| `directionBias` | Melody contour (ascending/descending) |
| `pathLength` | Note density (short=long notes, long=many notes) |
| `curvature` | Note velocity (dynamics) |
| `speedVariance` | Note duration variance |

**Note Generation:**
```csharp
for each resampled point i:
    // Calculate relative height (0 = bottom, 1 = top)
    float centeredY = (point.y - pitchCenter) / height;
    
    // Map to MIDI pitch via scale
    int midi = PitchFromRelative(centeredY, keyName);
    
    // Snap to chord tone on strong beats
    if (step % 4 == 0 && HasChord)
        midi = NearestChordTone(midi);
    
    // Calculate duration from drawing speed
    int duration = speed < 0.025 ? 6 : speed < 0.042 ? 4 : 2;
    
    // Glide from slope
    float glide = (next.y - prev.y) / height * 4;
```

### 5.3 Rhythm Derivation (Third - Anchoring)

**Purpose:** Generate drum patterns from shape characteristics

**Key Mappings:**

| Shape Feature | Musical Parameter |
|---------------|-------------------|
| `aspectRatio` | Kick pattern selection |
| `circularity` | Kick density |
| `symmetry` | Snare pattern (backbeat vs syncopated) |
| `angularity` | Hi-hat density (sharp=fast) |
| `wobble` | Swing amount |
| `pathLength` | Pattern length (2 or 4 bars) |

**Pattern Construction:**
```csharp
// Kick pattern based on aspect ratio
if (aspectRatio < 0.52f)      kick = [0, 6, 10, 13]  // Wide shape
else if (circularity > 0.75f) kick = [0, 8, 12]      // Circular
else                          kick = [0, 7, 10, 13]  // Default

// Snare pattern based on symmetry
if (symmetry > 0.6f) snare = [8]       // Symmetric = backbeat only
else                 snare = [5, 8, 13] // Asymmetric = syncopated

// Hi-hat stride based on angularity
int hatStride = angularity > 0.68f ? 1 : 2;  // Sharp = 8th notes

// Add ghost notes for complex shapes
if (density > 11 || symmetry < 0.45f)
    AddGhostNotes([3, 11, 15]);
```

**Velocity Calculation:**
```csharp
kickVelocity   = 0.6f + sound.body * 0.3f - index * 0.05f;
snareVelocity  = 0.56f + sound.transientSharpness * 0.26f;
hatVelocity    = 0.24f + sound.brightness * 0.22f;
```

**Micro-timing (Swing):**
```csharp
swing = sound.grooveInstability * 0.34f + sp.wobble * 0.08f;
microShift = sin(step * 1.47f + wobble * 5f) * grooveInstability;
```

---

## Stage 6: 3D Profile Mutations

After initial derivation, the 3D shape profile applies fine-tuning:

### Rhythm Mutations
```csharp
// Tilt and pressure variance affect accent strength
float rhythmAccentGain = 1f + 0.25f * (tiltMean - 0.5f)
                            + 0.15f * (thicknessVariance - 0.3f);

// Apply to all rhythm events
foreach (event in rhythm.events)
    event.velocity *= rhythmAccentGain;

// Temporal evenness affects micro-timing scale
float microScale = lerp(1.2f, 0.6f, temporalEvenness);
```

### Melody Mutations
```csharp
// Elongation affects dynamics
float melodyGain = 1f + 0.25f * (elongation3D - 0.3f);

// Helicity adds glide (spiral = pitch bending)
float helixBias = helicity;
foreach (note in melody.notes) {
    note.velocity *= melodyGain;
    note.glide += helixBias * 0.12f;  // -0.12 to +0.12
}
```

### Harmony Mutations
```csharp
// Depth span adds upper extensions
if (depthSpan > 0.5f)
    chord.Add(topNote + 12);  // Add octave on top

// Centroid depth adds sub-bass
if (centroidDepth < 0.3f)
    chord.Insert(0, root - 12);  // Add sub-bass
```

---

## Stage 7: Playback & Audio Generation

### Sequencer Scheduling

The **Sequencer** runs a lookahead scheduler:

```
Every frame:
1. Calculate next note time (AudioDSP.time + lookahead)
2. Schedule events within 0.12s window
3. Advance transport (step → bar → slot)
4. Warm next bar's clips in background
```

### Procedural Audio Rendering

**SamplePlayer** generates audio clips on-demand:

```
Clip Request → Check LRU Cache → Hit? Play
                              ↓ Miss?
                    Queue Background Render Task
                              ↓
                    Synth parameters from SoundProfile
                              ↓
                    Render to AudioClip (threaded)
                              ↓
                    Cache with LRU eviction (96 clip limit)
```

### Instrument Routing

Each facet routes through its own instrument preset:

```
MusicalShape
├── rhythmPresetId → InstrumentPreset (drum kit)
│   └── rhythmSoundProfile → voice parameters
├── melodyPresetId → InstrumentPreset (synth lead)
│   └── melodySoundProfile → voice parameters
└── harmonyPresetId → InstrumentPreset (pad synth)
    └── harmonySoundProfile → voice parameters
```

---

## Complete Data Flow

```
┌─────────────────────────────────────────────────────────────────────┐
│  1. USER DRAWS IN 3D SPACE                                          │
│     StylusHandler captures StrokeSample[]                           │
└─────────────────────┬───────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────────────────────────┐
│  2. STROKE CURVE CONSTRUCTION                                       │
│     StrokeCurve.FromSamples() creates 2D projection + 3D carrier    │
└─────────────────────┬───────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────────────────────────┐
│  3. GEOMETRIC ANALYSIS                                              │
│     StrokeAnalyzer.Analyze() → StrokeMetrics                        │
│     ShapeProfileCalculator.Derive() → ShapeProfile (20+ features)   │
│     ShapeProfile3DCalculator.Derive() → ShapeProfile3D (PCA, etc.)  │
└─────────────────────┬───────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────────────────────────┐
│  4. SOUND PROFILE MAPPING                                           │
│     PatternBehavior.DeriveSoundProfile() per pattern type           │
│     Maps shape → timbre (body, brightness, modulation, etc.)        │
└─────────────────────┬───────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────────────────────────┐
│  5. BOND STRENGTH RESOLUTION                                        │
│     BondStrengthResolver.Resolve() → Vector3(rhythm, melody, harm)  │
│     Determines facet mix for Free mode                              │
└─────────────────────┬───────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────────────────────────┐
│  6. UNIFIED DERIVATION                                              │
│     HarmonyDeriver.Derive()  → Chord progression (foundation)       │
│     MelodyDeriver.Derive()   → Melodic sequence (contour)           │
│     RhythmDeriver.Derive()   → Drum pattern (rhythmic anchor)       │
│     ApplyProfile3DMutations() → 3D fine-tuning                      │
└─────────────────────┬───────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────────────────────────┐
│  7. MUSICALSHAPE CREATION                                           │
│     Bundles all three facets + bond strength + per-facet routing    │
│     Persists to PatternDefinition with worldPoints (3D)             │
└─────────────────────┬───────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────────────────────────┐
│  8. SEQUENCER PLAYBACK                                              │
│     Lookahead scheduler triggers events at precise audio times      │
│     Routes through facet-specific instrument presets                │
└─────────────────────┬───────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────────────────────────┐
│  9. PROCEDURAL AUDIO GENERATION                                     │
│     SamplePlayer renders clips from SoundProfile parameters         │
│     LRU cache with background threading                             │
│     Mixer routing per genre (NewAge/Electronic/Jazz)                │
└─────────────────────┬───────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────────────────────────┐
│  10. VISUAL FEEDBACK                                                │
│     PatternVisualizer renders shape geometry in world space         │
│     PlaybackHaloRenderer pulses with beat                           │
│     PlaybackMarker travels along shape                              │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Key Design Principles

1. **Deterministic**: Same stroke → same music (quantized to 2 decimal places)
2. **Size-Preserving**: Actual drawn size affects musical scale (large = full, small = tight)
3. **Context-Aware**: Harmonic fabric ensures all patterns stay in key/chord
4. **Multi-Facet**: Single stroke produces rhythm + melody + harmony simultaneously
5. **3D-Enhanced**: Depth, tilt, pressure, and helicity add expressive nuance
6. **Background Processing**: Audio rendering on thread pool, no main-thread stalls

---

## Appendix A: Pattern Type Behaviors

### RhythmLoop

| Shape Characteristic | Musical Effect |
|---------------------|----------------|
| Closed loop | Enables loop mode |
| High circularity | Dense kick pattern (4-on-floor feel) |
| High angularity | Sharp transients, fast hi-hats |
| Low symmetry | Syncopated snare pattern |
| Large size | 4-bar pattern, more body |
| High wobble | More swing, ghost notes |

### MelodyLine

| Shape Characteristic | Musical Effect |
|---------------------|----------------|
| Vertical span | Pitch range (narrow/wide intervals) |
| Direction bias | Ascending vs descending contour |
| Path length | Note count (short=few, long=many) |
| Curvature | Note velocity (dynamics) |
| Tilt | Chord quality influence |
| 3D helicity | Glide/portamento amount |

### HarmonyPad

| Shape Characteristic | Musical Effect |
|---------------------|----------------|
| Tilt signed | Chord family (maj7/sus/minor) |
| Horizontal span | Voicing width (close/open) |
| Centroid height | Root pitch register |
| Path length | Chord duration (2/4/8 bars) |
| 3D depth span | Added upper extensions |
| 3D centroid depth | Sub-bass addition |

---

## Appendix B: Threshold Reference

### Pattern Length Thresholds

| Pattern Type | 2 Bar | 4 Bar | 8 Bar |
|-------------|-------|-------|-------|
| RhythmLoop | avgSize ≤ 0.30m | avgSize > 0.30m | — |
| MelodyLine | length ≤ 0.80m | length > 0.80m | — |
| HarmonyPad | length ≤ 0.70m | 0.70m < length ≤ 1.20m | length > 1.20m |

### Facet Mode Thresholds

| Condition | Facet Mode |
|-----------|------------|
| freeMode = true | Free (3D-derived bond strength) |
| bondStrength one-hot [1,0,0] | SoloRhythm |
| bondStrength one-hot [0,1,0] | SoloMelody |
| bondStrength one-hot [0,0,1] | SoloHarmony |
| bondStrength mixed | Free |

### Chord Quality Thresholds

| Tilt Signed | Chord Type | Voicing |
|-------------|------------|---------|
| > 0.28 | maj7 | [root, 3rd, 5th, 7th] |
| < -0.22 | sus4 | [root, 4th, 5th, 7th] |
| -0.22 to 0.28 | minor | [root, b3rd, 5th, b7th] |

---

## Appendix C: File Locations

| Component | File Path |
|-----------|-----------|
| Stroke Capture | `Assets/RhythmForge/Interaction/StrokeCapture.cs` |
| Draft Builder | `Assets/RhythmForge/Core/Session/DraftBuilder.cs` |
| Shape Profile Calculator | `Assets/RhythmForge/Core/Analysis/ShapeProfileCalculator.cs` |
| Shape Profile 3D Calculator | `Assets/RhythmForge/Core/Analysis/ShapeProfile3DCalculator.cs` |
| Stroke Analyzer | `Assets/RhythmForge/Core/Analysis/StrokeAnalyzer.cs` |
| Sound Profile Mapper | `Assets/RhythmForge/Core/Analysis/SoundProfileMapper.cs` |
| Bond Strength Resolver | `Assets/RhythmForge/Core/Sequencing/BondStrengthResolver.cs` |
| Unified Deriver | `Assets/RhythmForge/Core/Sequencing/UnifiedShapeDeriverBase.cs` |
| Rhythm Deriver | `Assets/RhythmForge/Core/Sequencing/RhythmDeriver.cs` |
| Melody Deriver | `Assets/RhythmForge/Core/Sequencing/MelodyDeriver.cs` |
| Harmony Deriver | `Assets/RhythmForge/Core/Sequencing/HarmonyDeriver.cs` |
| Stroke Curve | `Assets/RhythmForge/Core/Sequencing/StrokeCurve.cs` |
| Sequencer | `Assets/RhythmForge/Sequencer/Sequencer.cs` |
| Sample Player | `Assets/RhythmForge/Audio/SamplePlayer.cs` |

---

## Appendix D: Data Structures

### StrokeSample
```csharp
struct StrokeSample {
    Vector3 worldPos;      // World position
    float pressure;        // 0-1 pressure
    Quaternion stylusRot;  // Stylus orientation
    double timestamp;      // Seconds since stroke start
}
```

### ShapeProfile
```csharp
class ShapeProfile {
    float closedness, circularity, aspectRatio;
    float angularity, symmetry;
    float verticalSpan, horizontalSpan, pathLength;
    float speedVariance, curvatureMean, curvatureVariance;
    float centroidHeight, directionBias, tilt, tiltSigned, wobble;
    float worldWidth, worldHeight, worldLength;
    float worldAverageSize, worldMaxDimension;
}
```

### ShapeProfile3D
```csharp
class ShapeProfile3D {
    float thicknessMean, thicknessVariance;
    float tiltMean, tiltVariance;
    float depthSpan, planarity, elongation3D, centroidDepth;
    float helicity;  // -1 to +1
    float temporalEvenness, passCount;
}
```

### MusicalShape
```csharp
class MusicalShape {
    string id;
    ShapeProfile3D profile3D;
    SoundProfile soundProfile;
    Vector3 bondStrength;  // (rhythm, melody, harmony)
    ShapeFacetMode facetMode;
    int totalSteps, roleIndex, bars;
    DerivedShapeSequence facets;
    string rhythmPresetId, melodyPresetId, harmonyPresetId;
    SoundProfile rhythmSoundProfile, melodySoundProfile, harmonySoundProfile;
}
```

### DerivedShapeSequence
```csharp
class DerivedShapeSequence {
    RhythmSequence rhythm;
    MelodySequence melody;
    HarmonySequence harmony;
}
```

---

## Appendix E: Genre Variations

### Electronic Genre
- Default tempo: 120 BPM
- Default key: C minor
- Instruments: TR-808/908 drums, FM synth leads, supersaw pads
- Sound mapping: High brightness, sharp transients, tight groove

### NewAge Genre
- Default tempo: 80 BPM
- Default key: D major
- Instruments: Soft percussion, flute/piano leads, string pads
- Sound mapping: Warm body, smooth attacks, loose groove, high reverb

### Jazz Genre
- Default tempo: 100 BPM
- Default key: F major
- Instruments: Acoustic drum kit, sax/trumpet leads, piano/organ pads
- Sound mapping: Natural timbres, swing emphasis, dynamic expression

Each genre has its own:
- `IRhythmDeriver` - Rhythm pattern algorithms
- `IMelodyDeriver` - Melodic contour algorithms
- `IHarmonyDeriver` - Chord progression algorithms
- `IUnifiedShapeDeriver` - Combined derivation
- `GroupBusFx` - Mixer effects chain (reverb, EQ, compression)
