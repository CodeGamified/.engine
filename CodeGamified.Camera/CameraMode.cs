// Copyright CodeGamified 2025-2026
// MIT License

namespace CodeGamified.Camera
{
    /// <summary>
    /// Camera operating modes for <see cref="CameraRig"/>.
    /// </summary>
    public enum CameraMode
    {
        /// <summary>Free pan/orbit/zoom — no target lock. WASD, mouse orbit, scroll zoom.</summary>
        Free,

        /// <summary>Orbit around a target transform. Auto-follows position. Scroll zoom with dynamic limits.</summary>
        Orbit,

        /// <summary>On-target first-person view. Syncs to target pitch/roll/yaw. Mouse look.</summary>
        Deck
    }
}
