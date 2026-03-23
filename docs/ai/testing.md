# Testing Guide

Adapted from the testing and gameplay rules in Claude Code Game Studios.

## General Rules

- Name tests clearly and describe the behavior being verified.
- Keep arrange, act, and assert phases obvious.
- New bug fixes should include a regression test when practical.
- Prefer fast deterministic tests over brittle editor-dependent coverage.

## RhythmForge Priorities

For this repo, the most important verification types are:
- Play Mode checks for scene bootstrapping and runtime wiring
- input-to-audio trigger validation
- loop timing and preset loading validation
- desktop fallback verification for VR-intended features
- manual headset checks for spatial UX when needed

## What To Document When Full Testing Is Not Possible

If Unity editor or headset validation cannot be run, record:
- what was changed
- what still needs Play Mode verification
- any required inspector or asset setup
- any sample content the repo still lacks
