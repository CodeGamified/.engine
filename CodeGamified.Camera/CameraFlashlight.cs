// Copyright CodeGamified 2025-2026
// MIT License
using UnityEngine;

namespace CodeGamified.Camera
{
    /// <summary>
    /// Auto-fading spotlight attached to the camera.
    /// Useful for illuminating close targets in dark environments (e.g. satellites on the night side).
    ///
    /// Set <see cref="Active"/> to true/false — the light fades smoothly.
    /// </summary>
    public class CameraFlashlight : MonoBehaviour
    {
        [Header("Flashlight")]
        [Tooltip("Peak intensity when active")]
        public float intensity = 1.5f;

        [Tooltip("Light range")]
        public float range = 5f;

        [Tooltip("Spotlight cone angle")]
        public float spotAngle = 60f;

        [Tooltip("Fade speed (units per second)")]
        public float fadeSpeed = 3f;

        [Tooltip("Light color")]
        public Color color = new Color(1f, 0.98f, 0.95f);

        private Light _light;
        private float _targetIntensity;
        private float _currentIntensity;
        private bool _active;

        /// <summary>Enable/disable the flashlight (fades smoothly).</summary>
        public bool Active
        {
            get => _active;
            set
            {
                _active = value;
                _targetIntensity = value ? intensity : 0f;
            }
        }

        private void Awake()
        {
            var go = new GameObject("CameraFlashlight");
            go.transform.SetParent(transform);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;

            _light = go.AddComponent<Light>();
            _light.type = LightType.Spot;
            _light.intensity = 0f;
            _light.range = range;
            _light.spotAngle = spotAngle;
            _light.color = color;
            _light.shadows = LightShadows.None;
        }

        private void LateUpdate()
        {
            if (_light == null) return;

            _currentIntensity = Mathf.MoveTowards(
                _currentIntensity, _targetIntensity, fadeSpeed * Time.deltaTime);

            _light.intensity = _currentIntensity;
            _light.range = range;
            _light.spotAngle = spotAngle;
            _light.color = color;
        }
    }
}
