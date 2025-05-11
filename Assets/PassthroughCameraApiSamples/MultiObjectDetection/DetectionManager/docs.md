# Project Overview: Multi-Object Detection Sample

This document provides an overview of the C# scripts and their roles within the Multi-Object Detection sample project for Passthrough Camera API.

## Core Components

The system is primarily managed by `DetectionManager` which orchestrates interactions between UI, WebSocket communication, and in-world object visualization.

---

### 1. `DetectionManager.cs`

*   **Class:** `DetectionManager`
*   **Namespace:** `PassthroughCameraSamples.MultiObjectDetection`
*   **Responsibilities:**
    *   Acts as the central coordinator for the detection feature.
    *   Initializes and manages references to `WebSocketManager` for server communication and `DetectionUiMenuManager` for UI interactions.
    *   Receives detection data (object titles, coordinates) from the `WebSocketManager`.
    *   Processes incoming detection messages to:
        *   Update the UI via `DetectionUiMenuManager` with information about detected objects.
        *   Display 3D markers in the scene for each detected object using a `m_websocketMarkerPrefab`.
    *   Uses `Meta.XR.MRUtilityKit.EnvironmentRaycastManager` to perform raycasts into the environment, determining where to place the 3D markers based on detected object coordinates (from a 2D camera image).
    *   Visualizes these raycasts with lines and intersection points.
    *   Manages the lifecycle of these markers and ray visualizations (creation, clearing).
    *   Handles application pause/resume logic, triggered by `DetectionUiMenuManager`.
    *   Sends camera frames to the server via `WebSocketManager` at a defined interval if not paused and the WebSocket is connected.
    *   Shows in-world notifications (e.g., for errors, status changes) using the same marker prefab logic.
*   **Key Interactions:**
    *   Receives messages from `WebSocketManager.OnMessageReceived`.
    *   Sends image data via `WebSocketManager.SendRawImageData()`.
    *   Calls `DetectionUiMenuManager.SetUiDebugText()` to update UI text.
    *   Responds to `DetectionUiMenuManager.OnPause` event.
    *   Instantiates `m_websocketMarkerPrefab` which likely has a `DetectionSpawnMarkerAnim` component.
    *   Uses `Meta.XR.MRUtilityKit.EnvironmentRaycastManager` for scene understanding.
    *   Uses `WebCamTextureManager` (not detailed in provided files) to get camera frames.
*   **Data Structures (Defined within or for `DetectionManager`):**
    *   `WebSocketCommand`: Container for a list of `DetectedObject`s and an `action` string.
    *   `DetectedObject`: Contains `title` (string) and `coordinates` (`WebSocketCoordinate`).
    *   `WebSocketCoordinate`: Contains `x` and `y` (float) for 2D coordinates.

---

### 2. `WebSocketManager.cs`

*   **Class:** `WebSocketManager`
*   **Namespace:** `PassthroughCameraSamples.MultiObjectDetection`
*   **Responsibilities:**
    *   Manages the WebSocket connection to a specified server URL (`ws://10.207.2.75:8765` by default).
    *   Handles connection lifecycle: connecting, error handling, closing, and automatic reconnection logic.
    *   Provides methods to send data to the server:
        *   `SendRawImageData(WebCamTexture texture)`: Encodes a `WebCamTexture` to PNG and sends it as binary data.
        *   `SendTextData(string textMessage)`: Sends a JSON-formatted text message.
        *   `SendDetectionData(List<DetectionData> detections)`: Sends a list of `DetectionData` objects as JSON (currently seems unused by `DetectionManager` which uses `SendRawImageData` and expects detections back from server).
    *   Dispatches received messages (byte arrays converted to strings) via the `OnMessageReceived` event.
*   **Key Interactions:**
    *   Instantiates and uses `NativeWebSocket.WebSocket` for the underlying WebSocket communication.
    *   Invokes `OnMessageReceived` event, which `DetectionManager` subscribes to.
*   **Data Structures (Defined within `WebSocketManager.cs`):**
    *   `DetectionData`: Contains `className`, `confidence`, `worldPosition`. (Note: This seems to be for a different data flow than what `DetectionManager` currently uses for sending/receiving with the Python server).

---

### 3. `DetectionUiMenuManager.cs`

*   **Class:** `DetectionUiMenuManager`
*   **Namespace:** `PassthroughCameraSamples.MultiObjectDetection`
*   **Responsibilities:**
    *   Manages basic UI elements like information labels (`m_labelInfromation`) and panels for loading/permission states.
    *   Checks for camera permissions using `PassthroughCameraPermissions.HasCameraPermission` at startup.
    *   Based on camera permission, it either:
        *   Unpauses the system by invoking the `OnPause` event with `false`.
        *   Shows a permission error and keeps the system paused.
    *   Provides a public method `SetUiDebugText(string newText)` to update the main information label.
*   **Key Interactions:**
    *   Invokes `OnPause` event, which `DetectionManager` subscribes to, to signal changes in the application's pause state.
    *   `DetectionManager` calls `SetUiDebugText()` to display status and detection information.

---

### 4. `DetectionSpawnMarkerAnim.cs`

*   **Class:** `DetectionSpawnMarkerAnim`
*   **Namespace:** `PassthroughCameraSamples.MultiObjectDetection`
*   **Responsibilities:**
    *   Attached to the 3D marker prefabs instantiated by `DetectionManager`.
    *   Provides a simple continuous rotation animation to the marker's model (`m_model`).
    *   Updates a `TextMeshPro` component (`m_textModel`) with a given name (e.g., detected object's class name).
    *   Orients the text entity (`m_textEntity`) to always face the main camera (`OVRCameraRig.centerEyeAnchor`).
*   **Key Interactions:**
    *   Its `SetYoloClassName(string name)` method is likely called by `DetectionManager` after instantiating the marker prefab to set the display text.

---

### 5. `NativeWebSocket/Assets/WebSocket/WebSocket.cs`

*   **Class:** `WebSocket`
*   **Namespace:** `NativeWebSocket`
*   **Responsibilities:**
    *   Provides the actual WebSocket client implementation.
    *   This is a third-party library used by `WebSocketManager.cs`.
    *   It includes platform-specific implementations (WebGL via JSLIB and other platforms via .NET's `ClientWebSocket`).
    *   Manages the low-level details of sending/receiving messages, connection state, and event handling (OnOpen, OnMessage, OnError, OnClose).
*   **Helper Class:** `MainThreadUtil` (in the same file, global namespace)
    *   Ensures that coroutines or actions that need to run on Unity's main thread (like UI updates or Unity API calls) can be dispatched correctly from background threads (which `ClientWebSocket` might use).

---

## Data Flow (Object Detection Example)

1.  **`DetectionUiMenuManager`** checks camera permissions. If granted, it unpauses `DetectionManager`.
2.  **`DetectionManager`**, if unpaused and `WebSocketManager` is connected, captures frames from `WebCamTextureManager`.
3.  **`DetectionManager`** calls `WebSocketManager.SendRawImageData()` to send the camera frame to the Python server.
4.  The Python server (external) processes the image, performs object detection.
5.  The Python server sends a JSON message back (e.g., `{"action": "detected", "objects": [{"title": "cup", "coordinates": {"x": 0.5, "y": 0.5}}]}`).
6.  **`WebSocketManager`** receives this message and fires its `OnMessageReceived` event.
7.  **`DetectionManager`** (`HandleWebSocketMessage` method) receives the string message:
    *   Deserializes the JSON into `WebSocketCommand`.
    *   Updates UI text via `DetectionUiMenuManager.SetUiDebugText()`.
    *   For each `DetectedObject` in the command:
        *   Calculates a `Ray` from the camera through the normalized 2D coordinates.
        *   Uses `EnvironmentRaycastManager.Raycast()` to find a point in the 3D environment.
        *   Instantiates `m_websocketMarkerPrefab` at the hit point (or a default distance along the ray).
        *   Sets the text on the marker (likely via `DetectionSpawnMarkerAnim.SetYoloClassName()`).
        *   Visualizes the raycast.
    *   Shows an in-world notification summarizing the action.

This map should help in understanding the project's architecture and how the different classes collaborate.
