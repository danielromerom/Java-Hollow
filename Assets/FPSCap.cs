using UnityEngine;

[ExecuteInEditMode]
public class FPSCap : MonoBehaviour
{
    [SerializeField] private int frameRate = 30;

    private void Start()
    {
        QualitySettings.vSyncCount = 0; // Disable VSync
        Application.targetFrameRate = frameRate;
    }
}
