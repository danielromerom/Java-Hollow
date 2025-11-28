using UnityEngine;

public class GramophonePlayer : MonoBehaviour
{
    public AudioSource audioSource;
    public bool isPlaying = false;
    public void TogglePlay()
    {
        if (isPlaying)
        {
            audioSource.Stop();
        }
        else
        {
            audioSource.Play();
        }

        isPlaying = !isPlaying;
    }
}
