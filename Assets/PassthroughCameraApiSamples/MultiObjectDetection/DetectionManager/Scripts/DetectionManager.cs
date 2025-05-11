// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections;
using System.Collections.Generic;
using Meta.XR.Samples;
using UnityEngine;
using UnityEngine.Events;
using Newtonsoft.Json;
using System;
using Meta.XR;
using Meta.XR.MRUtilityKit; // Added for EnvironmentRaycastManager
// using Meta.XR.EnvironmentDepth; // No longer explicitly used by DetectionManager directly

namespace PassthroughCameraSamples.MultiObjectDetection
{
    [MetaCodeSample("PassthroughCameraApiSamples-MultiObjectDetection")]
    public class DetectionManager : MonoBehaviour
    {
        [SerializeField] private WebCamTextureManager m_webCamTextureManager;
        
        [Header("HTTP Communication")]  
        [SerializeField] private HTTPEndpointManager m_httpEndpointManager; // Changed from WebSocketManager
        [SerializeField] private float m_httpSendInterval = 1.0f; // Adjusted interval for HTTP requests
        private float m_timeSinceLastHttpSend = 0f;

        [Header("Ui references")]
        [SerializeField] private DetectionUiMenuManager m_uiMenuManager;

        [Header("AR Instruction Display")] // Renamed header
        [SerializeField] private GameObject m_arMarkerPrefab; // Renamed from m_websocketMarkerPrefab
        private List<GameObject> m_arCommandMarkers = new List<GameObject>(); // Renamed from m_websocketCommandMarkers

        [Header("Ray Visualization")]
        [SerializeField] private Color m_rayColor = Color.yellow; 
        [SerializeField] private float m_rayWidth = 0.01f;
        [SerializeField] private GameObject m_intersectionPointPrefab;
        [SerializeField] private float m_rayVisualizationDuration = 2.0f;
        private List<GameObject> m_activeRayVisualizations = new List<GameObject>();

        [Header("MRUK Settings")] // Renamed header
        [SerializeField] private EnvironmentRaycastManager m_environmentRaycastManager; 

        private bool m_isPaused = true;
        // private List<GameObject> m_spwanedEntities = new(); // This seemed unused, removing for now
        // private bool m_isStarted = false; // This seemed unused, removing for now
        private float m_delayPauseBackTime = 0;
        
        [SerializeField] private PassthroughCameraSamples.PassthroughCameraEye m_cameraEye = PassthroughCameraSamples.PassthroughCameraEye.Left;

        #region Unity Functions
        private void Awake() 
        {
            OVRManager.display.RecenteredPose += CleanMarkersCallBack;
            
            if (m_httpEndpointManager == null)
            {
                Debug.LogWarning("[DetectionManager] HTTPEndpointManager not assigned in Inspector. Attempting to find or add it.");
                m_httpEndpointManager = GetComponent<HTTPEndpointManager>();
                if (m_httpEndpointManager == null)
                {
                    m_httpEndpointManager = gameObject.AddComponent<HTTPEndpointManager>();
                }
            }

            if (m_httpEndpointManager != null)
            {
                m_httpEndpointManager.OnInstructionReceived += HandleHttpResponse;
                m_httpEndpointManager.OnErrorReceived += HandleHttpError;
            }
            else
            {
                Debug.LogError("[DetectionManager] HTTPEndpointManager could not be initialized. AR instructions will not be processed.");
            }

            if (m_uiMenuManager != null)
            {
                m_uiMenuManager.OnPause.AddListener(OnPauseFromUi);
            }
            else
            {
                Debug.LogError("[DetectionManager] DetectionUiMenuManager is not assigned. The application might not unpause correctly and debug toggles will not work.");
            }
            
            if (m_intersectionPointPrefab == null)
            {
                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);
                sphere.GetComponent<Renderer>().material.color = Color.red; 
                m_intersectionPointPrefab = sphere;
                m_intersectionPointPrefab.SetActive(false); 
            }

            if (m_environmentRaycastManager == null)
            {
                Debug.LogError("[DetectionManager] EnvironmentRaycastManager is not assigned in the Inspector! Please assign it.");
            }
            else
            {
                if (!EnvironmentRaycastManager.IsSupported)
                {
                     Debug.LogError("[DetectionManager] EnvironmentRaycastManager is not supported on this device/SDK version. Check documentation and device capabilities.");
                }
            }
        }

        private void OnDestroy()
        {
            if (m_httpEndpointManager != null)
            {
                m_httpEndpointManager.OnInstructionReceived -= HandleHttpResponse;
                m_httpEndpointManager.OnErrorReceived -= HandleHttpError;
            }
            OVRManager.display.RecenteredPose -= CleanMarkersCallBack;
             if (m_uiMenuManager != null)
            {
                m_uiMenuManager.OnPause.RemoveListener(OnPauseFromUi);
            }
            
            ClearArCommandMarkers();
            ClearRayVisualizations();
        }

        private IEnumerator Start()
        {
            if (m_uiMenuManager != null)
            {
                m_uiMenuManager.SetUiDebugText("HTTP AR detection ready.\nWaiting for instructions...");
            }
            yield break;
        }

        private void Update()
        {
            var hasWebCamTextureData = m_webCamTextureManager != null && m_webCamTextureManager.WebCamTexture != null && m_webCamTextureManager.WebCamTexture.didUpdateThisFrame;

            if (m_delayPauseBackTime > 0) {
                m_delayPauseBackTime -= Time.deltaTime;
                if (m_delayPauseBackTime < 0) m_delayPauseBackTime = 0;
            }
            
            bool shouldSendCameraFrames = m_uiMenuManager == null || m_uiMenuManager.SendCameraFrames; 

            if (!m_isPaused && hasWebCamTextureData && m_httpEndpointManager != null && shouldSendCameraFrames)
            {
                m_timeSinceLastHttpSend += Time.deltaTime;
                
                if (m_timeSinceLastHttpSend >= m_httpSendInterval)
                {
                    Debug.Log($"[DetectionManager] Sending camera frame via HTTP. Size: {m_webCamTextureManager.WebCamTexture.width}x{m_webCamTextureManager.WebCamTexture.height}");
                    Pose cameraPose = PassthroughCameraSamples.PassthroughCameraUtils.GetCameraPoseInWorld(m_cameraEye);
                    m_httpEndpointManager.SendFrameForInstructions(m_webCamTextureManager.WebCamTexture, cameraPose);
                    m_timeSinceLastHttpSend = 0f;
                }
            }
            else
            {
                if (!m_isPaused) { 
                    if (!shouldSendCameraFrames)
                        Debug.Log("[DetectionManager] Not sending: Send Camera Frames toggle is off.");
                    else if (!hasWebCamTextureData)
                        Debug.Log("[DetectionManager] Not sending: No new WebCamTexture data this frame or texture is null.");
                    else if (m_httpEndpointManager == null)
                        Debug.LogWarning("[DetectionManager] Not sending: HTTPEndpointManager is null.");
                }
            }
        }
        #endregion
        
        #region HTTP Response Handling
        private void HandleHttpResponse(string jsonResponse)
        {
            Debug.Log($"[DetectionManager] HTTP Response received: {jsonResponse}");
            try
            {
                var arInstruction = JsonConvert.DeserializeObject<ArInstructionResponse>(jsonResponse);
                if (arInstruction != null)
                {
                    Debug.Log($"[DetectionManager] Parsed AR Instruction. Status: {arInstruction.current_task_status}");
                    List<string> objectTitles = new List<string>();
                    if (arInstruction.objects != null)
                    {
                        foreach (var obj in arInstruction.objects)
                        {
                            string coordsStr = obj.coordinates != null ? $"x:{obj.coordinates.x}, y:{obj.coordinates.y}" : "null";
                            Debug.Log($"[DetectionManager] Detected Object in AR Instruction: '{obj.title}' at ({coordsStr})");
                            objectTitles.Add(obj.title);
                        }
                    }

                    DisplayObjectMarkers(arInstruction); // Adapted to take ArInstructionResponse

                    string debugText = $"Status: {arInstruction.current_task_status}";
                    debugText += !string.IsNullOrEmpty(arInstruction.message) ? $"\nMsg: {arInstruction.message}" : "";
                    debugText += objectTitles.Count > 0 ? $"\nObjects: {string.Join(", ", objectTitles)}" : "\nNo objects in instruction";
                    
                    if (m_uiMenuManager != null) m_uiMenuManager.SetUiDebugText(debugText);
                    else Debug.LogWarning("[DetectionManager] Cannot update UI: m_uiMenuManager is null");
                }
                else
                {
                     Debug.LogWarning("[DetectionManager] Deserialized ArInstructionResponse is null.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DetectionManager] Failed to parse AR Instruction JSON: {ex.Message}\nStackTrace: {ex.StackTrace}");
            }
        }

        private void HandleHttpError(string errorJson)
        {
            Debug.LogWarning($"[DetectionManager] HTTP Error received: {errorJson}");
            try
            {
                var errorResponse = JsonConvert.DeserializeObject<ErrorResponse>(errorJson);
                string errorMessage = errorResponse?.error ?? "Unknown HTTP error";
                if (m_uiMenuManager != null) m_uiMenuManager.SetUiDebugText($"Error: {errorMessage}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DetectionManager] Failed to parse error JSON: {ex.Message}. Raw error: {errorJson}");
                if (m_uiMenuManager != null) m_uiMenuManager.SetUiDebugText($"HTTP Error (raw): {errorJson.Substring(0, Mathf.Min(errorJson.Length, 100))}");
            }
        }
        
        private void DisplayObjectMarkers(ArInstructionResponse instruction) // Changed parameter type
        {
            bool shouldShowRays = m_uiMenuManager == null || m_uiMenuManager.ShowRayVisualizations; 
            if (shouldShowRays) 
            {
                ClearRayVisualizations(); 
            }

            if (m_arMarkerPrefab == null) // Used renamed variable
            {
                Debug.LogWarning("[DetectionManager] Cannot display object markers: Missing m_arMarkerPrefab.");
                return;
            }
            
            // If new instructions contain no objects, existing markers might be cleared or kept based on desired logic.
            // Current logic: if new instructions arrive, existing markers for *newly specified* objects are updated.
            // Markers for objects *not* in the new instruction list (but previously existing) are kept if not explicitly cleared here.
            if (instruction.objects == null) // Removed .Count == 0 check to allow for keeping markers if objects list is empty but not null
            {
                 Debug.Log("[DetectionManager] No objects found in AR instruction. Current markers unchanged unless explicitly managed.");
                 // Decide if all markers should be cleared if an empty but valid object list is received.
                 // For now, this means if instruction.objects is null, we do nothing further with markers here.
                 // If it's an empty list, the loop below won't run, and existingMarkers logic will take effect.
                 // return; // Returning here would mean no new markers are processed and no old ones are cleared based on new data.
            }
            
            if (m_environmentRaycastManager == null)
            {
                Debug.LogError("[DetectionManager] EnvironmentRaycastManager is not initialized. Cannot place markers accurately.");
                return;
            }
            
            Dictionary<string, GameObject> existingMarkers = new Dictionary<string, GameObject>();
            for (int i = m_arCommandMarkers.Count - 1; i >= 0; i--) // Used renamed list
            {
                GameObject marker = m_arCommandMarkers[i];
                if (marker == null)
                {
                    m_arCommandMarkers.RemoveAt(i);
                    continue;
                }
                var markerAnim = marker.GetComponent<DetectionSpawnMarkerAnim>();
                string markerTitle = markerAnim != null ? markerAnim.GetYoloClassName() : marker.GetComponentInChildren<TextMesh>()?.text;
                if (!string.IsNullOrEmpty(markerTitle) && !existingMarkers.ContainsKey(markerTitle))
                {
                    existingMarkers[markerTitle] = marker;
                }
            }
            
            // For HTTP, camera_pose is part of the request metadata, not typically in the response for placing objects from *that same request*.
            // We use the current camera pose for raycasting as object coordinates are relative to the image sent.
            Pose cameraPose = PassthroughCameraSamples.PassthroughCameraUtils.GetCameraPoseInWorld(m_cameraEye);
            
            // If instruction.objects is null, this loop won't execute. 
            // If it's an empty list, it also won't execute. Existing markers will remain.
            if (instruction.objects != null)
            {
                foreach (var detectedObject in instruction.objects)
                {
                    if (detectedObject.coordinates == null)
                    {
                        Debug.LogWarning($"[DetectionManager] Object '{detectedObject.title}' in AR instruction has missing coordinates, keeping existing marker if available.");
                        if (existingMarkers.ContainsKey(detectedObject.title)) existingMarkers.Remove(detectedObject.title); // Remove from existing so it's not cleared later
                        continue;
                    }
                    
                    Debug.Log($"[DetectionManager] Processing object '{detectedObject.title}' with coordinates: ({detectedObject.coordinates.x}, {detectedObject.coordinates.y})");

                    if (existingMarkers.TryGetValue(detectedObject.title, out GameObject oldMarker))
                    {
                        m_arCommandMarkers.Remove(oldMarker);
                        Destroy(oldMarker);
                        existingMarkers.Remove(detectedObject.title); 
                    }

                    float u = Mathf.Clamp01(detectedObject.coordinates.x);
                    float v = Mathf.Clamp01(detectedObject.coordinates.y);
                    
                    var intrinsics = PassthroughCameraSamples.PassthroughCameraUtils.GetCameraIntrinsics(m_cameraEye);
                    var directionInCamera = new Vector3
                    {
                        x = (u * intrinsics.Resolution.x - intrinsics.PrincipalPoint.x) / intrinsics.FocalLength.x,
                        y = ((1f - v) * intrinsics.Resolution.y - intrinsics.PrincipalPoint.y) / intrinsics.FocalLength.y,
                        z = 1
                    };
                    
                    Vector3 rayDirectionInWorld = cameraPose.rotation * directionInCamera;
                    Ray ray = new Ray(cameraPose.position, rayDirectionInWorld);
                    
                    EnvironmentRaycastHit environmentHit;
                    bool didHit = false;
                    float maxRaycastDistance = 10.0f;
                    float defaultPlacementDistance = 1.0f;
                    Vector3 markerPosition;
                    float hitDistance = defaultPlacementDistance;

                    if (m_environmentRaycastManager.Raycast(ray, out environmentHit, maxRaycastDistance))
                    {
                        if (environmentHit.status == EnvironmentRaycastHitStatus.Hit)
                        {
                            markerPosition = environmentHit.point;
                            hitDistance = Vector3.Distance(ray.origin, environmentHit.point); 
                            didHit = true;
                        }
                        else
                        {
                            markerPosition = ray.GetPoint(defaultPlacementDistance);
                        }
                    }
                    else
                    {
                        markerPosition = ray.GetPoint(defaultPlacementDistance);
                    }
                
                    GameObject commandMarker = Instantiate(m_arMarkerPrefab, markerPosition, Quaternion.identity); // Used renamed variable
                    m_arCommandMarkers.Add(commandMarker); // Used renamed list
                
                    var markerAnim = commandMarker.GetComponent<DetectionSpawnMarkerAnim>();
                    string displayText = detectedObject.title; 

                    if (markerAnim != null) markerAnim.SetYoloClassName(displayText);
                    else 
                    {
                        var textMesh = commandMarker.GetComponentInChildren<TextMesh>();
                        if (textMesh != null) textMesh.text = displayText;
                    }
                    
                    if (shouldShowRays)
                    {
                        VisualizeRay(ray.origin, ray.direction, hitDistance, markerPosition, didHit);
                    }
                }
            }

            // Clear any remaining old markers that were not in the new instruction set and had coordinates
            foreach (var pair in existingMarkers)
            {
                Debug.Log($"[DetectionManager] Clearing stale marker for object '{pair.Key}' not in new instructions.");
                m_arCommandMarkers.Remove(pair.Value);
                Destroy(pair.Value);
            }
        }
        
        private void VisualizeRay(Vector3 startPoint, Vector3 direction, float distance, Vector3 hitPoint, bool didHit)
        {
            GameObject rayVisualization = new GameObject("RayVisualization");
            m_activeRayVisualizations.Add(rayVisualization);
            
            GameObject rayLine = new GameObject("RayLine");
            rayLine.transform.SetParent(rayVisualization.transform);
            LineRenderer lineRenderer = rayLine.AddComponent<LineRenderer>();
            
            Color rayRenderColor = didHit ? Color.green : Color.red;
            
            lineRenderer.startColor = rayRenderColor;
            lineRenderer.endColor = rayRenderColor;
            lineRenderer.startWidth = m_rayWidth;
            lineRenderer.endWidth = m_rayWidth;
            lineRenderer.positionCount = 2;
            lineRenderer.SetPosition(0, startPoint);
            lineRenderer.SetPosition(1, startPoint + direction * distance);
            
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            
            GameObject intersectionPoint = Instantiate(m_intersectionPointPrefab, hitPoint, Quaternion.identity, rayVisualization.transform);
            intersectionPoint.SetActive(true);
            Renderer renderer = intersectionPoint.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = rayRenderColor;
            }
            
            // Debug.DrawRay(startPoint, direction * distance, rayRenderColor, m_rayVisualizationDuration); // Already visualized by LineRenderer
            
            StartCoroutine(DestroyRayVisualizationAfterDelay(rayVisualization, m_rayVisualizationDuration));
        }
        
        private IEnumerator DestroyRayVisualizationAfterDelay(GameObject rayVisualization, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (rayVisualization != null)
            {
                m_activeRayVisualizations.Remove(rayVisualization);
                Destroy(rayVisualization);
            }
        }
        
        private void ClearRayVisualizations()
        {
            foreach (var rayVis in m_activeRayVisualizations)
            {
                if (rayVis != null) Destroy(rayVis);
            }
            m_activeRayVisualizations.Clear();
        }
        
        private void ClearArCommandMarkers() // Renamed method
        {
            foreach (var marker in m_arCommandMarkers) // Used renamed list
            {
                if (marker != null) Destroy(marker);
            }
            m_arCommandMarkers.Clear(); // Used renamed list
        }
        #endregion

        private void CleanMarkersCallBack()
        {
            ClearArCommandMarkers(); 
            ClearRayVisualizations();
            
            if (m_uiMenuManager != null)
            {
                m_uiMenuManager.SetUiDebugText("Tracking recentered. Waiting for new instructions...");
            }
        }

        #region Public Functions
        public void OnPauseFromUi(bool pause)
        {
            m_isPaused = pause;
            if (!pause) 
            {
                // m_isStarted = true; // Unused
                if (m_uiMenuManager != null)
                {
                    m_uiMenuManager.SetUiDebugText("Detection active.\nWaiting for instructions...");
                }
                Debug.Log("[DetectionManager] Unpaused by UI Manager. Frame streaming should start if SendCameraFrames is true.");
            }
            else
            {
                if (m_uiMenuManager != null)
                {
                    m_uiMenuManager.SetUiDebugText("Detection paused.\nPress play to resume.");
                }
                Debug.Log("[DetectionManager] Paused by UI Manager.");
            }
        }
        #endregion
    }
    
    // These classes are now defined in HTTPEndpointManager.cs or a shared file.
    // Ensure they are accessible here (e.g. via namespace).
    // If ArInstructionResponse is defined in HTTPEndpointManager's namespace, 
    // you might need a using statement for that namespace if it's different.

    // [System.Serializable]
    // public class WebSocketCommand // This will be replaced by ArInstructionResponse
    // {
    //     public List<DetectedObject> objects { get; set; } = new List<DetectedObject>();
    //     public string action { get; set; }
    //     public CameraPoseData coordinates_relative_to_camera_pose { get; set; }
    // }
    
    [System.Serializable]
    public class DetectedObject // Assuming this structure is still valid for AR instructions
    {
        public string title { get; set; } // Ensure properties match JSON field names (case-sensitive with Newtonsoft.Json by default)
        public WebSocketCoordinate coordinates { get; set; } 
    }

    [System.Serializable]
    public class WebSocketCoordinate // Assuming this structure is still valid
    {
        public float x { get; set; }
        public float y { get; set; }
    }
}