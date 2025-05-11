using UnityEngine;

namespace PassthroughCameraSamples.MultiObjectDetection
{
    public class ToggleDebugControlsWithButtons : MonoBehaviour
    {
        [SerializeField] private DetectionUiMenuManager _uiMenuManager;

        private void Update()
        {
            // Safety: nothing happens if the UI is locked or the reference is missing
            if (_uiMenuManager == null || !_uiMenuManager.IsInputActive)
                return;

            /* A-button ⇒ first toggle */
            if (OVRInput.GetDown(OVRInput.RawButton.A))
            {
                _uiMenuManager.FlipShowRayVisualizations();
                // optional haptic blip
                // OVRInput.SetControllerVibration(0, 0.2f, OVRInput.Controller.RTouch);
            }

            /* B-button ⇒ second toggle */
            if (OVRInput.GetDown(OVRInput.RawButton.B))
            {
                _uiMenuManager.FlipSendCameraFrames();
            }
        }
    }
}
