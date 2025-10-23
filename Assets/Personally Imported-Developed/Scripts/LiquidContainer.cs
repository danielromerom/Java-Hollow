using UnityEngine;

public class LiquidContainer : MonoBehaviour
{
    [Header("Liquid Properties")]
    public LiquidType liquidType = LiquidType.Empty;
    [Range(0, 1)]
    public float liquidAmount = 0f; // 0 = empty, 1 = full
    public float maxCapacity = 1f;  // Max normalized capacity (set to 1 for simplicity)

    [Header("Visuals")]
    public GameObject liquidVisual; // Assign a mesh/child object that represents liquid
    [Tooltip("Total height in local units the liquid can fill to (Y axis scale)")]
    public float liquidFillHeight = 1f; // Edit per cup prefab!

    public void AddLiquid(float amount, LiquidType type)
    {
        if (liquidType == LiquidType.Empty || liquidType == type)
        {
            liquidType = type;
            liquidAmount = Mathf.Clamp(liquidAmount + amount, 0f, maxCapacity);
            UpdateLiquidVisual();
        }
        // (Optional) If you want to mix types, handle it here
    }

    public void RemoveLiquid(float amount)
    {
        liquidAmount = Mathf.Clamp(liquidAmount - amount, 0f, maxCapacity);
        if (liquidAmount <= 0f)
        {
            liquidType = LiquidType.Empty;
        }
        UpdateLiquidVisual();
    }

    public void UpdateLiquidVisual()
    {
        if (liquidVisual != null)
        {
            liquidVisual.SetActive(liquidAmount > 0f);

            var scale = liquidVisual.transform.localScale;
            scale.y = Mathf.Lerp(0.05f, liquidFillHeight, liquidAmount);
            liquidVisual.transform.localScale = scale;

            var rend = liquidVisual.GetComponent<Renderer>();
            if (rend != null)
            {
#if UNITY_EDITOR
                // Use sharedMaterial in edit/OnValidate to avoid material leaks
                rend.sharedMaterial.color = GetLiquidColor(liquidType);
#else
            // At runtime (play mode), still use material for instance
            rend.material.color = GetLiquidColor(liquidType);
#endif
            }
        }
    }


    private Color GetLiquidColor(LiquidType type)
    {
        switch (type)
        {
            case LiquidType.Espresso: return new Color(0.3f, 0.15f, 0.09f); // dark brown
            case LiquidType.Water: return Color.clear;
            case LiquidType.Milk: return Color.white;
            case LiquidType.SteamedMilk: return new Color(1f, 0.97f, 0.85f); // creamy
            default: return Color.clear;
        }
    }

#if UNITY_EDITOR
    // This updates the visuals in Edit mode as soon as you change inspector values
    void OnValidate()
    {
        UpdateLiquidVisual();
    }
#endif
}



public enum LiquidType
{
    Empty,
    Espresso,
    Water,
    Milk,
    SteamedMilk
}
