// CodeGamified.Quality — Shared quality management framework
// MIT License
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CodeGamified.Quality
{
    /// <summary>
    /// Static hub that owns the current quality tier and broadcasts changes.
    ///
    /// Replaces per-game FindObjectByType scatter (SettingsTerminal → SimpleEarth, etc.)
    /// with a single publish/subscribe system.
    ///
    /// Usage:
    ///   Settings UI:
    ///     QualityBridge.SetTier(QualityTier.High);
    ///
    ///   Game components (MonoBehaviour implementing IQualityResponsive):
    ///     void OnEnable()  => QualityBridge.Register(this);
    ///     void OnDisable() => QualityBridge.Unregister(this);
    ///     void OnQualityChanged(QualityTier tier) { /* rebuild mesh */ }
    ///
    ///   One-off listeners:
    ///     QualityBridge.OnTierChanged += tier => UpdateShadowDistance(tier);
    /// </summary>
    public static class QualityBridge
    {
        /// <summary>Current quality tier. Defaults to Ultra.</summary>
        public static QualityTier CurrentTier { get; private set; } = QualityTier.Ultra;

        /// <summary>Fired after the tier changes and all IQualityResponsive listeners are notified.</summary>
        public static event Action<QualityTier> OnTierChanged;

        private static readonly List<IQualityResponsive> _listeners = new(16);

        /// <summary>
        /// Set the global quality tier. Updates Unity's QualitySettings and notifies all listeners.
        /// </summary>
        public static void SetTier(QualityTier tier)
        {
            CurrentTier = tier;
            QualitySettings.SetQualityLevel((int)tier, true);

            // Notify interface listeners (iterate backwards for safe removal during callback)
            for (int i = _listeners.Count - 1; i >= 0; i--)
            {
                if (_listeners[i] is UnityEngine.Object obj && obj == null)
                {
                    _listeners.RemoveAt(i); // Clean up destroyed objects
                    continue;
                }
                _listeners[i].OnQualityChanged(tier);
            }

            OnTierChanged?.Invoke(tier);
        }

        /// <summary>
        /// Register a quality-responsive component. Typically called in OnEnable.
        /// </summary>
        public static void Register(IQualityResponsive listener)
        {
            if (listener != null && !_listeners.Contains(listener))
                _listeners.Add(listener);
        }

        /// <summary>
        /// Unregister a quality-responsive component. Typically called in OnDisable.
        /// </summary>
        public static void Unregister(IQualityResponsive listener)
        {
            _listeners.Remove(listener);
        }

        /// <summary>Number of currently registered listeners (for diagnostics).</summary>
        public static int ListenerCount => _listeners.Count;
    }
}
