using UnityEngine;

public class TreeFXController : MonoBehaviour
{
    public Transform player;
    public float activationRadius = 60f;
    private ParticleSystem ps;

    void Start() => ps = GetComponent<ParticleSystem>();

    void Update()
    {
        if (!player || ps == null) return;

        float dist = Vector3.Distance(player.position, transform.position);
        if (dist < activationRadius && !ps.isPlaying) ps.Play();
        else if (dist >= activationRadius && ps.isPlaying) ps.Pause();
    }
}
