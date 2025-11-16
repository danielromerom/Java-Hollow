using UnityEngine;

public class FootstepAudio : MonoBehaviour
{
    public AudioSource audioSource;
    public AudioClip[] footstepClips;
    public float stepInterval = 0.5f;
    public float moveThreshold = 0.01f; // smaller value so small movements count too

    private Transform cameraRig;
    private Vector3 lastPosition;
    private float stepTimer = 0f;

    void Start()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        // Find your main XR camera (listener)
        Camera mainCam = Camera.main;
        cameraRig = mainCam ? mainCam.transform : transform;

        lastPosition = cameraRig.position;
    }

    void Update()
    {
        // Measure camera rig movement (so it follows your head in XR)
        float distanceMoved = Vector3.Distance(cameraRig.position, lastPosition);
        lastPosition = cameraRig.position;

        bool isMoving = distanceMoved > moveThreshold * Time.deltaTime;

        if (isMoving)
        {
            stepTimer += Time.deltaTime;
            if (stepTimer >= stepInterval)
            {
                PlayFootstep();
                stepTimer = 0f;
            }
        }
        else
        {
            stepTimer = 0f;
        }
    }

    void PlayFootstep()
    {
        if (footstepClips.Length == 0 || audioSource == null) return;

        AudioClip clip = footstepClips[Random.Range(0, footstepClips.Length)];
        audioSource.pitch = Random.Range(0.95f, 1.05f);

        // Play sound at your feet level
        Vector3 footPos = cameraRig.position + Vector3.down * 1.2f;
        AudioSource.PlayClipAtPoint(clip, footPos, 0.2f);
    }
}
