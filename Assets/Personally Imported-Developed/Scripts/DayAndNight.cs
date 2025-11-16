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

    private Color[] baseEmissionColors;

    private float skyboxBlend = 0f;   // 0 = day, 1 = night

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

        // SKYBOX BLEND
        RenderSettings.skybox.Lerp(daySkybox, nightSkybox, skyboxBlend);

        // SUN INTENSITY
        sunLight.intensity = Mathf.Lerp(1f, 0f, skyboxBlend);

        // SUNSET TINT (warm colors)
        sunLight.color = sunsetColor.Evaluate(1f - skyboxBlend);

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
