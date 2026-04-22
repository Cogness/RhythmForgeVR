# RhythmForge VR — Project Description
### DevStudio 2026 by Logitech | MX Ink (MR Stylus for Meta Quest) Category

---

## What Is RhythmForge VR?

RhythmForge VR is a guided spatial music creation app for Meta Quest built around the Logitech MX Ink stylus. Instead of starting with a dense DAW interface, the user composes by drawing expressive shapes directly in the air. Each stroke becomes one musical layer in a structured five-step workflow: **Harmony, Melody, Groove, Bass, and Percussion**.

The current refactored app is designed to make music creation approachable for non-musicians without flattening the creative experience for advanced users. The system locks a safe musical foundation, then turns the geometry of each MX Ink stroke into a coherent 8-bar composition. The result is a tool that feels playful and physical, but consistently produces music that sounds intentional.

---

## Core Experience

### 1. Guided Composition In Five Phases

RhythmForge VR turns composition into a clear creative journey.

- **Harmony** defines the chord bed of the piece.
- **Melody** draws a lead line that follows the harmony bar by bar.
- **Groove** reshapes the timing, density, and rhythmic feel of the melody.
- **Bass** anchors the low end to the harmonic structure.
- **Percussion** creates the beat from a musically safe drum foundation.

Each phase accepts one committed stroke. Redrawing replaces the previous layer, so the experience stays clean, readable, and beginner-friendly.

---

### 2. Shape-To-Music Translation

The app analyzes the user’s stroke and derives musical behavior from its visual character.

- Stroke contour influences melodic motion.
- Tilt and shape balance influence harmonic color and phrase lift.
- Angularity, symmetry, and density influence groove and percussion variation.
- Shape traits are constrained by key, chord, and genre rules so the output remains musical.

This is the core idea of RhythmForge VR: the user is not programming notes step by step. They are sketching musical intent, and the system translates that intent into playable structure.

---

### 3. Always-Musical Guided Output

The refactored app is intentionally opinionated. In guided mode, the composition starts from a stable musical policy instead of a blank technical canvas.

- The piece is built as an **8-bar loop**.
- The engine keeps notes inside the active key.
- Strong melody moments align to the current chord.
- Bass stays locked to the harmonic foundation.
- Percussion starts from a recognizable beginner-safe beat, then adds variation from the stroke.

These constraints are a feature, not a limitation. They let first-time users create something satisfying within minutes while still leaving room for expressive differences between strokes.

---

### 4. Genre Switching Without Rewriting The Piece

RhythmForge VR currently supports three musical palettes:

- **Electronic** for lo-fi, trap, and dream-inspired textures
- **New Age** for meditative, spacious, tonal soundscapes
- **Jazz** for brush-kit rhythm, Rhodes color, and richer voicings

Switching genre re-derives the composition using new harmonic flavors, presets, and register policies. The same drawing can become a different musical identity instantly, which makes experimentation fast and intuitive.

---

### 5. Spatial Workflow For VR

The application is designed around embodied creation rather than flat menus.

- The user draws in mid-air with the MX Ink stylus.
- A commit card lets them save or discard each generated layer.
- A phase panel shows which parts of the composition are filled, active, or pending re-derivation.
- A single **Play Piece** control lets the user hear the full composition as one loop.
- Auto-advance can move the user from one phase to the next, turning composition into a guided creative flow.

This makes RhythmForge VR feel less like operating software and more like building music through motion.

---

## Why MX Ink Is Essential

RhythmForge VR is not a generic controller app with stylus support added later. The MX Ink stylus is the primary instrument interface.

- Its pen-like form makes drawing curves, arcs, and contours in 3D space feel natural.
- Pressure-gated stroke capture makes drawing intentional and expressive instead of accidental.
- Stylus pose data gives the system a precise spatial signal for shape analysis.
- Stylus buttons support direct interaction with world-space UI and panel movement inside VR.

The project depends on the MX Ink because the product idea depends on precision drawing in space. A handheld controller can point; a stylus can author.

---

## Who It Is For

- **Beginners and hobbyists** who want to make real music without learning a traditional DAW first
- **Educators** who want to teach rhythm, harmony, melody, and structure through physical interaction
- **Producers and songwriters** who want a fast ideation tool for sketching loops and moods in an immersive format

---

## Technical Foundation

RhythmForge VR is built in **Unity** for **Meta Quest**, using the Meta XR / OpenXR stack with the **Logitech MX Ink stylus interaction profile**. The app combines:

- a guided composition domain model
- real-time shape analysis
- per-genre derivation policies
- live playback of Harmony, Melody, Groove, Bass, and Percussion layers in VR

The refactored architecture is focused on reliability, musical coherence, and fast iteration inside headset.

---

## Summary

RhythmForge VR introduces a new way to compose: not by clicking piano-roll grids, but by drawing musical intent in space. Its guided five-phase workflow makes the app accessible, its genre re-derivation makes it expressive, and its MX Ink-first interaction model makes it feel native to mixed reality rather than adapted from desktop software.

In short, RhythmForge VR turns the Logitech MX Ink into a spatial composition instrument.

---

# Project Story

## About the project

RhythmForge VR started from a simple question: what if making music in VR felt less like operating software and more like drawing, conducting, and shaping sound with your hands?

The original pitch came from a frustration with traditional music tools. Most digital audio workstations are powerful, but for beginners they can feel intimidating, technical, and disconnected from the physical joy of music-making. At the same time, mixed reality hardware was becoming precise enough to support much more than games or novelty interactions. The Logitech MX Ink stood out because it felt like an actual creative instrument, not just a controller. That sparked the core idea behind RhythmForge VR: use a stylus to turn gestures in space into musical structure.

Our earliest vision was broad and ambitious. We imagined a spatial music studio where users could paint rhythms, sketch melodies, and physically shape a composition around themselves. As the project evolved, we realized the most important problem to solve was not adding more features, but making the creative experience feel musically safe, expressive, and immediate. That insight pushed us toward the refactored version of the app: a guided five-phase composition workflow built around Harmony, Melody, Groove, Bass, and Percussion.

What inspired us most was the possibility of lowering the barrier to music creation without making the result feel simplistic. We wanted a beginner to draw a shape and hear something coherent, but we also wanted that same interaction to feel expressive enough that an experienced musician could explore it creatively. That became the design north star for the whole project.

## How we built it

RhythmForge VR was built in Unity for Meta Quest, using the Meta XR / OpenXR stack together with the Logitech MX Ink stylus interaction profile. The stylus became the center of the experience. Instead of pressing buttons to place notes on a grid, the user draws one stroke in 3D space, and the system analyzes that shape to derive musical behavior.

To make that work, we built the app around several connected systems:

- real-time stroke capture in 3D space using the MX Ink stylus
- shape analysis that extracts features like contour, symmetry, angularity, and tilt
- a guided composition model that maps each stroke to one musical phase
- genre-aware derivation rules for Electronic, New Age, and Jazz
- a playback layer that combines Harmony, Melody, Groove, Bass, and Percussion into a single coherent loop

One of the biggest architectural shifts was moving away from a more free-form pattern model toward a guided composition system with stronger musical guarantees. In the current version, the app starts from a stable musical foundation and lets the user build one layer at a time. Each stroke is expressive, but it is also interpreted through rules that keep the result in key, aligned to the harmony, and structurally listenable.

## What we learned

The biggest lesson was that creative freedom works best when it is supported by invisible structure.

At first, it was tempting to think of musical expression in VR as pure openness: more controls, more gestures, more ways to manipulate sound. But in practice, especially for new users, too much freedom quickly becomes confusion. We learned that the most satisfying experience came from giving the user a strong musical framework and then letting gesture shape the variation inside that framework.

We also learned that hardware-specific design matters. The MX Ink is not valuable here just because it is new hardware; it is valuable because its form factor changes how the interaction feels. A stylus invites drawing, tracing, and sculpting in a way that standard VR controllers do not. That influenced both the interface design and the musical logic of the app.

On the technical side, we learned how important it is to align architecture with experience design. Once we shifted to the phased composition model, the codebase also had to change: domain types, guided defaults, re-derivation flows, phase state, and UI all needed to reflect the same mental model. The refactor was not just cleanup; it was a way of making the product more truthful to its own idea.

## Challenges we faced

The hardest challenge was balancing expressiveness with musical correctness.

If the system followed the drawing too literally, the output could become chaotic or unpleasant. If it constrained the drawing too aggressively, the app stopped feeling creative. We had to iterate on that middle ground repeatedly: deciding which parts of a stroke should influence melody, which should affect groove, how harmony should remain stable, and how much variation percussion could introduce before losing the beginner-safe feel.

Another challenge was scope. The original pitch imagined a very broad spatial music creation environment, but a hackathon project needs clarity. We had to decide what was essential to prove the idea. That led us to focus on the strongest version of the concept: guided composition through shape-driven musical phases, with the MX Ink stylus as the core interaction device.

We also faced the practical challenge of making multiple systems cooperate in real time inside VR: stylus input, stroke capture, UI interaction, guided state management, pattern replacement, genre switching, and musical re-derivation all had to feel stable and responsive enough to support a creative flow.

## Why this project matters to us

RhythmForge VR represents more than a music tool. It is our attempt to explore what native creative software for mixed reality can look like. Rather than porting desktop workflows into a headset, we wanted to ask what becomes possible when composition starts with motion, gesture, and space.

The result is a project that sits between instrument, interface, and composition assistant. It shows how the Logitech MX Ink can unlock a genuinely new form of creation in VR: not only writing or drawing in space, but composing with it.
