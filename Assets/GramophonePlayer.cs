using UnityEngine;

public class GramophonePlayer2 : MonoBehaviour
{
    [Header("Audio")]
    public AudioSource musicSource;
    public AudioSource crackleSource;
    public AudioSource scratchSource;

    [Header("Settings")]
    public float fadeTime = 1.5f;
    public float scratchIntervalMin = 8f;
    public float scratchIntervalMax = 18f;

    [Header("Animation")]
    public Animator animator;

    private bool isPlaying = false;
    private float nextScratchTime = 0f;

    void Start()
    {
        crackleSource.loop = true;
        crackleSource.Play();     // always on quietly
        crackleSource.volume = 0.15f;

        ScheduleNextScratch();
    }

    void Update()
    {
        if (!isPlaying) return;

        if (Time.time >= nextScratchTime)
        {
            scratchSource.Play();
            ScheduleNextScratch();
        }
    }

    void ScheduleNextScratch()
    {
        nextScratchTime = Time.time + Random.Range(scratchIntervalMin, scratchIntervalMax);
    }

    public void TogglePlay()
    {
        if (isPlaying)
        {
            StopAllCoroutines();
            StartCoroutine(FadeOut());
        }
        else
        {
            StopAllCoroutines();
            StartCoroutine(FadeIn());
        }

        isPlaying = !isPlaying;
        animator.SetBool("IsPlaying", isPlaying);
    }

    private System.Collections.IEnumerator FadeIn()
    {
        musicSource.Play();
        float t = 0f;
        while (t < fadeTime)
        {
            musicSource.volume = Mathf.Lerp(0f, 1f, t / fadeTime);
            t += Time.deltaTime;
            yield return null;
        }
        musicSource.volume = 1f;
    }

    private System.Collections.IEnumerator FadeOut()
    {
        float startVol = musicSource.volume;
        float t = 0f;
        while (t < fadeTime)
        {
            musicSource.volume = Mathf.Lerp(startVol, 0f, t / fadeTime);
            t += Time.deltaTime;
            yield return null;
        }
        musicSource.Stop();
        musicSource.volume = 1f;
    }
}
