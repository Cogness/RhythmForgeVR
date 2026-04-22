using System;
using UnityEngine;

namespace RhythmForge.Bootstrap
{
    /// <summary>
    /// Runtime controller that toggles between Quest PassThrough and a custom
    /// "Immersed" dark environment. Flips <see cref="OVRPassthroughLayer"/>,
    /// <see cref="OVRManager.isInsightPassthroughEnabled"/>, the CenterEye camera
    /// clear color alpha, and activates/deactivates the immersive environment root.
    /// Choice is persisted via PlayerPrefs (default: Immersed).
    /// </summary>
    public class ImmersionController : MonoBehaviour
    {
        private const string PrefKey = "RhythmForge.PassthroughEnabled";
        // 0 = Immersed (default), 1 = PassThrough
        private const int DefaultPref = 0;

        // Opaque near-black used for the immersed sky. Alpha=1 is required so the
        // Quest compositor does NOT blend real-world passthrough through the camera.
        private static readonly Color ImmersedSky = new Color(0.018f, 0.022f, 0.040f, 1f);
        // Fully transparent (alpha=0) lets the OVRPassthroughLayer composite through.
        private static readonly Color PassthroughClear = new Color(0f, 0f, 0f, 0f);

        private GameObject _environmentRoot;
        private Camera _centerEyeCamera;
        private OVRPassthroughLayer _passthroughLayer;

        private bool _passthroughEnabled;
        private bool _configured;
        private bool _cameraResolved;
        private float _cameraRetryTimer;

        /// <summary>Fired whenever the mode changes. True = passthrough, false = immersed.</summary>
        public event Action<bool> OnModeChanged;

        public bool PassthroughEnabled => _passthroughEnabled;
        public bool ImmersedEnabled => !_passthroughEnabled;

        /// <summary>
        /// Inject dependencies and apply the saved mode.
        /// Call this once right after the environment root and VR rig are built.
        /// </summary>
        public void Configure(GameObject environmentRoot, Transform centerEye)
        {
            _environmentRoot = environmentRoot;
            TryResolveCenterEye(centerEye);

            // Locate the passthrough layer from the scene (scene already has a
            // [BuildingBlock] Passthrough GameObject with an OVRPassthroughLayer).
            _passthroughLayer = UnityEngine.Object.FindFirstObjectByType<OVRPassthroughLayer>(FindObjectsInactive.Include);

            if (_passthroughLayer == null)
                Debug.LogWarning("[RhythmForge] ImmersionController: No OVRPassthroughLayer found. " +
                                 "PassThrough will be unavailable; forcing Immersed mode.");

            _configured = true;

            int saved = PlayerPrefs.GetInt(PrefKey, DefaultPref);
            bool passthrough = (saved == 1) && _passthroughLayer != null;
            ApplyMode(passthrough, persist: false);
        }

        private void TryResolveCenterEye(Transform centerEye)
        {
            if (centerEye != null)
            {
                _centerEyeCamera = centerEye.GetComponent<Camera>();
                if (_centerEyeCamera != null) { _cameraResolved = true; return; }
            }

            // Fallback: find the OVRCameraRig's CenterEye camera at runtime.
            var rig = UnityEngine.Object.FindFirstObjectByType<OVRCameraRig>();
            if (rig != null && rig.centerEyeAnchor != null)
            {
                _centerEyeCamera = rig.centerEyeAnchor.GetComponent<Camera>();
                _cameraResolved = _centerEyeCamera != null;
            }
        }

        private void Update()
        {
            // The OVR rig sometimes initializes after our Configure() call — retry for a couple seconds.
            if (_configured && !_cameraResolved && _cameraRetryTimer < 3f)
            {
                _cameraRetryTimer += Time.deltaTime;
                TryResolveCenterEye(null);
                if (_cameraResolved)
                    ApplyMode(_passthroughEnabled, persist: false);
            }
        }

        /// <summary>Toggles between PassThrough and Immersed.</summary>
        public void Toggle()
        {
            if (!_configured) return;
            SetPassthroughEnabled(!_passthroughEnabled);
        }

        /// <summary>Explicitly set the mode.</summary>
        public void SetPassthroughEnabled(bool passthrough)
        {
            if (!_configured) return;
            if (passthrough && _passthroughLayer == null)
            {
                Debug.LogWarning("[RhythmForge] ImmersionController: Cannot enable PassThrough — no OVRPassthroughLayer.");
                return;
            }
            ApplyMode(passthrough, persist: true);
        }

        private void ApplyMode(bool passthrough, bool persist)
        {
            _passthroughEnabled = passthrough;

            if (_passthroughLayer != null)
                _passthroughLayer.enabled = passthrough;

            if (OVRManager.instance != null)
                OVRManager.instance.isInsightPassthroughEnabled = passthrough;

            if (_centerEyeCamera != null)
            {
                // Keep clear flags as SolidColor — we just swap alpha to reveal or hide passthrough.
                _centerEyeCamera.clearFlags = CameraClearFlags.SolidColor;
                _centerEyeCamera.backgroundColor = passthrough ? PassthroughClear : ImmersedSky;
            }

            if (_environmentRoot != null)
                _environmentRoot.SetActive(!passthrough);

            if (persist)
            {
                PlayerPrefs.SetInt(PrefKey, passthrough ? 1 : 0);
                PlayerPrefs.Save();
            }

            OnModeChanged?.Invoke(passthrough);
        }
    }
}
