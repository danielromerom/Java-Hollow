using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class VRRadioController : MonoBehaviour
{
    [Header("References")]
    public AudioSource audioSource;
    public UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable powerSwitch;
    public UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable volumeKnob;
    public UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable tunerKnob;

    [Header("Settings")]
    public AudioClip[] stationClips;
    public AudioClip whiteNoise;
    public float minFrequency = 0f;
    public float maxFrequency = 1.75f;
    public float tunerSensitivity = 0.1f;
    public float volumeSensitivity = 0.1f;

    private float tunerValue = 0f;
    private float volumeValue = 0.5f;
    private bool isPowerOn = false;

    void Start()
    {
        audioSource.volume = 0f;
        audioSource.loop = true;

        // XR events
        powerSwitch.selectEntered.AddListener(OnPowerToggle);
        tunerKnob.activated.AddListener(OnTunerTurn);
        volumeKnob.activated.AddListener(OnVolumeTurn);
    }

    // POWER
    private void OnPowerToggle(SelectEnterEventArgs args)
    {
        isPowerOn = !isPowerOn;
        audioSource.volume = isPowerOn ? volumeValue : 0f;
    }

    // TUNING
    private void OnTunerTurn(ActivateEventArgs args)
    {
        tunerValue += tunerSensitivity;
        UpdateStation();
    }

    private void UpdateStation()
    {
        int stationIndex = Mathf.FloorToInt((tunerValue / maxFrequency) * stationClips.Length);

        if (stationIndex >= 0 && stationIndex < stationClips.Length)
        {
            if (audioSource.clip != stationClips[stationIndex])
            {
                audioSource.clip = stationClips[stationIndex];
                audioSource.Play();
            }
        }
        else
        {
            audioSource.clip = whiteNoise;
            audioSource.Play();
        }
    }

    // VOLUME
    private void OnVolumeTurn(ActivateEventArgs args)
    {
        volumeValue = Mathf.Clamp(volumeValue + volumeSensitivity, 0f, 1f);
        if (isPowerOn)
            audioSource.volume = volumeValue;
    }
}
