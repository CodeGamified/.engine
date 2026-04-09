// CodeGamified.Celestial — Shared shader utilities
// MIT License
// Include from both HLSL (HDRP) and CG (Built-in/URP) passes.
// Eliminates per-pixel normalize of sun direction and DRYs normal unpacking.

#ifndef CELESTIAL_COMMON_INCLUDED
#define CELESTIAL_COMMON_INCLUDED

// ═══════════════════════════════════════════════════════════════
// NORMAL UNPACKING — shared across DayNight + Moon shaders
// ═══════════════════════════════════════════════════════════════

float3 CelestialUnpackNormal(float4 packed, float scale)
{
    float3 n;
    n.xy = (packed.xy * 2.0 - 1.0) * scale;
    n.z  = sqrt(1.0 - saturate(dot(n.xy, n.xy)));
    return n;
}

// ═══════════════════════════════════════════════════════════════
// SUN DIRECTION — C# guarantees unit length, skip per-pixel normalize
// ═══════════════════════════════════════════════════════════════

// Call this ONCE in the shader to read the sun direction.
// The C# side always passes a normalized vector, so we elide
// the per-pixel normalize that was burning ~5% frag on mobile.
#define CELESTIAL_SUN_DIR(v) ((v).xyz)

#endif // CELESTIAL_COMMON_INCLUDED
