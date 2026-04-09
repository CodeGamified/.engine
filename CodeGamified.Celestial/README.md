# `CodeGamified.Celestial` — Earth/Planet/Moon/Sun/Skybox Rendering

Render pipeline–agnostic celestial body system with day/night cycle, atmosphere,
moonlight, and quality-responsive geometry/textures. Ported from BitNaughts' Earth
rendering stack and generalized for any CodeGamified game.

---

## Dependencies

| Assembly | Why |
|---|---|
| `CodeGamified.Time` | `SimulationTime.Instance.simulationTime` drives rotation + orbit |
| `CodeGamified.Quality` | `QualityBridge` / `IQualityResponsive` for mesh segments, texture mips, atmosphere layers |

---

## Module Map

```
CodeGamified.Celestial/
├── CelestialPlanet.cs          Earth-like planet: day/night, clouds, atmosphere, normal/specular
├── CelestialPlanetConfig.cs    Config struct with Earth/Mars presets + ApplyConfig()
├── CelestialMoon.cs            Moon: orbital mechanics, phase, earthshine, tidal lock
├── CelestialSun.cs             Sun: emissive sphere, optional directional light
├── CelestialSkybox.cs          Inverted sphere skybox with dual textures
├── AtmosphereSystem.cs         Multi-layer concentric atmosphere shells
├── AtmosphereLayerData.cs      Data model + CSV loader + Earth defaults + static Load
├── CelestialMeshUtility.cs     Cached static factory: UV, inverted, low-poly spheres
└── Shaders/
    ├── CelestialCommon.hlsl        Shared include: CelestialUnpackNormal, CELESTIAL_SUN_DIR
    ├── CelestialDayNight.shader
    ├── CelestialMoon.shader
    ├── CelestialAtmosphereLayer.shader
    ├── CelestialAtmosphere.shader
    ├── CelestialCloudLayer.shader
    └── CelestialSkybox.shader
```

---

## Quick Start

### Bootstrap Scene

```csharp
public class MyOrbitalBootstrap : GameBootstrap
{
    protected override string LogTag => "ORB";

    void Start()
    {
        var cam = EnsureCamera();
        var time = EnsureSimulationTime<MySimulationTime>();

        // Skybox
        var skybox = FindOrCreate<CelestialSkybox>();

        // Sun
        var sunGO = new GameObject("Sun");
        var sun = sunGO.AddComponent<CelestialSun>();
        sun.distance = 1920f;
        sun.angularDiameter = 0.53f;

        // Earth (one-liner config)
        var earthGO = new GameObject("Earth");
        var earth = earthGO.AddComponent<CelestialPlanet>();
        earth.ApplyConfig(CelestialPlanetConfig.Earth);

        // Moon
        var moonGO = new GameObject("Moon");
        var moon = moonGO.AddComponent<CelestialMoon>();
        moon.planet = earth;
        moon.orbitalDistance = 10f;
        moon.sunDirection = Vector3.right;

        // Wire moonlight
        earth.moon = moon;
    }
}
```

---

## CelestialPlanet

Renders an Earth-like body with all visual subsystems.

### Hierarchy

```
CelestialPlanet (root)
├── RotationPivot (rotates with SimulationTime)
│   └── PlanetMesh (scaled to radius, day/night shader)
│       └── CloudLayer (optional, slight altitude offset)
├── AtmosphereSystem (stationary — does NOT rotate)
└── SingleAtmosphere (legacy fallback)
```

### Key Fields

| Field | Default | Purpose |
|---|---|---|
| `radius` | 6.371 | Planet radius in Unity units |
| `rotationPeriodSeconds` | 86400 | Rotation period (Earth = 1 day) |
| `initialRotationOffset` | 180° | Aligns noon face with sun at t=0 |
| `sunDirection` | +X | World-space direction to the sun |
| `sphereSegments` | 96 | Mesh resolution (auto-managed by Quality) |

### Textures (auto-loaded from Resources)

| Resource Name | Shader Property |
|---|---|
| `8k_earth_daymap` | `_DayTex` |
| `8k_earth_nightmap` | `_NightTex` |
| `8k_earth_normal_map` | `_NormalMap` |
| `8k_earth_specular_map` | `_SpecularMap` |
| `8k_earth_clouds` | Cloud `_MainTex` |

### Quality Response

| Tier | Mesh Segments | Texture | Atmo Layers |
|---|---|---|---|
| Low | 32 | 1K | 3 |
| Medium | 48 | 2K | 4 |
| High | 72 | 4K | 5 |
| Ultra | 96 | 8K | 5 |

---

## CelestialMoon

### Orbital Mechanics

Orbit driven by `SimulationTime.Instance.timeScale`:

$$\omega = \frac{360°}{T_{\text{period}} \cdot 86400}$$

Position in inclined orbital plane:

$$x = d\cos\theta, \quad z = d\sin\theta\cos i, \quad y = d\sin\theta\sin i$$

Tidally locked: always `LookAt(planet)`.

### Phase Illumination

$$\text{illuminated} = \frac{\vec{M}{\to}\vec{S} \cdot \vec{M}{\to}\vec{E} + 1}{2}$$

- 0 = new moon (dark side toward observer)
- 1 = full moon (lit side toward observer)

### Earthshine

Inverse of moon phase — when moon is new, Earth is full and reflects light onto the dark side:

$$I_{\text{earthshine}} = \hat{n}\cdot\hat{e} \times \text{earthIllum} \times (1 - \text{dayAmount})$$

---

## CelestialSun

Positions an emissive sphere at `sunDirection * distance` with geometrically correct angular size:

$$r_{\text{sun}} = d \cdot \tan\left(\frac{\alpha}{2}\right)$$

Optional directional light created with `createDirectionalLight = true`.
Games add HDRP/URP-specific light config via `sun.DirectionalLight`.

---

## CelestialSkybox

Camera-following inverted sphere. Dual-texture system:
- **Base** (milky way + starfield)
- **Emissive** (star points, power-curve contrast boost)

---

## AtmosphereSystem

5 concentric low-poly shells per Earth-standard atmosphere layer.
Layer visibility gated by `QualityBridge.CurrentTier`.

### Layer Data

Built-in Earth defaults or loaded from CSV via `AtmosphereLayerDataLoader.csvResourcePath`:

```csv
layer_name,altitude_min_km,altitude_max_km,r,g,b,alpha,density,temp_min,temp_max,visual_scale,"description"
Troposphere,0,12,0.4,0.6,1.0,0.045,1.0,-56,15,8,"Where weather happens"
```

---

## CelestialMeshUtility

Consolidates 3 mesh types previously duplicated across SimpleEarth, SimpleMoon, AtmosphereManager, SpaceSkybox:

| Method | Normals | Tangents | Use |
|---|---|---|---|
| `CreateUVSphere(segs)` | Outward | Yes (TBN) | Planet, Moon, Sun |
| `CreateInvertedSphere(segs)` | Inward | No | Skybox |
| `CreateLowPolySphere(segs)` | Outward | No | Atmosphere shells |

All meshes are **cached** by `(type, segments)` key — zero GC after first call.
`ClearCache()` available for scene-unload memory pressure.

---

## CelestialPlanetConfig

One-shot config struct (cross-pollinated from WorldGraph’s `ArcMapConfig` pattern):

```csharp
var cfg = CelestialPlanetConfig.Earth;   // all 30+ fields
cfg.Radius = 3.0f;                       // override selectively
earth.ApplyConfig(cfg);
```

Built-in presets: `Earth`, `Mars`.

---

## AtmosphereSystemFactory

Games can substitute a custom atmosphere subclass via delegate:

```csharp
earth.AtmosphereFactory = go => go.AddComponent<MyCustomAtmosphere>();
```

Falls back to `AddComponent<AtmosphereSystem>()` when null.

---

## Decoupled Orbit

`CelestialMoon.AdvanceOrbit(float dt)` — public, caller-driven.
`Update()` auto-calls via `SimulationTime`, but games can bypass for manual control.

---

## Shader Summary

All shaders share `CelestialCommon.hlsl` (`CelestialUnpackNormal`, `CELESTIAL_SUN_DIR`).
Dual SubShader (HDRP HLSL + Built-in/URP CG fallback):

| Shader | Key Features |
|---|---|
| `CodeGamified/CelestialDayNight` | Day/night blend, normal map TBN, Blinn-Phong specular, fresnel rim, moonlight |
| `CodeGamified/CelestialMoon` | Phase terminator, earthshine, crater normal map |
| `CodeGamified/CelestialAtmosphereLayer` | Fresnel rim, altitude density fade, day/night brightness |
| `CodeGamified/CelestialAtmosphere` | Single-layer fallback, sun + moon specular highlights |
| `CodeGamified/CelestialCloudLayer` | Day/night brightness, luminance-based alpha |
| `CodeGamified/CelestialSkybox` | Dual-texture, emissive power curve |

---

## Improvements Over Original

| Area | Before (BitNaughts) | After (Engine) |
|---|---|---|
| Mesh generation | 3 duplicate implementations | Single `CelestialMeshUtility` with cache |
| Quality | Manual `SetSphereQuality()` calls | Automatic via `IQualityResponsive` |
| Time coupling | Hardcoded `SimulationTime.Instance` concrete class | Abstract `SimulationTime` + public `AdvanceOrbit(dt)` |
| Configuration | 30+ Inspector fields set individually | `CelestialPlanetConfig` struct + `ApplyConfig()` |
| Dependencies | `FindObjectOfType` scatter | Explicit serialized references |
| Render pipeline | HDRP C# imports (`#if` blocks) | Pipeline-agnostic C#, dual-SubShader shaders |
| Rotation period | Hardcoded 86400s (Earth) | Configurable `rotationPeriodSeconds` |
| Sun direction | Hardcoded `Vector3.right` | Configurable `sunDirection` field |
| Namespace | `BitNaughtsAI.Core` | `CodeGamified.Celestial` |
| Shader names | `BitNaughts/*` | `CodeGamified/*` |

---

## Extension Point Summary

| What to Extend | Class | Game Provides |
|---|---|---|
| Planet textures | `CelestialPlanet` | Day/night/cloud/normal/specular textures |
| Atmosphere data | `AtmosphereLayerDataLoader` | CSV resource path, custom layers, or `LoadStatic()` |
| Atmosphere impl | `CelestialPlanet.AtmosphereFactory` | Custom `AtmosphereSystem` subclass via `Func<>` delegate |
| Moon orbit | `CelestialMoon` | Period, distance, inclination, or manual `AdvanceOrbit(dt)` |
| Sun appearance | `CelestialSun` | Temperature, angular diameter, texture |
| Lens flare / VFX | (game code) | Add components to sun/moon GameObjects |
| HDRP light config | `CelestialSun.DirectionalLight` | Configure HDAdditionalLightData in game bootstrap |
| Planet presets | `CelestialPlanetConfig` | `Earth`, `Mars`, or custom structs |
