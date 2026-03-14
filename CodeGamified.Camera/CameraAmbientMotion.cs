// Copyright CodeGamified 2025-2026
// MIT License
using UnityEngine;

namespace CodeGamified.Camera
{
    /// <summary>
    /// Additive ambient camera motion — sine-based positional sway.
    /// Attach to any camera for subtle "breathing" motion.
    /// Works standalone or alongside <see cref="CameraRig"/>.
    ///
    /// Replaces game-specific sway components (e.g. PongCameraSway).
    /// </summary>
    public class CameraAmbientMotion : MonoBehaviour
    {
        [Header("Sway")]
        [Tooltip("Horizontal sway amplitude (world units)")]
        public float amplitudeX = 0.5f;

        [Tooltip("Vertical sway amplitude (world units)")]
        public float amplitudeY = 0.2f;

        [Tooltip("Sway cycle speed (Hz)")]
        public float speed = 0.3f;

        [Header("Look At")]
        [Tooltip("If true, camera always faces lookAtTarget")]
        public bool overrideLookAt = true;

        [Tooltip("World point the camera faces (when overrideLookAt is true)")]
        public Vector3 lookAtTarget = Vector3.zero;

        private Vector3 _basePosition;
        private bool _initialized;

        private void LateUpdate()
        {
            if (!_initialized)
            {
                _basePosition = transform.position;
                _initialized = true;
            }

            float t = Time.unscaledTime * speed;
            float offsetX = Mathf.Sin(t) * amplitudeX;
            float offsetY = Mathf.Sin(t * 0.7f) * amplitudeY;

            transform.position = _basePosition + new Vector3(offsetX, offsetY, 0f);

            if (overrideLookAt)
                transform.LookAt(lookAtTarget, Vector3.up);
        }

        /// <summary>Set the base position that sway oscillates around.</summary>
        public void SetBasePosition(Vector3 position)
        {
            _basePosition = position;
            _initialized = true;
        }
    }
}
