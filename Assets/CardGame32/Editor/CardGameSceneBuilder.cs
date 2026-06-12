using System;
using System.Collections.Generic;
using System.IO;
using CardGame32;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CardGame32Editor
{
    public static class CardGameSceneBuilder
    {
        private const string ScenePath = "Assets/CardGame32/Scenes/CasualMobilePrototype.unity";

        [MenuItem("CardGame32/Build Casual Mobile Prototype Scene")]
        public static void BuildCasualMobilePrototypeScene()
        {
            EnsureDirectory("Assets/CardGame32/Scenes");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "CasualMobilePrototype";

            GameObject root = new GameObject("CardGame32 Casual Mobile Prototype");
            CardGamePrototypeController controller = root.AddComponent<CardGamePrototypeController>();
            controller.playerCount = 6;
            controller.startingScore = 50;
            controller.roomNumber = 102938;

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Built CardGame32 casual mobile prototype scene at " + ScenePath);
        }

        public static void BuildFromCommandLine()
        {
            BuildCasualMobilePrototypeScene();
            EditorApplication.Exit(0);
        }

        [MenuItem("CardGame32/Build Android Release APK")]
        public static void BuildAndroidDebugApk()
        {
            BuildCasualMobilePrototypeScene();
            EnsureDirectory("Builds/Android");

            ConfigureAndroidPhoneBuild();
            EditorUserBuildSettings.buildAppBundle = false;

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/Android/CardGame32Prototype.apk",
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            BuildPipeline.BuildPlayer(options);
            Debug.Log("Built Android release APK at Builds/Android/CardGame32Prototype.apk");
        }

        private static void ConfigureAndroidPhoneBuild()
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

            PlayerSettings.productName = "32 Cards Prototype";
            PlayerSettings.companyName = "CardGame32";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.cardgame32.prototype");

            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });

            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.forceInternetPermission = true;
            PlayerSettings.Android.forceSDCardPermission = false;

            ConfigureBuiltInRenderPipelineForPrototype();
            StripXrPreloadedAssets();
        }

        private static void ConfigureBuiltInRenderPipelineForPrototype()
        {
            GraphicsSettings.defaultRenderPipeline = null;

            int qualityCount = QualitySettings.names.Length;
            for (int i = 0; i < qualityCount; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = null;
            }

            if (qualityCount > 0)
            {
                QualitySettings.SetQualityLevel(Mathf.Min(1, qualityCount - 1), false);
            }
        }

        private static void StripXrPreloadedAssets()
        {
            UnityEngine.Object[] preloadedAssets = PlayerSettings.GetPreloadedAssets();
            List<UnityEngine.Object> filteredAssets = new List<UnityEngine.Object>(preloadedAssets.Length);

            foreach (UnityEngine.Object asset in preloadedAssets)
            {
                if (asset == null)
                {
                    continue;
                }

                string assetPath = AssetDatabase.GetAssetPath(asset).Replace('\\', '/');
                if (assetPath.StartsWith("Assets/XR/", StringComparison.OrdinalIgnoreCase))
                {
                    Debug.Log($"Removed XR preloaded asset from Android phone build: {assetPath}");
                    continue;
                }

                filteredAssets.Add(asset);
            }

            PlayerSettings.SetPreloadedAssets(filteredAssets.ToArray());
        }

        public static void BuildAndroidDebugApkFromCommandLine()
        {
            BuildAndroidDebugApk();
            EditorApplication.Exit(0);
        }

        [MenuItem("CardGame32/Render Prototype Preview")]
        public static void RenderPrototypePreview()
        {
            const string previewPath = "Assets/CardGame32/References/prototype-preview.png";

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "CardGame32Preview";

            GameObject root = new GameObject("CardGame32 Preview Renderer");
            CardGamePrototypeController controller = root.AddComponent<CardGamePrototypeController>();
            controller.playerCount = 6;
            controller.startingScore = 50;
            controller.roomNumber = 102938;
            controller.BuildPrototypeForEditorPreview();

            Camera camera = Camera.main;
            RenderTexture renderTexture = new RenderTexture(390, 844, 24, RenderTextureFormat.ARGB32);
            Texture2D screenshot = new Texture2D(390, 844, TextureFormat.RGBA32, false);
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;

            camera.targetTexture = renderTexture;
            RenderTexture.active = renderTexture;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            screenshot.ReadPixels(new Rect(0, 0, 390, 844), 0, 0);
            screenshot.Apply();

            File.WriteAllBytes(previewPath, screenshot.EncodeToPNG());

            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            UnityEngine.Object.DestroyImmediate(renderTexture);
            UnityEngine.Object.DestroyImmediate(screenshot);

            AssetDatabase.ImportAsset(previewPath);
            AssetDatabase.Refresh();
            Debug.Log("Rendered CardGame32 prototype preview at " + previewPath);
        }

        public static void RenderPreviewFromCommandLine()
        {
            RenderPrototypePreview();
            EditorApplication.Exit(0);
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            EditorBuildSettingsScene[] existing = EditorBuildSettings.scenes;
            int existingIndex = -1;
            for (int i = 0; i < existing.Length; i++)
            {
                if (existing[i].path == scenePath)
                {
                    existingIndex = i;
                    break;
                }
            }

            int updatedLength = existingIndex >= 0 ? existing.Length : existing.Length + 1;
            EditorBuildSettingsScene[] updated = new EditorBuildSettingsScene[updatedLength];
            updated[0] = new EditorBuildSettingsScene(scenePath, true);

            int target = 1;
            for (int i = 0; i < existing.Length; i++)
            {
                if (i == existingIndex)
                {
                    continue;
                }

                updated[target] = existing[i];
                target++;
            }

            EditorBuildSettings.scenes = updated;
        }

        private static void EnsureDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }
    }
}
