using UnityEngine;

namespace RhythmForge.Bootstrap
{
    /// <summary>
    /// Finds all critical VR rig transforms and the MX Ink stylus handler at runtime.
    /// Works by traversing the OVRCameraRig hierarchy using standard anchor names.
    /// </summary>
    public class VRRigLocator
    {
        public Transform CenterEye      { get; private set; }
        public Transform LeftController  { get; private set; }
        public Transform RightController { get; private set; }
        public Transform TrackingSpace   { get; private set; }
        public StylusHandler Stylus      { get; private set; }
        public bool IsValid              { get; private set; }

        public static VRRigLocator Find()
        {
            var locator = new VRRigLocator();

            // ── OVRCameraRig anchors ──
            var rig = Object.FindFirstObjectByType<OVRCameraRig>();
            if (rig != null)
            {
                locator.CenterEye       = rig.centerEyeAnchor;
                locator.LeftController  = rig.leftControllerAnchor;
                locator.RightController = rig.rightControllerAnchor;
                locator.TrackingSpace   = rig.trackingSpace;
            }
            else
            {
                // Fallback: search by name for XR Origin setups
                var centerEyeGo = GameObject.Find("CenterEyeAnchor");
                if (centerEyeGo != null) locator.CenterEye = centerEyeGo.transform;

                var leftCtrlGo = GameObject.Find("LeftControllerAnchor");
                if (leftCtrlGo != null) locator.LeftController = leftCtrlGo.transform;

                var rightCtrlGo = GameObject.Find("RightControllerAnchor");
                if (rightCtrlGo != null) locator.RightController = rightCtrlGo.transform;
            }

            // ── MX Ink StylusHandler ──
            locator.Stylus = Object.FindFirstObjectByType<StylusHandler>();

            // ── Validity check ──
            locator.IsValid = locator.CenterEye != null;

            if (!locator.IsValid)
                Debug.LogWarning("[RhythmForge] VRRigLocator: CenterEyeAnchor not found. " +
                    "Ensure OVRCameraRig is present in the scene.");

            if (locator.Stylus == null)
                Debug.LogWarning("[RhythmForge] VRRigLocator: No StylusHandler found. " +
                    "MX Ink input will be unavailable.");

            return locator;
        }
    }
}
