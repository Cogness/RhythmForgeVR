# Zen Main Menu

This feature adds a generated zen-style VR main-menu scene for RhythmForge.

## What It Includes

- a scene generator based on `Assets/Scenes/MXInkSample.unity`
- a stylized placeholder zen environment built from Unity primitives
- ambient placeholder zen audio that fades out when player creation begins
- a generic music-reactive signal layer for wind, ripples, and atmosphere
- water ripple pooling and beat-driven triggering
- manual keyboard signal testing for editor iteration

## Create The Scene

1. Open Unity.
2. Run `RhythmForge/Create Zen Main Menu Scene` from the top menu.
3. Open `Assets/Scenes/ZenMainMenu.unity`.
4. Replace the placeholder ambient loop by assigning a real zen track to `ZenAmbientMusicController` if desired.

## Manual Test Controls

- Draw with MX Ink to trigger the player-created flow.
- Press `Space` to simulate a beat if you need to test reactive visuals without drawing audio yet.
- Press `M` to inject a non-beat loudness pulse.

## Replace Later

- water material
- mountain meshes
- tree meshes
- ambient zen track
- ripple look
- optional skybox and post-processing
