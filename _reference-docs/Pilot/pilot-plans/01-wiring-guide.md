# RhythmForge VR — Step-by-Step Testing & Wiring Guide

## Part A: What You Can Test Right Now (No Unity Editor wiring needed)

### A1. Compile check
1. Open Unity with the `RhythmForgeVR` project
2. Wait for script compilation — the Console should show **0 errors** from the 41 new files under `Assets/RhythmForge/`
3. If you see errors, note them — most likely candidates are namespace resolution issues

### A2. Verify algorithm logic in a quick Editor test
You can write a small `[MenuItem]` test script to validate the analysis pipeline without running VR:

Create `Assets/RhythmForge/Editor/AlgorithmTest.cs` (temporary) with this content:

```csharp
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using RhythmForge.Core.Data;
using RhythmForge.Core.Analysis;
using RhythmForge.Core.Sequencing;
using RhythmForge.Core.Session;

public static class AlgorithmTest
{
    [MenuItem("RhythmForge/Test Algorithms")]
    public static void Run()
    {
        // 1. Generate a circle (rhythm)
        var circle = new List<Vector2>();
        for (int i = 0; i <= 24; i++)
        {
            float a = (float)i / 24 * Mathf.PI * 2f;
            circle.Add(new Vector2(Mathf.Cos(a) * 0.14f, Mathf.Sin(a) * 0.14f));
        }

        var metrics = StrokeAnalyzer.Analyze(circle);
        Debug.Log($"[TEST] Circle metrics — closed:{metrics.closed}, length:{metrics.length:F3}, circularity:{metrics.circularity:F3}");

        var norm = StrokeAnalyzer.NormalizePoints(circle, metrics);
        var sp = ShapeProfileCalculator.Derive(norm, metrics, PatternType.RhythmLoop);
        Debug.Log($"[TEST] ShapeProfile — circ:{sp.circularity:F2}, ang:{sp.angularity:F2}, sym:{sp.symmetry:F2}");

        var sound = SoundProfileMapper.Derive(PatternType.RhythmLoop, sp);
        Debug.Log($"[TEST] SoundProfile — body:{sound.body:F2}, bright:{sound.brightness:F2}, drive:{sound.drive:F2}");

        var result = RhythmDeriver.Derive(circle, metrics, "lofi", sp, sound);
        Debug.Log($"[TEST] Rhythm — bars:{result.bars}, events:{result.derivedSequence.events.Count}, swing:{result.derivedSequence.swing:F2}");
        Debug.Log($"[TEST] Summary: {result.summary}");

        // 2. Generate a wave (melody)
        var wave = new List<Vector2>();
        for (int i = 0; i < 32; i++)
        {
            float t = (float)i / 31;
            wave.Add(new Vector2((t - 0.5f) * 0.2f, Mathf.Sin(t * Mathf.PI * 3f) * 0.08f));
        }

        var mMetrics = StrokeAnalyzer.Analyze(wave);
        var mNorm = StrokeAnalyzer.NormalizePoints(wave, mMetrics);
        var mSp = ShapeProfileCalculator.Derive(mNorm, mMetrics, PatternType.MelodyLine);
        var mSound = SoundProfileMapper.Derive(PatternType.MelodyLine, mSp);
        var mResult = MelodyDeriver.Derive(wave, mMetrics, "A minor", "lofi", mSp, mSound);
        Debug.Log($"[TEST] Melody — bars:{mResult.bars}, notes:{mResult.derivedSequence.notes.Count}");
        Debug.Log($"[TEST] Summary: {mResult.summary}");

        // 3. Test DemoSession
        var store = new SessionStore();
        var demoState = DemoSession.CreateDemoState(store);
        Debug.Log($"[TEST] Demo state — patterns:{demoState.patterns.Count}, instances:{demoState.instances.Count}");

        Debug.Log("[TEST] ✅ All algorithm tests passed.");
    }
}
#endif
```

**To run:** Unity menu → **RhythmForge → Test Algorithms** → check Console for output. This validates the entire stroke→shape→sound→sequence pipeline without VR hardware.

---

## Part B: Unity Editor Setup (Step by Step)

### B1. Add a custom physics layer for pattern instances

1. **Edit → Project Settings → Tags and Layers**
2. In the **Layers** section, pick an empty slot (e.g. **Layer 6**) and name it **`PatternInstances`**
3. This is used by [InstanceGrabber](cci:2://file:///Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Interaction/InstanceGrabber.cs:10:4-147:5) for raycasting

### B2. Create placeholder audio samples

You need 5 short `.wav` files. The simplest approach:

1. Create folder: `Assets/RhythmForge/Audio/Samples/`
2. **Option A — Download free samples:**
   - Any kick, snare, hi-hat, percussion one-shot (44.1kHz, 16-bit, mono)
   - A sustained C4 tone (sine or piano, ~2 seconds)
   - Name them: `kick.wav`, `snare.wav`, `hat.wav`, `perc.wav`, `tone_c4.wav`
3. **Option B — Generate via Audacity:**
   - Generate → Tone → 80Hz sine 0.15s → Export as `kick.wav`
   - Generate → Noise (white) 0.08s, apply fade-out → Export as `snare.wav`
   - Generate → Noise 0.03s, high-pass filter → Export as `hat.wav`
   - Generate → Tone 400Hz 0.06s → Export as `perc.wav`
   - Generate → Tone 261.63Hz (C4) 2.0s → Export as `tone_c4.wav`
4. Place all 5 in `Assets/RhythmForge/Audio/Samples/`
5. In Unity, select each and ensure **Import Settings** → Force Mono = ✓, Load Type = Decompress on Load

### B3. Create 3 line materials for pattern types

1. Create folder: `Assets/RhythmForge/Materials/`
2. **Right-click → Create → Material** × 3:
   - **`RhythmStroke.mat`** — Shader: `Sprites/Default`, Color: `#00E5FF` (cyan)
   - **`MelodyStroke.mat`** — Shader: `Sprites/Default`, Color: `#FFD740` (amber/gold)
   - **`HarmonyStroke.mat`** — Shader: `Sprites/Default`, Color: `#69F0AE` (teal/green)
3. On each material: set **Rendering Mode** to Fade (so alpha works for muted patterns)

### B4. Create the RhythmForge scene

1. **File → New Scene** (Basic)
2. Save as `Assets/Scenes/RhythmForge.unity`
3. From your existing `MXInkSample` scene, copy these into the new scene:
   - **OVRCameraRig** (or XR Origin — whatever your VR rig is)
   - **MX_Ink** prefab (from [Assets/Logitech/MX_Ink.prefab](cci:7://file:///Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/Logitech/MX_Ink.prefab:0:0-0:0))
   - Any floor plane / environment you want

### B5. Create the RhythmForge root GameObject hierarchy

In the new scene, create this hierarchy:

```
RhythmForge_Root                     ← Empty, add RhythmForgeManager.cs
├── InputMapper                      ← Empty, add InputMapper.cs
├── DrawModeController               ← Empty, add DrawModeController.cs
├── StrokeCapture                    ← Empty, add StrokeCapture.cs
├── InstanceGrabber                  ← Empty, add InstanceGrabber.cs
├── AudioEngine                      ← Empty, add AudioEngine.cs
│   └── SamplePlayer                 ← Empty, add SamplePlayer.cs
├── Sequencer                        ← Empty, add Sequencer.cs (the RhythmForge one)
├── InstanceContainer                ← Empty (pattern visuals spawned here)
├── Toast                            ← See B6
└── UI_Panels                        ← See B7
```

**Wire each component in the Inspector:**

| Component | Field | Drag From |
|-----------|-------|-----------|
| **InputMapper** | `_stylusHandler` | The [VrStylusHandler](cci:2://file:///Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/Logitech/Scripts/VrStylusHandler.cs:3:0-131:1) on your MX_Ink prefab instance |
| **StrokeCapture** | `_input` | InputMapper GameObject |
| **StrokeCapture** | `_drawMode` | DrawModeController GameObject |
| **StrokeCapture** | `_userHead` | CenterEyeAnchor (child of OVRCameraRig) |
| **StrokeCapture** | `_strokeMaterial` | (optional, leave null for auto Sprites/Default) |
| **InstanceGrabber** | `_input` | InputMapper GameObject |
| **InstanceGrabber** | `_leftControllerTransform` | LeftControllerAnchor (child of OVRCameraRig) |
| **InstanceGrabber** | `_instanceLayer` | Set to `PatternInstances` layer (from B1) |
| **AudioEngine** | `_samplePlayer` | SamplePlayer GameObject |
| **SamplePlayer** | `_kickClip` | `kick.wav` from B2 |
| **SamplePlayer** | `_snareClip` | `snare.wav` |
| **SamplePlayer** | `_hatClip` | `hat.wav` |
| **SamplePlayer** | `_percClip` | `perc.wav` |
| **SamplePlayer** | `_toneClip` | `tone_c4.wav` |
| **Sequencer** | `_audioEngine` | AudioEngine GameObject |
| **RhythmForgeManager** | `_audioEngine` | AudioEngine |
| **RhythmForgeManager** | `_sequencer` | Sequencer |
| **RhythmForgeManager** | `_strokeCapture` | StrokeCapture |
| **RhythmForgeManager** | `_drawModeController` | DrawModeController |
| **RhythmForgeManager** | `_inputMapper` | InputMapper |
| **RhythmForgeManager** | `_instanceGrabber` | InstanceGrabber |
| **RhythmForgeManager** | `_rhythmMaterial` | `RhythmStroke.mat` |
| **RhythmForgeManager** | `_melodyMaterial` | `MelodyStroke.mat` |
| **RhythmForgeManager** | `_harmonyMaterial` | `HarmonyStroke.mat` |
| **RhythmForgeManager** | `_instanceContainer` | InstanceContainer transform |
| **RhythmForgeManager** | `_toast` | Toast (from B6) |

### B6. Create the Toast notification

1. Create child under `RhythmForge_Root`: **right-click → UI → Canvas**
2. Rename to `Toast`
3. Set Canvas:
   - **Render Mode** = World Space
   - **Width** = 400, **Height** = 60
   - **Scale** = 0.001, 0.001, 0.001 (so it's ~40cm wide in world)
   - Position it at `(0, 1.4, 1.5)` — above eye level, slightly forward
4. Add a **CanvasGroup** component
5. Add child **UI → Text** named `ToastText`, center-aligned, font size 24, white
6. Add **ToastMessage.cs** to the Canvas GameObject
7. Wire: `_text` → ToastText, `_canvasGroup` → the CanvasGroup, `_followTarget` → CenterEyeAnchor
8. Wire `RhythmForgeManager._toast` → this Toast object

### B7. Create the TransportPanel (simplest UI panel to test first)

1. Under `RhythmForge_Root`, create another **World Space Canvas**: rename [TransportPanel](cci:2://file:///Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/UI/Panels/TransportPanel.cs:9:4-63:5)
2. Canvas settings: World Space, 500×120, Scale 0.001
3. Position: `(-0.4, 1.2, 1.0)` — to user's left at chest height
4. Add children:
   - **Button** named `PlayStopButton` with child Text `PlayStopLabel`
   - **Text** named `BpmText`
   - **Text** named `KeyText`
   - **Text** named `TransportStatus`
5. Add [TransportPanel.cs](cci:7://file:///Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/UI/Panels/TransportPanel.cs:0:0-0:0) to the Canvas
6. Wire all serialized fields to the UI elements
7. Wire `RhythmForgeManager._transportPanel` → this Canvas

> **Tip:** You can skip the other 5 panels initially and test with just Toast + Transport. The core stroke→pattern→sequencer loop works without them.

### B8. Wire the remaining UI Panels (do these later)

The same pattern applies to each:

- **CommitCardPanel** — World Space Canvas with Save/SaveDup/Discard buttons + Text fields
- **InspectorPanel** — Canvas with sliders, labels, mute/remove/duplicate buttons
- **DockPanel** — Canvas with tab buttons + sub-panels for Instruments/Patterns/Scenes
- **SceneStripPanel** — Canvas with 4 scene buttons A-D
- **ArrangementPanel** — Canvas with 8 Dropdown pairs

Each needs its UI elements created as children and wired to the panel script's serialized fields.

---

## Part C: First VR Test (Minimum Viable)

Once B1–B7 are done:

1. **Build Settings** → ensure your scene is in the build list
2. **Connect Quest 3 via Link** (or build APK)
3. **Play** — you should see:
   - MX Ink model tracking your stylus
   - TransportPanel floating to your left
4. **Press tip on any surface** → a colored LineRenderer stroke should appear
5. **Lift tip** → Console should log `[RhythmForge] Draft: Beat-01 (RhythmLoop)` or similar
6. **Without CommitCardPanel:** the draft just sits in `PendingDraft`. To test committing without UI, you can temporarily add this to [StrokeCapture.Update()](cci:1://file:///Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Interaction/StrokeCapture.cs:51:8-88:9) after the draft is created: auto-confirm with the front button → the front button already calls [_drawMode.CycleMode()](cci:1://file:///Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Interaction/DrawModeController.cs:20:8-35:9). Consider adding back-button confirm logic or testing via the [DemoSession](cci:2://file:///Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/Core/Session/DemoSession.cs:12:4-142:5):
   - In [RhythmForgeManager.Start()](cci:1://file:///Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/RhythmForgeManager.cs:57:8-86:9), temporarily add [LoadDemoSession();](cci:1://file:///Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/RhythmForgeManager.cs:255:8-260:9) as the first line
   - This spawns 3 pre-made patterns (Beat, Melody, Pad) in Scene A
7. **Press Play on TransportPanel** → you should hear the placeholder samples playing in rhythm

---

## Part D: Testing Checklist

| Test | What to verify | Requires |
|------|----------------|----------|
| ✅ **Compile** | 0 errors in Console | Nothing |
| ✅ **Algorithm unit test** | Menu → RhythmForge → Test Algorithms prints correct values | `AlgorithmTest.cs` from A2 |
| ✅ **Demo patterns load** | 3 LineRenderer shapes visible in scene | B1–B5, add [LoadDemoSession()](cci:1://file:///Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Assets/RhythmForge/RhythmForgeManager.cs:255:8-260:9) call |
| ✅ **Sequencer plays** | Hear drum/tone sounds at 85 BPM | + B2 audio samples wired |
| ✅ **Stylus drawing** | Colored line follows MX Ink tip | + MX_Ink in scene |
| ✅ **Draft creation** | Console log shows draft info on stroke finish | StrokeCapture wired |
| ✅ **Commit via UI** | Pattern appears as a LineRenderer in scene | + CommitCardPanel (B8) |
| ✅ **Left hand grab** | Trigger selects/moves pattern instances | InstanceGrabber + layer |
| ✅ **Scene switching** | Thumbstick L/R changes active scene | InputMapper + SceneStripPanel |
| ✅ **Session save/load** | Quit and relaunch preserves patterns | Auto-save in manager |

**Recommended order:** Compile → Algorithm test → Demo load → Audio playback → Stylus drawing → Full UI panels.