// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections;
using Meta.XR.Samples;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PassthroughCameraSamples.MultiObjectDetection
{
    [MetaCodeSample("PassthroughCameraApiSamples-MultiObjectDetection")]
    public class DetectionUiMenuManager : MonoBehaviour
    {
        // [Header("Ui buttons")] // Not needed if no interactive menu
        // [SerializeField] private OVRInput.RawButton m_actionButton = OVRInput.RawButton.A;

        [Header("Ui elements ref.")]
        [SerializeField] private GameObject m_loadingPanel; // Will be disabled
        [SerializeField] private GameObject m_initialPanel; // Will be disabled
        [SerializeField] private GameObject m_noPermissionPanel; // Will be disabled
        [SerializeField] private Text m_labelInfromation;

        [Header("Debug Toggles")]
        [SerializeField] private Toggle m_toggleShowRayVisualizations;
        [SerializeField] private Toggle m_toggleSendCameraFrames;

        public bool IsInputActive { get; set; } = false;

        public UnityEvent<bool> OnPause;

        // Removed m_initialMenu
        // Removed m_objectsDetected, m_objectsIdentified as they are detection specific

        public bool IsPaused { get; private set; } = true; // Starts paused, unpaused by Start()

        // Public properties for debug toggle states
        public bool ShowRayVisualizations { get; private set; }
        public bool SendCameraFrames { get; private set; }

        #region Unity Functions
        private void Awake()
        {
            // Initialize debug toggle states and subscribe to events
            if (m_toggleShowRayVisualizations != null)
            {
                ShowRayVisualizations = m_toggleShowRayVisualizations.isOn;
                m_toggleShowRayVisualizations.onValueChanged.AddListener(SetShowRayVisualizations);
                Debug.Log("[DetectionUiMenuManager] Added listener to ShowRayVisualizations toggle.");
            }
            else
            {
                Debug.LogWarning("[DetectionUiMenuManager] ToggleShowRayVisualizations is not assigned in the inspector. Defaulting to false.");
                ShowRayVisualizations = false;
            }

            if (m_toggleSendCameraFrames != null)
            {
                SendCameraFrames = m_toggleSendCameraFrames.isOn;
                m_toggleSendCameraFrames.onValueChanged.AddListener(SetSendCameraFrames);
                Debug.Log("[DetectionUiMenuManager] Added listener to SendCameraFrames toggle.");
            }
            else
            {
                Debug.LogWarning("[DetectionUiMenuManager] ToggleSendCameraFrames is not assigned in the inspector. Defaulting to true (assuming default desired behavior).");
                SendCameraFrames = true; // Default to true if not assigned, as sending frames is core
            }
        }

        private IEnumerator Start()
        {
            // --- Begin modification: Direct to active state ---
            if (m_loadingPanel != null) m_loadingPanel.SetActive(false);
            if (m_initialPanel != null) m_initialPanel.SetActive(false);
            if (m_noPermissionPanel != null) m_noPermissionPanel.SetActive(false);

            // Wait for camera permission to be determined
            // This is crucial before unpausing DetectionManager which might rely on WebCamTexture
            float waitTime = 0;
            while(!PassthroughCameraPermissions.HasCameraPermission.HasValue && waitTime < 5.0f) // Max 5s wait
            {
                yield return null;
                waitTime += Time.deltaTime;
            }

            if(PassthroughCameraPermissions.HasCameraPermission.HasValue && PassthroughCameraPermissions.HasCameraPermission.Value)
            {
                Debug.Log("[DetectionUiMenuManager] Camera permission granted.");
                IsPaused = false;
                IsInputActive = true; // Enable general input if needed for other things
                Debug.Log("[DetectionUiMenuManager] Invoking OnPause(false) to unpause DetectionManager.");
                OnPause?.Invoke(false); // Unpause DetectionManager
            }
            else
            {
                if (!PassthroughCameraPermissions.HasCameraPermission.HasValue)
                {
                    Debug.LogError("[DetectionUiMenuManager] Camera permission status unknown after timeout.");
                }
                else if (!PassthroughCameraPermissions.HasCameraPermission.Value)
                {
                    Debug.LogError("[DetectionUiMenuManager] Camera permission denied.");
                }

                if (m_labelInfromation != null)
                {
                     m_labelInfromation.text = "ERROR: Camera permission denied or status unknown.";
                }
                if(m_noPermissionPanel != null) m_noPermissionPanel.SetActive(true); // Show no permission panel
                IsPaused = true;
                IsInputActive = false;
                Debug.Log("[DetectionUiMenuManager] Invoking OnPause(true) to keep DetectionManager paused due to permission issue.");
                OnPause?.Invoke(true); // Keep DetectionManager paused
                Debug.LogError("[DetectionUiMenuManager] Camera permission denied or status unknown. Cannot proceed.");
            }
            // --- End modification ---
        }

        private void Update()
        {
            // Input processing can be added here if needed for other functionalities
            // For now, it's minimal as the menu is removed.
            if (!IsInputActive)
                return;
        }
        #endregion

        // Removed OnNoPermissionMenu
        // Removed OnInitialMenu
        // Removed InitialMenuUpdate

        private void OnPauseMenu(bool visible) // This is called by itself in Start now.
        {
            IsPaused = visible;

            if (m_initialPanel != null) m_initialPanel.SetActive(false);
            if (m_noPermissionPanel != null) m_noPermissionPanel.SetActive(false);
            if (m_loadingPanel != null) m_loadingPanel.SetActive(false);

            OnPause?.Invoke(visible);
        }

        public void SetUiDebugText(string newText)
        {
            if (m_labelInfromation != null)
            {
                m_labelInfromation.text = newText;
            }
        }

        // Listener methods for Toggles
        private void SetShowRayVisualizations(bool isOn)
        {
            ShowRayVisualizations = isOn;
            Debug.Log($"[DetectionUiMenuManager] Ray Visualizations Toggled: {ShowRayVisualizations}");
        }

        private void SetSendCameraFrames(bool isOn)
        {
            SendCameraFrames = isOn;
            Debug.Log($"[DetectionUiMenuManager] Send Camera Frames Toggled: {SendCameraFrames}");
        }

        /// <summary>Invert the Show-Ray-Visualizations toggle and fire its listeners.</summary>
        public void FlipShowRayVisualizations()
        {
            Debug.Log("[DetectionUiMenuManager] FlipShowRayVisualizations called");
            if (m_toggleShowRayVisualizations == null) return;
            m_toggleShowRayVisualizations.isOn = !m_toggleShowRayVisualizations.isOn;  // invokes onValueChanged
        }

        /// <summary>Invert the Send-Camera-Frames toggle and fire its listeners.</summary>
        public void FlipSendCameraFrames()
        {
            Debug.Log("[DetectionUiMenuManager] FlipSendCameraFrames called");
            if (m_toggleSendCameraFrames == null) return;
            m_toggleSendCameraFrames.isOn = !m_toggleSendCameraFrames.isOn;
        }
    }
}
