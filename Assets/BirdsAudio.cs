using UnityEngine;
using System.Collections;

public class RandomBirdSectionPlayer : MonoBehaviour
{
    public AudioSource audioSource;
    public AudioClip birdClip;   // your 30s ambience
    public float minPlayTime = 2f;   // shortest snippet
    public float maxPlayTime = 8f;   // longest snippet
    public float minDelay = 6f;      // min wait between plays
    public float maxDelay = 15f;

    void Start()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
        StartCoroutine(PlayRandomSections());
    }

    IEnumerator PlayRandomSections()
    {
        while (true)
        {
            // Wait random delay before next snippet
            yield return new WaitForSeconds(Random.Range(minDelay, maxDelay));

            float clipLength = birdClip.length;
            float playDuration = Random.Range(minPlayTime, maxPlayTime);

            // make sure the start point + duration never exceeds total length
            float maxStartTime = Mathf.Max(0f, clipLength - playDuration);
            float startTime = Random.Range(0f, maxStartTime);

            audioSource.clip = birdClip;
            audioSource.time = startTime;
            audioSource.pitch = Random.Range(0.95f, 1.05f);
            audioSource.Play();

            yield return new WaitForSeconds(playDuration);
            audioSource.Stop();
        }
    }
}
