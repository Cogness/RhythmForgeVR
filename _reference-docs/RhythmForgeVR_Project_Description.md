# RhythmForge VR — Project Description
### DevStudio 2026 by Logitech | MX Ink (MR Stylus for Meta Quest) Category

---

## What Is RhythmForge VR?

RhythmForge VR is a spatial music creation application for Meta Quest that transforms the Logitech MX Ink stylus into a professional musical instrument. Instead of clicking through menus in a traditional Digital Audio Workstation, users **paint sound directly into 3D space** — drawing rhythm patterns, sculpting melodies, and mixing tracks by physically moving audio objects around them in virtual reality.

The core premise is simple but powerful: **every gesture creates music**. A circle drawn in the air becomes a looping beat. A curve sketched upward becomes a melody. Moving a sound object closer makes it louder. This is not a music game — it is a genuine creative tool that outputs professional-quality audio files.

---

## Core Features & Functionality

### 1. Rhythm Canvas (MX Ink — Primary Mode)

The Rhythm Canvas is the entry point for beat creation and the feature that most directly showcases the MX Ink's unique capabilities.

The user draws a circular or linear path in mid-air using the MX Ink stylus. That path becomes a persistent, infinitely looping animation visible in VR space. The user then taps trigger points along the path to assign drum sounds — kick, snare, hi-hat, clap, or any sample from the instrument library. As the loop rotates, each time the animation crosses a trigger point, the assigned sound fires.

**MX Ink integration is direct and meaningful here:**
- **Pressure sensitivity (4,096 levels)** controls the velocity of each drum hit — a light tap produces a quiet, soft hit; a firm press delivers a hard, punchy strike.
- **Position tracking** (sub-millimeter precision) determines exactly where trigger points are placed along the path, enabling fine rhythmic timing adjustments.
- **Low latency (<20ms)** ensures the audio response feels instant and musical, not mechanical.

The result is a visual, physical drumming experience that requires zero music theory knowledge to use, yet gives experienced producers granular dynamic control.

---

### 2. Melody Sculptor (MX Ink — Secondary Mode)

Switching modes with a single press of the MX Ink's programmable button, the user enters Melody Sculptor — a tool for drawing harmonic content directly in 3D space.

The user draws a curve freehand in the air. The **vertical height of the stroke maps to musical pitch** — drawing upward raises the note, drawing downward lowers it. The stroke is automatically quantized to a pentatonic scale by default (ensuring everything sounds musical), with an optional chromatic mode for advanced users. The horizontal length of the stroke determines note duration.

**Additional MX Ink controls:**
- **Pressure variation along the stroke** creates dynamic expression — pressing harder on certain notes adds accent and volume.
- **Tilt detection (full 360°)** enables real-time pitch bending, replicating the effect of bending a guitar string or using a synthesizer's pitch wheel.

Once drawn, the melody loops automatically in sync with any active rhythm patterns. Users can layer multiple melody lines, assign different instruments (piano, strings, synth leads, pads), and create full harmonic compositions through intuitive spatial drawing.

---

### 3. Instrument Library (Quest Controller — Left Hand)

A floating radial menu anchored to the user's left hand provides access to the full instrument palette. The library is organized into four categories:

- **Drums:** Kick, snare, hi-hat variants, toms, cymbals, and percussion
- **Bass:** 808 sub-bass, synth bass, acoustic bass
- **Melody:** Piano, strings, synth leads, atmospheric pads
- **FX:** Risers, impacts, vinyl crackle, ambient textures

Selecting an instrument spawns it as a visible 3D object floating in the virtual space. The user can then drag this object onto any active rhythm loop or melody line to assign it as the sound source. Scaling the object up or down adjusts its base volume. This physical, object-based model makes the instrument assignment feel intuitive and tactile rather than menu-driven.

---

### 4. Spatial Audio Mixing (Both Hands)

This is the feature that most fundamentally reimagines what music production can feel like.

Every sound in a RhythmForge VR session exists as a three-dimensional object positioned in space around the user. The physical position of each object directly determines how it sounds in the mix:

- **Distance from the user → Volume:** Moving an object closer makes it louder; moving it away quiets it — acting as a physical fader.
- **Left/Right position → Stereo Panning:** Placing a sound to the left pushes it into the left channel; to the right pushes it right — providing instinctive stereo control.
- **Vertical height → Tonal Character (EQ):** Higher placement brightens the sound (boosting high frequencies); lower placement warms and darkens it (emphasizing low frequencies).

The user physically sculpts the mix by grabbing and repositioning sound objects with the left hand. The MX Ink stylus adds a further layer: by painting a "trail" over any sound object, the user applies real-time audio effects — reverb, delay, filter sweeps — with the same brush-stroke gesture used throughout the application.

Mixing in RhythmForge VR is not adjusting numbers on a screen. It is spatial sculpture.

---

### 5. Timeline, Recording & Export (Left Hand Gesture)

A downward swipe of the left hand summons the session timeline beneath the user. From here:

- **Scrub playback** by pinching and dragging through the timeline
- **Record mode** captures a full 1–4 minute session as a high-quality audio loop
- **Export** delivers the composition as a WAV or MP3 file, suitable for direct sharing or import into a professional DAW (Ableton, FL Studio, Logic Pro)
- **MIDI export** (available in the Pro tier) sends MIDI data from all melody lines directly to a connected DAW via a companion desktop plugin

This export pipeline is critical: RhythmForge VR is designed to complement professional workflows, not compete with them. Producers use it as an expressive creative sketchbook in VR, then take their ideas back to their desktop DAW for final production.

---

## Target Users

RhythmForge VR is designed to serve three distinct but overlapping audiences:

**Music producers and electronic musicians** gain a spatial creative tool for rapid ideation — sketching beat concepts and melodic ideas in an immersive environment before translating them to their main production setup.

**Music educators** can use the application to teach composition concepts — rhythm, melody, harmony, dynamics — through physical, visual interaction rather than abstract notation. Students who cannot read sheet music can immediately begin making and manipulating musical structures.

**Hobbyists and beginners** with no musical training can create complete, satisfying tracks within minutes, thanks to pentatonic scale defaults, automatic tempo sync, and an interface that makes musical "mistakes" nearly impossible. The freemium model makes this audience accessible without requiring an upfront commitment.

---

## Why the Logitech MX Ink Is Essential

RhythmForge VR is not a general VR app that happens to support the MX Ink. The stylus is the instrument. Every core interaction is designed around capabilities that the MX Ink uniquely provides:

- **Pressure sensitivity** is used for musical dynamics — something hand tracking and standard controllers cannot replicate with meaningful precision.
- **Tilt detection** enables pitch-bending gestures that mirror real instrument technique.
- **Physical weight and form factor** of a stylus — held like a pen or a conductor's baton — creates a tactile connection to the music that controllers held in a fist cannot.
- **Sub-millimeter positional accuracy** allows for precise placement of rhythm trigger points, which is the difference between a tight groove and a sloppy beat.

The MX Ink was designed to bring the precision of a physical writing instrument into mixed reality. RhythmForge VR extends that purpose into music — where precision, feel, and physical feedback are equally essential.

---

## Technical Foundation

RhythmForge VR is built in **Unity** using the **Meta XR SDK** for hardware integration and hand/stylus tracking. Audio synthesis and playback are handled by **FMOD Studio**, which provides the low-latency audio engine required for real-time musical interaction. Total system latency is under 20ms — below the threshold of human auditory perception for cause-and-effect relationships.

The application targets Meta Quest 3 and is architected to meet Meta App Store submission requirements, including comfort guidelines, performance targets (90fps minimum), and privacy compliance.

---

## Business Model

**Freemium on the Meta App Store:**
- **Free tier:** 3 instruments, 30-second recordings, WAV export
- **Pro ($9.99/month or $79/year):** Unlimited instruments, full recording length, MIDI export, cloud save, DAW plugin
- **Lifetime ($149 one-time):** All Pro features, permanent access

**Secondary revenue:**
- DAW Plugin add-on ($19.99) for stem/MIDI export to Ableton and FL Studio
- In-app sample pack marketplace (30% revenue share with creators)
- Education licensing ($299/year per classroom)

The addressable market spans the $4–6B DAW software industry, 20M+ active Meta Quest users, and an estimated 50M+ music creators globally — representing a combined opportunity exceeding $500M.

---

## Summary

RhythmForge VR introduces a new category: **spatial music production**. It makes the act of creating music physical, visual, and expressive — accessible to complete beginners while offering genuine depth for working producers. By building every interaction around the unique capabilities of the Logitech MX Ink stylus, it creates an experience that is only possible with this hardware — and demonstrates a compelling, commercially viable path for the MX Ink platform on Meta Quest.

*"Conduct reality. Compose destiny. Create music with motion."*
