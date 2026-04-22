# RhythmForge VR

RhythmForge VR is a guided spatial music creation experience for Meta Quest, built around the Logitech MX Ink stylus.

Instead of composing by clicking through a traditional DAW, users draw expressive shapes in 3D space. Each stroke becomes one musical layer in a five-phase workflow: **Harmony, Melody, Groove, Bass, and Percussion**. The system interprets the geometry of each shape and turns it into a coherent 8-bar composition that stays musical, approachable, and expressive.

## Why It Matters

RhythmForge VR explores a simple idea: music creation in mixed reality should feel physical.

- Draw a shape in the air.
- Let the system translate it into musical structure.
- Build a full loop layer by layer.
- Hear a complete piece emerge from motion.

The project is designed for beginners, educators, and producers alike. It lowers the barrier to entry without removing the feeling of authorship.

## Core Experience

The current refactored app is centered on a guided composition flow:

- **Harmony** creates the chord foundation.
- **Melody** derives a lead line that follows the harmony bar by bar.
- **Groove** reshapes timing, density, and rhythmic feel.
- **Bass** anchors the harmonic low end.
- **Percussion** creates a beginner-safe beat with shape-driven variation.

Each phase accepts one committed stroke. Redrawing replaces the previous layer for that phase, keeping the composition clean and easy to understand.

## MX Ink Interaction

RhythmForge VR is built specifically around the Logitech MX Ink stylus.

- **Tip pressure**: draw a stroke in 3D space.
- **Front button**: confirm a pending draft or click world-space UI.
- **Back button**: discard a pending draft; hold over a panel to drag it.
- **Stylus pose**: drives drawing, hovering, and UI pointing.

In guided mode, the user draws a shape, reviews the generated draft card, saves or discards it, and moves to the next musical phase.

## Musical Model

The current guided build intentionally favors musical safety:

- one structured **8-bar loop**
- notes constrained to the active key
- melody strong beats aligned to the current chord
- bass locked to the harmonic foundation
- percussion starting from a stable rhythmic floor

RhythmForge currently supports three genre palettes:

- **Electronic**
- **New Age**
- **Jazz**

Switching genre re-derives the composition with different voicings, presets, and register policies while preserving the user’s drawn intent.

## Tech Stack

- **Engine:** Unity `6000.4.3f1`
- **Target:** Meta Quest
- **XR stack:** Meta XR / OpenXR
- **Primary input:** Logitech MX Ink stylus
- **Runtime architecture:** guided composition domain model, real-time stroke capture, shape analysis, genre-aware music derivation, VR playback UI

## Quick Start

1. Open the project in Unity `6000.4.3f1`.
2. Load [Assets/Scenes/MXInkSample.unity](Assets/Scenes/MXInkSample.unity).
3. Press Play.
4. The `RhythmForgeBootstrapper` builds the runtime automatically if the scene contains `OVRCameraRig` and `MX_Ink`.

Notes:

- On device, MX Ink input is expected through Logitech's stylus handler.
- In the Unity Editor, MX Ink-specific bindings are intentionally suppressed, so on-headset testing is the real source of truth for stylus behavior.
- The app starts in a fresh guided session by default.

## Usage

### Guided flow

When the app starts in guided mode, the main workflow is:

1. Choose the current phase on the Phase panel: `Harmony`, `Melody`, `Groove`, `Bass`, or `Percussion`.
2. Draw one shape in the air with the MX Ink stylus.
3. Review the generated draft on the commit card.
4. Save it or discard it.
5. Move to the next phase and continue building the loop.

Each phase keeps one committed drawing. Redrawing a phase replaces the previous layer for that phase.

### How to draw a shape

- Point the MX Ink stylus into empty space.
- Press with the stylus tip to start drawing.
- Move the stylus through space to create the stroke.
- Release pressure to finish the stroke.

The runtime captures the stroke in 3D, analyzes its geometry, and generates a musical draft from it.

### How to commit or cancel a draft

When a drawing is finished, a commit card appears in world space near the stroke.

- Use the stylus ray and press the **front button** to click UI buttons.
- Press **Save** to commit the draft into the current phase.
- Press **Discard** to throw the draft away.
- In guided mode, the secondary button on the commit card toggles `Auto Next` on or off.

There are also fast stylus actions for pending drafts:

- **Front button**: confirm the pending draft
- **Back button**: discard the pending draft
- **Back double tap**: alternate discard path

### How to cancel while drawing

- If a draft is already pending, press the **back button** to discard it.
- If you are not holding a pending draft, pressing the **back button** clears the current uncommitted stroke state.

### How to use the UI

- The MX Ink stylus emits a world-space ray for UI interaction.
- Hover the ray over a button to highlight it.
- Press the **front button** to click that button.

The current UI pointer is button-focused, so the main interaction pattern is based around button-style controls.

### How to move a UI panel

- Aim the stylus ray at a world-space panel.
- Hold the **back button** while the ray is hitting that panel.
- Move the stylus to drag the panel through space.
- Release the **back button** to drop it.

While dragging, the panel reorients to keep facing the user.

### Playback and listening

- Use the **Play Piece** button to hear the current 8-bar composition.
- Use the same control again to stop playback.
- As more phases are committed, the loop becomes fuller: harmony first, then melody, groove, bass, and percussion.

## Repo Layout

- [Assets/RhythmForge](Assets/RhythmForge): core app runtime, audio, sequencing, interaction, and UI
- [Assets/Scenes](Assets/Scenes): Unity scenes, including `MXInkSample.unity`
- [_reference-docs](_reference-docs): design notes, handover plans, pitch material, and implementation documentation
- [ProjectSettings](ProjectSettings): Unity project configuration

## Key Docs

- [Hackathon Project Description](_reference-docs/20260422-gap-refactor/Hackathon-Project-Description.md)
- [VR UI User Guide](_reference-docs/RhythmForgeVR_VR_UI_User_Guide.md)
- [Developer Architecture Snapshot](_reference-docs/RhythmForgeVR_Developer_Architecture_Snapshot.md)
- [Phased Music Creation Plan](_reference-docs/20260421-creating-music-in-phases.md/phased-music-creation-plan.md)

## Project Story

RhythmForge VR began with the idea that the MX Ink should feel like a creative instrument, not just a pointing device. The original concept imagined a broad spatial music studio in VR. Through iteration and refactoring, that vision became more focused: a guided shape-to-music workflow that helps non-musicians compose while still feeling expressive to advanced users.

That shift shaped both the product and the codebase. The result is not just a stylus-enabled interface, but a native mixed reality composition experience built around drawing, listening, and refining music in space.

## Status

This repository reflects the current guided-composition refactor of RhythmForge VR. The strongest supported path today is the five-phase guided flow for Harmony, Melody, Groove, Bass, and Percussion.
