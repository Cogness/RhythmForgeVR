---
name: rhythmforge-unity-dots
description: Use for DOTS, ECS, Jobs, Burst, and data-oriented performance decisions in RhythmForgeVR.
---

# RhythmForge Unity DOTS

## When To Use

Use this skill only when evaluating whether a subsystem should move to DOTS or when implementing a measured performance-driven ECS change.

## Primary Reference

Read `docs/ai/unity-dots.md` first.

## RhythmForge Rules

- MonoBehaviour remains the default.
- DOTS must be justified by profiling and subsystem scale, not by preference.
- Keep the boundary to GameObject systems explicit.
