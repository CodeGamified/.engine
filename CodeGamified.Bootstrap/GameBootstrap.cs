// CodeGamified.Bootstrap — Shared bootstrap patterns for all games
// MIT License
using System.Collections;
using UnityEngine;
using CodeGamified.Time;

namespace CodeGamified.Bootstrap
{
    /// <summary>
    /// Abstract base class for game bootstraps.
    ///
    /// Provides common patterns shared across Pong, SeaRäuber, BitNaughts:
    ///   - Conditional debug logging with per-game tag
    ///   - Camera find-or-create
    ///   - SimulationTime find-or-create
    ///   - Manager factory helpers
    ///   - Boot sequence formatting
    ///
    /// Does NOT define Start()/Update()/OnDestroy() — subclasses own their lifecycle.
    /// This is a toolkit, not a template.
    /// </summary>
    public abstract class GameBootstrap : MonoBehaviour
    {
        [Header("Bootstrap")]
        [Tooltip("Log detailed bootstrap info to console")]
        public bool debugLogging = true;

        /// <summary>Short tag for log messages, e.g. "PONG", "SEA", "ORB".</summary>
        protected abstract string LogTag { get; }

        // ═══════════════════════════════════════════════════════════
        // LOGGING
        // ═══════════════════════════════════════════════════════════

        protected void Log(string message)
        {
            if (debugLogging)
                Debug.Log($"[{LogTag}] {message}");
        }

        /// <summary>Print a separator line (────…).</summary>
        protected void LogDivider()
        {
            Log("────────────────────────────────────────");
        }

        /// <summary>Print a labeled status line: "  LABEL │ value".</summary>
        protected void LogStatus(string label, string value)
        {
            Log($"  {label} │ {value}");
        }

        /// <summary>Print a feature toggle: "  LABEL │ ✅ ACTIVE" or "── disabled".</summary>
        protected void LogEnabled(string label, bool enabled, string detail = null)
        {
            string status = enabled ? "✅ ACTIVE" : "── disabled";
            if (enabled && detail != null)
                status = $"✅ {detail}";
            Log($"  {label} │ {status}");
        }

        // ═══════════════════════════════════════════════════════════
        // CAMERA
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Find or create a main camera. Pass your serialized field (or null).
        /// Returns the camera and creates an AudioListener if one was spawned.
        /// </summary>
        protected Camera EnsureCamera(Camera existing = null)
        {
            var cam = existing;
            if (cam == null) cam = Camera.main;

            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
                Log("Created Main Camera");
            }

            return cam;
        }

        // ═══════════════════════════════════════════════════════════
        // SIMULATION TIME
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Find existing SimulationTime or create one with the given subclass.
        /// Returns the instance (may be pre-existing).
        /// </summary>
        protected T EnsureSimulationTime<T>() where T : SimulationTime
        {
            if (SimulationTime.Instance != null)
            {
                Log("SimulationTime already exists.");
                return SimulationTime.Instance as T;
            }

            var go = new GameObject("SimulationTime");
            var sim = go.AddComponent<T>();
            Log($"Created {typeof(T).Name}");
            return sim;
        }

        // ═══════════════════════════════════════════════════════════
        // MANAGER FACTORY
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Create a new GameObject with the given component.
        /// Name defaults to the component type name.
        /// </summary>
        protected T CreateManager<T>(string name = null) where T : Component
        {
            var go = new GameObject(name ?? typeof(T).Name);
            return go.AddComponent<T>();
        }

        /// <summary>
        /// Find an existing instance of T in the scene, or create one.
        /// </summary>
        protected T FindOrCreate<T>(string name = null) where T : Component
        {
            var existing = FindAnyObjectByType<T>();
            if (existing != null)
            {
                Log($"{typeof(T).Name} already exists, reusing.");
                return existing;
            }
            return CreateManager<T>(name);
        }

        // ═══════════════════════════════════════════════════════════
        // BOOT SEQUENCE
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Run an action after waiting N frames (default 2).
        /// Standard pattern: wait for all systems' Start() to complete.
        /// </summary>
        protected Coroutine RunAfterFrames(System.Action onReady, int frames = 2)
        {
            return StartCoroutine(DelayedAction(onReady, frames));
        }

        private IEnumerator DelayedAction(System.Action onReady, int frames)
        {
            for (int i = 0; i < frames; i++)
                yield return null;
            onReady?.Invoke();
        }
    }
}
