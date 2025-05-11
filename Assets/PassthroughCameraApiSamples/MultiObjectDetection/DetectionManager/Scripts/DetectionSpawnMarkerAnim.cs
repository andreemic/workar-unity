// Copyright (c) Meta Platforms, Inc. and affiliates.

using Meta.XR.Samples;
using UnityEngine;
using TMPro;

namespace PassthroughCameraSamples.MultiObjectDetection
{
    [MetaCodeSample("PassthroughCameraApiSamples-MultiObjectDetection")]
    public class DetectionSpawnMarkerAnim : MonoBehaviour
    {
        [SerializeField] private Vector3 m_anglesSpeed = new(20.0f, 40.0f, 60.0f);
        [SerializeField] private Transform m_model;
        [SerializeField] private TextMeshPro m_textModel;
        [SerializeField] private Transform m_textEntity;

        private Vector3 m_angles;
        private OVRCameraRig m_camera;

        private void Update()
        {
            m_angles.x = AddAngle(m_angles.x, m_anglesSpeed.x * Time.deltaTime);
            m_angles.y = AddAngle(m_angles.y, m_anglesSpeed.y * Time.deltaTime);
            m_angles.z = AddAngle(m_angles.z, m_anglesSpeed.z * Time.deltaTime);

            m_model.rotation = Quaternion.Euler(m_angles);

            // More robust camera finding logic - try each frame if not set
            if (!m_camera)
            {
                m_camera = FindFirstObjectByType<OVRCameraRig>();
                
                // If still not found, try looking for it in the scene by tag or name
                if (!m_camera)
                {
                    GameObject camRig = GameObject.Find("[BuildingBlock] Camera Rig");
                    if (camRig)
                    {
                        m_camera = camRig.GetComponentInChildren<OVRCameraRig>();
                    }
                }
            }

            // Only proceed if we have a camera reference AND it has a valid centerEyeAnchor
            if (m_camera && m_camera.centerEyeAnchor)
            {
                m_textEntity.transform.LookAt(m_camera.centerEyeAnchor);
                m_textEntity.transform.Rotate(0f, 180f, 0f, Space.Self);
            }
            else
            {
                // Fallback to Camera.main if OVRCameraRig isn't available
                if (Camera.main)
                {
                    m_textEntity.transform.LookAt(Camera.main.transform);
                    m_textEntity.transform.Rotate(0f, 180f, 0f, Space.Self);
                }
            }
        }

        private float AddAngle(float value, float toAdd)
        {
            value += toAdd;
            if (value > 360.0f)
            {
                value -= 360.0f;
            }

            if (value < 0.0f)
            {
                value = 360.0f - value;
            }

            return value;
        }

        public void SetYoloClassName(string name)
        {
            m_textModel.text = name;
        }

        public string GetYoloClassName()
        {
            return m_textModel.text;
        }
    }
}
