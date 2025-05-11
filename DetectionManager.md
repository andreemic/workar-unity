# DetectionManager Script Overview

The `DetectionManager.cs` script is a core component responsible for handling WebSocket communications, processing detected object data, placing visual markers in the 3D scene, managing UI feedback, and enabling debug visualizations for depth data in an augmented reality application.

## 1. Depth Visualization

Depth visualization helps in debugging how the application perceives the real-world environment. It can be toggled on or off during runtime.

**Toggling Visualization:**
- Pressing the **'B' key** toggles the depth visualization.
- This is handled in the `Update()` method:

```csharp
// In Update()
if (Input.GetKeyDown(KeyCode.B))
{
    m_showDepthVisualization = !m_showDepthVisualization;
    Debug.Log($"[DetectionManager] Depth visualization toggled to: {m_showDepthVisualization}");

    if (m_depthVisualizationObject != null)
    {
        m_depthVisualizationObject.SetActive(m_showDepthVisualization);
    }
    else if (m_environmentDepthManager != null)
    {
        // If no specific object, try a common debug property (this is speculative)
        // e.g., m_environmentDepthManager.EnableDebugRendering = m_showDepthVisualization;
        Debug.LogWarning("[DetectionManager] m_depthVisualizationObject is not assigned. Direct toggling on EnvironmentDepthManager is not standard and may require specific MRUK sample code for visualization.");
    }
    else
    {
        Debug.LogWarning("[DetectionManager] EnvironmentDepthManager is null. Cannot toggle depth visualization.");
    }
}
```

**Mechanism:**
- A boolean flag `m_showDepthVisualization` tracks the state.
- If `m_depthVisualizationObject` (a `GameObject` to be assigned in the Inspector, presumably set up to render depth data from MRUK) is assigned, its `SetActive()` state is toggled.
- If `m_depthVisualizationObject` is not assigned but `m_environmentDepthManager` exists, a warning is logged, suggesting that direct toggling on `EnvironmentDepthManager` is not standard.
- The `m_environmentDepthManager` itself is typically found or associated with the `m_environmentRaycastManager` in `Awake()`.

## 2. Object Placement with Raycasting

When object detection data is received via WebSocket, the script places markers in the scene corresponding to these detected objects. This uses the MR Utility Kit's (MRUK) `EnvironmentRaycastManager` for accurate placement on real-world surfaces.

**Process in `DisplayObjectMarkers(WebSocketCommand command)`:**
1.  **Ray Creation**: For each `detectedObject` with coordinates:
    - 2D screen coordinates (`u`, `v`) are normalized and potentially adjusted (e.g., `1f - v` for Y-axis inversion if needed, depending on coordinate system origins).
    - A ray is created from the main camera's perspective:
      ```csharp
      float u = Mathf.Clamp01(detectedObject.coordinates.x);
      float v = Mathf.Clamp01(detectedObject.coordinates.y);
      Ray ray = Camera.main.ViewportPointToRay(new Vector3(u, 1f - v, 0f));
      ```

2.  **Environment Raycasting**:
    - The `m_environmentRaycastManager` (assigned in the Inspector) is used to cast this ray against the environment's depth data.
    - `maxRaycastDistance` defines how far the ray checks for hits.
    ```csharp
    EnvironmentRaycastHit environmentHit;
    bool didHit = false;
    float maxRaycastDistance = 10.0f;
    float defaultPlacementDistance = 1.0f;
    Vector3 markerPosition;

    if (m_environmentRaycastManager.Raycast(ray, out environmentHit, maxRaycastDistance))
    {
        if (environmentHit.Status == EnvironmentRaycastHitStatus.Success)
        {
            markerPosition = environmentHit.Point;
            didHit = true;
            // ... log success ...
        }
        else
        {
            markerPosition = ray.GetPoint(defaultPlacementDistance);
            // ... log non-success status ...
        }
    }
    else
    {
        markerPosition = ray.GetPoint(defaultPlacementDistance);
        // ... log miss ...
    }
    ```

3.  **Marker Instantiation**:
    - A `commandMarker` (from `m_websocketMarkerPrefab`) is instantiated at the calculated `markerPosition`.
    - Text on the marker is set using `DetectionSpawnMarkerAnim` or a `TextMesh` component.

4.  **Ray Visualization (for debugging)**:
    - The `VisualizeRay()` method is called to draw the ray used for placement, colored green for a successful hit and red otherwise.
    ```csharp
    // In DisplayObjectMarkers()
    VisualizeRay(ray.origin, ray.direction, (didHit ? environmentHit.Distance : defaultPlacementDistance), markerPosition, didHit);

    // In VisualizeRay()
    // ... LineRenderer setup ...
    Color rayRenderColor = didHit ? Color.green : Color.red;
    lineMaterialInstance.color = rayRenderColor;
    lineRenderer.startColor = rayRenderColor;
    lineRenderer.endColor = rayRenderColor;
    // ...
    Debug.DrawRay(startPoint, direction * distance, rayRenderColor, m_rayVisualizationDuration);
    ```

## 3. UI Logging / Navigations

The script provides feedback to the user through UI elements, primarily for debug information and notifications.

**A. UI Debug Text:**
- An instance of `DetectionUiMenuManager` (assigned as `m_uiMenuManager` in the Inspector) is used to display text on a UI panel.
- **Updates occur in several places:**
    - `Start()`: Initial message.
    - `HandleWebSocketMessage()`: Displays the received action and detected object titles.
      ```csharp
      // In HandleWebSocketMessage()
      string debugText = $"Action: {webSocketCommand.action}";
      debugText += objectTitles.Count > 0 ? $"\nObjects: {string.Join(", ", objectTitles)}" : "\nNo objects detected";
      if (m_uiMenuManager != null) m_uiMenuManager.SetUiDebugText(debugText);
      ```
    - `CleanMarkersCallBack()`: Message after tracking recenters.
    - `OnPauseFromUi()`: Messages indicating paused or active detection state.

**B. Notifications (Pop-up style):**
- The `ShowNotification(string text, float duration)` method displays temporary messages in the user's view.
- **Mechanism in `NotificationCoroutine(string text, float duration)`:**
    - Instantiates `m_websocketMarkerPrefab` at a position relative to the camera.
    - Sets the notification text using `DetectionSpawnMarkerAnim` or `TextMesh` on the instantiated prefab.
    - The notification object is automatically destroyed after `duration`.
    - If `m_websocketMarkerPrefab` is null, the coroutine exits silently without showing a notification.
    ```csharp
    // In NotificationCoroutine()
    if (m_websocketMarkerPrefab == null) yield break;

    var cameraTransform = Camera.main.transform;
    Vector3 notificationPosition = cameraTransform.position + cameraTransform.forward * m_notificationDisplayOffset + cameraTransform.up * 0.1f;
    m_currentNotificationObject = Instantiate(m_websocketMarkerPrefab, notificationPosition, cameraTransform.rotation);
    // ... set text on m_currentNotificationObject ...
    yield return new WaitForSeconds(duration);
    if (m_currentNotificationObject != null) Destroy(m_currentNotificationObject);
    ```
- **Usage:**
    - `HandleWebSocketMessage()`: Notifies about received actions/objects or parsing errors.
    - `CleanMarkersCallBack()`: Notifies that tracking recentered and markers were cleared. 