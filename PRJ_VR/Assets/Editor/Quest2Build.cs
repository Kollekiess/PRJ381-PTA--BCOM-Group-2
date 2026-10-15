#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;

public static class Quest2Build
{
    private const string DemoScene = "Assets/Scenes/DemoScene.unity";
    private const string SettingsPath = "Assets/XR/XRGeneralSettingsPerBuildTarget.asset";
    private const string OpenXRLoaderType = "UnityEngine.XR.OpenXR.OpenXRLoader";

    [MenuItem("BCSignVR/1. Configure Quest 2 Android XR")]
    public static void Configure()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
        {
            if (EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
                Debug.Log("Switched to Android. Run BCSignVR > Configure Quest 2 Android XR again after Unity finishes importing.");
            else
                Debug.LogError("Unity could not switch the active build target to Android.");
            return;
        }

        Directory.CreateDirectory(Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Assets/XR"));
        var perTarget = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(SettingsPath);
        if (perTarget == null)
        {
            perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
            AssetDatabase.CreateAsset(perTarget, SettingsPath);
        }
        EditorBuildSettings.AddConfigObject(XRGeneralSettings.settingsKey, perTarget, true);

        var general = perTarget.SettingsForBuildTarget(BuildTargetGroup.Android);
        if (general == null)
        {
            general = ScriptableObject.CreateInstance<XRGeneralSettings>();
            general.name = "Android Settings";
            perTarget.SetSettingsForBuildTarget(BuildTargetGroup.Android, general);
            AssetDatabase.AddObjectToAsset(general, perTarget);
        }
        general.InitManagerOnStart = true;

        if (general.Manager == null)
        {
            general.Manager = ScriptableObject.CreateInstance<XRManagerSettings>();
            general.Manager.name = "Android Providers";
            AssetDatabase.AddObjectToAsset(general.Manager, perTarget);
        }
        general.Manager.automaticLoading = true;
        general.Manager.automaticRunning = true;

        if (!XRPackageMetadataStore.IsLoaderAssigned(OpenXRLoaderType, BuildTargetGroup.Android) &&
            !XRPackageMetadataStore.AssignLoader(general.Manager, OpenXRLoaderType, BuildTargetGroup.Android))
        {
            Debug.LogError("Could not assign the Unity OpenXR loader. Check that XR Plug-in Management and OpenXR packages have finished importing.");
            return;
        }

        FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);
        var questFeature = FeatureHelpers.GetFeatureWithIdForBuildTarget(BuildTargetGroup.Android, "com.unity.openxr.feature.metaquest");
        var touchFeature = FeatureHelpers.GetFeatureWithIdForBuildTarget(BuildTargetGroup.Android, "com.unity.openxr.feature.input.oculustouch");
        if (questFeature == null || touchFeature == null)
        {
            Debug.LogError("OpenXR did not expose Meta Quest Support and Oculus Touch Controller Profile. Review Project Settings > XR Plug-in Management > OpenXR.");
            return;
        }
        questFeature.enabled = true;
        touchFeature.enabled = true;

        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.prj381.bcsignvr");
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(DemoScene, true) };

        EditorUtility.SetDirty(perTarget);
        EditorUtility.SetDirty(general);
        EditorUtility.SetDirty(general.Manager);
        EditorUtility.SetDirty(questFeature);
        EditorUtility.SetDirty(touchFeature);
        AssetDatabase.SaveAssets();
        Debug.Log("Quest 2 OpenXR configured. Run BCSignVR > Build Quest 2 APK when ready.");
    }

    [MenuItem("BCSignVR/2. Build Quest 2 APK")]
    public static void Build()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
        {
            Debug.LogError("Run BCSignVR > Configure Quest 2 Android XR first, then run it again after Unity switches to Android.");
            return;
        }

        var projectRoot = Directory.GetParent(Application.dataPath).FullName;
        var output = Path.GetFullPath(Path.Combine(projectRoot, "../Builds/BCSignVR-Quest2.apk"));
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var options = new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
            locationPathName = output,
            target = BuildTarget.Android,
            options = BuildOptions.None
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result == BuildResult.Succeeded)
            Debug.Log("Quest 2 APK created: " + output);
        else
            Debug.LogError("Quest 2 build failed: " + report.summary.result + " (" + report.summary.totalErrors + " errors)");
    }
}
#endif
