using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json; // For JSON serialization

namespace PassthroughCameraSamples.MultiObjectDetection
{
    // --- Data Structures (potentially reused or adapted from WebSocketManager) ---

    [System.Serializable]
    public class Vector3Data
    {
        public float x;
        public float y;
        public float z;

        public Vector3Data(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }
    }

    [System.Serializable]
    public class QuaternionData
    {
        public float x;
        public float y;
        public float z;
        public float w;

        public QuaternionData(float x, float y, float z, float w)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.w = w;
        }
    }

    [System.Serializable]
    public class CameraPoseData
    {
        public Vector3Data position;
        public QuaternionData rotation;

        public CameraPoseData(Vector3Data position, QuaternionData rotation)
        {
            this.position = position;
            this.rotation = rotation;
        }
    }

    [System.Serializable]
    public class ImageMetadata
    {
        public string timestamp;
        public int width;
        public int height;
        public CameraPoseData camera_pose;
        // Add any other relevant metadata fields here

        public ImageMetadata(string timestamp, int width, int height, CameraPoseData cameraPose)
        {
            this.timestamp = timestamp;
            this.width = width;
            this.height = height;
            this.camera_pose = cameraPose;
        }
    }

    // --- AR Instruction Response Data Structures ---
    [System.Serializable]
    public class ArInstructionResponse
    {
        public string current_task_status;
        public string message;
        // public object next_step; // Placeholder, define if structure is known
        public List<DetectedObject> objects = new List<DetectedObject>();
        // Add other instruction fields if any
    }

    // Reusing DetectedObject and WebSocketCoordinate from DetectionManager's scope
    // If they are not accessible, they would need to be defined here or in a shared file.
    // Assuming DetectedObject and WebSocketCoordinate are defined as in DetectionManager.cs:
    // [System.Serializable]
    // public class DetectedObject
    // {
    //     public string title { get; set; }
    //     public WebSocketCoordinate coordinates { get; set; } 
    // }

    // [System.Serializable]
    // public class WebSocketCoordinate
    // {
    //     public float x { get; set; }
    //     public float y { get; set; }
    // }
    
    [System.Serializable]
    public class ErrorResponse
    {
        public string error;
    }


    public class HTTPEndpointManager : MonoBehaviour
    {
        [Header("HTTP Endpoint Configuration")]
        [SerializeField] private string serverUrl = "http://localhost:8000/get-ar-instructions"; // Replace with your server URL
        [SerializeField] private float requestTimeout = 10.0f; // Seconds

        public event Action<string> OnInstructionReceived;
        public event Action<string> OnErrorReceived; // For structured error messages

        private bool isRequestInProgress = false;
        private Texture2D _frameTexture; // Reusable texture

        public CameraPoseData CameraPoseToSerializable(Pose pose)
        {
            return new CameraPoseData(
                new Vector3Data(pose.position.x, pose.position.y, pose.position.z),
                new QuaternionData(pose.rotation.x, pose.rotation.y, pose.rotation.z, pose.rotation.w)
            );
        }

        public void SendFrameForInstructions(WebCamTexture texture, Pose cameraPose)
        {
            if (isRequestInProgress)
            {
                Debug.LogWarning("[HTTPEndpointManager] Request already in progress. Skipping new request.");
                return;
            }
            if (texture == null)
            {
                Debug.LogError("[HTTPEndpointManager] WebCamTexture is null. Cannot send frame.");
                return;
            }
            StartCoroutine(PostFrameCoroutine(texture, cameraPose));
        }

        private IEnumerator PostFrameCoroutine(WebCamTexture texture, Pose cameraPose)
        {
            isRequestInProgress = true;

            // 1. Prepare Metadata
            var cameraPoseData = CameraPoseToSerializable(cameraPose);
            var metadata = new ImageMetadata(
                DateTime.UtcNow.ToString("o"),
                texture.width,
                texture.height,
                cameraPoseData
            );
            string metadataJson = JsonConvert.SerializeObject(metadata);

            // 2. Prepare Image Data (JPEG)
            if (_frameTexture == null || _frameTexture.width != texture.width || _frameTexture.height != texture.height)
            {
                if (_frameTexture != null) Destroy(_frameTexture); // Clean up old texture
                _frameTexture = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            }
            _frameTexture.SetPixels32(texture.GetPixels32());
            _frameTexture.Apply();
            byte[] imageBytes = _frameTexture.EncodeToJPG(75); // Quality 75

            if (imageBytes == null)
            {
                Debug.LogError("[HTTPEndpointManager] Failed to encode image to JPG.");
                isRequestInProgress = false;
                yield break;
            }

            // 3. Create Multipart Form Data
            List<IMultipartFormSection> formData = new List<IMultipartFormSection>();
            formData.Add(new MultipartFormFileSection("image", imageBytes, "frame.jpg", "image/jpeg"));
            formData.Add(new MultipartFormDataSection("metadata", metadataJson, "application/json"));
            
            // 4. Create and Send UnityWebRequest
            using (UnityWebRequest www = UnityWebRequest.Post(serverUrl, formData))
            {
                www.timeout = (int)requestTimeout;
                Debug.Log($"[HTTPEndpointManager] Sending frame to {serverUrl} with metadata: {metadataJson}");

                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.ConnectionError ||
                    www.result == UnityWebRequest.Result.ProtocolError ||
                    www.result == UnityWebRequest.Result.DataProcessingError)
                {
                    Debug.LogError($"[HTTPEndpointManager] Error: {www.error}. Status Code: {www.responseCode}. Response: {www.downloadHandler?.text}");
                    OnErrorReceived?.Invoke(www.downloadHandler?.text ?? $"{{"error":"{www.error}"}}");
                }
                else if (www.responseCode == 200)
                {
                    Debug.Log($"[HTTPEndpointManager] Success! Response: {www.downloadHandler.text}");
                    OnInstructionReceived?.Invoke(www.downloadHandler.text);
                }
                else
                {
                    Debug.LogWarning($"[HTTPEndpointManager] Received non-200 status: {www.responseCode}. Response: {www.downloadHandler.text}");
                    // Gracefully handle by logging and invoking error event if available
                    OnErrorReceived?.Invoke(www.downloadHandler.text ?? $"{{"error":"Received status {www.responseCode}"}}");
                }
            }
            isRequestInProgress = false;
        }
        
        void OnDestroy()
        {
            if (_frameTexture != null)
            {
                Destroy(_frameTexture);
            }
        }
    }
} 