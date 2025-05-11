// using System.Collections.Generic;
// using UnityEngine;
// using UnityEngine.XR;
// using Unity.XR.Oculus;                    // ← Utils lives here
// using Meta.XR.EnvironmentDepth;           // the Manager / Setup component

// namespace PassthroughCameraSamples.MultiObjectDetection
// {
//     [RequireComponent(typeof(Renderer))]
//     public class DepthDebugPlane : MonoBehaviour
//     {
//         [SerializeField] EnvironmentDepthManager depthManager; // assign or auto-find

//         Material   _mat;          // instance copy
//         XRDisplaySubsystem _display;
//         uint       _texId;        // texture handle from XR
//         RenderTexture _rt;        // cached RT to avoid allocs

//         void Awake()
//         {
//             if (!depthManager)
//                 depthManager = FindFirstObjectByType<EnvironmentDepthManager>();

//             // The Manager calls Utils.SetupEnvironmentDepth internally on Start().
//             // Make sure depth rendering is ON in case you toggled it off elsewhere.
//             Utils.SetEnvironmentDepthRendering(true);

//             _mat = GetComponent<Renderer>().material;

//             // Grab the active XR display once.
//             var list = new List<XRDisplaySubsystem>();
//             SubsystemManager.GetSubsystems(list);
//             if (list.Count > 0) _display = list[0];
//         }

//         void LateUpdate()
//         {
//             // Ask the runtime for today's depth texture ID.
//             if (!Utils.GetEnvironmentDepthTextureId(ref _texId) || _texId == 0 || _display == null)
//                 return;

//             // Resolve that ID to an actual RenderTexture.
//             _rt = _display.GetRenderTexture(_texId);

//             if (_rt && _mat.mainTexture != _rt)
//                 _mat.mainTexture = _rt;
//         }
//     }
// }
