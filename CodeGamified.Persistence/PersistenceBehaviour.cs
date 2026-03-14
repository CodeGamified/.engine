// CodeGamified.Persistence — Git-backed persistence framework
// MIT License
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CodeGamified.Persistence
{
    /// <summary>
    /// Optional MonoBehaviour for autosave and dirty tracking.
    /// Games subclass to define what triggers saves and how often.
    ///
    /// Attach to a persistent GameObject. Requires an IGitRepository
    /// to be assigned before first save.
    /// </summary>
    public abstract class PersistenceBehaviour : MonoBehaviour
    {
        // ═══════════════════════════════════════════════════════════════
        // CONFIGURATION
        // ═══════════════════════════════════════════════════════════════

        [Header("Autosave")]
        [Tooltip("Seconds between autosave checks. 0 = disabled.")]
        public float autosaveInterval = 60f;

        [Tooltip("Seconds between sync (push/pull) attempts. 0 = disabled.")]
        public float syncInterval = 300f;

        // ═══════════════════════════════════════════════════════════════
        // STATE
        // ═══════════════════════════════════════════════════════════════

        protected IGitRepository Repository { get; private set; }

        /// <summary>True if there are unsaved changes.</summary>
        public bool IsDirty { get; private set; }

        float _lastSaveTime;
        float _lastSyncTime;

        // ═══════════════════════════════════════════════════════════════
        // EVENTS
        // ═══════════════════════════════════════════════════════════════

        public Action OnSaveStarted;
        public Action<GitResult> OnSaveCompleted;
        public Action<GitResult> OnSyncCompleted;

        // ═══════════════════════════════════════════════════════════════
        // ABSTRACT — game must define
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Collect all dirty entities and save them to the repository.</summary>
        protected abstract GitResult PerformSave(IGitRepository repo);

        // ═══════════════════════════════════════════════════════════════
        // VIRTUAL — override to customize
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Called after a successful save. Override for UI feedback.</summary>
        protected virtual void OnSaved(GitResult result) { }

        /// <summary>Called after a sync attempt. Override for UI feedback.</summary>
        protected virtual void OnSynced(GitResult result) { }

        /// <summary>Called on save/sync failure. Override for error handling.</summary>
        protected virtual void OnError(string operation, GitResult result)
        {
            Debug.LogWarning($"[Persistence] {operation} failed: {result.Error}");
        }

        // ═══════════════════════════════════════════════════════════════
        // PUBLIC API
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Assign the repository implementation. Call once during init.</summary>
        public void Initialize(IGitRepository repository)
        {
            Repository = repository;
        }

        /// <summary>Mark state as dirty — next autosave cycle will trigger a save.</summary>
        public void MarkDirty()
        {
            IsDirty = true;
        }

        /// <summary>Manually trigger a save now.</summary>
        public void SaveNow()
        {
            if (Repository == null)
            {
                OnError("Save", GitResult.Fail("Repository not initialized"));
                return;
            }

            OnSaveStarted?.Invoke();
            var result = PerformSave(Repository);
            IsDirty = false;
            _lastSaveTime = Time.unscaledTime;

            if (result.Success)
                OnSaved(result);
            else
                OnError("Save", result);

            OnSaveCompleted?.Invoke(result);
        }

        /// <summary>Manually trigger a sync (push + pull) now.</summary>
        public void SyncNow()
        {
            if (Repository == null)
            {
                OnError("Sync", GitResult.Fail("Repository not initialized"));
                return;
            }

            // Save first if dirty
            if (IsDirty) SaveNow();

            var pushResult = Repository.Push();
            if (!pushResult.Success)
            {
                OnError("Push", pushResult);
                OnSyncCompleted?.Invoke(pushResult);
                return;
            }

            var pullResult = Repository.Pull();
            _lastSyncTime = Time.unscaledTime;

            if (pullResult.Success)
                OnSynced(pullResult);
            else
                OnError("Pull", pullResult);

            OnSyncCompleted?.Invoke(pullResult);
        }

        // ═══════════════════════════════════════════════════════════════
        // LIFECYCLE
        // ═══════════════════════════════════════════════════════════════

        protected virtual void Update()
        {
            if (Repository == null) return;

            float now = Time.unscaledTime;

            // Autosave
            if (autosaveInterval > 0f && IsDirty && now - _lastSaveTime >= autosaveInterval)
                SaveNow();

            // Auto-sync
            if (syncInterval > 0f && now - _lastSyncTime >= syncInterval)
                SyncNow();
        }

        protected virtual void OnApplicationPause(bool paused)
        {
            if (paused && IsDirty && Repository != null)
                SaveNow();
        }

        protected virtual void OnApplicationQuit()
        {
            if (IsDirty && Repository != null)
                SaveNow();
        }
    }
}
