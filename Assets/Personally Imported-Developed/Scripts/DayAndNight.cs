using System.Numerics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.Experimental.GlobalIllumination;

public class DayAndNight : MonoBehaviour
{
    
    UnityEngine.Vector3 rotation = new UnityEngine.Vector3(90, 0, 0);
    float degreesPerSecond = 5;
    public float updateInterval = 0.1f;
    private float timer = 0f;

    public string mushroomTag = "Mushroom";
    private Light[][] mushroomLights;
    private Color[] initialColors;
    GameObject[] mushrooms;

    void Start()
    {
        mushrooms = GameObject.FindGameObjectsWithTag(mushroomTag);
        mushroomLights = new Light[mushrooms.Length][];
        initialColors = new Color[mushrooms.Length];
        Debug.Log("Found " + mushrooms.Length + " mushrooms with tag: " + mushroomTag);
        for (int i = 0; i < mushrooms.Length; i++)
        {
            var rend = mushrooms[i].GetComponent<Renderer>();
            if (rend != null)
            {
                Material mushroomMaterial = rend.material;
                mushroomMaterial.EnableKeyword("_EMISSION");
                initialColors[i] = mushroomMaterial.GetColor("_EmissionColor");
            }
                mushroomLights[i] = mushrooms[i].GetComponentsInChildren<Light>();
        }
    }

    // Update is called once per frame
    void Update()
    {
        rotation.x += Time.deltaTime * degreesPerSecond;
        if (rotation.x > 360f)
            rotation.x -= 360f;

        transform.rotation = UnityEngine.Quaternion.Euler(rotation);

        timer += Time.deltaTime;
        if (timer >= updateInterval)
        {
            UpdateLampIntensities();
            timer = 0f;
        }
    }

    void UpdateLampIntensities()
    {
        if (mushroomLights == null) return;

        float sunAngle = rotation.x;
        if (sunAngle > 180f)
            sunAngle -= 360f; // convert to [-180, 180]

        float targetIntensity = CalculateLampIntensity(sunAngle);

        // Apply to all mushrooms
        for (int i = 0; i < mushrooms.Length; i++)
        {
            var rend = mushrooms[i].GetComponent<Renderer>();
            if (rend != null)
            {
                Material mushroomMaterial = rend.material;
                mushroomMaterial.SetColor("_EmissionColor", initialColors[i] * 1 * targetIntensity);
            }

            foreach (Light light in mushroomLights[i])
            {
                if (light != null)
                light.intensity = targetIntensity;
            }
        }
    }

    float CalculateLampIntensity(float sunAngle)
    {
        if (sunAngle >= 170f && sunAngle <= 350f)
            return 1f;

        if (sunAngle > 350f || sunAngle < 10f)
        {
            float t = (sunAngle >= 350f) ? (sunAngle - 350f) / 20f : sunAngle / 10f;
            return Mathf.Lerp(1f, 0.5f, t);
        }

        if (sunAngle >= 10f && sunAngle <= 170f)
        {
            float t = Mathf.Abs(sunAngle - 90f) / 80f; 
            return Mathf.Clamp01(t * 0.5f); 
        }

        return 1f;
    }


}
