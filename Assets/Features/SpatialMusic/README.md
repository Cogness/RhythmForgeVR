# Spatial Music Drawing

This feature implements the first playable stroke-to-music loop for RhythmForge VR.

## Included Systems

- multi-source drawing input
- world-space stroke capture
- basic shape analysis
- persistent musical shapes with spatial looping audio
- in-progress stroke preview
- shape clear/remove controls
- runtime bootstrap for the zen scene

## Runtime Behavior

- press and hold draw to capture a stroke in 3D space
- release to finalize it into a looping musical shape
- circle-like strokes create melodic loops
- lines create drone-like loops
- zigzags create more rhythmic/percussive loops
- arcs create rising arpeggio-style loops

## Input

MX Ink:
- tip or middle pressure starts drawing
- back button clears last shape
- back double tap clears all shapes

Quest controller:
- trigger draws
- primary button clears last shape
- hold secondary button for 0.6s clears all shapes
- right hand is preferred by default

Desktop fallback:
- hold left mouse button to draw
- Backspace clears last shape
- Shift+Backspace clears all shapes

## Scene Setup

No manual component wiring is required for the MVP.

Open `Assets/Scenes/ZenMainMenu.unity` and enter Play Mode. The runtime bootstrap will:
- disable the old sample `LineDrawing`
- install the new drawing system
- reuse the scene's existing music-reactive signal source when available

## Replace Later

- swap procedural loop generation for richer sound engine integration
- replace heuristic shape recognition with more advanced gesture analysis
- attach instrument selection and per-shape editing UI
- add true authored visual assets for final shapes
