using UnityEngine;
using System.Collections.Generic;

public class DayAndNight : MonoBehaviour
{
    [Header("Skyboxes (Drag & Drop)")]
    public Material daySkybox;
    public Material nightSkybox;

    [Header("Directional Light (Sun)")]
    public Light sunLight;
    public float rotationSpeed = 3f;
    public Gradient sunsetColor;   // NEW — controls warm sunset tint
    public Color moonColor = new Color(0.1f, 0.1f, 0.3f); // Blueish moonlight
    public float moonIntensity = 0.2f;

    [Header("Ambient Lighting")]
    public float dayAmbientIntensity = 1.0f;
    public float sunsetAmbientIntensity = 0.3f;
    public float nightAmbientIntensity = 0.05f;

    public float dayReflectionIntensity = 1.0f;
    public float nightReflectionIntensity = 0.1f;

    [Header("Glow Objects (Lights + Emission Materials)")]
    public List<Renderer> glowRenderers = new List<Renderer>();
    public List<Light> glowLights = new List<Light>();
    public float glowNightIntensity = 1.5f;

    [Header("Audio Ambience")]
    public AudioSource nightAmbience; // Drag your new "Bugs/Crickets" AudioSource here

    private Color[] baseEmissionColors;

    private float skyboxBlend = 0f;   // 0 = day, 1 = night

    // Public property to let other scripts know if it's night
    public bool IsNight => skyboxBlend > 0.5f;

    void Start()
    {
        // Store original emission colors
        baseEmissionColors = new Color[glowRenderers.Count];
        for (int i = 0; i < glowRenderers.Count; i++)
        {
            Material mat = glowRenderers[i].material;
            mat.EnableKeyword("_EMISSION");
            baseEmissionColors[i] = mat.GetColor("_EmissionColor");
        }

        // Use day skybox at start
        RenderSettings.skybox = daySkybox;

        // Force full control of ambient light
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        DynamicGI.UpdateEnvironment();

        // Ensure night audio is playing but start at volume 0 if it's day
        if (nightAmbience != null)
        {
            if (!nightAmbience.isPlaying) nightAmbience.Play();
            nightAmbience.loop = true;
            nightAmbience.volume = 0f;
        }
    }

    void Update()
    {
        RotateSun();
        UpdateLighting();
        UpdateGlowObjects();
    }

    // -----------------------------------------------------------------
    // SUN ROTATION
    // -----------------------------------------------------------------
    void RotateSun()
    {
        sunLight.transform.Rotate(Vector3.right * rotationSpeed * Time.deltaTime);
    }

    // -----------------------------------------------------------------
    // LIGHTING LOGIC
    // -----------------------------------------------------------------
    void UpdateLighting()
    {
        float angle = sunLight.transform.rotation.eulerAngles.x;

        // Normalize angles
        if (angle > 180) angle -= 360;

        // --- Determine skybox blend ---
        if (angle < -20f) skyboxBlend = 1f; // full night
        else if (angle < 20f) skyboxBlend = Mathf.InverseLerp(20f, -20f, angle);
        else skyboxBlend = 0f; // full day

        // --- AUDIO BLENDING ---
        // Fade the bugs in as it gets darker (0 volume at day, 1 volume at night)
        if (nightAmbience != null)
        {
            nightAmbience.volume = skyboxBlend;
        }

        // SKYBOX BLEND
        // RenderSettings.skybox.Lerp(daySkybox, nightSkybox, skyboxBlend); // Lerp can cause black screen if shaders differ
        if (skyboxBlend > 0.5f && RenderSettings.skybox != nightSkybox)
        {
            RenderSettings.skybox = nightSkybox;
        }
        else if (skyboxBlend <= 0.5f && RenderSettings.skybox != daySkybox)
        {
            RenderSettings.skybox = daySkybox;
        }

        // SUN INTENSITY & COLOR (Transition to Moon)
        if (skyboxBlend < 0.5f)
        {
            // Day to Sunset
            sunLight.intensity = Mathf.Lerp(1f, 0.5f, skyboxBlend * 2f);
            sunLight.color = sunsetColor.Evaluate(skyboxBlend * 2f);
        }
        else
        {
            // Sunset to Night (Moon)
            sunLight.intensity = Mathf.Lerp(0.5f, moonIntensity, (skyboxBlend - 0.5f) * 2f);
            sunLight.color = Color.Lerp(sunsetColor.Evaluate(1f), moonColor, (skyboxBlend - 0.5f) * 2f);
        }

        // AMBIENT LIGHTING
        float ambient =
            (skyboxBlend < 0.4f)
            ? Mathf.Lerp(dayAmbientIntensity, sunsetAmbientIntensity, skyboxBlend * 2.5f)
            : Mathf.Lerp(sunsetAmbientIntensity, nightAmbientIntensity, (skyboxBlend - 0.4f) * 1.7f);

        RenderSettings.ambientLight = Color.white * ambient;

        // REFLECTION INTENSITY
        RenderSettings.reflectionIntensity =
            Mathf.Lerp(dayReflectionIntensity, nightReflectionIntensity, skyboxBlend);

        DynamicGI.UpdateEnvironment();
    }

    // -----------------------------------------------------------------
    // GLOW OBJECTS (lamps, mushrooms, crystals, etc)
    // -----------------------------------------------------------------
    void UpdateGlowObjects()
    {
        float glow = Mathf.Lerp(0.1f, glowNightIntensity, skyboxBlend);

        // Emission materials
        for (int i = 0; i < glowRenderers.Count; i++)
        {
            Material mat = glowRenderers[i].material;
            mat.SetColor("_EmissionColor", baseEmissionColors[i] * glow);
        }

        // Lights
        foreach (Light l in glowLights)
        {
            l.intensity = glow;
            l.enabled = glow > 0.15f;
        }
    }
}
