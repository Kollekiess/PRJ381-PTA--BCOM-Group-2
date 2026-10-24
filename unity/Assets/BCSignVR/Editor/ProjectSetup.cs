using BCSignVR;
using System.Linq;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Hands.OpenXR;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;

namespace BCSignVR.Editor
{
    public static class ProjectSetup
    {
        [MenuItem("BCSignVR/Configure Project")]
        public static void Configure()
        {
            PlayerSettings.companyName = "BCSignVR";
            PlayerSettings.productName = "signzzZ";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Standalone, "za.ac.belgiumcampus.bcsignvr");
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.runInBackground = true;
            ConfigureOpenXR(BuildTargetGroup.Standalone);
            ConfigureOpenXR(BuildTargetGroup.Android);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("BCSignVR Application").AddComponent<BCSignVRApp>();
            System.IO.Directory.CreateDirectory("Assets/Scenes");
            const string scenePath = "Assets/Scenes/Classroom.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("BCSignVR project configured successfully.");
        }

        [MenuItem("BCSignVR/Build macOS Demo")]
        public static void BuildMac()
        {
            ConfigureOpenXR(BuildTargetGroup.Standalone, useMacLoader: true);
            AssetDatabase.SaveAssets();
            System.IO.Directory.CreateDirectory("Builds/macOS");
            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Classroom.unity" },
                locationPathName = "Builds/macOS/BCSignVR.app",
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new System.Exception($"macOS build failed: {report.summary.result}");
            Debug.Log($"BCSignVR macOS build complete: {report.summary.totalSize} bytes");
        }

        [MenuItem("BCSignVR/Build Windows 11 Demo")]
        public static void BuildWindows()
        {
            ConfigureOpenXR(BuildTargetGroup.Standalone);
            AssetDatabase.SaveAssets();
            System.IO.Directory.CreateDirectory("Builds/Windows");
            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Classroom.unity" },
                locationPathName = "Builds/Windows/BCSignVR Standalone.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new System.Exception($"Windows build failed: {report.summary.result}");
            Debug.Log($"BCSignVR Windows 11 build complete: {report.summary.totalSize} bytes");
        }

        [MenuItem("BCSignVR/Build Quest 3 APK")]
        public static void BuildQuest()
        {
            ConfigureOpenXR(BuildTargetGroup.Android);
            ConfigureQuestBranding();
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "za.ac.belgiumcampus.bcsignvr");
            AssetDatabase.SaveAssets();
            System.IO.Directory.CreateDirectory("Builds/Quest");
            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Classroom.unity" },
                locationPathName = "Builds/Quest/BCSignVR-Quest3.apk",
                target = BuildTarget.Android,
                options = BuildOptions.Development
            };
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new System.Exception($"Quest 3 APK build failed: {report.summary.result}");
            Debug.Log($"BCSignVR Quest 3 APK complete: {report.summary.totalSize} bytes");
        }

        static void ConfigureQuestBranding()
        {
            PlayerSettings.productName = "signzzZ";
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Branding/signzzz-app-icon.png");
            if (icon == null)
                throw new System.InvalidOperationException("Quest app icon is missing: Assets/Branding/signzzz-app-icon.png");

            ConfigureAndroidIcon(AndroidPlatformIconKind.Adaptive, icon);
        }

        static void ConfigureAndroidIcon(PlatformIconKind kind, Texture2D icon)
        {
            var target = NamedBuildTarget.Android;
            var slots = PlayerSettings.GetPlatformIcons(target, kind);
            if (slots == null || slots.Length == 0)
                throw new System.InvalidOperationException($"Unity returned no Android icon slots for {kind}.");

            foreach (var slot in slots)
                slot.SetTextures(Enumerable.Repeat(icon, slot.maxLayerCount).ToArray());

            PlayerSettings.SetPlatformIcons(target, kind, slots);
        }

        static void ConfigureOpenXR(BuildTargetGroup group, bool useMacLoader = false)
        {
            const string settingsPath = "Assets/XR/XRGeneralSettingsPerBuildTarget.asset";
            var perBuildTarget = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(settingsPath);
            if (perBuildTarget == null)
            {
                System.IO.Directory.CreateDirectory("Assets/XR");
                perBuildTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(perBuildTarget, settingsPath);
            }
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.settingsKey, perBuildTarget, true);
            if (!perBuildTarget.HasManagerSettingsForBuildTarget(group))
                perBuildTarget.CreateDefaultManagerSettingsForBuildTarget(group);
            var settingsForTarget = perBuildTarget.SettingsForBuildTarget(group);
            if (settingsForTarget == null || settingsForTarget.Manager == null)
            {
                Debug.LogWarning($"Could not create XR settings for {group}; enable OpenXR manually in Project Settings.");
                return;
            }
            XRPackageMetadataStore.AssignLoader(settingsForTarget.Manager, "UnityEngine.XR.OpenXR.OpenXRLoader", group);
            // Mac previews use desktop input unless the simulator launcher explicitly requests XR.
            settingsForTarget.InitManagerOnStart = group != BuildTargetGroup.Standalone;
            settingsForTarget.Manager.automaticLoading = true;
            settingsForTarget.Manager.automaticRunning = true;
            var settings = AssetDatabase.LoadAllAssetsAtPath("Assets/XR/Settings/OpenXR Package Settings.asset")
                .OfType<OpenXRSettings>()
                .FirstOrDefault(candidate => candidate.name == group.ToString())
                ?? OpenXRSettings.GetSettingsForBuildTargetGroup(group);
            var handTracking = settings?.GetFeature<HandTracking>();
            if (handTracking != null) handTracking.enabled = true;
            if (group == BuildTargetGroup.Android && settings != null)
            {
                // These features are required for a normal Quest Android
                // player. Without them the headset can start passthrough and
                // an OpenXR session while Unity's stereo camera renders no
                // application geometry.
                foreach (var feature in settings.GetFeatures())
                {
                    var typeName = feature?.GetType().FullName;
                    if (typeName == "UnityEngine.XR.OpenXR.Features.MetaQuestSupport.MetaQuestFeature" ||
                        typeName == "UnityEngine.XR.OpenXR.Features.Interactions.MetaQuestTouchPlusControllerProfile" ||
                        typeName == "UnityEngine.XR.OpenXR.Features.Interactions.OculusTouchControllerProfile")
                        feature.enabled = true;
                    else if (typeName == "UnityEngine.XR.OpenXR.Features.OculusQuestSupport.OculusQuestFeature")
                        feature.enabled = false;
                }
            }
            if (settings != null && group == BuildTargetGroup.Standalone)
            {
                var serializedSettings = new SerializedObject(settings);
                var loaderName = serializedSettings.FindProperty("customLoaderName");
                if (loaderName != null)
                {
                    loaderName.stringValue = useMacLoader
                        ? System.IO.Path.GetFullPath(
                            "Builds/macOS/BCSignVR.app/Contents/PlugIns/ARM64/libopenxr_loader.dylib")
                        : string.Empty;
                    serializedSettings.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            EditorUtility.SetDirty(settingsForTarget.Manager);
            EditorUtility.SetDirty(settingsForTarget);
            if (settings != null) EditorUtility.SetDirty(settings);
            EditorUtility.SetDirty(perBuildTarget);
        }
    }
}

