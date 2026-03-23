# Unity UI Guide

Adapted from the Unity UI specialist guidance in Claude Code Game Studios and narrowed to this repo.

## Choosing A UI System

- Use UI Toolkit for new screen-space menus and tooling.
- Use UGUI for world-space or XR-attached UI where Toolkit support is weaker.
- Avoid mixing UI Toolkit and UGUI inside the same screen unless there is a clear technical reason.

## Architecture

- UI displays state; it should not own or mutate core game state directly.
- Prefer command or event-driven interactions.
- Keep screen logic separate from persistent runtime systems.
- Cache visual references; do not query the visual tree every frame.

## Input And Accessibility

- Support keyboard and XR input paths where a screen is intended to work outside the headset.
- Keep focus management explicit for menu-like flows.
- Avoid relying on color alone for state communication.
- Respect reduced-motion and scalable text requirements for any non-diegetic UI.

## Performance

- Separate static and dynamic canvases when using UGUI.
- Avoid unnecessary layout rebuilds.
- Pool repeated list items or use virtualization.
- Keep UI sound triggering routed through the audio system rather than direct clip spam.

## RhythmForge Context

- The initial product focus is spatial creation, so avoid overbuilding flat menu hierarchies.
- Favor radial, hand-anchored, or contextual UI patterns that complement MX Ink and VR interaction.
- Keep any desktop-only test UI isolated from the eventual spatial UX layer.
