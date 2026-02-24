# RhythmForge VR - Unity Implementation Guide

**Target Platform:** Meta Quest 3 with Logitech MX Ink Stylus  
**Unity Version:** 2022.3 LTS or newer  
**SDK:** Meta XR All-in-One SDK (successor to Oculus Integration)  
**Audio:** FMOD Studio 2.02+ or Unity's built-in Audio + Spatializer

---

## 1. Project Setup & Dependencies

### 1.1 Create Unity Project

```bash
Unity Hub -> New Project
Template: 3D (URP Core)
Name: RhythmForgeVR
Unity Version: 2022.3 LTS
```

### 1.2 Install Required Packages

**Via Unity Package Manager (Window > Package Manager):**

1. **Meta XR All-in-One SDK**
   - Add via Asset Store ("My Assets")
   - Import all components
   - Includes: Core SDK, Interaction SDK, Haptics SDK

2. **XR Plugin Management**
   - Window > Package Manager > Unity Registry
   - Search "XR Plugin Management"
   - Install

3. **MX Ink OpenXR Integration**
   - Download from: https://logitech.github.io/mxink/
   - Import `MXInk_OpenXR_Unity.unitypackage`
   - Enables pressure, tilt, button input

4. **FMOD for Unity** *(Optional but recommended)*
   - Download: https://www.fmod.com/download
   - Import FMOD Unity Integration package
   - Provides low-latency spatial audio

**Project Settings Configuration:**

```
Edit > Project Settings > XR Plug-in Management
[X] Oculus (for Meta Quest 3)

Edit > Project Settings > Player > Android Settings
- Minimum API Level: Android 10.0 (API level 29)
- Install Location: Automatic
- Graphics API: Vulkan (remove OpenGLES3)

Edit > Project Settings > Quality
- V Sync Count: Don't Sync (for VR frame pacing)
- Anti-Aliasing: 2x Multi Sampling (balance quality/performance)
```

---

## 2. Scene Structure & Hierarchy

### 2.1 Core Scene Graph

```
RhythmForgeScene
├── [XR Environment]
│   ├── OVRCameraRig (Meta XR SDK prefab)
│   │   ├── TrackingSpace
│   │   │   ├── CenterEyeAnchor (main camera)
│   │   │   │   └── AudioListener
│   │   │   ├── LeftHandAnchor
│   │   │   │   └── LeftControllerAnchor
│   │   │   │       └── OVRControllerPrefab
│   │   │   └── RightHandAnchor (MX Ink stylus)
│   │   │       └── StylusVisual (custom 3D model)
│   │   │           └── StylusTipMarker (empty, tracks tip position)
│   │   └── OVRManager (component)
│   │
│   ├── MXInkInputHandler (custom script)
│   └── VROriginFloor (empty, Y=0, reference point)
│
├── [Music System]
│   ├── MusicManager (singleton)
│   ├── AudioEngine (FMOD or Unity Audio)
│   ├── LoopContainer (empty, parent for all loops)
│   └── SpatialMixingZone (3D grid visualization)
│
├── [Gesture Recognition]
│   ├── GestureRecognizer (singleton)
│   └── MotionTracker (attached to stylus tip)
│
├── [Visual Feedback]
│   ├── TrailRenderer (particle system manager)
│   ├── SoundObjectContainer (parent for spawned sound orbs)
│   └── UICanvas (world-space canvas)
│       ├── ModeIndicator (text: "Rhythm Mode")
│       └── TutorialPrompts
│
├── [UI & Menus]
│   ├── LeftHandMenu (radial menu prefab)
│   │   ├── InstrumentPalette
│   │   ├── ModeSelector
│   │   └── GlobalControls
│   └── DebugPanel (FPS, latency, active loops)
│
└── [Environment]
    ├── SpatialGrid (floor grid, low opacity)
    ├── MusicalStaffLines (appears in Melody mode)
    └── AmbientLighting (skybox + directional light)

```

---

## 3. Core MonoBehaviours & Class Architecture

### 3.1 Input System: MX Ink Integration

**File:** `Scripts/Input/MXInkInputHandler.cs`

```csharp
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.InputSystem;

public class MXInkInputHandler : MonoBehaviour
{
    [Header("Input Actions (assign in Inspector)")]
    [SerializeField] private InputActionReference _tipPressure;
    [SerializeField] private InputActionReference _button1;
    [SerializeField] private InputActionReference _button2;
    [SerializeField] private InputActionReference _stylusPosition;
    [SerializeField] private InputActionReference _stylusRotation;
    
    [Header("Output")]
    public StylusState CurrentState;
    
    private void OnEnable()
    {
        _tipPressure.action.Enable();
        _button1.action.Enable();
        _button2.action.Enable();
        _stylusPosition.action.Enable();
        _stylusRotation.action.Enable();
    }
    
    private void Update()
    {
        // Read pressure (0.0 to 1.0, 4096 levels mapped)
        CurrentState.pressure = _tipPressure.action.ReadValue<float>();
        
        // Read buttons
        CurrentState.button1Pressed = _button1.action.IsPressed();
        CurrentState.button2Pressed = _button2.action.IsPressed();
        
        // Read position & rotation
        CurrentState.position = _stylusPosition.action.ReadValue<Vector3>();
        CurrentState.rotation = _stylusRotation.action.ReadValue<Quaternion>();
        
        // Calculate derived values
        CurrentState.velocity = (CurrentState.position - CurrentState.prevPosition) / Time.deltaTime;
        CurrentState.tiltAngle = CalculateTilt(CurrentState.rotation);
        
        CurrentState.prevPosition = CurrentState.position;
        
        // Broadcast to listeners
        OnStylusUpdate?.Invoke(CurrentState);
    }
    
    private Vector2 CalculateTilt(Quaternion rotation)
    {
        // Convert rotation to tilt angles (pitch/yaw)
        Vector3 forward = rotation * Vector3.forward;
        float pitch = Mathf.Atan2(forward.y, forward.z) * Mathf.Rad2Deg;
        float yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        return new Vector2(pitch, yaw);
    }
    
    // Event for other systems to subscribe
    public static System.Action<StylusState> OnStylusUpdate;
}

[System.Serializable]
public struct StylusState
{
    public Vector3 position;
    public Vector3 prevPosition;
    public Quaternion rotation;
    public float pressure;        // 0.0 - 1.0
    public Vector3 velocity;      // m/s
    public Vector2 tiltAngle;     // (pitch, yaw) in degrees
    public bool button1Pressed;
    public bool button2Pressed;
}
```

**Setup in Inspector:**
- Attach to `MXInkInputHandler` GameObject
- Assign Input Actions from `MXInk_OpenXR_Actions` asset:
  - Tip Pressure -> `MXInk/Tip Pressure`
  - Button 1 -> `MXInk/Cluster Front`
  - Button 2 -> `MXInk/Cluster Back`
  - Stylus Position -> `MXInk/Inking Pose/Position`
  - Stylus Rotation -> `MXInk/Inking Pose/Rotation`

---

### 3.2 Gesture Recognition System

**File:** `Scripts/Gesture/GestureRecognizer.cs`

```csharp
using UnityEngine;
using System.Collections.Generic;

public class GestureRecognizer : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private float _circleTolerancePercent = 0.15f;
    [SerializeField] private float _strikeVelocityThreshold = 0.5f; // m/s
    [SerializeField] private int _historyFrames = 120; // 1 second at 120fps
    
    private List<MotionFrame> _motionHistory = new List<MotionFrame>();
    private GestureType _currentGesture = GestureType.None;
    
    private void OnEnable()
    {
        MXInkInputHandler.OnStylusUpdate += OnStylusUpdate;
    }
    
    private void OnStylusUpdate(StylusState state)
    {
        // Add to history
        _motionHistory.Add(new MotionFrame
        {
            position = state.position,
            pressure = state.pressure,
            velocity = state.velocity,
            timestamp = Time.time
        });
        
        // Limit history size
        if (_motionHistory.Count > _historyFrames)
            _motionHistory.RemoveAt(0);
        
        // Analyze for gestures (only if sufficient data)
        if (_motionHistory.Count >= 30)
            DetectGesture();
    }
    
    private void DetectGesture()
    {
        // Check in priority order
        if (IsCircularMotion(out CircleData circle))
        {
            _currentGesture = GestureType.Pirouette;
            OnGestureDetected?.Invoke(new GestureEvent
            {
                type = GestureType.Pirouette,
                circleData = circle
            });
        }
        else if (IsStrikeMotion(out Vector3 strikeDirection))
        {
            _currentGesture = GestureType.GrandJete;
            OnGestureDetected?.Invoke(new GestureEvent
            {
                type = GestureType.GrandJete,
                direction = strikeDirection
            });
        }
        else if (IsWaveMotion(out List<Vector3> wavePath))
        {
            _currentGesture = GestureType.Arabesque;
            OnGestureDetected?.Invoke(new GestureEvent
            {
                type = GestureType.Arabesque,
                pathPoints = wavePath
            });
        }
    }
    
    private bool IsCircularMotion(out CircleData result)
    {
        result = new CircleData();
        
        if (_motionHistory.Count < 30)
            return false;
        
        // Get recent path
        List<Vector3> recentPath = new List<Vector3>();
        for (int i = _motionHistory.Count - 30; i < _motionHistory.Count; i++)
            recentPath.Add(_motionHistory[i].position);
        
        // Fit circle to path
        Vector3 center;
        float radius;
        FitCircle(recentPath, out center, out radius);
        
        // Calculate average deviation from fitted circle
        float totalDeviation = 0f;
        foreach (Vector3 point in recentPath)
        {
            float distToCenter = Vector3.Distance(point, center);
            totalDeviation += Mathf.Abs(distToCenter - radius);
        }
        float avgDeviation = totalDeviation / recentPath.Count;
        
        // Check closure (start ~= end)
        float closure = Vector3.Distance(recentPath[0], recentPath[recentPath.Count - 1]);
        
        bool isCircular = (avgDeviation / radius < _circleTolerancePercent) &&
                         (closure < radius * 0.3f);
        
        if (isCircular)
        {
            result.center = center;
            result.radius = radius;
            result.path = recentPath;
        }
        
        return isCircular;
    }
    
    private bool IsStrikeMotion(out Vector3 direction)
    {
        direction = Vector3.zero;
        
        if (_motionHistory.Count < 10)
            return false;
        
        // Check recent velocity
        Vector3 avgVelocity = Vector3.zero;
        int samples = Mathf.Min(10, _motionHistory.Count);
        
        for (int i = _motionHistory.Count - samples; i < _motionHistory.Count; i++)
            avgVelocity += _motionHistory[i].velocity;
        
        avgVelocity /= samples;
        
        if (avgVelocity.magnitude > _strikeVelocityThreshold)
        {
            direction = avgVelocity.normalized;
            return true;
        }
        
        return false;
    }
    
    private bool IsWaveMotion(out List<Vector3> wavePath)
    {
        wavePath = new List<Vector3>();
        
        if (_motionHistory.Count < 20)
            return false;
        
        // Collect points with varying Y (height)
        float minY = float.MaxValue;
        float maxY = float.MinValue;
        
        for (int i = _motionHistory.Count - 20; i < _motionHistory.Count; i++)
        {
            Vector3 pos = _motionHistory[i].position;
            wavePath.Add(pos);
            
            if (pos.y < minY) minY = pos.y;
            if (pos.y > maxY) maxY = pos.y;
        }
        
        // Wave must have significant vertical variation (>20cm)
        return (maxY - minY) > 0.2f;
    }
    
    private void FitCircle(List<Vector3> points, out Vector3 center, out float radius)
    {
        // Simple least-squares circle fit in XZ plane
        // (For production, use Pratt's method or Taubin fit)
        
        center = Vector3.zero;
        foreach (Vector3 p in points)
            center += p;
        center /= points.Count;
        
        radius = 0f;
        foreach (Vector3 p in points)
            radius += Vector3.Distance(p, center);
        radius /= points.Count;
    }
    
    public static System.Action<GestureEvent> OnGestureDetected;
}

public enum GestureType
{
    None,
    Pirouette,      // Circular motion
    Arabesque,      // Wave with height variation
    GrandJete,      // Fast strike
    PortDeBras,     // Flowing modulation
    Plie,           // Downward fade
    Chaine,         // Rapid rotation
    Tendu,          // Slow extension
    Balance         // Side-to-side sway
}

[System.Serializable]
public struct GestureEvent
{
    public GestureType type;
    public CircleData circleData;
    public List<Vector3> pathPoints;
    public Vector3 direction;
}

[System.Serializable]
public struct CircleData
{
    public Vector3 center;
    public float radius;
    public List<Vector3> path;
}

[System.Serializable]
public struct MotionFrame
{
    public Vector3 position;
    public float pressure;
    public Vector3 velocity;
    public float timestamp;
}
```

---

### 3.3 Music System Architecture

**File:** `Scripts/Audio/MusicManager.cs`

```csharp
using UnityEngine;
using System.Collections.Generic;

public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }
    
    [Header("Configuration")]
    [SerializeField] private float _bpm = 120f;
    [SerializeField] private int _beatsPerBar = 4;
    
    [Header("Mode")]
    [SerializeField] private CreativeMode _currentMode = CreativeMode.Rhythm;
    
    private List<RhythmLoop> _activeRhythmLoops = new List<RhythmLoop>();
    private List<MelodyLine> _activeMelodies = new List<MelodyLine>();
    private AudioEngine _audioEngine;
    private float _currentBeat = 0f;
    
    public float BeatsPerSecond => _bpm / 60f;
    public CreativeMode CurrentMode => _currentMode;
    
    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
        
        _audioEngine = GetComponent<AudioEngine>();
    }
    
    private void Update()
    {
        // Advance musical time
        _currentBeat += BeatsPerSecond * Time.deltaTime;
        
        // Update all active loops
        foreach (var loop in _activeRhythmLoops)
            loop.Update(_currentBeat);
        
        foreach (var melody in _activeMelodies)
            melody.Update(_currentBeat);
    }
    
    public void CreateRhythmLoop(CircleData circleData, float pressure)
    {
        RhythmLoop loop = new RhythmLoop();
        loop.Initialize(circleData, _bpm, pressure);
        
        _activeRhythmLoops.Add(loop);
        
        Debug.Log($"Created rhythm loop: radius={circleData.radius:F2}m, tempo={_bpm} BPM");
    }
    
    public void CreateMelodyLine(List<Vector3> pathPoints, float pressure)
    {
        MelodyLine melody = new MelodyLine();
        melody.Initialize(pathPoints, _bpm, pressure);
        
        _activeMelodies.Add(melody);
        
        Debug.Log($"Created melody: {pathPoints.Count} points");
    }
    
    public void SetMode(CreativeMode mode)
    {
        _currentMode = mode;
        OnModeChanged?.Invoke(mode);
    }
    
    public static System.Action<CreativeMode> OnModeChanged;
}

public enum CreativeMode
{
    Rhythm,
    Melody,
    Harmony
}
```

**File:** `Scripts/Audio/RhythmLoop.cs`

```csharp
using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class RhythmLoop
{
    public List<TriggerPoint> triggers = new List<TriggerPoint>();
    public float loopLengthBeats = 4f;
    private float _lastTriggerBeat = -1f;
    
    public void Initialize(CircleData circleData, float bpm, float pressure)
    {
        // Convert circle circumference to loop length in beats
        // Larger circles = slower tempo feel
        float circumference = 2f * Mathf.PI * circleData.radius;
        loopLengthBeats = Mathf.Clamp(circumference / 0.5f, 2f, 8f);
        
        // Default: create 4 evenly-spaced triggers (kick pattern)
        triggers.Add(new TriggerPoint
        {
            beatPosition = 0f,
            sampleName = "Kick",
            velocity = pressure
        });
        
        triggers.Add(new TriggerPoint
        {
            beatPosition = loopLengthBeats * 0.5f,
            sampleName = "Snare",
            velocity = pressure * 0.9f
        });
        
        triggers.Add(new TriggerPoint
        {
            beatPosition = loopLengthBeats * 0.25f,
            sampleName = "HiHat",
            velocity = pressure * 0.7f
        });
        
        triggers.Add(new TriggerPoint
        {
            beatPosition = loopLengthBeats * 0.75f,
            sampleName = "HiHat",
            velocity = pressure * 0.7f
        });
    }
    
    public void Update(float currentBeat)
    {
        float loopPosition = currentBeat % loopLengthBeats;
        
        foreach (var trigger in triggers)
        {
            // Check if trigger should fire this frame
            if (ShouldFire(trigger.beatPosition, loopPosition, _lastTriggerBeat))
            {
                AudioEngine.Instance.PlaySample(trigger.sampleName, trigger.velocity);
            }
        }
        
        _lastTriggerBeat = loopPosition;
    }
    
    private bool ShouldFire(float triggerBeat, float currentPos, float lastPos)
    {
        // Handle loop wraparound
        if (lastPos > currentPos)
        {
            return triggerBeat >= lastPos || triggerBeat <= currentPos;
        }
        else
        {
            return triggerBeat >= lastPos && triggerBeat <= currentPos;
        }
    }
}

[System.Serializable]
public struct TriggerPoint
{
    public float beatPosition;   // 0 to loopLengthBeats
    public string sampleName;
    public float velocity;       // 0.0 to 1.0
}
```

**File:** `Scripts/Audio/AudioEngine.cs`

```csharp
using UnityEngine;
using System.Collections.Generic;
// If using FMOD: using FMODUnity;

public class AudioEngine : MonoBehaviour
{
    public static AudioEngine Instance { get; private set; }
    
    [Header("Sample Library")]
    [SerializeField] private List<AudioSample> _samples = new List<AudioSample>();
    
    private Dictionary<string, AudioClip> _sampleDict = new Dictionary<string, AudioClip>();
    private List<AudioSource> _voicePool = new List<AudioSource>();
    private const int MAX_VOICES = 32;
    
    private void Awake()
    {
        Instance = this;
        
        // Build sample dictionary
        foreach (var sample in _samples)
            _sampleDict[sample.name] = sample.clip;
        
        // Create voice pool
        for (int i = 0; i < MAX_VOICES; i++)
        {
            GameObject voiceObj = new GameObject($"Voice_{i}");
            voiceObj.transform.SetParent(transform);
            
            AudioSource source = voiceObj.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f; // Full 3D
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.maxDistance = 10f;
            
            _voicePool.Add(source);
        }
    }
    
    public void PlaySample(string sampleName, float velocity, Vector3 position = default)
    {
        if (!_sampleDict.ContainsKey(sampleName))
        {
            Debug.LogWarning($"Sample not found: {sampleName}");
            return;
        }
        
        // Find free voice
        AudioSource voice = GetFreeVoice();
        if (voice == null)
        {
            Debug.LogWarning("All voices busy");
            return;
        }
        
        // Configure and play
        voice.clip = _sampleDict[sampleName];
        voice.volume = velocity;
        voice.transform.position = position;
        voice.Play();
    }
    
    private AudioSource GetFreeVoice()
    {
        foreach (var voice in _voicePool)
        {
            if (!voice.isPlaying)
                return voice;
        }
        return null;
    }
}

[System.Serializable]
public class AudioSample
{
    public string name;
    public AudioClip clip;
}
```

---

### 3.4 Visual Feedback System

**File:** `Scripts/Visual/TrailRenderer.cs`

```csharp
using UnityEngine;

public class MotionTrailRenderer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform _stylusTip;
    [SerializeField] private ParticleSystem _particleSystem;
    
    [Header("Trail Settings")]
    [SerializeField] private float _emissionRate = 60f;
    [SerializeField] private float _particleLifetime = 2.5f;
    [SerializeField] private AnimationCurve _widthOverPressure = AnimationCurve.Linear(0, 2, 1, 12);
    
    private ParticleSystem.EmitParams _emitParams;
    private CreativeMode _currentMode;
    private Color _trailColor;
    
    private readonly Color RHYTHM_COLOR = new Color(0.91f, 0.27f, 0.38f); // #e94560
    private readonly Color MELODY_COLOR = new Color(0.20f, 0.60f, 0.85f); // #3498db
    private readonly Color HARMONY_COLOR = new Color(0.61f, 0.35f, 0.71f); // #9b59b6
    
    private void OnEnable()
    {
        MusicManager.OnModeChanged += OnModeChanged;
        MXInkInputHandler.OnStylusUpdate += OnStylusUpdate;
    }
    
    private void OnModeChanged(CreativeMode mode)
    {
        _currentMode = mode;
        
        switch (mode)
        {
            case CreativeMode.Rhythm:
                _trailColor = RHYTHM_COLOR;
                break;
            case CreativeMode.Melody:
                _trailColor = MELODY_COLOR;
                break;
            case CreativeMode.Harmony:
                _trailColor = HARMONY_COLOR;
                break;
        }
    }
    
    private void OnStylusUpdate(StylusState state)
    {
        if (state.pressure < 0.05f)
            return; // No trail when not pressing
        
        // Emit particles
        float particlesThisFrame = _emissionRate * Time.deltaTime;
        int count = Mathf.CeilToInt(particlesThisFrame);
        
        _emitParams.position = _stylusTip.position;
        _emitParams.startColor = _trailColor;
        _emitParams.startSize = _widthOverPressure.Evaluate(state.pressure) * 0.01f;
        _emitParams.startLifetime = _particleLifetime;
        
        _particleSystem.Emit(_emitParams, count);
    }
}
```

---

## 4. Integration Flow & Events

### 4.1 Event-Driven Architecture

```
[User Input] MX Ink Stylus
      |
      v
[MXInkInputHandler] reads raw input -> broadcasts StylusState
      |
      v
[GestureRecognizer] analyzes motion -> detects Pirouette/Arabesque/Strike
      |
      v
[MusicManager] receives gesture event -> creates RhythmLoop/MelodyLine
      |
      v
[AudioEngine] plays samples with spatial positioning
      |
      v
[TrailRenderer] visualizes motion with color-coded particles
```

**Key Events:**

```csharp
// Subscribe in OnEnable(), unsubscribe in OnDisable()

MXInkInputHandler.OnStylusUpdate += HandleStylusUpdate;
GestureRecognizer.OnGestureDetected += HandleGesture;
MusicManager.OnModeChanged += HandleModeChange;
```

---

## 5. Performance Optimization

### 5.1 Frame Budget (Quest 3 @ 120Hz)

**Target:** 8.33ms per frame

| System | Budget | Optimization |
|--------|--------|--------------|
| Input Processing | 0.5ms | Cached references, minimal allocations |
| Gesture Recognition | 1.0ms | Downsample to 60Hz, run on background thread |
| Audio Synthesis | 2.0ms | Voice pooling, sample pre-loading |
| Rendering (trails) | 3.5ms | GPU instancing, particle LOD |
| Spatial Audio | 1.0ms | Cull inaudible sources (<-60dB) |

### 5.2 Optimization Techniques

**Audio:**
```csharp
// Pre-load all samples at start
Resources.LoadAll<AudioClip>("Audio/Samples");

// Voice pooling (reuse AudioSources)
if (!audioSource.isPlaying)
    audioSource.Play();

// Distance culling
if (Vector3.Distance(listener, source) > maxAudibleDistance)
    source.enabled = false;
```

**Rendering:**
```csharp
// GPU instancing for particles
particleSystemRenderer.enableGPUInstancing = true;

// LOD for distant objects
if (distanceToCamera > 5f)
    objectRenderer.enabled = false;
```

---

## 6. Testing & Debugging

### 6.1 In-Editor Testing (without headset)

**Simulate MX Ink with Mouse:**

```csharp
#if UNITY_EDITOR
private void Update()
{
    if (Input.GetMouseButton(0))
    {
        StylusState fakeStylusState = new StylusState
        {
            position = Camera.main.ScreenToWorldPoint(
                new Vector3(Input.mousePosition.x, Input.mousePosition.y, 2f)
            ),
            pressure = Input.GetKey(KeyCode.LeftShift) ? 1f : 0.5f,
            velocity = (currentPos - prevPos) / Time.deltaTime
        };
        
        MXInkInputHandler.OnStylusUpdate?.Invoke(fakeStylusState);
    }
}
#endif
```

### 6.2 Debug Visualization

**File:** `Scripts/Debug/DebugVisualizer.cs`

```csharp
private void OnDrawGizmos()
{
    // Draw motion history
    if (_motionHistory.Count > 1)
    {
        Gizmos.color = Color.yellow;
        for (int i = 1; i < _motionHistory.Count; i++)
        {
            Gizmos.DrawLine(
                _motionHistory[i - 1].position,
                _motionHistory[i].position
            );
        }
    }
    
    // Draw detected circle
    if (_lastDetectedCircle.radius > 0)
    {
        Gizmos.color = Color.green;
        DrawCircle(_lastDetectedCircle.center, _lastDetectedCircle.radius);
    }
}
```

---

## 7. Build & Deployment

### 7.1 Build Settings

```
File > Build Settings
Platform: Android
- Texture Compression: ASTC
- Development Build: [X] (for debugging)
- Script Debugging: [X] (for profiling)

Player Settings > Android:
- Package Name: com.yourcompany.rhythmforge
- Version: 0.1.0
- Minimum API Level: Android 10 (API 29)
- Target API Level: Android 13 (API 33)
- Scripting Backend: IL2CPP
- Target Architectures: ARM64 [X]

XR Settings:
- Stereo Rendering Mode: Multiview
- V Sync: Don't Sync
```

### 7.2 Deploy to Quest 3

```bash
# Enable Developer Mode on Quest 3 via Meta Quest app

# Build APK in Unity
Build Settings > Build

# Install via ADB
adb install -r RhythmForgeVR.apk

# Monitor logs
adb logcat -s Unity
```

---

## 8. Next Steps & Roadmap

### Week 1: Core Foundation
- [X] MX Ink input integration
- [X] Gesture recognition (Pirouette, Arabesque, Strike)
- [X] Basic rhythm loop system
- [ ] Spatial audio setup (FMOD)

### Week 2: Music Features
- [ ] Melody mode with pitch mapping
- [ ] Instrument spawning system
- [ ] Spatial mixing (grab & move sound objects)
- [ ] Export to WAV

### Week 3: Visual Polish
- [ ] Enhanced particle trails
- [ ] Beat indicators and pulsing
- [ ] Left-hand radial menu UI
- [ ] Musical staff visualization (Melody mode)

### Week 4: UX & Testing
- [ ] Tutorial system (onboarding)
- [ ] Gesture calibration
- [ ] Performance profiling (hit 120fps)
- [ ] User testing with 5-10 people

---

## 9. Troubleshooting Common Issues

### Issue: MX Ink not detected

**Solution:**
```
1. Pair stylus via Meta Quest Settings > Controllers > Pair Stylus
2. Verify MX Ink OpenXR profile enabled in Unity XR settings
3. Check Input Actions are assigned in MXInkInputHandler Inspector
```

### Issue: Audio latency too high

**Solution:**
```csharp
// In AudioSettings (Edit > Project Settings > Audio)
DSP Buffer Size: Best Latency
Sample Rate: 48000 Hz

// For FMOD
FMOD.Studio.System.setDSPBufferSize(512, 4);
```

### Issue: Gesture recognition false positives

**Solution:**
```csharp
// Increase thresholds
_circleTolerancePercent = 0.10f; // More strict (was 0.15f)
_strikeVelocityThreshold = 0.7f; // Faster required (was 0.5f)

// Add cooldown between gestures
if (Time.time - _lastGestureTime < 0.5f)
    return; // Ignore rapid successive detections
```

---

## 10. Resources & References

**Official Documentation:**
- Meta XR SDK: https://developers.meta.com/horizon/downloads/package/meta-xr-sdk/
- MX Ink Unity Integration: https://logitech.github.io/mxink/
- FMOD Unity Integration: https://fmod.com/resources/documentation-unity

**Sample Projects:**
- MX Ink OpenXR Demo: https://github.com/dilmerv/MetaMXInkDemo
- VR Gesture Recognition: https://github.com/alecfilios/Unity-Hand-Tracking-Gesture-Recognition

**Community:**
- Meta Quest Developer Forum: https://communityforums.atmeta.com/t5/Quest-Development/bd-p/quest-development
- Unity XR Discord: https://discord.gg/unity

---

**This Unity implementation guide provides the complete technical foundation for building RhythmForge VR. All core systems (input, gesture recognition, audio, visual feedback) are architecture-ready for immediate development.**