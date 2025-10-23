using UnityEngine;

public class LiquidContainer : MonoBehaviour
{
    [Header("Liquid Properties")]
    public LiquidType liquidType = LiquidType.Empty;
    [Range(0, 1)]
    public float liquidAmount = 0f; // 0 = empty, 1 = full
    public float maxCapacity = 1f;  // Normalized for simplicity

    [Header("Visuals")]
    public GameObject liquidVisual;
    [Tooltip("Total height in local units the liquid can fill to (Y axis scale)")]
    public float liquidFillHeight = 1f; // Set per cup prefab/instance

    public void AddLiquid(float amount, LiquidType type)
    {
        if (liquidType == LiquidType.Empty || liquidType == type)
        {
            liquidType = type;
            liquidAmount = Mathf.Clamp(liquidAmount + amount, 0f, maxCapacity);
            UpdateLiquidVisual();
        }
        // (Optional) For mixing types, add logic here
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
            // Show or hide the liquid mesh based on fill
            liquidVisual.SetActive(liquidAmount > 0f);

            // Scale the Y dimension for fill visualization – change only scale
            var scale = liquidVisual.transform.localScale;
            scale.y = Mathf.Lerp(0.05f, liquidFillHeight, liquidAmount);
            liquidVisual.transform.localScale = scale;
        }
    }

#if UNITY_EDITOR
    // Updates mesh immediately in the Editor when you change slider/type
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
