---
name: rhythmforge-unity-addressables
description: Use for Addressables adoption, asset-loading strategy, bundle organization, and memory lifecycle decisions in RhythmForgeVR.
---

# RhythmForge Unity Addressables

## When To Use

Use this skill when evaluating or implementing Addressables, streaming, or large asset-memory workflows.

## Primary Reference

Read `docs/ai/unity-addressables.md` first.

## RhythmForge Rules

- Do not replace the current `Resources`-based audio preset loading casually.
- Prefer abstraction layers that allow later migration.
- Only introduce Addressables when there is a concrete content or memory need.
