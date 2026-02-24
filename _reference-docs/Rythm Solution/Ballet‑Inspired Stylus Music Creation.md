Below is a structured Markdown description of the concept that you can treat as a lightweight spec when implementing later (Unity/Unreal/Meta XR/MX Ink).

***

# RhythmForge VR – Ballet‑Inspired Stylus Music Creation  
*“Conduct reality. Compose destiny. Create music with motion.”*

## 1. High‑Level Concept

RhythmForge VR is a **spatial music creation instrument** where the user composes and performs music through **ballet‑like movements** of a stylus in VR.

- The **right hand (MX Ink stylus)** acts as a **conductor’s baton + paintbrush for sound**.
- The **left hand (Quest controller)** acts as a **palette and stage manager** for instruments, loops, and global controls.
- Every gesture leaves a **glowing trail in 3D space** that becomes a **living musical object** (loop, melody line, effect field).
- Movement design is inspired by **ballet vocabulary**: smooth, continuous, expressive; no “UI twitching”, no clicky/gamey gestures.

The mental model for the user:  
> “I am a choreographer of sound. When I draw in the air, I’m writing music. When I move around the stage, I’m mixing it.”

***

## 2. Target Platform & Hardware

- **Headset**: Meta Quest 3 (native)
- **Primary Input**: Logitech MX Ink stylus (right hand)
- **Secondary Input**: Quest controller (left hand)
- **Tracking**: 6DoF for both devices
- **Audio**: Low‑latency spatial audio (Unity Audio / FMOD / Wwise – pick one engine)

Implementation assumptions:

- Use **asymmetric interaction**: stylus = high‑precision, controller = coarse selection, navigation, menus.
- Latency budget: **< 20 ms end‑to‑end input → audio output** for playable feel.

***

## 3. Core Interaction Model

### 3.1 Modes (Global “Brushes”)

There are **three core creative modes** mapped to how trails look and how audio is generated:

1. **Rhythm Mode**
   - Semantics: beats, pulses, percussive sequences.
   - Visual: bold, pulsing trails (accent color: `--color-trail-rhythm`).
   - Audio: drum hits, percussive loops, rhythmic triggers.

2. **Melody Mode**
   - Semantics: single‑voice lines, hooks, lead lines.
   - Visual: thinner, flowing curves (accent color: `--color-trail-melody`).
   - Audio: pitched notes snapped to a scale, mono line looping.

3. **Harmony Mode**
   - Semantics: chords, pads, harmonic beds.
   - Visual: thicker, slower‑fading ribbons (accent color: `--color-trail-harmony`).
   - Audio: chords, sustained textures, evolving pads.

**Mode Switching UX:**

- Primary option (for hackathon MVP): **UI buttons / radial menu** (left controller).
- Long‑term: **MX Ink button 1** cycles modes (Rhythm → Melody → Harmony), **button 2** toggles an alternate tool (e.g., eraser / selection).

***

## 4. Gesture Vocabulary (Ballet‑Inspired)

Each gesture is defined by:

- **Movement pattern** (shape + speed).
- **Stylus parameters** (pressure, tilt, height).
- **Resulting musical mapping** (what it creates/controls).

These don’t need explicit gesture classification initially; instead, you **infer from the stroke’s geometric properties** and current mode.

### 4.1 Pirouette Draw – Rhythmic Loops

- **Motion**: Smooth circular/oval movement in mid‑air, repeated at least ~270°.
- **Canonical use**: In **Rhythm Mode**.
- **Derived parameters**:
  - Circle **radius** → loop **length/tempo**:
    - Small radius = short loop, faster pattern.
    - Large radius = long loop, slower pattern.
  - Average **pressure on the stroke** → base **velocity (volume)** of that loop.
- **Musical result**:
  - Creates a **rotating loop path**.
  - User can later place **trigger points** on this circle (drum hits).

Implementation hint:

- Detect “loop‑like stroke” by checking if:
  - Start/end points are within a small distance threshold.
  - Length of path / bounding box ≈ circular ratio.

### 4.2 Arabesque Wave – Melodic Curves

- **Motion**: Long, smooth arc rising or falling with continuous stylus movement (think arm extension).
- **Canonical use**: **Melody Mode**.
- **Derived parameters**:
  - **Vertical position (Y)** along the path → **pitch** (snap to scale).
  - **Path length** → **duration** of melody phrase.
  - **Curvature** → phrase contour (smoother vs more articulated).
  - Light **pressure variance** → note dynamics / micro‑accents.
- **Musical result**:
  - Converts the 3D trajectory into a **melody line** that loops.
  - Option: Quantize note onset positions to a tempo grid.

Implementation hint:

- Sample points along stroke (e.g., every N cm).
- Map each point’s Y to a scale degree using a configurable pitch map.

### 4.3 Grand Jeté Strike – Percussive Accents

- **Motion**: Quick, decisive linear thrust across space (fast speed, shorter length).
- **Canonical use**: **Rhythm Mode** or **FX** overlay.
- **Derived parameters**:
  - **Speed** (distance / time) → which percussion sample (soft vs hard).
  - **Peak pressure** → hit velocity.
- **Musical result**:
  - Single‑shot hit or small flam cluster.
  - Ideal for adding fills and accents on top of loops.

Implementation hint:

- If stroke duration < threshold (e.g., 150–250 ms) and path length > minimal distance = treat as “strike”.

### 4.4 Port de Bras Flow – Dynamics & Expression

- **Motion**: Slow, continuous, sweeping motion, often following or overlapping existing trails.
- **Canonical use**: **Any mode**, mainly as **post‑processing gesture**.
- **Derived parameters**:
  - Average **pressure** profile along overlapped segments → remap **volume envelope**.
  - **Tilt** of stylus → filter modulation (e.g., low‑pass cutoff).
- **Musical result**:
  - When moving through existing trails, it acts like **“brushing” expressiveness** over them (e.g., swells a pad line, adds filter sweeps).

Implementation hint:

- Raycast/volume‑test the stylus path against existing trail volumes.
- For each intersected trail, compute time‑mapped modulation curves.

### 4.5 Plié Fade – Fade‑Outs / Diminuendos

- **Motion**: Downward motion with **gradually decreasing pressure**.
- **Canonical use**: To **reduce intensity** of loops / voices.
- **Derived parameters**:
  - Vertical **downward distance** → fade length (in seconds/beats).
  - **Pressure drop** slope → curve type (linear, exponential).
- **Musical result**:
  - Softly fades out targeted loop(s), group, or global mix segment.

Implementation hint:

- Use a screen‑space/VR overlay “target” to indicate which loop/object the Plié applies to (e.g., look‑at + stylus direction).

### 4.6 Chaîné Turns – Ornamentation

- **Motion**: Quick rotational flicks or small rapid circles.
- **Canonical use**: **Melody Mode** for trills, **FX** for spins.
- **Derived parameters**:
  - Number of rotations and speed → trill rate / pitch modulation depth.
- **Musical result**:
  - Adds trills, stutters, or rapid filters on the currently focused melodic object.

### 4.7 Tendu Extension – Crescendos / Swells

- **Motion**: Long, slow extension from center outwards (straight or slightly curved).
- **Canonical use**: **Harmony Mode**.
- **Derived parameters**:
  - Distance from origin → target peak volume.
  - Speed → attack time (slow move → slow swell).
- **Musical result**:
  - Gradually brings in new harmonic layers or increases pad intensity.

### 4.8 Balancé Sway – Tempo & Time Feel

- **Motion**: Side‑to‑side rocking with consistent timing (like gentle weight shift).
- **Canonical use**: **Global tempo / groove control**.
- **Derived parameters**:
  - Period of sway (time between peaks) → global tempo adjustment.
  - Amplitude → swing/groove amount or subtle time‑stretching.
- **Musical result**:
  - Allows user to “breathe” tempo slightly, without exact BPM fiddling.

***

## 5. Visual System

### 5.1 Trails

- Each stroke becomes a **Trail Object** with:
  - `mode`: rhythm | melody | harmony
  - `points[]`: 3D polyline
  - `color`: pulled from mode (`--color-trail-*`)
  - `width`: mode‑dependent (rhythm thicker than melody)
  - `lifetime`: infinite for musical structures, short for ephemeral FX
- Rendering:
  - Use **line renderer / tube mesh** with **glow (bloom)**.
  - Apply **particle sprites** that spawn along path and fade (`life` parameter in your current canvas demo is a good reference).

### 5.2 State & Feedback

- **Color**: Encodes mode.
- **Glow intensity**: Encodes recent activity / loudness.
- **Pulsing animation**:
  - On every audio trigger (kick/snare/note), pulse the nearest segment or node.
- **HUD / No HUD**:
  - Prefer minimal floating UI; let the **space itself be the interface**.
  - Use **subtle text tags** or icons floating near objects for debugging early builds.

***

## 6. Audio System & Mapping

### 6.1 Engine Structure

- One **MusicManager** coordinating:
  - **Clock/Transport** (BPM, time signature).
  - **Track objects** (each trail = track).
  - **Audio nodes** (samplers, synths, FX units).

### 6.2 Rhythm Mapping

- For **Pirouette loops**:
  - Convert circular path to normalized [0,1) phase around circle.
  - Maintain an array of **TriggerPoints**:
    - `phasePosition` ∈ [0,1)
    - `instrumentId`
    - `velocityScale`
  - At runtime:
    - Increment `loopPhase` with global clock.
    - Fire samples when `loopPhase` passes trigger phases.

### 6.3 Melody Mapping

- Sample stroke points along time axis.
- Quantize X/Z projection to beat grid if desired.
- Map Y → note index in scale:
  - Example: define a pitch band (e.g., 0.5m vertical span = one octave).
  - Use pentatonic by default for “no wrong notes”.
- Play back as:
  - Mono line with chosen synth voice.
  - Loop over defined duration (e.g., stroke length mapped to 1, 2, or 4 bars).

### 6.4 Harmony Mapping

- Harmony trails define:
  - **Chord center** (position) and **extent** (length).
  - Y or tilt could select chord type (maj, min, sus, 7).
- Options for MVP:
  - Simpler: treat Harmony strokes as **pad triggers** that sustain while active.

***

## 7. Spatial Audio & Mixing

- Every musical object = **3D audio emitter**:
  - **Distance from user** → volume (with min/max clamp).
  - **Left/right offset** → stereo pan / HRTF position.
  - **Height** → brightness or filter cutoff (higher = brighter).
- Implement **grab & move** on left controller:
  - When user grabs a sound object and moves it:
    - Live update spatial audio parameters.
- Optional: **Depth (Z)** → reverb send amount (further away = more reverb, more “in the hall”).

***

## 8. Bimanual Interaction (Right Stylus + Left Controller)

### Right Hand (Stylus – Expressive Input)

- Draws trails and shapes.
- Modulates:
  - Pressure → volume/dynamics.
  - Tilt → pitch bend, filter, or timbre parameter.
- Buttons:
  - **B1**: mode switch or temporary “alt mode” (e.g., erase / select).
  - **B2**: context action (e.g., commit trail, open micro‑menu on hovered object).

### Left Hand (Controller – Palette & World Tools)

- **Radial menu** for:
  - Mode selection (Rhythm/Melody/Harmony).
  - Instrument category (Drums, Bass, Keys, Pads, FX).
  - Global tools (Undo, Clear last object, Save/Load preset).
- **Grab** interactions:
  - Grab sound objects, move in space (mixing by movement).
- **Timeline**:
  - Gesture (e.g., swipe downward) to reveal a simple timeline or overview of active loops.
  - For MVP, this could just be a vertical list of active loops with mute/solo toggles.

***

## 9. Session & UX Flow (MVP)

1. **Onboarding (30–60s)**
   - Show ghosted trail demonstrating a Pirouette.
   - Ask user to imitate the circle → on completion, they hear their first loop.

2. **Create Beat (Rhythm Mode)**
   - Draw one circle (Pirouette Draw) → base loop.
   - System places a default 4‑on‑the‑floor pattern for instant gratification.
   - Optional: tap 2–4 points on the circle to customize hits.

3. **Add Melody (Melody Mode)**
   - Switch mode via left‑hand menu or stylus button.
   - Draw an Arabesque Wave → melody line appears and plays.

4. **Add Harmony (Harmony Mode)**
   - Draw a long Tendu Extension to slowly introduce a pad.

5. **Mix Spatially**
   - Grab drums, move them closer (louder).
   - Lift melody higher (brighter tone).
   - Push pads deeper (more reverb, less present).

6. **Expressive Edits**
   - Brush trails with Port de Bras to add swells.
   - Use Plié Fade to soften or remove elements.

7. **Record & Export**
   - Simple capture: record 1–4 minute master output as WAV in MVP.
   - Future: also export MIDI/pattern data.

***

## 10. Internal Data Structures (Conceptual)

You can adapt this to C#/Unity classes later:

```text
StrokeTrail
- id
- mode (Rhythm/Melody/Harmony)
- points[]: [Vector3]
- createdAtTime
- audioBinding: RhythmLoop | MelodyLine | HarmonyPad

RhythmLoop
- bpm
- lengthInBeats
- triggerPoints[]: { phase: float, instrumentId: string, velocity: float }

MelodyLine
- scaleId
- notes[]: { time: float, pitch: int, velocity: float }

HarmonyPad
- chordType
- rootPitch
- envelope: attack/decay/sustain/release

SoundObject
- worldPosition: Vector3
- audioSourceRef
- volumeMultiplier
- filterCutoff
- reverbSend
```

***

## 11. MVP vs Stretch Goals

### MVP (Hackathon‑Ready)

- Rhythm/Melody/Harmony modes.
- Basic Pirouette, Arabesque, Grand Jeté mapping (circles, curves, strikes).
- Spatial mixing (position controls volume & panning).
- Simple timeline/transport (play/stop, fixed BPM).
- Master audio record & export.

### Stretch

- AI‑assisted harmony suggestions.
- Scalebank & key detection.
- Collaborative sessions (networked multi‑user).
- DAW export (MIDI + stems).
- More nuanced gesture recognition (explicit classification via ML / thresholds).

***

## 12. Implementation Notes

- Start with **pure geometry‑to‑music mapping** (no complex ML classifiers).
- Use **simple heuristics**:
  - Stroke length, duration, bounding box, curvature → infer gesture intent.
- Emphasize:
  - **No dead gestures**: any movement should produce something musically valid (even if simple).
  - **Pentatonic / constrained pitch** by default.
  - Immediate **visual + audio feedback** at every stroke.

***

If you want, the next step can be a **Unity‑oriented breakdown** (scene graph, main MonoBehaviours, and event flow) or a **concrete C# interface design** for `StrokeTrail` / `MusicManager` that you can start coding against.

Sources
[1] Selena Zhong - Portfolio https://selenazhong.com/work/music-player
[2] Asymmetric Interaction for VR sketching http://empathiccomputing.org/project/asymmetric-interaction-for-vr-sketching/
[3] Creating Spatialized Music for AR/VR https://www.youtube.com/watch?v=Owd0dbG76YM
[4] From gesture to sound: A study for a musical interface using gesture following techniques http://mtg.upf.edu/system/files/publications/Charalampos-Christopoulos-Master-Thesis-2014.pdf
[5] Cross-Space Hybrid Bimanual Interaction on Horizontal ... https://www.youtube.com/watch?v=iHgpMMFVtgg
[6] Audio and Mixers https://toolkit.spatial.io/docs/audio-and-mixers
[7] MuGeVI: A Multi-Functional Gesture-Controlled Virtual https://nime.org/proceedings/2023/nime2023_75.pdf
[8] Pen+Touch+Midair: Cross-Space Hybrid Bimanual ... https://openreview.net/forum?id=TPhs6nnLZUm
[9] What is Spatial Audio? | IxDF https://www.interaction-design.org/literature/topics/spatial-audio
[10] A Modern Reimagination of Gesture-Based Sound Design https://www.arxiv.org/pdf/2505.10686.pdf
[11] Haptic Stylus vs. Handheld Controllers: A Comparative Study for Surface Visualization Interactions http://www.arxiv.org/abs/2412.07065
[12] Spatial audio and player performance | by Denis Zlobin https://uxdesign.cc/spatial-audio-and-player-performance-8694d43b708
[13] DonalMcGahon/Gesture-Based-UI https://github.com/DonalMcGahon/Gesture-Based-UI-
[14] Frontiers | Off-The-Shelf Stylus: Using XR Devices for Handwriting and Sketching on Physically Aligned Virtual Surfaces https://www.frontiersin.org/journals/virtual-reality/articles/10.3389/frvir.2021.684498/full
[15] Towards a Shared Spatial Audio Design Space https://dl.acm.org/doi/10.1145/3715336.3735712
