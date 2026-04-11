# Shape-to-Sound Algorithm

## Overview

This document describes the mathematical and musical relationships that transform hand-drawn shapes into audible patterns in RhythmForge VR. The system operates through a four-stage pipeline:

1. **Geometric Analysis** — Extract features from the raw stroke
2. **Timbre Mapping** — Translate geometry into synthesis parameters
3. **Sequence Generation** — Construct rhythmic, melodic, or harmonic events
4. **Audio Synthesis** — Render the final sound

Each drawing mode (Rhythm Loop, Melody Line, Harmony Pad) uses the same geometric input but applies mode-specific transformations to produce musically coherent results.

---

## Stage 1: Geometric Feature Extraction

When a user completes a stroke, the system extracts 16 normalized geometric features (all values range 0–1, except where noted).

### Bounding & Centroid Features

| Feature | Mathematical Definition | Musical Significance |
|---------|------------------------|---------------------|
| **Closedness** | `1 - (start-to-end distance / 0.28)` | Whether the stroke forms a closed loop; loops suggest cyclic patterns |
| **Circularity** | `(4π × area) / perimeter² / 0.9` | How circle-like the shape is; circles suggest regular pulse |
| **Aspect Ratio** | `min(w,h) / max(w,h)` | Elongation of the shape; wide shapes spread sound in stereo |
| **Centroid Height** | `1 - (centroid Y / canvas height)` | Vertical position of the shape's center; maps to pitch register |
| **Vertical Span** | `height / 0.95` | Total vertical extent; determines melodic range |
| **Horizontal Span** | `width / 0.95` | Total horizontal extent; determines stereo width and chord voicing |
| **Path Length** | `total stroke length / 2.75` | Cumulative drawing distance; suggests duration and complexity |

### Shape Character Features

| Feature | Mathematical Definition | Musical Significance |
|---------|------------------------|---------------------|
| **Angularity** | `mean(|turn angles|) × 0.72 + variance(|turn angles|) × 0.52` | Sharpness of corners; angular shapes produce sharp, transient sounds |
| **Symmetry** | Mirror-match score against vertical axis through centroid | Balance of the shape; symmetrical shapes produce steady grooves |
| **Curvature Mean** | `mean(|turn angles|) / (π × 0.55)` | Average bending of the line; curved lines suggest smooth timbres |
| **Curvature Variance** | `stddev(|turn angles|) / (π × 0.4)` | Consistency of bending; varied curvature suggests dynamic modulation |
| **Speed Variance** | `stddev(segment lengths) / mean(segment lengths) / 0.95` | Drawing speed consistency; erratic speed suggests humanization |
| **Wobble** | `stddev(radii from centroid) / mean(radius) / 0.4` | Deviation from perfect circularity; wobble adds groove instability |
| **Direction Bias** | `(end X - start X) / width × 0.5 + 0.5` | Left-to-right vs right-to-left tendency; affects melodic direction |
| **Tilt (Signed)** | `(end Y - start Y) / height` (clamped -1 to +1) | Upward or downward slope (-1 = down, +1 = up); determines harmonic mood |
| **Tilt (Normalized)** | `(tilt_signed + 1) / 2` | Tilt remapped to 0–1 range |

### Derived Composite Features

The system computes several composite values used across all modes:

- **Smoothness** = `1 - angularity` — inverse of sharpness
- **Asymmetry** = `1 - symmetry` — degree of imbalance
- **Tilt Amount** = `|tilt_signed|` — absolute deviation from horizontal
- **Contour Pull** = `|direction_bias - 0.5| × 2` — strength of directional tendency
- **Instability** (rhythm-specific) = `wobble × 0.7 + asymmetry × 0.55 + curvature_variance × 0.35`

---

## Stage 2: Sound Profile Mapping

Geometric features are transformed into 15 synthesis parameters through mode-specific polynomial relationships. Each formula takes the form:

```
parameter = clamp(base_value + feature₁ × weight₁ + feature₂ × weight₂ + ...)
```

All results are clamped to the range [0, 1].

### Universal Synthesis Parameters

| Parameter | Description | Typical Influence |
|-----------|-------------|-----------------|
| **Brightness** | Spectral energy distribution | High values = more treble, sharper attack |
| **Resonance** | Emphasis on particular frequencies | High values = ringing, bell-like quality |
| **Drive** | Distortion/saturation amount | High values = grit, compression, edge |
| **Attack Bias** | Speed of sound onset | High values = instantaneous, percussive |
| **Release Bias** | Speed of sound decay | High values = short, tight; low = long, ambient |
| **Detune** | Pitch deviation from center | High values = chorus, thickness, instability |
| **Modulation Depth** | Amount of pitch/filter movement | High values = vibrato, wah-wah effects |
| **Stereo Spread** | Panning width | High values = wide stereo image |
| **Groove Instability** | Timing deviation from grid | High values = swung, humanized feel |
| **Delay Bias** | Amount of echo/repetition | High values = more audible delay feedback |
| **Reverb Bias** | Amount of spatial tail | High values = larger virtual space |
| **Wave Morph** | Blend between wave shapes | High values = more complex, saw/square content |
| **Filter Motion** | Dynamic filter movement | High values = opening/closing filter sweep |
| **Transient Sharpness** | Initial click/impact prominence | High values = punchy, defined attacks |
| **Body** | Low-mid energy, fullness | High values = warm, thick foundation |

### Rhythm Mode Mapping

Rhythm mode prioritizes angularity and instability for transient character:

**Instability Calculation:**
```
instability = clamp(wobble × 0.70 + asymmetry × 0.55 + curvature_variance × 0.35)
```

**Key Parameter Formulas:**

- **Brightness** = `0.22 + angularity × 0.45 + instability × 0.16`  
  *Sharp corners add high-frequency content*

- **Drive** = `0.12 + angularity × 0.68 + asymmetry × 0.22`  
  *Angular, unbalanced shapes sound more aggressive*

- **Transient Sharpness** = `0.25 + angularity × 0.70`  
  *Corners directly translate to percussive punch*

- **Attack Bias** = `0.24 + angularity × 0.60 + (1 - circularity) × 0.12`  
  *Sharp non-circles have immediate attack*

- **Release Bias** = `0.22 + circularity × 0.60 + symmetry × 0.16`  
  *Circular, symmetric shapes sustain longer*

- **Body** = `0.24 + circularity × 0.58 + symmetry × 0.14`  
  *Round, balanced shapes have more low-end fullness*

- **Groove Instability** = `instability` (direct pass-through)  
  *Wobbly, asymmetric strokes produce swung, off-grid timing*

- **Stereo Spread** = `0.12 + (1 - aspect_ratio) × 0.34 + asymmetry × 0.14`  
  *Wide, unbalanced shapes spread across the stereo field*

### Melody Mode Mapping

Melody mode emphasizes vertical extent, curvature, and directional features for pitch and articulation:

**Contour Pull Calculation:**
```
contour_pull = |direction_bias - 0.5| × 2
```

**Key Parameter Formulas:**

- **Brightness** = `0.24 + angularity × 0.40 + centroid_height × 0.14 + vertical_span × 0.16`  
  *High shapes and sharp corners produce brighter tones*

- **Resonance** = `0.16 + curvature_mean × 0.44 + curvature_variance × 0.18`  
  *Curved lines add ringing sustain*

- **Drive** = `0.08 + angularity × 0.44 + speed_variance × 0.20`  
  *Sharp corners and erratic drawing add edge*

- **Attack Bias** = `0.18 + speed_variance × 0.52 + angularity × 0.18`  
  *Variable speed strokes start with more articulation*

- **Release Bias** = `0.20 + smoothness × 0.56 + horizontal_span × 0.12`  
  *Smooth, wide shapes sustain longer*

- **Modulation Depth** = `0.12 + curvature_mean × 0.36 + contour_pull × 0.26`  
*Curved lines and clear direction add pitch movement*

- **Stereo Spread** = `0.14 + horizontal_span × 0.44 + contour_pull × 0.12`  
*Wide horizontal strokes pan across speakers*

- **Filter Motion** = `0.16 + contour_pull × 0.42 + curvature_variance × 0.16`  
*Directional strokes open/close the filter*

### Harmony Mode Mapping

Harmony mode emphasizes tilt, span, and path length for chord quality and spatial character:

**Key Parameter Formulas:**

- **Brightness** = `0.16 + centroid_height × 0.24 + angularity × 0.18 + tilt_amount × 0.18`  
  *High placement, sharpness, and tilt add harmonic brilliance*

- **Resonance** = `0.10 + tilt_amount × 0.44 + asymmetry × 0.20`  
  *Sloped, unbalanced shapes ring more*

- **Detune** = `0.12 + asymmetry × 0.42 + horizontal_span × 0.16`  
*Unbalanced, wide chords have natural chorus*

- **Stereo Spread** = `0.24 + horizontal_span × 0.48`  
*Width directly maps to stereo field*

- **Modulation Depth** = `0.16 + tilt_amount × 0.32 + asymmetry × 0.24`  
*Sloped, asymmetric chords breathe more*

- **Reverb Bias** = `0.24 + path_length × 0.36 + smoothness × 0.18`  
*Long, smooth strokes exist in larger spaces*

- **Filter Motion** = `0.22 + tilt_amount × 0.50 + asymmetry × 0.14`  
*Sloped chords sweep their filters*

---

## Stage 3: Sequence Generation

Each mode constructs a musical sequence using its sound profile and geometric features.

### Rhythm Loop Generation

**Duration Determination:**
- If average stroke size > 0.30 → 4 bars
- Else → 2 bars

**Accent Density:**
```
density = clamp(round(6 + angularity × 4 + asymmetry × 3 + wobble × 3), 6, 14)
```
Higher angularity, asymmetry, and wobble produce busier rhythms (6–14 active steps per bar).

**Swing Amount:**
```
swing = clamp(instability × 0.34 + wobble × 0.08, 0, 0.42)
```
Groove instability translates to timing offset (0–42% swing).

**Kick Pattern Selection:**
- **Tall narrow shapes** (aspect_ratio < 0.52): Beats `{0, 6, 10, 13}` — syncopated, broken
- **Circular shapes** (circularity > 0.75): Beats `{0, 8, 12}` — minimal, spacious
- **Default**: Beats `{0, 7, 10, 13}` — standard four-on-the-floor variant

**Snare Pattern Selection:**
- **Symmetric shapes** (symmetry > 0.6): Beat `{8}` only — backbeat minimalism
- **Asymmetric shapes**: Beats `{5, 8, 13}` — syncopated, driving

**Hi-Hat Pattern:**
- **Angular shapes** (angularity > 0.68): Every step (16th notes) — busy, energetic
- **Smooth shapes**: Every 2 steps (8th notes) — steady, restrained

**Ghost Notes:**
Added when `density > 11` or `symmetry < 0.45`, appearing at beats `{3, 11, 15}` with reduced velocity.

**Velocity Calculations:**
- Kick: `0.60 + body × 0.30 - index × 0.05` (decreasing emphasis through pattern)
- Snare: `0.56 + transient_sharpness × 0.26 - index × 0.05`
- Hats: `0.24 + brightness × 0.22 + (step % 4 == 0 ? 0.08 : 0)`
- Ghost: `0.26 + drive × 0.18`

**Micro-Timing:**
Each event receives a timing offset:
```
micro_shift = sin((step + offset) × frequency + feature × scale) × groove_instability × magnitude
```
This creates the "humanized" feel where unstable shapes have more timing variation.

### Melody Line Generation

**Duration Determination:**
- If stroke length > 0.80 → 4 bars
- Else → 2 bars

**Slice Division:**
- If length > 0.75 or path_length > 0.68 → 16 slices
- Else → 8 slices

The stroke is resampled into equal-spaced points (slices), each becoming a note.

**Pitch Mapping:**
For each slice point:
```
centered_y = ((point_y - pitch_center) / height) × vertical_scale + 0.5
clamped_y = clamp(centered_y, 0, 0.999)
midi_note = pitch_from_relative(clamped_y, key)
```

- **Pitch center** = `min_y + height / 2` (middle of the drawing)
- **Vertical scale** = `0.72 + vertical_span × 1.2` (wider drawings have larger octave ranges)

Higher vertical positions on the canvas produce higher pitches. The `pitch_from_relative` function maps 0–1 to scale degrees in the current musical key.

**Note Duration:**
```
base_duration = speed < 0.025 ? 6 : speed < 0.042 ? 4 : 2
duration = clamp(round(base_duration + (1 - speed_variance) × 1.5 - transient_sharpness × 1.2), 2, 7)
```
- Slow drawing → longer notes (6 steps ≈ dotted quarter)
- Fast drawing → shorter notes (2 steps ≈ 8th note)
- High speed variance or sharp transients reduce duration

**Velocity:**
```
velocity = clamp(0.34 + local_curvature × 0.86 + curvature_mean × 0.16, 0.30, 0.96)
```
Curved sections are played louder; the overall curvature character adds base energy.

**Glide (Portamento):**
```
slope = clamp((next_y - prev_y) / height × 4, -1, 1)
glide = slope × (0.30 + filter_motion × 0.85)
```
Upward slopes produce positive glide (pitch rises into the note); downward slopes produce negative glide. Filter motion exaggerates this effect.

### Harmony Pad Generation

**Duration Determination:**
- If stroke length > 1.20 → 8 bars
- Else if length > 0.70 → 4 bars
- Else → 2 bars

**Root Note:**
```
root_midi = pitch_from_relative(1 - centroid_height, key) - 12
```
Higher centroid positions produce lower root notes (inverted mapping for bass warmth).

**Chord Quality Selection (from Tilt):**

| Tilt Signed | Chord Type | Intervals | Musical Character |
|-------------|-----------|-----------|-------------------|
| > +0.28 | Major 7 | `{0, 4, 7, 11}` | Bright, resolved, jazzy |
| < -0.22 | Sus4 | `{0, 5, 7, 10}` | Open, floating, ambiguous |
| Default | Minor 7 | `{0, 3, 7, 10}` | Moody, introspective, stable |

The upward tilt suggests rising, major quality; downward tilt suggests suspended, unresolved quality; near-horizontal suggests neutral minor.

**Voicing Spread:**
```
spread = round(horizontal_span × 10)
```

For each chord tone:
- Root (index 0): If `horizontal_span > 0.72`, drop one octave (`-12`)
- Third (index 2): Add `round(spread × 0.45)` semitones
- Seventh (index 3): Add `spread` semitones

Wide horizontal drawings produce wide interval voicings (open position), narrow drawings produce closed voicings.

---

## Stage 4: Audio Synthesis

All sounds are generated procedurally through mathematical synthesis. No samples are loaded from disk.

### Drum Synthesis

**Kick Drum:**
- **Body**: Sine wave with exponential frequency sweep from 80 Hz → 28 Hz
- **Envelope**: Exponential decay (`exp(-progress × 10)`)
- **Transient**: 8ms click (340 Hz sine burst) for definition
- **Character**: Soft-clipping saturation controlled by drive parameter
- **Duration**: 320 ms

**Snare Drum:**
- **Tonal body**: 200 Hz sine with fast decay (`exp(-progress × 32)`)
- **Noise tail**: White noise with medium decay (`exp(-progress × 14)`)
- **Transient**: 5 ms click (600 Hz burst)
- **Mix**: 60% tone, 70% noise, plus click
- **Duration**: 220 ms

**Hi-Hat:**
- **Source**: White noise through one-pole highpass filter (~3 kHz)
- **Metallic shimmer**: Inharmonic partials at 8372 Hz and 10548 Hz
- **Envelope**: Fast exponential decay (`exp(-t/duration × 6)`)
- **Duration**: 80 ms

**Percussion (Tom/Conga):**
- **Tone**: Pitched sine with upward frequency sweep at onset
- **Sweep**: `frequency × (1 + exp(-progress × 22) × 1.2)` — pitch drops after strike
- **Noise**: Brief noise burst for stick/finger impact
- **Duration**: 120 ms (variable frequency based on context)

### Tonal Synthesis

**Melody Voice (Tone):**
- **Waveform**: Multi-harmonic sine stack
  - Fundamental: 80% amplitude
  - 2nd harmonic: 12% amplitude (warmth)
  - 3rd harmonic: 4% amplitude (brightness)
- **Envelope**: Linear attack (12 ms), full sustain, linear release (250 ms)
- **Pitch**: Base C4 (261.63 Hz), shifted by semitone interval for target note
- **Pitch shift formula**: `pitch_multiplier = 2^(semitones / 12)`

**Harmony Voice (Pad):**
- **Waveform**: Dual detuned oscillators
  - Primary: 50% amplitude at fundamental
  - Detuned: 40% amplitude at `frequency × 1.0035` (3.5 cents up)
  - 2nd harmonic: 8% amplitude
- **Envelope**: Slow attack (320 ms), long sustain, gentle release (600 ms)
- **Modulation**: LFO at 0.3 Hz on amplitude (20% depth) for "breathing" filter effect
- **Duration**: 3000 ms

### Playback Modulation

During performance, the SoundProfile modulates the synthesis:

**Pitch Variation (Drums):**
```
pitch_multiplier = clamp(1 + (brightness - 0.5) × 0.15, 0.8, 1.3)
```
Bright shapes raise drum pitch; dull shapes lower it.

**Stereo Panning:**
- Drums: Centered with subtle spread from `stereo_spread`
- Melody notes: Positioned by horizontal position in drawing
- Harmony chords: Spread across stereo field by chord tone index

**Amplitude Scaling:**
```
final_volume = clamp(velocity × gain, 0, 1)
```

---

## Cross-Mode Feature Relationships

### How the Same Shape Produces Different Results

Consider a **tall, angular, upward-sloping stroke** drawn from bottom-left to top-right:

| Feature | Value | Rhythm Interpretation | Melody Interpretation | Harmony Interpretation |
|---------|-------|----------------------|----------------------|----------------------|
| High angularity | ~0.8 | Sharp transients, busy 16th hats, high drive | Bright, articulated tone, fast attack | Sharp harmonic attack, wave morph |
| Vertical span | ~0.9 | — | Large octave range (2+ octaves) | — |
| Upward tilt | +0.6 | — | Rising contour, positive glide | Major 7 chord quality |
| Horizontal span | ~0.7 | Wide stereo image | Moderate pan movement | Wide chord voicing (+7 semitones on 7th) |
| Speed variance | ~0.4 | — | Mixed note durations | — |

**Resulting Patterns:**
- **Rhythm**: Busy 4-bar loop with 16th-note hats, sharp kicks on `{0,7,10,13}`, heavy swing from groove instability
- **Melody**: Ascending line with 16 slices spanning 2+ octaves, articulated attacks, upward glides into notes
- **Harmony**: 4-bar major 7 chord with wide voicing, bright spectral character, gentle filter opening

### How Different Shapes Produce the Same Mode Results

Two shapes can produce musically similar results through different geometric paths:

**Example 1: Smooth Rhythms**
- **Circle**: High circularity + high symmetry → 2-bar loop, minimal kicks `{0,8,12}`, single backbeat, steady 8th hats, long body
- **Horizontal line**: Low angularity + high symmetry → Same musical result through different geometry

**Example 2: Angular Melodies**
- **Zigzag**: High angularity + high speed variance → Bright, staccato line with sharp attacks
- **Spiky burst**: High angularity + high curvature variance → Same timbre through clustered sharp turns

---

## Summary: The Geometric-Musical Lexicon

| Draw This | To Get This Sound |
|-----------|------------------|
| Perfect circle | Regular 4/4 kick pattern, steady groove, warm sustained body |
| Tall narrow loop | Broken beat, syncopated kicks, sharp transients |
| Wide horizontal line | Wide stereo pad, sustained tones, open chord voicing |
| Upward diagonal | Rising melody, major harmony, positive filter sweep |
| Downward diagonal | Falling melody, suspended harmony, gentle close |
| Wobbly/noisy line | Swung rhythm, humanized timing, chorus detune |
| Sharp corners | Percussive attacks, bright timbre, defined transients |
| Smooth curves | Legato phrasing, soft attacks, resonant sustain |
| Wide aspect ratio | Narrow stereo, closed voicing, focused sound |
| Low on canvas | Bass register, dark tone, sub-heavy |
| High on canvas | Treble register, bright tone, airy quality |
| Long path | Extended duration, more bars, complex evolution |
| Short path | Brief fragment, 2 bars, concise statement |

---

## Implementation Notes

The algorithm uses normalized world-space coordinates where the drawing canvas maps to approximately 1.0 × 1.0 units. All thresholds and scaling factors were tuned against pilot user testing to ensure musically coherent results across diverse drawing styles.

The system is deterministic: the same shape drawn in the same location will always produce the same sound. However, small variations in human gesture naturally produce unique musical variations, making each performance slightly different while maintaining the intended musical character.
