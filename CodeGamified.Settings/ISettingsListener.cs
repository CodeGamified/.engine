// CodeGamified.Settings — Shared settings management framework
// MIT License

namespace CodeGamified.Settings
{
    /// <summary>
    /// Implemented by MonoBehaviours that react to settings changes.
    ///
    /// Register/unregister with <see cref="SettingsBridge"/>:
    ///   OnEnable  → SettingsBridge.Register(this);
    ///   OnDisable → SettingsBridge.Unregister(this);
    ///
    /// Examples:
    ///   TerminalWindow subclass: rebuild row layout on FontSize change
    ///   AudioManager: adjust mixer group volumes
    ///   ProceduralMesh: rebuild geometry on QualityLevel change
    /// </summary>
    public interface ISettingsListener
    {
        /// <summary>
        /// Called when any setting changes. Inspect the snapshot to decide
        /// which fields matter for this component.
        /// </summary>
        void OnSettingsChanged(SettingsSnapshot settings, SettingsCategory changed);
    }
}
