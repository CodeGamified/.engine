// CodeGamified.TUI.Blur — Editor auto-setup
// MIT License
//
// On first import (or after any domain reload where the setup is missing),
// this script automatically:
//   1. Creates the Kawase blur material (for the render feature)
//   2. Creates the UI blur material in Resources/ (for runtime loading)
//   3. Ensures the active URP renderer is Forward (not Renderer2D)
//   4. Adds TUIBlurFeature to the active URP renderer asset
//
// All generated assets go to Assets/CodeGamified.TUI.Blur.Generated/
// (outside the engine submodule — safe to .gitignore or commit).

using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CodeGamified.TUI.Blur.Editor
{
    [InitializeOnLoad]
    static class TUIBlurEditorSetup
    {
        const string GENERATED_ROOT       = "Assets/CodeGamified.TUI.Blur.Generated";
        const string RESOURCES_DIR        = GENERATED_ROOT + "/Resources";
        const string KAWASE_MAT_PATH      = GENERATED_ROOT + "/TUIKawaseBlur.mat";
        const string UI_MAT_PATH          = RESOURCES_DIR  + "/TUIUIBlur.mat";
        const string FORWARD_RENDERER_PATH = GENERATED_ROOT + "/ForwardRenderer.asset";

        static TUIBlurEditorSetup()
        {
            EditorApplication.delayCall += TryAutoSetup;
        }

        static void TryAutoSetup()
        {
            // ── 1. Find shaders ─────────────────────────────────
            var kawaseShader = Shader.Find("Hidden/CodeGamified/KawaseBlur");
            var uiShader     = Shader.Find("CodeGamified/UIBackgroundBlur");
            if (kawaseShader == null || uiShader == null) return; // shaders not compiled yet

            // ── 2. Create material assets ───────────────────────
            EnsureDirectory(GENERATED_ROOT);
            EnsureDirectory(RESOURCES_DIR);

            var kawaseMat = EnsureMaterial(KAWASE_MAT_PATH, kawaseShader, "TUIKawaseBlur");
            EnsureMaterial(UI_MAT_PATH, uiShader, "TUIUIBlur");

            // ── 3. Ensure Forward renderer ──────────────────────
            var rendererData = EnsureForwardRenderer();
            if (rendererData == null) return;

            // ── 4. Add render feature to renderer ───────────────
            EnsureRenderFeature(rendererData, kawaseMat);
        }

        static Material EnsureMaterial(string path, Shader shader, string name)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var mat = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(mat, path);
            Debug.Log($"[TUI Blur] Created {path}");
            return mat;
        }

        static ScriptableRendererData EnsureForwardRenderer()
        {
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null) return null;

            var pipelineSO = new SerializedObject(pipeline);
            var rendererListProp = pipelineSO.FindProperty("m_RendererDataList");
            if (rendererListProp == null || rendererListProp.arraySize == 0) return null;

            var currentRenderer = rendererListProp.GetArrayElementAtIndex(0).objectReferenceValue
                as ScriptableRendererData;

            // Already a Forward (Universal) renderer — just ensure postProcessData.
            if (currentRenderer is UniversalRendererData urd)
            {
                EnsurePostProcessData(urd, null);
                return urd;
            }

            // Currently using Renderer2D — create a Forward renderer.
            var existing = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(FORWARD_RENDERER_PATH);
            if (existing == null)
            {
                existing = ScriptableObject.CreateInstance<UniversalRendererData>();
                existing.name = "ForwardRenderer";
                AssetDatabase.CreateAsset(existing, FORWARD_RENDERER_PATH);
                AssetDatabase.SaveAssets();
                Debug.Log("[TUI Blur] Created Forward renderer at " + FORWARD_RENDERER_PATH);
            }

            // Ensure post-processing data is wired up (required for bloom).
            EnsurePostProcessData(existing, currentRenderer);

            // Point the pipeline at the Forward renderer.
            rendererListProp.GetArrayElementAtIndex(0).objectReferenceValue = existing;
            pipelineSO.FindProperty("m_RendererType").intValue = 1; // Forward
            pipelineSO.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
            AssetDatabase.SaveAssets();
            Debug.Log("[TUI Blur] Switched URP pipeline from Renderer2D to Forward renderer.");

            return existing;
        }

        static void EnsurePostProcessData(UniversalRendererData renderer, ScriptableRendererData sourceRenderer)
        {
            var rdSO = new SerializedObject(renderer);
            var ppDataProp = rdSO.FindProperty("postProcessData");
            if (ppDataProp == null || ppDataProp.objectReferenceValue != null) return;

            // Try to copy from source renderer (e.g. Renderer2D being replaced).
            Object ppData = null;
            if (sourceRenderer != null)
            {
                var srcProp = new SerializedObject(sourceRenderer).FindProperty("m_PostProcessData");
                if (srcProp != null) ppData = srcProp.objectReferenceValue;
            }
            // Fallback: load from URP package by known GUID.
            if (ppData == null)
            {
                string ppPath = AssetDatabase.GUIDToAssetPath("41439944d30ece34e96484bdb6645b55");
                if (!string.IsNullOrEmpty(ppPath))
                    ppData = AssetDatabase.LoadAssetAtPath<Object>(ppPath);
            }
            if (ppData == null) return;

            ppDataProp.objectReferenceValue = ppData;
            rdSO.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(renderer);
            AssetDatabase.SaveAssets();
            Debug.Log("[TUI Blur] Set postProcessData on Forward renderer.");
        }

        static void EnsureRenderFeature(ScriptableRendererData rendererData, Material kawaseMat)
        {
            if (rendererData == null) return;

            // Check if feature already exists
            if (rendererData.rendererFeatures != null)
            {
                foreach (var f in rendererData.rendererFeatures)
                {
                    if (f is TUIBlurFeature existing)
                    {
                        // Repair broken material reference (e.g. after generated assets moved)
                        if (existing.settings.blurMaterial == null && kawaseMat != null)
                        {
                            existing.settings.blurMaterial = kawaseMat;
                            EditorUtility.SetDirty(existing);
                            EditorUtility.SetDirty(rendererData);
                            AssetDatabase.SaveAssets();
                            Debug.Log("[TUI Blur] Repaired missing blurMaterial on existing render feature.");
                        }
                        return;
                    }
                }
            }

            // Create feature instance as sub-asset of the renderer data
            var feature = ScriptableObject.CreateInstance<TUIBlurFeature>();
            feature.name = "TUI Blur";
            feature.settings.blurMaterial = kawaseMat;

            AssetDatabase.AddObjectToAsset(feature, rendererData);

            // Update serialized renderer feature list + map
            var rdSO = new SerializedObject(rendererData);
            var featuresProp  = rdSO.FindProperty("m_RendererFeatures");
            var featureMapProp = rdSO.FindProperty("m_RendererFeatureMap");

            int idx = featuresProp.arraySize;
            featuresProp.arraySize = idx + 1;
            featuresProp.GetArrayElementAtIndex(idx).objectReferenceValue = feature;

            if (featureMapProp != null)
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    feature, out string _, out long localId);
                featureMapProp.arraySize = idx + 1;
                featureMapProp.GetArrayElementAtIndex(idx).longValue = localId;
            }

            rdSO.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rendererData);
            AssetDatabase.SaveAssets();

            Debug.Log("[TUI Blur] Auto-configured render feature on URP renderer.");
        }

        static void EnsureDirectory(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string fullPath = Path.Combine(Application.dataPath,
                path.Substring("Assets/".Length));
            Directory.CreateDirectory(fullPath);
            AssetDatabase.Refresh();
        }
    }
}
