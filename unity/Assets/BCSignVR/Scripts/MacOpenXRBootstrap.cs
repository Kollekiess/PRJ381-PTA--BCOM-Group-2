using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;

namespace BCSignVR
{
    public sealed class MacOpenXRBootstrap : MonoBehaviour
    {
        IEnumerator Start()
        {
            if (Application.platform != RuntimePlatform.OSXPlayer ||
                Environment.GetEnvironmentVariable("BCSIGNVR_XR") != "1") yield break;
            var manager = XRGeneralSettings.Instance?.Manager;
            if (manager == null) { Debug.LogWarning("OpenXR settings are unavailable."); yield break; }
            var directory = new DirectoryInfo(Application.dataPath);
            string loader = null;
            for (var i = 0; directory != null && i < 4; i++, directory = directory.Parent)
            {
                var candidate = Path.Combine(directory.FullName, "PlugIns/ARM64/libopenxr_loader.dylib");
                if (File.Exists(candidate)) { loader = candidate; break; }
            }
            if (loader == null) { Debug.LogWarning("The bundled Mac OpenXR loader is missing."); yield break; }
            // Unity's build processor clears customLoaderName. Set it after settings load,
            // immediately before explicit initialization, to use this app's portable library.
            typeof(OpenXRSettings).GetField("customLoaderName", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(OpenXRSettings.Instance, loader);
            yield return manager.InitializeLoader();
            if (manager.activeLoader != null)
            {
                manager.StartSubsystems(); Debug.Log("BCSignVR Meta simulator OpenXR session started.");
            }
            else Debug.LogWarning("Meta simulator was unavailable; desktop classroom remains usable.");
        }
    }
}
