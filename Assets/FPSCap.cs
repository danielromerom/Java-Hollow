using UnityEngine;

public class FPSCap : MonoBehaviour
{
    // The normal experience is uncapped. XR frame pacing belongs to the XR runtime.
    [SerializeField] private int frameRate = -1;

    private void Start()
    {
        QualitySettings.vSyncCount = 0; // Disable VSync
        Application.targetFrameRate = frameRate;
    }
}
