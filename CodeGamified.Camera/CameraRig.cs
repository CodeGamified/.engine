// Copyright CodeGamified 2025-2026
// MIT License
using UnityEngine;

namespace CodeGamified.Camera
{
    /// <summary>
    /// Generalized camera controller with three modes:
    ///
    ///   FREE  — WASD pan, scroll zoom, right-drag orbit, Q/E rotate, middle-mouse pan
    ///   ORBIT — orbit around a target transform, dynamic zoom limits, auto-exit on zoom-out
    ///   DECK  — on-target first-person view, synced to target pitch/roll/yaw, mouse-look
    ///
    /// Replaces OceanCameraController (SeaRäuber), SimpleCameraController (BitNaughts),
    /// and PongCameraSway (Pong — use CameraAmbientMotion instead for sway-only).
    ///
    /// Optional companion components:
    ///   <see cref="CameraAmbientMotion"/> — additive sine sway
    ///   <see cref="CameraFlashlight"/>    — auto-fading spotlight
    ///
    /// Games configure via inspector fields and call the public API:
    ///   SetTarget()    — enter Orbit mode around a transform
    ///   EnterDeckMode() — enter Deck mode on a transform
    ///   ClearTarget()  — return to Free mode
    ///   TrackObject()  — auto-rotate to face a moving object while orbiting
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        // ═══════════════════════════════════════════════════════════════
        // INSPECTOR
        // ═══════════════════════════════════════════════════════════════

        [Header("Free Mode — Pan")]
        [Tooltip("Enable WASD / arrow key panning")]
        public bool enableWASDPan = true;

        [Tooltip("Enable middle-mouse-button panning")]
        public bool enableMiddleMousePan = true;

        [Tooltip("Pan speed (world units per second)")]
        public float panSpeed = 30f;

        [Tooltip("Sprint multiplier when holding Shift")]
        public float panSprintMultiplier = 2.5f;

        [Header("Orbit — Mouse / Keyboard")]
        [Tooltip("Enable Q/E keyboard rotation")]
        public bool enableKeyboardRotate = true;

        [Tooltip("Mouse orbit sensitivity (degrees per pixel)")]
        public float orbitSpeed = 0.3f;

        [Tooltip("Keyboard rotation speed (degrees per second)")]
        public float keyRotateSpeed = 60f;

        [Header("Zoom")]
        [Tooltip("Scroll zoom sensitivity")]
        public float zoomSpeed = 10f;

        [Tooltip("Minimum orbit distance (overridden by target radius in Orbit mode)")]
        public float minZoomDistance = 5f;

        [Tooltip("Maximum orbit distance (overridden by target radius in Orbit mode)")]
        public float maxZoomDistance = 200f;

        [Header("Pitch Limits")]
        [Tooltip("Minimum pitch angle (degrees). Positive = above horizon.")]
        [Range(-89f, 89f)]
        public float minPitch = 10f;

        [Tooltip("Maximum pitch angle (degrees)")]
        [Range(-89f, 89f)]
        public float maxPitch = 80f;

        [Header("Orbit Target")]
        [Tooltip("Default orbit distance when entering Orbit mode")]
        public float defaultOrbitDistance = 25f;

        [Tooltip("Default pitch angle when entering Orbit mode")]
        public float defaultOrbitPitch = 30f;

        [Tooltip("Auto-zoom distance = target radius × this multiplier")]
        public float autoZoomRadiusMultiplier = 3f;

        [Tooltip("Minimum zoom = target radius × this multiplier (prevents clipping into target)")]
        public float minZoomClearanceMultiplier = 1.1f;

        [Tooltip("Zoom out past this distance to auto-exit Orbit → Free. Set 0 to disable.")]
        public float orbitExitDistance = 120f;

        [Header("Deck Mode")]
        [Tooltip("Camera offset from target center in local space")]
        public Vector3 deckOffset = new Vector3(0f, 6f, -4f);

        [Tooltip("How much target pitch/roll affects camera (0 = yaw only, 1 = full sync)")]
        [Range(0f, 1f)]
        public float deckMotionSync = 0.6f;

        [Tooltip("Mouse look sensitivity in Deck mode")]
        public float deckLookSpeed = 2f;

        [Tooltip("Pitch limits in Deck mode (degrees)")]
        public float deckMinPitch = -30f;

        [Tooltip("Pitch limits in Deck mode (degrees)")]
        public float deckMaxPitch = 60f;

        [Tooltip("Scroll up past this height to auto-exit Deck → Orbit")]
        public float deckExitHeight = 20f;

        [Header("Smoothing")]
        [Tooltip("Transition speed when switching modes (higher = faster)")]
        public float transitionSpeed = 4f;

        [Tooltip("Position smoothing in Free mode (0 = instant, 0.99 = very smooth)")]
        [Range(0f, 0.99f)]
        public float freeSmoothness = 0.85f;

        [Tooltip("Position smoothing in Orbit mode")]
        [Range(0f, 0.99f)]
        public float orbitSmoothness = 0.92f;

        [Tooltip("Zoom lerp speed")]
        public float zoomLerpSpeed = 8f;

        [Tooltip("Object tracking lerp speed (smooth tracking only)")]
        public float trackingSpeed = 5f;

        [Header("Behavior")]
        [Tooltip("Escape key returns to Free mode")]
        public bool enableEscapeToFree = true;

        [Tooltip("Clamp free-mode look target Y to 0 (useful for games with a ground plane)")]
        public bool clampLookTargetY = false;

        [Tooltip("Snap position/zoom (no lerp) when time scale exceeds this threshold. Set 0 to disable.")]
        public float snapTimeScaleThreshold = 1000f;

        // ═══════════════════════════════════════════════════════════════
        // STATE
        // ═══════════════════════════════════════════════════════════════

        private CameraMode _mode = CameraMode.Free;

        // Orbit state
        private Vector3 _lookTarget;
        private float _currentPitch;
        private float _currentYaw;
        private float _currentDistance;
        private float _targetDistance;

        // Orbit target
        private Transform _orbitTarget;
        private float _orbitTargetRadius = 1f;

        // Dynamic zoom limits (updated when target changes)
        private float _dynamicMinZoom;
        private float _dynamicMaxZoom;

        // Deck state
        private float _deckPitch;
        private float _deckYaw;

        // Object tracking
        private Transform _trackedObject;
        private bool _smoothTracking;

        // Transition
        private bool _isTransitioning;
        private Vector3 _transitionStartPos;
        private Quaternion _transitionStartRot;
        private float _transitionProgress;

        // Time scale provider (optional — set via SetTimeScaleProvider)
        private System.Func<float> _getTimeScale;

        // ═══════════════════════════════════════════════════════════════
        // EVENTS
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Fired when camera mode changes. Args: (oldMode, newMode).</summary>
        public event System.Action<CameraMode, CameraMode> OnModeChanged;

        /// <summary>Fired when orbit target changes. Null when clearing target.</summary>
        public event System.Action<Transform> OnTargetChanged;

        // ═══════════════════════════════════════════════════════════════
        // PROPERTIES
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Current camera mode.</summary>
        public CameraMode Mode => _mode;

        /// <summary>Current orbit distance from target.</summary>
        public float CurrentDistance => _currentDistance;

        /// <summary>Current orbit target transform (null in Free mode).</summary>
        public Transform OrbitTarget => _orbitTarget;

        /// <summary>True while transitioning between modes/targets.</summary>
        public bool IsTransitioning => _isTransitioning;

        /// <summary>True if tracking an object (auto-rotating to face it).</summary>
        public bool IsTracking => _trackedObject != null;

        /// <summary>The currently tracked object (null if not tracking).</summary>
        public Transform TrackedObject => _trackedObject;

        // ═══════════════════════════════════════════════════════════════
        // LIFECYCLE
        // ═══════════════════════════════════════════════════════════════

        private void Start()
        {
            // Compute initial orbit parameters from current camera transform
            _lookTarget = transform.position + transform.forward * 20f;
            if (clampLookTargetY) _lookTarget.y = 0f;

            Vector3 offset = transform.position - _lookTarget;
            _currentDistance = offset.magnitude;
            _targetDistance = _currentDistance;
            _currentPitch = Mathf.Asin(offset.y / Mathf.Max(_currentDistance, 0.01f)) * Mathf.Rad2Deg;
            _currentYaw = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
            _currentPitch = Mathf.Clamp(_currentPitch, minPitch, maxPitch);

            _dynamicMinZoom = minZoomDistance;
            _dynamicMaxZoom = maxZoomDistance;
        }

        private void LateUpdate()
        {
            // Escape → Free
            if (enableEscapeToFree && _mode != CameraMode.Free
                && Input.GetKeyDown(KeyCode.Escape))
            {
                ClearTarget();
                return;
            }

            // Smooth transitions between modes
            if (_isTransitioning)
            {
                UpdateTransition();
                return;
            }

            // Object tracking (Orbit mode — auto-rotates yaw/pitch toward tracked object)
            if (_mode == CameraMode.Orbit)
                UpdateObjectTracking();

            switch (_mode)
            {
                case CameraMode.Free:  UpdateFreeMode();  break;
                case CameraMode.Orbit: UpdateOrbitMode(); break;
                case CameraMode.Deck:  UpdateDeckMode();  break;
            }

            // Smooth zoom lerp (_currentDistance → _targetDistance)
            UpdateZoomLerp();
        }

        // ═══════════════════════════════════════════════════════════════
        // FREE MODE
        // ═══════════════════════════════════════════════════════════════

        private void UpdateFreeMode()
        {
            if (enableWASDPan)        HandleKeyboardPan();
            if (enableMiddleMousePan) HandleMousePan();
            HandleMouseOrbit();
            if (enableKeyboardRotate) HandleKeyboardRotate();
            HandleScrollZoom();
            ApplyOrbitTransform(_lookTarget, freeSmoothness);
        }

        private void HandleKeyboardPan()
        {
            float h = 0f, v = 0f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))    v += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))  v -= 1f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))  h -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) h += 1f;

            if (Mathf.Abs(h) < 0.01f && Mathf.Abs(v) < 0.01f) return;

            float speed = panSpeed * (Input.GetKey(KeyCode.LeftShift) ? panSprintMultiplier : 1f);

            // Pan vectors aligned to camera yaw
            float yawRad = _currentYaw * Mathf.Deg2Rad;
            Vector3 forward = new Vector3(Mathf.Sin(yawRad), 0f, Mathf.Cos(yawRad));
            Vector3 right = new Vector3(forward.z, 0f, -forward.x);

            _lookTarget += (forward * v + right * h).normalized * speed * Time.deltaTime;
            if (clampLookTargetY) _lookTarget.y = 0f;
        }

        private void HandleMousePan()
        {
            if (!Input.GetMouseButton(2)) return;

            float h = -Input.GetAxis("Mouse X") * panSpeed * 0.1f;
            float v = -Input.GetAxis("Mouse Y") * panSpeed * 0.1f;

            float yawRad = _currentYaw * Mathf.Deg2Rad;
            Vector3 forward = new Vector3(Mathf.Sin(yawRad), 0f, Mathf.Cos(yawRad));
            Vector3 right = new Vector3(forward.z, 0f, -forward.x);

            _lookTarget += forward * v + right * h;
            if (clampLookTargetY) _lookTarget.y = 0f;
        }

        // ═══════════════════════════════════════════════════════════════
        // ORBIT MODE
        // ═══════════════════════════════════════════════════════════════

        private void UpdateOrbitMode()
        {
            if (_orbitTarget == null) { ClearTarget(); return; }

            // Follow target position
            Vector3 targetPos = _orbitTarget.position;
            if (ShouldSnap())
                _lookTarget = targetPos;
            else
                _lookTarget = Vector3.Lerp(_lookTarget, targetPos,
                    Time.deltaTime * transitionSpeed * 2f);

            // Orbit + zoom controls (always available — tracking pauses while mouse held)
            HandleMouseOrbit();
            if (enableKeyboardRotate) HandleKeyboardRotate();
            HandleScrollZoom();

            // Auto-exit on zoom past threshold
            if (orbitExitDistance > 0f && _targetDistance >= orbitExitDistance)
            {
                ClearTarget();
                return;
            }

            ApplyOrbitTransform(_lookTarget, orbitSmoothness);
        }

        // ═══════════════════════════════════════════════════════════════
        // DECK MODE
        // ═══════════════════════════════════════════════════════════════

        private void UpdateDeckMode()
        {
            if (_orbitTarget == null) { ClearTarget(); return; }

            // Mouse look (right-click held)
            if (Input.GetMouseButton(1))
            {
                _deckYaw += Input.GetAxis("Mouse X") * deckLookSpeed;
                _deckPitch -= Input.GetAxis("Mouse Y") * deckLookSpeed;
                _deckPitch = Mathf.Clamp(_deckPitch, deckMinPitch, deckMaxPitch);
            }

            // Scroll adjusts deck height — way up exits to Orbit
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.001f)
            {
                deckOffset.y = Mathf.Clamp(deckOffset.y - scroll * 2f, 2f, deckExitHeight + 1f);
                if (deckOffset.y >= deckExitHeight)
                {
                    SetTarget(_orbitTarget, _orbitTargetRadius);
                    return;
                }
            }

            // Base rotation from target (blend between yaw-only and full rotation)
            Quaternion targetRot = _orbitTarget.rotation;
            Quaternion baseRot = Quaternion.Slerp(
                Quaternion.Euler(0f, targetRot.eulerAngles.y, 0f),
                targetRot,
                deckMotionSync);

            // Player look on top of target rotation
            Quaternion lookRot = baseRot * Quaternion.Euler(_deckPitch, _deckYaw, 0f);

            // Position: target center + rotated local offset
            Vector3 worldOffset = baseRot * deckOffset;
            Vector3 targetPos = _orbitTarget.position + worldOffset;

            if (ShouldSnap())
            {
                transform.position = targetPos;
                transform.rotation = lookRot;
            }
            else
            {
                float lerpSpeed = Time.deltaTime * transitionSpeed * 3f;
                transform.position = Vector3.Lerp(transform.position, targetPos, lerpSpeed);
                transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, lerpSpeed);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // SHARED INPUT HANDLERS
        // ═══════════════════════════════════════════════════════════════

        private void HandleMouseOrbit()
        {
            if (!Input.GetMouseButton(1)) return;

            _currentYaw += Input.GetAxis("Mouse X") * orbitSpeed;
            _currentPitch = Mathf.Clamp(
                _currentPitch - Input.GetAxis("Mouse Y") * orbitSpeed,
                minPitch, maxPitch);
        }

        private void HandleKeyboardRotate()
        {
            if (Input.GetKey(KeyCode.Q))
                _currentYaw -= keyRotateSpeed * Time.deltaTime;
            if (Input.GetKey(KeyCode.E))
                _currentYaw += keyRotateSpeed * Time.deltaTime;
        }

        private void HandleScrollZoom()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) < 0.001f) return;

            _targetDistance -= scroll * zoomSpeed * (_targetDistance * 0.1f);
            _targetDistance = Mathf.Clamp(_targetDistance, _dynamicMinZoom, _dynamicMaxZoom);
        }

        // ═══════════════════════════════════════════════════════════════
        // OBJECT TRACKING — auto-rotate to face a moving object
        // ═══════════════════════════════════════════════════════════════

        private void UpdateObjectTracking()
        {
            if (_trackedObject == null) return;
            if (_orbitTarget == null) { StopTracking(); return; }

            // Skip tracking while user is manually orbiting
            if (Input.GetMouseButton(1)) return;

            // Direction from orbit center to tracked object
            Vector3 dir = (_trackedObject.position - _orbitTarget.position).normalized;
            float targetYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            float targetPitch = Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg;
            targetPitch = Mathf.Clamp(targetPitch, minPitch, maxPitch);

            if (_smoothTracking)
            {
                _currentYaw = Mathf.LerpAngle(_currentYaw, targetYaw,
                    Time.deltaTime * trackingSpeed);
                _currentPitch = Mathf.Lerp(_currentPitch, targetPitch,
                    Time.deltaTime * trackingSpeed);
            }
            else
            {
                _currentYaw = targetYaw;
                _currentPitch = targetPitch;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // SHARED ORBIT MATH
        // ═══════════════════════════════════════════════════════════════

        private Vector3 ComputeOrbitPosition(Vector3 target)
        {
            float pitchRad = _currentPitch * Mathf.Deg2Rad;
            float yawRad = _currentYaw * Mathf.Deg2Rad;

            Vector3 offset = new Vector3(
                Mathf.Sin(yawRad) * Mathf.Cos(pitchRad),
                Mathf.Sin(pitchRad),
                Mathf.Cos(yawRad) * Mathf.Cos(pitchRad)
            ) * _currentDistance;

            return target + offset;
        }

        private void ApplyOrbitTransform(Vector3 target, float smoothness)
        {
            Vector3 targetPos = ComputeOrbitPosition(target);

            if (ShouldSnap())
                transform.position = targetPos;
            else
                transform.position = Vector3.Lerp(transform.position, targetPos, 1f - smoothness);

            transform.LookAt(target);
        }

        // ═══════════════════════════════════════════════════════════════
        // ZOOM LERP + TIME SCALE
        // ═══════════════════════════════════════════════════════════════

        private void UpdateZoomLerp()
        {
            if (ShouldSnap())
                _currentDistance = _targetDistance;
            else
                _currentDistance = Mathf.Lerp(_currentDistance, _targetDistance,
                    Time.deltaTime * zoomLerpSpeed);
        }

        private bool ShouldSnap()
        {
            return snapTimeScaleThreshold > 0f
                && _getTimeScale != null
                && _getTimeScale() > snapTimeScaleThreshold;
        }

        // ═══════════════════════════════════════════════════════════════
        // SMOOTH TRANSITIONS (cubic ease-out)
        // ═══════════════════════════════════════════════════════════════

        private void BeginTransition()
        {
            _isTransitioning = true;
            _transitionStartPos = transform.position;
            _transitionStartRot = transform.rotation;
            _transitionProgress = 0f;
        }

        private void UpdateTransition()
        {
            _transitionProgress += Time.deltaTime * transitionSpeed;

            if (_transitionProgress >= 1f)
            {
                _isTransitioning = false;
                return;
            }

            // Cubic ease-out
            float t = 1f - Mathf.Pow(1f - _transitionProgress, 3f);

            Vector3 targetPos;
            Quaternion targetRot;

            if (_mode == CameraMode.Orbit && _orbitTarget != null)
            {
                _lookTarget = _orbitTarget.position;
                targetPos = ComputeOrbitPosition(_lookTarget);
                Vector3 lookDir = _lookTarget - targetPos;
                targetRot = lookDir.sqrMagnitude > 0.0001f
                    ? Quaternion.LookRotation(lookDir)
                    : transform.rotation;
            }
            else if (_mode == CameraMode.Deck && _orbitTarget != null)
            {
                Quaternion shipRot = _orbitTarget.rotation;
                Quaternion baseRot = Quaternion.Slerp(
                    Quaternion.Euler(0f, shipRot.eulerAngles.y, 0f),
                    shipRot, deckMotionSync);
                targetPos = _orbitTarget.position + baseRot * deckOffset;
                targetRot = baseRot * Quaternion.Euler(_deckPitch, _deckYaw, 0f);
            }
            else
            {
                _isTransitioning = false;
                return;
            }

            transform.position = Vector3.Lerp(_transitionStartPos, targetPos, t);
            transform.rotation = Quaternion.Slerp(_transitionStartRot, targetRot, t);
        }

        // ═══════════════════════════════════════════════════════════════
        // PUBLIC API
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Enter Orbit mode around a target transform.
        /// </summary>
        /// <param name="target">Transform to orbit. Null calls ClearTarget().</param>
        /// <param name="radius">Target radius — used for auto-zoom and dynamic zoom limits.</param>
        /// <param name="overrideDistance">Override orbit distance. 0 = auto (radius × autoZoomRadiusMultiplier).</param>
        public void SetTarget(Transform target, float radius = 1f, float overrideDistance = 0f)
        {
            if (target == null) { ClearTarget(); return; }

            CameraMode oldMode = _mode;
            _mode = CameraMode.Orbit;
            _orbitTarget = target;
            _orbitTargetRadius = radius;
            _lookTarget = target.position;

            _targetDistance = overrideDistance > 0f
                ? overrideDistance
                : radius * autoZoomRadiusMultiplier;
            _currentDistance = _targetDistance;
            _currentPitch = defaultOrbitPitch;

            _dynamicMinZoom = radius * minZoomClearanceMultiplier;
            _dynamicMaxZoom = Mathf.Max(maxZoomDistance, radius * 20f);

            _trackedObject = null;
            _smoothTracking = false;

            BeginTransition();
            OnTargetChanged?.Invoke(target);
            if (oldMode != _mode) OnModeChanged?.Invoke(oldMode, _mode);
        }

        /// <summary>
        /// Enter Deck mode on a target transform (first-person, motion-synced).
        /// </summary>
        public void EnterDeckMode(Transform target)
        {
            if (target == null) return;

            CameraMode oldMode = _mode;
            _orbitTarget = target;
            _mode = CameraMode.Deck;
            _deckPitch = 5f;
            _deckYaw = 0f;

            _trackedObject = null;
            _smoothTracking = false;

            BeginTransition();
            OnTargetChanged?.Invoke(target);
            if (oldMode != _mode) OnModeChanged?.Invoke(oldMode, _mode);
        }

        /// <summary>
        /// Return to Free mode. Detaches from any target and tracking.
        /// </summary>
        public void ClearTarget()
        {
            if (_mode == CameraMode.Free && _orbitTarget == null) return;

            CameraMode oldMode = _mode;
            _mode = CameraMode.Free;
            _orbitTarget = null;
            _trackedObject = null;
            _smoothTracking = false;

            // Derive look target from current camera forward
            _lookTarget = transform.position + transform.forward * _currentDistance;
            if (clampLookTargetY) _lookTarget.y = 0f;

            _dynamicMinZoom = minZoomDistance;
            _dynamicMaxZoom = maxZoomDistance;

            OnTargetChanged?.Invoke(null);
            if (oldMode != _mode) OnModeChanged?.Invoke(oldMode, _mode);
        }

        /// <summary>
        /// Track a moving object while in Orbit mode.
        /// Camera auto-rotates yaw/pitch to keep the tracked object in view.
        /// Tracking pauses while the user holds right-mouse-button.
        /// </summary>
        /// <param name="objectToTrack">Transform to track.</param>
        /// <param name="smoothTracking">If true, yaw/pitch lerp (slow movers). If false, snap (fast movers).</param>
        public void TrackObject(Transform objectToTrack, bool smoothTracking = false)
        {
            _trackedObject = objectToTrack;
            _smoothTracking = smoothTracking;
        }

        /// <summary>Stop tracking any object.</summary>
        public void StopTracking()
        {
            _trackedObject = null;
            _smoothTracking = false;
        }

        /// <summary>Snap the look target to a world position (Free mode).</summary>
        public void LookAt(Vector3 worldPos)
        {
            _lookTarget = worldPos;
            if (clampLookTargetY) _lookTarget.y = 0f;
        }

        /// <summary>Set orbit distance directly.</summary>
        public void SetDistance(float distance)
        {
            _targetDistance = Mathf.Clamp(distance, _dynamicMinZoom, _dynamicMaxZoom);
        }

        /// <summary>Set orbit angles directly.</summary>
        public void SetOrbitAngles(float pitch, float yaw)
        {
            _currentPitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            _currentYaw = yaw;
        }

        /// <summary>
        /// Provide a time-scale accessor for snap-mode (skip lerping at very high time scales).
        /// Example: <c>rig.SetTimeScaleProvider(() => SimulationTime.Instance?.timeScale ?? 1f);</c>
        /// </summary>
        public void SetTimeScaleProvider(System.Func<float> provider)
        {
            _getTimeScale = provider;
        }
    }
}
