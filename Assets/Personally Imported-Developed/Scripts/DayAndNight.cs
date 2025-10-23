using UnityEngine;

public class DayAndNight : MonoBehaviour
{
    Vector3 rotation = new Vector3(90, 0, 0);
    float degreesPerSecond = 5;
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        rotation.x = Time.deltaTime * degreesPerSecond;
        transform.Rotate(rotation);

    }
}
