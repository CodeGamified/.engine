// CodeGamified.Time — Shared simulation time framework
// MIT License
using System;
using UnityEngine;

namespace CodeGamified.Time
{
    /// <summary>
    /// Abstract base for time warp to a future event.
    /// Smoothly accelerates and decelerates time scale to arrive at a target simulation time.
    /// 
    /// State machine:
    ///   IDLE → ACCELERATING → CRUISING → DECELERATING → ARRIVED → IDLE
    /// 
    /// Games subclass to define:
    ///   - What to warp to (launches, deadlines, battles)
    ///   - Camera behavior during warp
    ///   - Arrival behavior
    /// </summary>
    public abstract class TimeWarpController : MonoBehaviour
    {
        // ═══════════════════════════════════════════════════════════════
        // CONFIGURATION
        // ═══════════════════════════════════════════════════════════════

        [Header("Warp Settings")]
        [Tooltip("Maximum time scale during cruise phase")]
        public float maxWarpSpeed = 10000000f;

        [Tooltip("How long (real seconds) to accelerate to max warp speed")]
        public float accelerationDuration = 3f;

        [Tooltip("How long (real seconds) to decelerate to arrival speed")]
        public float decelerationDuration = 3f;

        [Tooltip("Time scale when arriving at event")]
        public float arrivalTimeScale = 1f;

        [Tooltip("Minimum warp speed (prevents stalling at low speeds)")]
        public float minWarpSpeed = 100f;

        [Tooltip("Hold at arrival speed for this many seconds before returning to idle")]
        public float arrivalHoldDuration = 2f;

        // ═══════════════════════════════════════════════════════════════
        // STATE
        // ═══════════════════════════════════════════════════════════════

        public enum WarpState { Idle, Accelerating, Cruising, Decelerating, Arrived }

        public WarpState CurrentState { get; private set; } = WarpState.Idle;

        /// <summary>Target simulation time to warp to.</summary>
        public double TargetTime { get; protected set; }

        /// <summary>Is a warp in progress?</summary>
        public bool IsWarping => CurrentState != WarpState.Idle;

        private float _preWarpTimeScale;
        private float _currentWarpSpeed;
        private float _accelerationStartTime;
        private float _decelerationStartTime;
        private float _decelerationStartSpeed;
        private float _arrivedAtTime;

        // ═══════════════════════════════════════════════════════════════
        // EVENTS
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Fired when warp state changes.</summary>
        public event Action<WarpState> OnWarpStateChanged;

        /// <summary>Fired when arriving at target time.</summary>
        public event Action OnWarpArrived;

        /// <summary>Fired when warp is cancelled.</summary>
        public event Action OnWarpCancelled;

        /// <summary>Fired when warp completes (hold duration elapsed, returning to idle).</summary>
        public event Action OnWarpComplete;

        // ═══════════════════════════════════════════════════════════════
        // VIRTUAL HOOKS
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Called when warp is about to start. Set up camera, UI, etc.</summary>
        protected virtual void OnWarpStarting(double targetTime) { }

        /// <summary>Called each frame during warp. Update camera tracking, UI, etc.</summary>
        protected virtual void OnWarpUpdating(double timeRemaining) { }

        /// <summary>Called when arriving at target time. Spawn entities, trigger events, etc.</summary>
        protected virtual void OnWarpArriving() { }

        /// <summary>Called when warp fully completes (after hold). Clean up tracking state.</summary>
        protected virtual void OnWarpCompleting() { }

        // ═══════════════════════════════════════════════════════════════
        // PUBLIC API
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Start warping to a target simulation time.
        /// Returns false if target is in the past or SimulationTime is missing.
        /// </summary>
        public bool WarpToTime(double targetSimTime)
        {
            if (SimulationTime.Instance == null) return false;

            double currentTime = SimulationTime.Instance.simulationTime;
            if (targetSimTime <= currentTime) return false;

            TargetTime = targetSimTime;
            _preWarpTimeScale = SimulationTime.Instance.timeScale;
            _currentWarpSpeed = SimulationTime.Instance.timeScale;

            OnWarpStarting(targetSimTime);
            SetState(WarpState.Accelerating);

            return true;
        }

        /// <summary>Cancel warp and restore previous time scale.</summary>
        public void CancelWarp()
        {
            if (CurrentState == WarpState.Idle) return;

            if (SimulationTime.Instance != null)
                SimulationTime.Instance.timeScale = _preWarpTimeScale;

            TargetTime = 0;
            SetState(WarpState.Idle);
            OnWarpCancelled?.Invoke();
        }

        /// <summary>Time remaining to target in simulation seconds.</summary>
        public double GetTimeRemaining()
        {
            if (!IsWarping || SimulationTime.Instance == null) return 0;
            return Math.Max(0, TargetTime - SimulationTime.Instance.simulationTime);
        }

        /// <summary>Time remaining as human-readable string.</summary>
        public string GetTimeRemainingFormatted()
        {
            double remaining = GetTimeRemaining();
            if (remaining <= 0) return "Arriving...";

            TimeSpan ts = TimeSpan.FromSeconds(remaining);
            if (ts.TotalDays >= 365) return $"{ts.TotalDays / 365:F1} years";
            if (ts.TotalDays >= 30)  return $"{ts.TotalDays / 30:F1} months";
            if (ts.TotalDays >= 1)   return $"{ts.TotalDays:F1} days";
            if (ts.TotalHours >= 1)  return $"{ts.TotalHours:F1} hours";
            if (ts.TotalMinutes >= 1) return $"{ts.TotalMinutes:F1} min";
            return $"{ts.TotalSeconds:F0} sec";
        }

        // ═══════════════════════════════════════════════════════════════
        // LIFECYCLE
        // ═══════════════════════════════════════════════════════════════

        protected virtual void Update()
        {
            if (CurrentState == WarpState.Idle) return;

            if (SimulationTime.Instance == null) return;

            double timeToTarget = TargetTime - SimulationTime.Instance.simulationTime;
            OnWarpUpdating(timeToTarget);

            switch (CurrentState)
            {
                case WarpState.Accelerating: UpdateAccelerating(timeToTarget); break;
                case WarpState.Cruising:     UpdateCruising(timeToTarget);     break;
                case WarpState.Decelerating: UpdateDecelerating(timeToTarget); break;
                case WarpState.Arrived:      UpdateArrived();                  break;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // STATE MACHINE
        // ═══════════════════════════════════════════════════════════════

        private void UpdateAccelerating(double timeToTarget)
        {
            float elapsed = UnityEngine.Time.time - _accelerationStartTime;
            float t = Mathf.Clamp01(elapsed / accelerationDuration);

            // Cubic ease-in: slow start, fast finish
            float easeIn = t * t * t;

            _currentWarpSpeed = Mathf.Lerp(_preWarpTimeScale, maxWarpSpeed, easeIn);
            _currentWarpSpeed = Mathf.Clamp(_currentWarpSpeed, minWarpSpeed, maxWarpSpeed);
            SimulationTime.Instance.timeScale = _currentWarpSpeed;

            // Sim-time needed for deceleration (cubic ease-out averages ~0.25 of start speed)
            double simTimeForDecel = _currentWarpSpeed * decelerationDuration * 0.25;

            if (timeToTarget <= simTimeForDecel)
                SetState(WarpState.Decelerating);
            else if (t >= 1f)
                SetState(WarpState.Cruising);
        }

        private void UpdateCruising(double timeToTarget)
        {
            SimulationTime.Instance.timeScale = maxWarpSpeed;

            double simTimeForDecel = maxWarpSpeed * decelerationDuration * 0.25;
            if (timeToTarget <= simTimeForDecel)
                SetState(WarpState.Decelerating);
        }

        private void UpdateDecelerating(double timeToTarget)
        {
            float realElapsed = UnityEngine.Time.time - _decelerationStartTime;
            float progress = Mathf.Clamp01(realElapsed / decelerationDuration);

            // Cubic ease-out: fast start, slow finish
            float easeOut = 1f - Mathf.Pow(1f - progress, 3f);

            _currentWarpSpeed = Mathf.Lerp(_decelerationStartSpeed, arrivalTimeScale, easeOut);
            _currentWarpSpeed = Mathf.Max(_currentWarpSpeed, arrivalTimeScale);
            SimulationTime.Instance.timeScale = _currentWarpSpeed;

            if (timeToTarget <= 0 || progress >= 1f)
            {
                SimulationTime.Instance.timeScale = arrivalTimeScale;
                _arrivedAtTime = UnityEngine.Time.time;
                SetState(WarpState.Arrived);
                OnWarpArriving();
                OnWarpArrived?.Invoke();
            }
        }

        private void UpdateArrived()
        {
            SimulationTime.Instance.timeScale = arrivalTimeScale;

            if (UnityEngine.Time.time - _arrivedAtTime >= arrivalHoldDuration)
            {
                OnWarpCompleting();
                OnWarpComplete?.Invoke();
                TargetTime = 0;
                SetState(WarpState.Idle);
            }
        }

        private void SetState(WarpState newState)
        {
            if (CurrentState == newState) return;

            if (newState == WarpState.Accelerating)
                _accelerationStartTime = UnityEngine.Time.time;
            else if (newState == WarpState.Decelerating)
            {
                _decelerationStartTime = UnityEngine.Time.time;
                _decelerationStartSpeed = _currentWarpSpeed;
            }

            CurrentState = newState;
            OnWarpStateChanged?.Invoke(newState);
        }
    }
}
