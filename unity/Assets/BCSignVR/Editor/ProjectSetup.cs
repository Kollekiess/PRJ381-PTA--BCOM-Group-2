using BCSignVR;
using System.Reflection;
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
            PlayerSettings.productName = "signzzZ";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Standalone, "za.ac.belgiumcampus.bcsignvr");
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.runInBackground = true;
            ConfigureRuntimeInputAndPhysics();
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
            ConfigureRuntimeInputAndPhysics();
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
            ConfigureRuntimeInputAndPhysics();
            ConfigureOpenXR(BuildTargetGroup.Android);
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

        // Unity 6 can store these values in the active Build Profile instead of
        // the legacy ProjectSettings asset. Configure both locations so the
        // runtime keeps the existing keyboard/mouse path for desktop while XR
        // input continues to work on the headset. Dynamic colliders are also
        // needed by the board's hand/controller pointer.
        static void ConfigureRuntimeInputAndPhysics()
        {
            var configured = false;
            var buildProfileType = typeof(PlayerSettings).Assembly.GetType("UnityEditor.Build.Profile.BuildProfile");
            if (buildProfileType != null)
            {
                var globalField = buildProfileType.GetField("s_GlobalPlayerSettings", BindingFlags.Static | BindingFlags.NonPublic);
                var globalSettings = globalField?.GetValue(null) as PlayerSettings;
                configured |= ApplyRuntimeSettings(globalSettings);

                var getActiveProfile = buildProfileType.GetMethod(
                    "GetActiveBuildProfile", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                var activeProfile = getActiveProfile?.Invoke(null, null);
                var overrideField = buildProfileType.GetField("m_PlayerSettings", BindingFlags.Instance | BindingFlags.NonPublic);
                var overrideSettings = activeProfile != null
                    ? overrideField?.GetValue(activeProfile) as PlayerSettings
                    : null;
                configured |= ApplyRuntimeSettings(overrideSettings);
            }

            if (!configured)
            {
                var fallback = Resources.FindObjectsOfTypeAll<PlayerSettings>().FirstOrDefault();
                configured = ApplyRuntimeSettings(fallback);
            }

            if (!configured)
                Debug.LogWarning("BCSignVR could not locate Unity Player Settings; desktop and XR input may need manual configuration.");
        }

        static bool ApplyRuntimeSettings(PlayerSettings playerSettings)
        {
            if (playerSettings == null) return false;

            var serialized = new SerializedObject(playerSettings);
            var inputHandler = serialized.FindProperty("activeInputHandler");
            var stripEngineCode = serialized.FindProperty("stripEngineCode");
            if (inputHandler == null || stripEngineCode == null) return false;

            inputHandler.intValue = 2; // New Input System + legacy Input API.
            stripEngineCode.boolValue = false; // Keep runtime physics components available.
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(playerSettings);
            return true;
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
            // Quest must be declared as a Meta XR application in the Android
            // manifest. Without this feature Unity still starts OpenXR, but
            // Quest treats the APK like a flat 2D panel and the classroom is
            // not presented in the headset view.
            var metaQuest = settings?.GetFeatures().FirstOrDefault(feature =>
                feature != null && (feature.name == "MetaQuestFeature Android" ||
                    feature.GetType().FullName?.EndsWith(".MetaQuestFeature") == true));
            if (metaQuest != null && group == BuildTargetGroup.Android) metaQuest.enabled = true;
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

