using System;  
using System.Collections;  
using System.Collections.Generic;  
using UnityEngine;  
using NativeWebSocket;  
using System.Text;  
using Newtonsoft.Json;  
using System.Threading.Tasks;

namespace PassthroughCameraSamples.MultiObjectDetection  
{  
    /// <summary>
    /// Handles WebSocket communication with server for detection data exchange and image streaming.
    /// Manages connection lifecycle with auto-reconnect capabilities and message event dispatching.
    /// 
    /// Lifecycle:
    /// Start() → ConnectToServer() → [Connected] → SendDetectionData()/SendRawImageData() ↔ OnMessage
    ///                            ↘ [Error/Closed] → ReconnectCoroutine() ↗
    /// </summary>
    public class WebSocketManager : MonoBehaviour  
    {  
        [Header("WebSocket Configuration")]  
        [SerializeField] private string serverUrl = "ws://10.207.2.75:8765";  
          
        [Header("Connection Settings")]  
        [SerializeField] private bool autoReconnect = true;  
        [SerializeField] private float reconnectDelay = 3f;  
          
        private NativeWebSocket.WebSocket websocket;  
        private bool isConnecting = false;  
        private bool shouldReconnect = false;  
          
        // Re-usable buffers to avoid per-frame allocations / GC pressure
        private Texture2D _frameTexture;        // CPU readable texture reused for every capture
        private Color32[] _pixelBuffer;         // Reused color buffer
        private bool _encodeInProgress = false; // Prevent overlapping encodes
          
        public bool IsConnected => websocket != null && websocket.State == WebSocketState.Open;  
          
        public delegate void MessageReceivedHandler(string message);  
        public event MessageReceivedHandler OnMessageReceived;  
          
        private void Start()  
        {  
            ConnectToServer();  
        }  
          
        private async void ConnectToServer()  
        {  
            if (isConnecting) return;  
              
            isConnecting = true;  
            shouldReconnect = autoReconnect;  
              
            Debug.Log($"WebSocketManager: Attempting to connect to {serverUrl}. Current state: {(websocket == null ? "null" : websocket.State.ToString())}");
              
            websocket = new NativeWebSocket.WebSocket(serverUrl);  
              
            websocket.OnOpen += () => {  
                Debug.Log($"WebSocketManager: Connection OPENED successfully. State: {websocket.State}");
                isConnecting = false;  
            };  
              
            websocket.OnError += (e) => {  
                Debug.LogError($"WebSocketManager: ERROR received. Message: {e}. Current state: {websocket.State}");
                isConnecting = false;  
                if (shouldReconnect)
                {
                    StartCoroutine(ReconnectCoroutine());
                }
            };  
              
            websocket.OnClose += (e) => {  
                Debug.Log($"WebSocketManager: Connection CLOSED. Code: {e}. Current state: {websocket.State}");
                isConnecting = false;  
                  
                if (shouldReconnect)  
                {  
                    StartCoroutine(ReconnectCoroutine());  
                }  
            };  
              
            websocket.OnMessage += (bytes) => {  
                var message = Encoding.UTF8.GetString(bytes);  
                Debug.Log($"WebSocketManager: Message received: {message}");  
                OnMessageReceived?.Invoke(message);  
            };  
              
            try  
            {  
                await websocket.Connect();  
            }  
            catch (Exception ex)  
            {  
                Debug.LogError($"WebSocketManager: Connection failed! {ex.Message}");  
                isConnecting = false;  
                  
                if (shouldReconnect)  
                {  
                    StartCoroutine(ReconnectCoroutine());  
                }  
            }  
        }  
          
        private IEnumerator ReconnectCoroutine()  
        {  
            yield return new WaitForSeconds(reconnectDelay);  
            Debug.Log($"WebSocketManager: ReconnectCoroutine started. Attempting to connect again to {serverUrl}.");
            ConnectToServer();  
        }  
          
        private async void OnApplicationQuit()  
        {  
            shouldReconnect = false;  
              
            if (websocket != null && websocket.State == WebSocketState.Open)  
            {  
                await websocket.Close();  
            }  
        }  
          
        private void Update()  
        {  
            if (websocket != null)  
            {  
                websocket.DispatchMessageQueue();  
            }  
        }  
          
        public async void SendDetectionData(List<DetectionData> detections)  
        {  
            if (!IsConnected) return;  
              
            string json = JsonConvert.SerializeObject(new {  
                timestamp = DateTime.UtcNow.ToString("o"),  
                detections = detections  
            });  
              
            try  
            {  
                await websocket.SendText(json);  
            }  
            catch (Exception ex)  
            {  
                Debug.LogError($"WebSocketManager: Failed to send detection data! {ex.Message}");  
            }  
        }  
        
        /// <summary>
        /// Converts a camera pose to a JSON-serializable object
        /// </summary>
        /// <param name="pose">The camera pose to convert</param>
        /// <returns>A CameraPoseData object that can be serialized to JSON</returns>
        public CameraPoseData CameraPoseToJson(Pose pose)
        {
            return new CameraPoseData(
                new Vector3Data(pose.position.x, pose.position.y, pose.position.z),
                new QuaternionData(pose.rotation.x, pose.rotation.y, pose.rotation.z, pose.rotation.w)
            );
        }
        
        /// <summary>
        /// Converts a JSON-serializable CameraPoseData back to a Unity Pose
        /// </summary>
        /// <param name="poseData">The serialized camera pose data</param>
        /// <returns>A Unity Pose object</returns>
        public Pose JsonToCameraPose(CameraPoseData poseData)
        {
            if (poseData == null) return new Pose();
            
            return new Pose(
                new Vector3(poseData.position.x, poseData.position.y, poseData.position.z),
                new Quaternion(poseData.rotation.x, poseData.rotation.y, poseData.rotation.z, poseData.rotation.w)
            );
        }
          
        public void SendRawImageData(WebCamTexture texture, Pose cameraPose)
        {
            // Preconditions
            if (!IsConnected || texture == null || _encodeInProgress) { return; }

            // Lazily (re)create reusable buffers if the incoming frame size changed
            if (_frameTexture == null || _frameTexture.width != texture.width || _frameTexture.height != texture.height)
            {
                _frameTexture = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            }

            int pixelCount = texture.width * texture.height;
            if (_pixelBuffer == null || _pixelBuffer.Length != pixelCount)
            {
                _pixelBuffer = new Color32[pixelCount];
            }

            // Copy pixel data from the webcam texture (must execute on the main thread)
            texture.GetPixels32(_pixelBuffer);
            _frameTexture.SetPixels32(_pixelBuffer);
            _frameTexture.Apply(false, false);

            // Convert camera pose to JSON-serializable format
            var cameraPoseData = CameraPoseToJson(cameraPose);
            
            // Create metadata object with camera pose and timestamp
            var metadata = new ImageMetadata(
                DateTime.UtcNow.ToString("o"),
                texture.width,
                texture.height,
                cameraPoseData
            );
            
            // Serialize the metadata
            string metadataJson = JsonConvert.SerializeObject(metadata);

            _encodeInProgress = true;

            // Off-load the heavy JPG encoding work to a background thread to keep the main thread smooth
            Task.Run(() =>
            {
                // 50 % quality keeps size modest – tune to taste / bandwidth
                return ImageConversion.EncodeToJPG(_frameTexture, 50);
            })
            .ContinueWith(async t =>
            {
                try
                {
                    if (t.Status == TaskStatus.RanToCompletion && t.Result != null)
                    {
                        // First send the metadata as text
                        await websocket.SendText(metadataJson);
                        // Then send the image data as binary
                        await websocket.Send(t.Result);
                    }
                    else if (t.IsFaulted && t.Exception != null)
                    {
                        Debug.LogError($"WebSocketManager: Image encoding task faulted → {t.Exception.Flatten().InnerException?.Message}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"WebSocketManager: Failed to send image data! {ex.Message}");
                }
                finally
                {
                    _encodeInProgress = false;
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }
        
        // Keep the old method for backward compatibility
        public void SendRawImageData(WebCamTexture texture)
        {
            SendRawImageData(texture, new Pose(Vector3.zero, Quaternion.identity));
        }
  
        public async void SendTextData(string textMessage)  
        {  
            if (!IsConnected) return;  
  
            string json = JsonConvert.SerializeObject(new {  
                timestamp = DateTime.UtcNow.ToString("o"),  
                message = textMessage  
            });  
  
            try  
            {  
                await websocket.SendText(json);  
            }  
            catch (Exception ex)  
            {  
                Debug.LogError($"WebSocketManager: Failed to send text data! {ex.Message}");  
            }  
        }  
    }  
      
    [Serializable]  
    public class DetectionData  
    {  
        public string className;  
        public float confidence;  
        public Vector3 worldPosition;  
          
        public DetectionData(string className, float confidence, Vector3 worldPosition)  
        {  
            this.className = className;  
            this.confidence = confidence;  
            this.worldPosition = worldPosition;  
        }  
    }
    
    [Serializable]
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
    
    [Serializable]
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
    
    [Serializable]
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
    
    [Serializable]
    public class ImageMetadata
    {
        public string timestamp;
        public int width;
        public int height;
        public CameraPoseData camera_pose;
        
        public ImageMetadata(string timestamp, int width, int height, CameraPoseData cameraPose)
        {
            this.timestamp = timestamp;
            this.width = width;
            this.height = height;
            this.camera_pose = cameraPose;
        }
    }
}
