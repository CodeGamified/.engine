// CodeGamified.Time — Shared simulation time framework
// MIT License
using System;
using UnityEngine;

namespace CodeGamified.Time
{
    /// <summary>
    /// Abstract base for simulation time management.
    /// Singleton MonoBehaviour providing time scale control, pause, presets, and input.
    /// 
    /// Games subclass to define:
    ///   - MaxTimeScale, default presets
    ///   - Sun/day-night model (or leave stubs)
    ///   - Time formatting
    /// 
    /// Shared across all CodeGamified games via git submodule.
    /// </summary>
    public abstract class SimulationTime : MonoBehaviour
    {
        public static SimulationTime Instance { get; private set; }

        // ═══════════════════════════════════════════════════════════════
        // CORE STATE
        // ═══════════════════════════════════════════════════════════════

        [Header("Time Control")]
        [Tooltip("Simulation time in seconds since game-defined epoch")]
        public double simulationTime = 0;

        [Tooltip("Time scale multiplier")]
        public float timeScale = 1f;

        [Tooltip("Is simulation paused?")]
        public bool isPaused = false;

        [Header("Presets")]
        public float[] timeScalePresets = { 1f, 2f, 5f, 10f };
        protected int currentPresetIndex = 0;

        // ═══════════════════════════════════════════════════════════════
        // EVENTS
        // ═══════════════════════════════════════════════════════════════

        public Action<double> OnSimulationTimeChanged;
        public Action<float> OnTimeScaleChanged;
        public Action<bool> OnPausedChanged;

        // ═══════════════════════════════════════════════════════════════
        // ABSTRACT — game must define
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Maximum allowed time scale for this game.</summary>
        protected abstract float MaxTimeScale { get; }

        /// <summary>Format simulation time as a game-appropriate display string.</summary>
        public abstract string GetFormattedTime();

        // ═══════════════════════════════════════════════════════════════
        // VIRTUAL — override for game-specific sun/day-night
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Sun direction in world space. Default: straight up (always noon).</summary>
        public virtual Vector3 GetSunDirection() => Vector3.up;

        /// <summary>Sun altitude: 1 = overhead, 0 = horizon, negative = below. Default: 1 (always noon).</summary>
        public virtual float GetSunAltitude() => 1f;

        /// <summary>Is it daytime? Default: always true.</summary>
        public virtual bool IsDaytime() => true;

        /// <summary>Current time of day in hours (0-24). Default: 12 (noon).</summary>
        public virtual float GetTimeOfDay() => 12f;

        /// <summary>Called once in Awake after singleton setup. Override for init (e.g. set simulationTime from startingHour).</summary>
        protected virtual void OnInitialize() { }

        /// <summary>Called each frame when not paused, after simulationTime advances. Override for per-frame game logic.</summary>
        protected virtual void OnTimeAdvanced(double deltaSimTime) { }

        /// <summary>Handle game-specific input beyond the default space/+/-. Called each frame.</summary>
        protected virtual void HandleGameInput() { }

        // ═══════════════════════════════════════════════════════════════
        // LIFECYCLE
        // ═══════════════════════════════════════════════════════════════

        protected virtual void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            OnInitialize();
        }

        protected virtual void Update()
        {
            if (!isPaused)
            {
                double dt = UnityEngine.Time.deltaTime * timeScale;
                simulationTime += dt;
                OnSimulationTimeChanged?.Invoke(simulationTime);
                OnTimeAdvanced(dt);
            }
            HandleInput();
            HandleGameInput();
        }

        private void HandleInput()
        {
            if (Input.GetKeyDown(KeyCode.P))
                TogglePause();

            if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus))
                IncreaseTimeScale();

            if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus))
                DecreaseTimeScale();
        }

        // ═══════════════════════════════════════════════════════════════
        // TIME SCALE API
        // ═══════════════════════════════════════════════════════════════

        public void TogglePause()
        {
            isPaused = !isPaused;
            OnPausedChanged?.Invoke(isPaused);
        }

        public void SetPaused(bool paused)
        {
            if (isPaused != paused)
            {
                isPaused = paused;
                OnPausedChanged?.Invoke(isPaused);
            }
        }

        public void SetTimeScale(float scale)
        {
            scale = Mathf.Clamp(scale, 0f, MaxTimeScale);
            if (!Mathf.Approximately(timeScale, scale))
            {
                timeScale = scale;
                OnTimeScaleChanged?.Invoke(timeScale);
            }
        }

        public void IncreaseTimeScale()
        {
            if (currentPresetIndex < timeScalePresets.Length - 1)
                SetTimeScalePreset(currentPresetIndex + 1);
        }

        public void DecreaseTimeScale()
        {
            if (currentPresetIndex > 0)
                SetTimeScalePreset(currentPresetIndex - 1);
        }

        public void SetTimeScalePreset(int index)
        {
            if (index >= 0 && index < timeScalePresets.Length)
            {
                currentPresetIndex = index;
                SetTimeScale(timeScalePresets[index]);
            }
        }

        public int CurrentPresetIndex => currentPresetIndex;

        /// <summary>
        /// Formatted time scale: "PAUSED", "100x", "0.25x".
        /// </summary>
        public virtual string GetFormattedTimeScale()
        {
            if (isPaused) return "PAUSED";
            if (timeScale >= 1f) return $"{timeScale:F0}x";
            return $"{timeScale:F2}x";
        }
    }
}
