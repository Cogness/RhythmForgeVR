# RhythmForge Concept Stage: Product + Algorithm Handoff

## Purpose

This document exports the complete concept and the implemented algorithms from the HTML pilot in `/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVRPilot/index.html` so another agent can rebuild the same behavior in another application.

This is not just the original idea. It reflects the current implemented pilot:

- pattern-first composition instead of note-level editing
- reusable pattern masters plus scene instances
- ensembles / instrument groups
- scene-based arrangement
- geometry-driven sequencing
- geometry-driven timbre and motion
- simple spatial mixing
- persistent local session state

The pilot is a single self-contained web app, but the architecture and algorithms below are portable to desktop, game engine, VR, or native applications.

---

## Core Product Concept

The product is a composition environment where the user does not edit notes directly. The user draws gesture-shapes, and each gesture becomes a reusable musical object.

There are three pattern types:

- `RhythmLoop`: a closed loop gesture that becomes a drum / percussion groove
- `MelodyLine`: an open curved line that becomes a melodic phrase
- `HarmonyPad`: an open, usually broad stroke that becomes a sustained chord bed

Each saved drawing becomes a reusable pattern master. Scenes only reference masters through instances. Instances have their own position, preset override, mute state, and mix state, but they inherit geometry and sequence from the pattern master.

The important design rule is:

- geometry is the source of identity
- presets define the voice family
- scenes define arrangement context
- instances define placement and mix

---

## VR-to-Desktop Translation Used in the Pilot

The pilot explicitly translates VR concepts to 2D desktop interactions:

- stylus draw -> pointer / mouse / trackpad draw on stage
- grab in space -> drag a floating pattern instance card
- depth in 3D -> explicit `Depth` slider in inspector
- scene tiles -> visible A-D scene strip
- instrument palette -> left dock

This mapping should remain conceptually equivalent if the target app returns to VR.

---

## UI Regions in the Pilot

The pilot is organized into four persistent regions.

1. Left dock
   - tabs: `Instruments`, `Patterns`, `Scenes`
   - select instrument groups
   - browse saved pattern masters
   - spawn / duplicate patterns
   - edit / copy scenes

2. Center stage
   - freehand drawing canvas
   - floating pattern instances
   - pattern commit card after stroke completion

3. Right inspector
   - selected instance controls
   - preset override
   - depth control
   - pattern metadata
   - `Shape DNA`

4. Bottom transport bar
   - scene slots `A-D`
   - arrangement strip with 8 slots
   - each arrangement slot chooses scene and bar length

Topbar session defaults:

- tempo default: `85 BPM`
- key default: `A minor`
- active group default: `Lo-Fi Kit`
- buttons: `Play`, `Start Audio`, `Load Demo Session`, `New Session`

---

## Data Model

Use these structures as the target domain model.

```ts
type PatternType = "RhythmLoop" | "MelodyLine" | "HarmonyPad";

type PatternDefinition = {
  id: string;
  type: PatternType;
  name: string;
  bars: number;
  tempoBase: number;
  key: string;
  groupId: string;
  presetId: string;
  points: Point[];                 // normalized, centered-square coordinates
  derivedSequence: RhythmSeq | MelodySeq | HarmonySeq;
  tags: string[];
  color: string;
  shapeProfile: ShapeProfile;
  soundProfile: SoundProfile;      // geometry-derived, before preset bias
  shapeSummary: string;
};

type InstrumentPreset = {
  id: string;
  label: string;
  category: string;
  voiceType: string;
  supportedTypes: PatternType[];
  envelope: { attack: number; release: number };
  filter: { base: number; range: number };
  fxSend: number;
};

type InstrumentGroup = {
  id: string;
  name: string;
  defaultPresetByType: Record<PatternType, string>;
  busFx: { reverb: number; delay: number };
  swatches: string[];
};

type PatternInstance = {
  id: string;
  patternId: string;
  sceneId: string;
  x: number;                      // normalized stage x, 0..1
  y: number;                      // normalized stage y, 0..1
  depth: number;                  // normalized depth, 0..1
  presetOverrideId: string | null;
  muted: boolean;
  gain: number;
  pan: number;
  brightness: number;
};

type Scene = {
  id: string;
  name: string;
  instanceIds: string[];
  tempoOverride: number | null;
  keyOverride: string | null;
};

type ArrangementSlot = {
  id: string;
  sceneId: string;                // empty string means unused
  bars: 4 | 8 | 16;
};

type AppState = {
  version: number;
  tempo: number;
  key: string;
  activeGroupId: string;
  activeTab: "instruments" | "patterns" | "scenes";
  drawMode: PatternType;
  patterns: PatternDefinition[];
  instances: PatternInstance[];
  scenes: Scene[];
  arrangement: ArrangementSlot[];
  activeSceneId: string;
  selectedInstanceId: string | null;
  selectedPatternId: string | null;
  queuedSceneId: string | null;
  counters: { rhythm: number; melody: number; harmony: number };
};
```

### Important invariants

- pattern masters contain geometry and sequence
- instances never own geometry or sequence
- swapping presets changes family, not geometry
- duplicating a pattern master copies the DNA
- duplicating an instance keeps the same pattern master
- copying a scene duplicates instances, not pattern masters

---

## Built-In Musical Catalog

### Keys

- `A minor`: root 57, scale `[0,2,3,5,7,8,10]`
- `C major`: root 60, scale `[0,2,4,5,7,9,11]`
- `D minor`: root 62, scale `[0,2,3,5,7,8,10]`
- `G major`: root 67, scale `[0,2,4,5,7,9,11]`

### Presets

Rhythm presets:

- `lofi-drums`
- `trap-drums`
- `dream-perc`

Melody presets:

- `lofi-piano`
- `lofi-bass`
- `trap-bell`
- `dream-lead`

Harmony presets:

- `lofi-pad`
- `trap-atmos`
- `dream-pad`

### Instrument groups

`Lo-Fi Kit`

- RhythmLoop -> `lofi-drums`
- MelodyLine -> `lofi-piano`
- HarmonyPad -> `lofi-pad`
- bus FX: reverb `0.18`, delay `0.08`

`Trap Kit`

- RhythmLoop -> `trap-drums`
- MelodyLine -> `trap-bell`
- HarmonyPad -> `trap-atmos`
- bus FX: reverb `0.10`, delay `0.18`

`Dream Ensemble`

- RhythmLoop -> `dream-perc`
- MelodyLine -> `dream-lead`
- HarmonyPad -> `dream-pad`
- bus FX: reverb `0.26`, delay `0.14`

---

## App Responsibilities

Even though the pilot is a single HTML file, the internal responsibilities are cleanly separable:

- `store`: state, persistence, pattern/scene/instance operations
- `interactionController`: pointer input, commit flow, inspector input, arrangement input
- `renderer`: dock, stage, inspector, scene strip, arrangement strip
- `sequencer`: transport, scene/arrangement playback, scheduling
- `audioEngine`: synthesis and FX graph

Use the same separation in the new app even if the UI stack changes.

---

## End-to-End User Workflow

### 1. Draw

The user chooses a draw mode:

- `RhythmLoop`
- `MelodyLine`
- `HarmonyPad`

The user draws a stroke on the stage.

### 2. Draft

On pointer release:

- the stroke is analyzed
- a `pendingDraft` is generated
- the UI shows a commit card with:
  - auto-name
  - summary
  - details
  - `Save`
  - `Save + Duplicate`
  - `Discard`

### 3. Save

Saving does all of the following:

- creates a new pattern master
- stores normalized geometry
- stores derived sequence
- stores `shapeProfile`
- stores `soundProfile`
- stores `shapeSummary`
- spawns one instance into the active scene
- optionally spawns a second instance if using `Save + Duplicate`

### 4. Reuse

Any saved pattern can be:

- spawned into the current scene
- duplicated as a new master
- reused across scenes

### 5. Arrange

The user can:

- edit Scene A-D
- copy one scene into another
- fill arrangement slots 1-8
- choose scene and bar count for each slot

### 6. Play

Playback runs in:

- `scene` mode if no arrangement slots are populated
- `arrangement` mode if at least one slot references a scene

Scene switching while playing is quantized to bar boundaries when not in arrangement mode.

---

## Geometry Pipeline

The stroke-to-sound pipeline has two distinct analysis layers.

### Layer 1: raw stroke metrics

Raw pointer points are analyzed in screen space.

Computed metrics:

- bounding box
- width / height
- center
- stroke length
- average size
- wobble
- closed / not closed
- tilt

Important implementation detail:

- `RhythmLoop` requires a closed stroke
- closed check:
  - `distance(first,last) < min(64, averageSize * 0.32)`

### Layer 2: normalized geometry

The saved pattern geometry is normalized into a centered square:

```ts
size = max(bbox.width, bbox.height)
originX = bbox.centerX - size / 2
originY = bbox.centerY - size / 2

normalized.x = (point.x - originX) / size
normalized.y = (point.y - originY) / size
```

Important reason:

- aspect ratio is preserved
- the pattern can be reused independent of canvas size
- all shape DNA is deterministic and portable

The pilot uses:

- raw metrics for sequence generation thresholds such as pixel length and pixel size
- normalized points for shape DNA and timbre DNA

If porting to another app, replace pixel thresholds with world-space or viewport-independent thresholds.

---

## ShapeProfile

`shapeProfile` is a normalized geometry description, almost entirely in `0..1`.

```ts
type ShapeProfile = {
  closedness: number;
  circularity: number;
  aspectRatio: number;
  angularity: number;
  symmetry: number;
  verticalSpan: number;
  horizontalSpan: number;
  pathLength: number;
  speedVariance: number;
  curvatureMean: number;
  curvatureVariance: number;
  centroidHeight: number;
  directionBias: number;
  tilt: number;
  tiltSigned: number;   // -1..1
  wobble: number;
};
```

### Exact metric formulas

Given normalized `points`:

#### `closedness`

```ts
closedness = clamp(1 - closeDistance / 0.28, 0, 1)
```

#### `circularity`

For rhythm loops:

```ts
perimeter = strokeLength + closeDistance
circularity = clamp((4 * PI * polygonArea(points)) / perimeter^2 / 0.9, 0, 1)
```

For non-loops:

```ts
circularity = clamp(circularity * 0.35 + closedness * 0.3, 0, 1)
```

#### `aspectRatio`

```ts
aspectRatio = min(width, height) / max(width, height)
```

Near `1.0` means square / round. Lower values mean stretched.

#### `curvatureMean`

```ts
curvatureMean = clamp(avg(abs(turnAngles)) / (PI * 0.55), 0, 1)
```

#### `curvatureVariance`

```ts
curvatureVariance = clamp(std(abs(turnAngles)) / (PI * 0.4), 0, 1)
```

#### `angularity`

```ts
angularity = clamp(curvatureMean * 0.72 + curvatureVariance * 0.52, 0, 1)
```

#### `symmetry`

The pilot resamples the stroke to 20 points, mirrors index pairs around the centroid, and computes an error score. The final score is:

```ts
symmetry = clamp(1 - error / (samples.length * 0.55), 0, 1)
```

#### `verticalSpan`

```ts
verticalSpan = clamp(height / 0.95, 0, 1)
```

#### `horizontalSpan`

```ts
horizontalSpan = clamp(width / 0.95, 0, 1)
```

#### `pathLength`

```ts
pathLength = clamp(strokeLength / 2.75, 0, 1)
```

#### `speedVariance`

```ts
speedVariance = clamp(std(segmentLengths) / meanSegmentLength / 0.95, 0, 1)
```

#### `centroidHeight`

```ts
centroidHeight = clamp(1 - centroid.y, 0, 1)
```

Higher means visually higher on the normalized frame.

#### `directionBias`

```ts
directionBias = clamp((last.x - first.x) / width * 0.5 + 0.5, 0, 1)
```

Near `0.5` is balanced. Far from `0.5` means strong directional pull.

#### `tiltSigned`

```ts
tiltSigned = clamp((last.y - first.y) / height, -1, 1)
```

#### `tilt`

```ts
tilt = clamp((tiltSigned + 1) / 2, 0, 1)
```

#### `wobble`

Based on radius variance from centroid:

```ts
wobble = clamp(std(radii) / meanRadius / 0.4, 0, 1)
```

---

## SoundProfile

`soundProfile` is derived only from geometry. It is the geometry-to-synthesis bridge.

```ts
type SoundProfile = {
  brightness: number;
  resonance: number;
  drive: number;
  attackBias: number;
  releaseBias: number;
  detune: number;
  modDepth: number;
  stereoSpread: number;
  grooveInstability: number;
  delayBias: number;
  reverbBias: number;
  waveMorph: number;
  filterMotion: number;
  transientSharpness: number;
  body: number;
};
```

### RhythmLoop sound mapping

Supporting terms:

```ts
smoothness = 1 - angularity
asymmetry = 1 - symmetry
instability = clamp(wobble * 0.7 + asymmetry * 0.55 + curvatureVariance * 0.35, 0, 1)
```

Final mapping:

```ts
brightness         = 0.22 + angularity * 0.45 + instability * 0.16
resonance          = 0.16 + angularity * 0.34 + curvatureVariance * 0.24
drive              = 0.12 + angularity * 0.68 + asymmetry * 0.22
attackBias         = 0.24 + angularity * 0.60 + (1 - circularity) * 0.12
releaseBias        = 0.22 + circularity * 0.60 + symmetry * 0.16
detune             = 0.05 + instability * 0.26
modDepth           = 0.08 + instability * 0.54
stereoSpread       = 0.12 + (1 - aspectRatio) * 0.34 + asymmetry * 0.14
grooveInstability  = instability
delayBias          = 0.04 + (1 - aspectRatio) * 0.18 + instability * 0.18
reverbBias         = 0.08 + circularity * 0.18 + asymmetry * 0.14
waveMorph          = 0.20 + angularity * 0.62
filterMotion       = 0.10 + instability * 0.44
transientSharpness = 0.25 + angularity * 0.70
body               = 0.24 + circularity * 0.58 + symmetry * 0.14
```

Interpretation:

- round + symmetric -> heavier, longer, more stable
- spiky + asymmetric -> sharper, brighter, more driven, more unstable

### MelodyLine sound mapping

Supporting term:

```ts
contourPull = abs(directionBias - 0.5) * 2
```

Final mapping:

```ts
brightness         = 0.24 + angularity * 0.40 + centroidHeight * 0.14 + verticalSpan * 0.16
resonance          = 0.16 + curvatureMean * 0.44 + curvatureVariance * 0.18
drive              = 0.08 + angularity * 0.44 + speedVariance * 0.20
attackBias         = 0.18 + speedVariance * 0.52 + angularity * 0.18
releaseBias        = 0.20 + smoothness * 0.56 + horizontalSpan * 0.12
detune             = 0.08 + curvatureVariance * 0.48 + asymmetry * 0.18
modDepth           = 0.12 + curvatureMean * 0.36 + contourPull * 0.26
stereoSpread       = 0.14 + horizontalSpan * 0.44 + contourPull * 0.12
grooveInstability  = 0.08 + speedVariance * 0.28
delayBias          = 0.12 + contourPull * 0.34 + horizontalSpan * 0.10
reverbBias         = 0.12 + smoothness * 0.28 + verticalSpan * 0.16
waveMorph          = 0.16 + angularity * 0.62
filterMotion       = 0.16 + contourPull * 0.42 + curvatureVariance * 0.16
transientSharpness = 0.20 + angularity * 0.56 + speedVariance * 0.18
body               = 0.18 + smoothness * 0.46 + verticalSpan * 0.12
```

Interpretation:

- smooth, wide lines -> rounder, longer, more singing
- jagged lines -> brighter, sharper, shorter, more articulated
- strong directional slant -> more glide pull and more motion

### HarmonyPad sound mapping

Supporting terms:

```ts
smoothness = 1 - angularity
asymmetry = 1 - symmetry
tiltAmount = abs(tiltSigned)
```

Final mapping:

```ts
brightness         = 0.16 + centroidHeight * 0.24 + angularity * 0.18 + tiltAmount * 0.18
resonance          = 0.10 + tiltAmount * 0.44 + asymmetry * 0.20
drive              = 0.05 + angularity * 0.26 + asymmetry * 0.12
attackBias         = 0.12 + angularity * 0.42 + asymmetry * 0.12
releaseBias        = 0.32 + pathLength * 0.28 + smoothness * 0.28
detune             = 0.12 + asymmetry * 0.42 + horizontalSpan * 0.16
modDepth           = 0.16 + tiltAmount * 0.32 + asymmetry * 0.24
stereoSpread       = 0.24 + horizontalSpan * 0.48
grooveInstability  = 0.02 + curvatureVariance * 0.18
delayBias          = 0.08 + tiltAmount * 0.18
reverbBias         = 0.24 + pathLength * 0.36 + smoothness * 0.18
waveMorph          = 0.22 + angularity * 0.36 + tiltAmount * 0.18
filterMotion       = 0.22 + tiltAmount * 0.50 + asymmetry * 0.14
transientSharpness = 0.08 + angularity * 0.32
body               = 0.28 + smoothness * 0.42 + verticalSpan * 0.12
```

Interpretation:

- wider, smoother, longer strokes -> larger bloom and wider spatial pad
- more tilt -> more chord color movement and more filter motion
- more asymmetry -> more shimmer and detune

---

## Preset Bias + Effective Sound

The final voice is not just geometry. It is:

```ts
effectiveSound = geometrySound * 0.78 + presetBias * 0.42
```

Clamped to `0..1` per field.

This intentionally lets geometry dominate while preserving the preset family.

### Preset-bias rules

Base preset bias starts from:

```ts
brightness 0.28
resonance 0.24
drive 0.18
attackBias 0.28
releaseBias 0.32
detune 0.16
modDepth 0.20
stereoSpread 0.20
grooveInstability 0.14
delayBias 0.14
reverbBias 0.18
waveMorph 0.24
filterMotion 0.18
transientSharpness 0.28
body 0.28
```

Then preset family applies additive bias:

`trap`

- brightness `+0.18`
- drive `+0.20`
- transientSharpness `+0.16`
- delayBias `+0.08`
- releaseBias `-0.08`

`dream`

- reverbBias `+0.24`
- modDepth `+0.18`
- stereoSpread `+0.18`
- releaseBias `+0.20`

`bass`

- body `+0.32`
- brightness `-0.14`
- detune `-0.08`
- attackBias `+0.08`

`pad`

- releaseBias `+0.24`
- reverbBias `+0.20`
- stereoSpread `+0.12`
- body `+0.10`

`bell`

- brightness `+0.24`
- transientSharpness `+0.18`
- resonance `+0.12`
- delayBias `+0.10`

`piano`

- body `+0.12`
- attackBias `+0.08`
- waveMorph `-0.04`

`drums` or any `RhythmLoop`

- transientSharpness `+0.16`
- grooveInstability `+0.08`
- body `+0.10`

`perc`

- brightness `+0.08`
- transientSharpness `+0.08`

### Waveform resolution

After effective sound is computed, waveform choice is selected.

For `HarmonyPad`:

- `waveA = sawtooth` if `waveMorph > 0.55`, else `triangle`
- `waveB = triangle` if `body > 0.58`, else `sine`

For `MelodyLine`:

- bass voices:
  - `waveA = triangle` if `body > 0.6`, else `square`
  - `waveB = sine`
- bell voices:
  - `waveA = triangle`
  - `waveB = sine` if `waveMorph > 0.58`, else `square`
- other melody voices:
  - `waveA = square` if `waveMorph > 0.58`
  - else `triangle` if `body > 0.6`
  - else `sine`
  - `waveB = triangle` if `brightness > 0.6`, else `sine`

For `RhythmLoop`:

- `waveA = triangle` if `transientSharpness > 0.58`, else `sine`
- `waveB = sine`

### Filter type resolution

```ts
if HarmonyPad:
  bandpass if filterMotion > 0.62 else lowpass
else if brightness > 0.74 and body < 0.45:
  highpass
else if resonance > 0.6 and MelodyLine:
  bandpass
else:
  lowpass
```

---

## Plain-Language Shape-to-Sound Explanation

This section restates the implemented system in direct product language so another agent can understand not just the formulas, but the audible result.

### Pipeline

In the current prototype, a shape changes sound in two layers.

First, the stroke is analyzed into a normalized `shapeProfile`: things like `circularity`, `angularity`, `symmetry`, `verticalSpan`, `horizontalSpan`, `pathLength`, `speedVariance`, `curvature`, `tilt`, and `wobble`. In the current pilot, that comes from the shape-analysis pipeline in [index.html:1336](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVRPilot/index.html#L1336) and [index.html:1464](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVRPilot/index.html#L1464).

Second, that `shapeProfile` is converted into a `soundProfile`: `brightness`, `resonance`, `drive`, `attackBias`, `releaseBias`, `detune`, `modDepth`, `stereoSpread`, `grooveInstability`, `delayBias`, `reverbBias`, `waveMorph`, `filterMotion`, `transientSharpness`, and `body`. In the current pilot, that mapping is in [index.html:1545](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVRPilot/index.html#L1545).

That DNA is stored on the pattern master when the user saves it, so reused instances keep the same geometry-driven identity. In the current pilot, draft creation and persistence of that DNA happen in [index.html:1821](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVRPilot/index.html#L1821). Presets then bias that DNA rather than replacing it in [index.html:1985](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVRPilot/index.html#L1985).

### Rhythm

For `RhythmLoop`, the shape affects both the groove pattern and the drum timbre.

#### Sequence behavior

Sequence generation is implemented in [index.html:1664](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVRPilot/index.html#L1664).

- Bigger closed loops become `4` bars; smaller ones become `2`.
- More `angularity`, less `symmetry`, and more `wobble` increase density, so the loop gains more accents and extra percussion.
- `aspectRatio` changes the kick pattern:
  - stretched or less round loops bias one kick layout
  - very circular loops bias a simpler, more centered kick layout
- `symmetry` changes the snare behavior:
  - more symmetric loops tend toward a single centered snare hit
  - less symmetric loops introduce extra snare placements
- `angularity` changes hat stride:
  - sharper loops go denser, down to every step
  - smoother loops stay more open
- `grooveInstability` and `wobble` generate swing and per-hit `microShift`, so timing gets more broken and more human when the shape is rougher

#### Timbre behavior

Timbre mapping is derived in [index.html:1545](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVRPilot/index.html#L1545) and applied during playback in [index.html:3232](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVRPilot/index.html#L3232).

- More `circularity` increases `body` and `releaseBias`, so kicks get heavier and longer.
- More `angularity` increases `transientSharpness`, `drive`, `brightness`, and `resonance`, so drums get harder, brighter, and more distorted.
- More asymmetry and `wobble` raise `grooveInstability`, `detune`, `modDepth`, and `filterMotion`, so the loop sounds less stable and more restless.

On playback, that becomes:

- kick click gets brighter and sharper
- kick pitch and decay change
- snares get brighter, noisier, or fuller
- hats and percussion get brighter, narrower, or more biting
- filter movement, delay, reverb, and stereo spread all increase with more extreme shapes

The practical reading is:

- a near-perfect circle sounds heavier, more grounded, and more stable
- a spiky, uneven ellipse sounds busier, sharper, more swung, and more aggressive

### Melody

For `MelodyLine`, the shape strongly affects pitch contour, articulation, glide, and tone.

#### Sequence behavior

Note derivation is implemented in [index.html:1738](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVRPilot/index.html#L1738).

- Longer strokes or larger `pathLength` increase slice count from `8` to `16`, so the phrase produces more note events.
- `verticalSpan` increases pitch reach because the line is mapped higher and lower across the scale.
- Each sampled point’s `y` position maps to scale pitch.
- Local stroke speed changes note length:
  - slower segments create longer notes
  - faster segments create shorter notes
- Local curvature raises note velocity, so bends and turns become accents.
- `directionBias` and local slope create `glide`, so the line’s directional pull becomes pitch slide into notes.

#### Timbre behavior

Timbre mapping is derived in [index.html:1545](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVRPilot/index.html#L1545) and applied during melodic playback in [index.html:3341](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVRPilot/index.html#L3341).

- More `angularity` makes the sound brighter, sharper, more driven, and more metallic.
- More smoothness increases `releaseBias` and `body`, so notes feel rounder and more legato.
- More `speedVariance` increases `attackBias`, so unevenly drawn lines become more staccato and more percussive.
- More `curvatureMean` and `curvatureVariance` increase `resonance`, `detune`, and `modDepth`, so wavier lines get more motion and vibrato-like animation.
- Stronger `directionBias` increases `filterMotion` and `delayBias`, so strongly left-to-right or right-to-left lines have more contour pull and more echo feel.
- More `horizontalSpan` increases stereo spread and release, so wide gestures feel broader in space.

On playback, that becomes:

- attack and release scaling
- waveform choice shifts
- oscillator detune increases
- vibrato depth and rate increase
- glide amount into the target pitch increases

The practical reading is:

- a smooth arc gives a warmer, singing, connected phrase
- a jagged zig-zag gives a brighter, more biting, more animated line with shorter notes and stronger glide accents

### Harmony

For `HarmonyPad`, the shape affects chord choice, voicing, sustain, bloom, and filter motion.

#### Sequence and harmony behavior

Harmony derivation is implemented in [index.html:1783](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVRPilot/index.html#L1783).

- Longer strokes become `2`, `4`, or `8` bars.
- `centroidHeight` affects the root register.
- `tiltSigned` chooses chord flavor:
  - upward tilt -> `maj7`
  - downward tilt -> `sus`
  - middle or neutral tilt -> `minor`
- `horizontalSpan` widens the voicing:
  - wider pads spread upper chord tones farther apart
  - very wide pads can drop the root an octave lower

#### Timbre behavior

Timbre mapping is derived in [index.html:1545](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVRPilot/index.html#L1545) and applied during sustained voice playback in [index.html:3341](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVRPilot/index.html#L3341).

- More tilt increases `filterMotion` and `resonance`, so tilted pads sweep more and feel less static.
- More `horizontalSpan` increases `stereoSpread` and `detune`, so wide pads feel more open and more chorused.
- More `pathLength` increases `releaseBias` and `reverbBias`, so long gestures bloom and trail more.
- More smoothness increases `body` and release, so soft strokes become dense, warm beds.
- More asymmetry increases detune and modulation, so uneven pad shapes shimmer more.
- More `angularity` adds some brightness and attack, but pads remain less percussive than melody and rhythm.

The practical reading is:

- a long, smooth, wide stroke becomes a lush, blooming, spacious pad
- a tilted, asymmetrical stroke becomes a more animated, resonant pad with moving color and wider detuned spread

### Preset Interaction

Presets still matter, but they now act like a family bias, not the whole sound. That blend is resolved in [index.html:1985](/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVRPilot/index.html#L1985).

That means:

- the preset says “this is still a trap drum kit / piano / dream pad family”
- the shape says “this specific instance is heavy, sharp, unstable, blooming, animated, narrow, wide, smooth, or broken”

So if the user swaps presets, the same shape keeps its geometry personality, but the voice family changes around it.

### Recommended future appendix

A useful extension for future handoff material would be a shape-by-shape cheat sheet, for example:

- perfect circle vs spiky loop
- smooth melody arc vs jagged line
- flat pad stroke vs steep tilted pad

That kind of appendix would help another implementation team tune the system by ear.

---

## Pattern Generation Algorithms

This is the part another app must preserve carefully.

## RhythmLoop derivation

### Eligibility

- the stroke must be closed

### Bar length

Raw-space rule in the pilot:

- `4 bars` if `averageSize > 240`
- else `2 bars`

### Density

```ts
density = clamp(round(6 + angularity*4 + (1 - symmetry)*3 + wobble*3), 6, 14)
```

### Swing

```ts
swing = clamp(grooveInstability * 0.34 + wobble * 0.08, 0, 0.42)
```

### Kick pattern

```ts
if aspectRatio < 0.52:
  [0, 6, 10, 13]
else if circularity > 0.75:
  [0, 8, 12]
else:
  [0, 7, 10, 13]
```

### Snare pattern

```ts
if symmetry > 0.6:
  [8]
else:
  [5, 8, 13]
```

### Hat stride

```ts
hatStride = 1 if angularity > 0.68 else 2
```

### Event creation

For each bar:

- create kicks from `kickPattern`
- create snares from `snarePattern`
- fill remaining steps with alternating `hat` and `perc`
- add bonus accents at `[3, 11, 15]` when density is high or symmetry is low

#### Kick event values

```ts
velocity  = 0.6 + body * 0.3 - index * 0.05
microShift = sin((step + 1) * 1.13 + wobble * 5) * grooveInstability * 0.05
```

#### Snare event values

```ts
velocity  = 0.56 + transientSharpness * 0.26 - index * 0.05
microShift = sin((step + 3) * 0.92 + angularity * 4) * grooveInstability * 0.08
```

#### Hat / perc event values

```ts
emphasis = 0.08 if step % 4 === 0 else 0
velocity = 0.24 + brightness * 0.22 + emphasis
microShift = sin((step + 2) * 1.47 + curvatureVariance * 5) * grooveInstability * 0.18
```

#### Bonus accent values

```ts
velocity = 0.26 + drive * 0.18
microShift = sin((step + 4) * 1.81) * grooveInstability * 0.12
```

### Result

```ts
derivedSequence = {
  kind: "rhythm",
  totalSteps: bars * 16,
  swing,
  events
}
```

## MelodyLine derivation

### Bar length

Raw-space rule in the pilot:

- `4 bars` if `strokeLength > 880`
- else `2 bars`

### Slice count

```ts
sliceCount = 16 if strokeLength > 820 or pathLength > 0.68 else 8
```

The stroke is resampled to `sliceCount` points.

### Register reach

```ts
verticalScale = 0.72 + verticalSpan * 1.2
pitchCenter = bbox.minY + bbox.height / 2
centeredY = clamp((sample.y - pitchCenter) / bbox.height * verticalScale + 0.5, 0, 0.999)
midi = pitchFromRelative(centeredY, key)
```

`pitchFromRelative` quantizes to the currently selected key.

### Duration

Local speed is measured from adjacent resampled points.

```ts
durationBase =
  6 if speed < 24
  4 if speed < 40
  2 otherwise

durationSteps = clamp(round(durationBase + (1 - speedVariance)*1.5 - transientSharpness*1.2), 2, 7)
```

### Velocity

```ts
curvature = abs(next.y - sample.y) + abs(sample.y - previous.y)
velocity = clamp(0.34 + curvature * 0.86 + curvatureMean * 0.16, 0.3, 0.96)
```

### Glide

```ts
slope = clamp((next.y - previous.y) / bbox.height * 4, -1, 1)
glide = slope * (0.3 + filterMotion * 0.85)
```

### Step placement

```ts
step = floor((index / sliceCount) * totalSteps)
```

### Result

```ts
derivedSequence = {
  kind: "melody",
  totalSteps: bars * 16,
  notes: [{ step, midi, durationSteps, velocity, glide }]
}
```

## HarmonyPad derivation

### Bar length

Raw-space rule in the pilot:

- `8 bars` if `strokeLength > 1200`
- `4 bars` if `strokeLength > 700`
- else `2 bars`

### Root

```ts
rootMidi = pitchFromRelative(1 - centroidHeight, key) - 12
```

### Chord flavor from tilt

```ts
if tiltSigned > 0.28:
  flavor = "maj7"
else if tiltSigned < -0.22:
  flavor = "sus"
else:
  flavor = "minor"
```

Intervals:

- `sus` -> `[0, 5, 7, 10]`
- `maj7` -> `[0, 4, 7, 11]`
- `minor` -> `[0, 3, 7, 10]`

### Voicing spread from width

```ts
spread = round(horizontalSpan * 10)
```

Chord construction:

- if `horizontalSpan > 0.72`, drop the root octave on the first note
- push the 3rd chord tone up by `round(spread * 0.45)`
- push the top note up by `spread`

### Result

```ts
derivedSequence = {
  kind: "harmony",
  totalSteps: bars * 16,
  flavor,
  rootMidi,
  chord
}
```

---

## Sequencer and Timing

### Step resolution

- `BAR_STEPS = 16`
- one step duration:

```ts
stepDuration = 60 / tempo / 4
```

This is a 16th-note grid.

### Scheduling model

The pilot uses a simple lookahead scheduler:

- look ahead `0.12s`
- tick every `25ms`
- when `nextNoteTime < currentTime + 0.12`, schedule the current step and advance transport

### Scene mode

If no arrangement slots are populated:

- loop the active scene
- track `sceneStep`
- queued scene changes apply on the next bar

### Arrangement mode

If any arrangement slot has a scene:

- playback starts from the first populated slot
- track `slotIndex` and `slotStep`
- each slot plays for `slot.bars * 16` steps
- then move to the next populated slot, wrapping around

### Per-instance scheduling

At each step:

- get all instances in `playbackSceneId`
- skip muted instances
- read their pattern master
- compute effective preset
- compute effective sound profile
- compute local step as `currentStep % pattern.totalSteps`

Then:

- `RhythmLoop`: schedule events matching current step, with swing and microShift
- `MelodyLine`: schedule notes matching current step
- `HarmonyPad`: trigger chord only on local step `0`

### Rhythm timing adjustments

```ts
swingOffset = swing * stepDuration * 0.45 on odd steps
playTime = stepTime + swingOffset + microShift * stepDuration
```

---

## Audio Engine

The pilot uses plain Web Audio, but the synthesis logic is portable.

### Global graph

- `master`
- `reverb` via impulse-response convolver
- `delay` with feedback and lowpass tone shaping

### Voice chain

Each voice passes through:

- gain output
- filter
- waveshaper drive
- stereo panner
- reverb send
- delay send
- optional LFO into filter frequency

### Filter frequency

```ts
base =
  500 for RhythmLoop
  320 for HarmonyPad
  700 for MelodyLine

filter.frequency =
  base + (brightness*0.4 + soundProfile.brightness*0.6) * range

range =
  4200 for HarmonyPad
  6400 otherwise
```

### Filter Q

```ts
Q = 0.9 + resonance * 15
```

### Drive

Drive uses a tanh waveshaper:

```ts
amount = 1 + drive * 18
```

### FX sends

```ts
reverbSendGain = reverbSend * (0.35 + reverbBias * 1.1)
delaySendGain  = delaySend  * (0.24 + delayBias  * 1.08)
```

### Filter LFO

LFO exists when `filterMotion > 0.08`.

Rate:

- rhythm: `3 + modDepth * 10`
- melody: `1.2 + modDepth * 7`
- harmony: `0.25 + modDepth * 2.2`

Depth:

```ts
lfoDepth = filter.frequency * (0.04 + filterMotion * 0.18)
```

---

## Drum synthesis behavior

### Kick

The kick is built from:

- oscillator body
- short noisy click

Click filter:

```ts
1800 + transientSharpness * 5000
```

Body pitch:

```ts
trap-drums ? 122 : 96
+ body * 52
```

Pitch drops to:

```ts
34 + body * 18
```

Decay and amplitude both scale with `releaseBias` and `body`.

Result:

- more circular / weighty loops -> fuller and longer kick
- more angular loops -> harder click and sharper attack

### Snare

The snare is built from:

- filtered noise burst
- optional tonal body if `body > 0.44`

Noise filter:

```ts
trap-drums ? 1700 : 1100
+ brightness * 2800
```

Tone frequency:

```ts
180 + body * 120
```

Result:

- brighter / sharper shapes -> brighter crack
- body-heavy shapes -> thicker snare with more tone

### Hat / perc

Built from filtered noise.

Hat / perc filter:

```ts
perc:
  1000 + body*600 + brightness*2200

hat:
  dream-perc ? 6200 : 7600
  + brightness*2400
```

Q:

```ts
0.8 + resonance * 10
```

Result:

- angular shapes -> brighter, tighter hats
- unstable shapes -> more animated percussion timing

---

## Melody / Harmony synthesis behavior

### Envelope scaling

Preset envelope is scaled by geometry:

```ts
attack = clamp(preset.attack * (0.35 + attackBias * 1.6), 0.003, 0.24)
release = clamp(preset.release * (0.45 + releaseBias * 1.9), 0.08, 1.4)
```

For `HarmonyPad`, the max envelope limits are larger:

- attack up to `1.6`
- release up to `4.2`

### Glide

Melody and harmony voices use glide on oscillator A:

```ts
startFrequency = targetFrequency * 2^(glide * (0.18 + filterMotion * 0.24) / 12)
exponentialRampToValueAtTime(targetFrequency, time + max(0.015, attack * 1.4))
```

### Detune / modulation

Oscillator A detune:

```ts
bass:
  detune * 8 - 2
other:
  detune * 16
```

If `modDepth > 0.1`, vibrato is added:

```ts
vibrato rate:
  HarmonyPad -> 3.2 + modDepth * 2.5
  other      -> 3.2 + modDepth * 6.5

vibrato depth:
  bass -> 4 + modDepth * 26
  other -> 10 + modDepth * 26
```

### Oscillator B

Oscillator B is added unless:

- voice is bass
- and type is not HarmonyPad

Frequency:

- bell voices -> one octave up
- otherwise -> slight spread using detune

Result:

- melody lines with more curvature / motion become more animated
- harmony pads with more width and asymmetry become wider and more chorused

---

## Spatial Mixing Model

The pilot does not use true 3D audio positioning. It translates position into mix controls.

### Horizontal position -> pan

When instance `x` changes:

```ts
pan = x * 2 - 1
```

### Vertical position -> brightness

When instance `y` changes:

```ts
brightness = 1 - y
```

Higher on the screen sounds brighter.

### Depth -> gain and FX emphasis

When `depth` changes:

```ts
gain = 1.08 - depth * 0.58
```

Depth also scales reverb and delay sends in playback functions.

Interpretation:

- closer / shallower depth -> louder, more present
- deeper -> lower gain, more ambient send contribution

---

## Visual Feedback Rules

The pilot’s visuals also reflect shape DNA and should be preserved conceptually.

### RhythmLoop

- more angular shapes -> larger flicker and harsher dashed overlay
- transient-heavy sounds -> more aggressive white edge accent

### MelodyLine

- modulation and filter motion create a ghosted animated duplicate trace
- more curvature -> thicker / livelier glow

### HarmonyPad

- width and bloom create a large aura ellipse
- tilt rotates the aura
- higher reverb bias increases fill softness

### Universal

- pattern pulses on recent triggers
- a bright marker travels along the stroke during playback

These visuals are not just decoration. They teach the user how geometry affects sound.

---

## Scene and Arrangement Behavior

### Scene behavior

- there are four fixed scenes: `scene-a`, `scene-b`, `scene-c`, `scene-d`
- editing scene changes which instances are visible and draggable
- copying a scene:
  - removes target scene instances
  - duplicates source instances into target
  - keeps references to the same pattern masters
  - copies scene-level overrides

### Arrangement behavior

- maximum 8 slots
- each slot chooses:
  - a scene
  - `4`, `8`, or `16` bars
- if at least one slot is populated, transport runs in arrangement mode
- arrangement playback wraps to the next populated slot

### Scene switching during playback

Without arrangement:

- selecting another scene while playing queues it
- queued scene activates on the next bar

With arrangement:

- arrangement controls scene switching

---

## Persistence

The pilot persists full session state to browser `localStorage`.

Storage key:

- `rhythmforge-concept-v1`

Current state version:

- `2`

Persisted state includes:

- patterns
- instances
- scenes
- arrangement
- tempo
- key
- active group
- selection
- counters

On load, the pilot normalizes state and backfills legacy patterns by recomputing missing `shapeProfile`, `soundProfile`, and `shapeSummary`.

---

## Draft / Commit Rules

The draft stage is important and should be preserved.

### Draft contents

A draft includes:

- pattern metadata
- derived sequence
- spawn position
- shape DNA
- human-readable summary
- human-readable detail string

### Draft naming

Naming counters are separate by type:

- `Beat-01`, `Beat-02`, ...
- `Melody-01`, `Melody-02`, ...
- `Pad-01`, `Pad-02`, ...

### Save + Duplicate

This saves the master once, then spawns two instances in the current scene with slightly offset positions.

---

## Inspector Requirements

The inspector should expose three distinct ideas:

1. Pattern identity
   - name
   - type
   - bars
   - preset
   - tags

2. Reusable Pattern rule
   - instances are references
   - swapping presets changes voice family
   - shape DNA still drives character

3. Shape DNA
   - summary text
   - 5-6 metric bars depending on type
   - trait chips such as:
     - `Heavy body`
     - `Sharp edge`
     - `Driven`
     - `Animated`
     - `Bloom`
     - `Moving filter`
     - `Echo pull`
   - preset family description
   - geometry contribution description
   - effective blend readout

This explanatory layer is essential if another app wants users to understand the mapping instead of treating it as a black box.

---

## Recommended Porting Strategy

If porting this to a game engine, VR app, or native tool:

1. Preserve the data model exactly.
2. Preserve normalized geometry storage.
3. Preserve the separation between:
   - shape analysis
   - sequence derivation
   - geometry sound derivation
   - preset family bias
4. Replace raw pixel thresholds with world-space thresholds.
5. Keep pattern masters reusable and instances lightweight.
6. Preserve scene copy semantics and arrangement semantics.
7. Keep the explanatory `Shape DNA` layer.

### Safe adaptations

- replace Web Audio with FMOD, Wwise, Unity DSPGraph, Unreal MetaSounds, JUCE, or custom audio
- replace 2D drag with full 3D placement
- replace pointer input with stylus or VR controller drawing
- replace HTML inspector with radial menus or floating panels

### Behaviors that should not be lost

- geometry affects both sequence and timbre
- preset swaps do not rewrite geometry identity
- one pattern master can be instantiated many times
- arrangement uses scene references, not pattern copies
- the system remains pattern-level, not piano-roll-level

---

## Minimal Reimplementation Checklist

Another agent implementing this in another application should support at least:

- three pattern types
- stroke capture
- draft commit flow
- pattern library masters
- scene instances
- instrument groups
- preset overrides
- scenes A-D
- arrangement slots
- shape analysis
- sound profile derivation
- preset bias blending
- rhythm/melody/harmony sequence derivation
- playback scheduler
- spatial mix mapping
- persistence
- `Shape DNA` explanation UI

---

## Summary

The HTML pilot proves a specific design:

- drawing is the composition gesture
- geometry is reusable musical DNA
- presets define family, not full identity
- scenes and arrangement build songs from pattern references
- the system is expressive because shapes change both what is played and how it sounds

That combination is the product, not just the interface.
