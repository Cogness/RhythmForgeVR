# Unity Addressables Guide

Adapted from the Unity Addressables specialist guidance in Claude Code Game Studios.

## Current Project Status

RhythmForge currently uses `Resources` for audio preset sample lookup. Do not convert this project to Addressables casually.

## When To Use Addressables Here

Consider Addressables only when one of these becomes true:
- Sample libraries become too large for simple `Resources` loading.
- Preset banks need selective download or streaming.
- Scene content or instrument packs need explicit memory lifecycle control.
- Remote content delivery becomes a real requirement.

## Addressables Rules

- Organize groups by loading context, not asset type.
- Load asynchronously and release handles correctly.
- Keep shared dependencies explicit.
- Profile memory and bundle dependencies before adopting broadly.
- Document labels, addresses, and load ownership.

## RhythmForge Recommendation

For now:
- Keep the audio preset convention simple and local.
- Prefer a clean abstraction layer so `Resources` can later be swapped for Addressables.
- If migrating, start with optional sample packs rather than the entire project.
