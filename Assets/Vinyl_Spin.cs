using UnityEngine;

public class VinylSpin : MonoBehaviour
{
    public AudioSource music;
    public float spinSpeed = 150f;

    void Update()
    {
        if (music != null && music.isPlaying)
        {
            transform.Rotate(Vector3.up * spinSpeed * Time.deltaTime);
        }
    }
}
