using BCSignVR;
using System.Linq;
using UnityEditor;
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
            PlayerSettings.productName = "BCSignVR Standalone";
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
            ConfigureOpenXR(BuildTargetGroup.Standalone);
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

        static void ConfigureOpenXR(BuildTargetGroup group)
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
            if (settings != null && group == BuildTargetGroup.Standalone)
            {
                var serializedSettings = new SerializedObject(settings);
                var loaderName = serializedSettings.FindProperty("customLoaderName");
                if (loaderName != null)
                {
                    loaderName.stringValue = System.IO.Path.GetFullPath(
                        "Builds/macOS/BCSignVR.app/Contents/PlugIns/ARM64/libopenxr_loader.dylib");
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

