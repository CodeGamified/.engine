# Texture Import Settings for BitNaughts AI

## Required Import Settings

For optimal visual quality, configure the texture import settings in Unity:

### Normal Map (`8k_earth_normal_map.tif`)
1. Select the texture in Unity
2. In Inspector, set:
   - **Texture Type**: Normal map
   - **sRGB (Color Texture)**: ☐ UNCHECKED (linear space)
   - **Max Size**: 8192
   - **Compression**: High Quality

### Specular Map (`8k_earth_specular_map.tif`)  
1. Select the texture in Unity
2. In Inspector, set:
   - **Texture Type**: Default
   - **sRGB (Color Texture)**: ☐ UNCHECKED (linear space for masks)
   - **Max Size**: 8192
   - **Alpha Source**: From Gray Scale (if using as smoothness)

### Day/Night Maps (`8k_earth_daymap.jpg`, `8k_earth_nightmap.jpg`)
1. Select each texture in Unity
2. In Inspector, set:
   - **Texture Type**: Default
   - **sRGB (Color Texture)**: ☑ CHECKED (color data)
   - **Max Size**: 8192
   - **Compression**: High Quality

### Cloud Map (`8k_earth_clouds.jpg`)
1. Select the texture in Unity
2. In Inspector, set:
   - **Texture Type**: Default  
   - **sRGB (Color Texture)**: ☑ CHECKED
   - **Max Size**: 4096 (can be lower than earth)
   - **Alpha Source**: From Gray Scale (white = clouds)

### Star Field (`8k_stars_milky_way.jpg`, `8k_stars.jpg`)
1. Select each texture in Unity
2. In Inspector, set:
   - **Texture Type**: Default
   - **sRGB (Color Texture)**: ☑ CHECKED
   - **Max Size**: 8192
   - **Wrap Mode**: Repeat

## Quick Fix Script

If textures look wrong, run this in Unity's console or create an Editor script:

```csharp
// Fix normal map import settings
var normalMap = AssetImporter.GetAtPath("Assets/Resources/8k_earth_normal_map.tif") as TextureImporter;
if (normalMap != null)
{
    normalMap.textureType = TextureImporterType.NormalMap;
    normalMap.sRGBTexture = false;
    normalMap.SaveAndReimport();
}
```

## Memory Considerations

8K textures use significant VRAM:
- Each 8K texture ≈ 256MB uncompressed
- With compression ≈ 64-128MB
- Total for Earth (5 textures) ≈ 320-640MB VRAM

For lower-end systems, consider:
- Reducing Max Size to 4096
- Using more aggressive compression
- Disabling clouds or normal maps
