#if UNITY_EDITOR
using UnityEngine;

namespace RhythmForge.Bootstrap
{
    /// <summary>
    /// Disables VrStylusHandler at the earliest possible moment in the editor.
    /// VrStylusHandler.Update() floods LogErrors because MX Ink OpenXR action
    /// bindings don't exist without a physical device — this suppresses that.
    ///
    /// Attach this to ANY always-active GameObject in the scene (e.g. the OVRCameraRig).
    /// ExecutionOrder -200 ensures it runs before VrStylusHandler (default order 0).
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class EditorStylusSuppressor : MonoBehaviour
    {
        private void Awake()
        {
            var stylus = Object.FindFirstObjectByType<VrStylusHandler>();
            if (stylus != null)
            {
                stylus.enabled = false;
                Debug.Log("[RhythmForge] EditorStylusSuppressor: VrStylusHandler disabled in editor.");
            }
        }
    }
}
#endif
