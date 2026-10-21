#if JAVA_HOLLOW_VR_CHECK && !UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.XR;

// Compiled only into the explicitly requested diagnostic build.
public sealed class JavaHollowStandaloneMeasurement : MonoBehaviour
{
    double elapsed;
    int frames, worn;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Initialize()
    {
        int quality = Array.IndexOf(QualitySettings.names, "VR");
        if (quality >= 0) QualitySettings.SetQualityLevel(quality, true);
        var go = new GameObject("Standalone frame measurement");
        DontDestroyOnLoad(go);
        go.AddComponent<JavaHollowStandaloneMeasurement>();
        Debug.Log($"VR_CHECK quality={QualitySettings.names[QualitySettings.GetQualityLevel()]}; API={SystemInfo.graphicsDeviceType}; GPU={SystemInfo.graphicsDeviceName}");
    }

    void Update()
    {
        elapsed += Time.unscaledDeltaTime;
        frames++;
        if (InputDevices.GetDeviceAtXRNode(XRNode.Head).TryGetFeatureValue(CommonUsages.userPresence, out bool present) && present) worn++;
        if (elapsed < 10) return;
        Debug.Log($"VR_CHECK fps={frames / elapsed:F2}; meanMs={elapsed * 1000 / frames:F2}; headsetPresence={worn}/{frames}; XRactive={XRSettings.isDeviceActive}");
        frames = worn = 0;
        elapsed = 0;
    }
}
#endif
