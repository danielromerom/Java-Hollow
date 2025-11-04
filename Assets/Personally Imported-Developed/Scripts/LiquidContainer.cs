using System.Collections.Generic;
using UnityEngine;

public class LiquidContainer : MonoBehaviour
{
    [Header("Liquid Properties")]
    public LiquidType liquidType = LiquidType.Empty;
    [Range(0, 1)]
    public virtual float liquidAmount
    {
        get { return Mathf.Clamp01(GetTotalVolume() / maxCapacity); }
    }

    public Dictionary<LiquidType, float> liquidVolumes = new Dictionary<LiquidType, float>();
    public float maxCapacity = 1f;  

    [Header("Visuals")]
    public GameObject liquidVisual;
    [Tooltip("Total height in local units the liquid can fill to (Y axis scale)")]
    public float liquidFillHeight = 1f;

    public void AddLiquid(float amount, LiquidType type)
    {
        Debug.Log($"Adding {amount} of {type} to {gameObject.name}");

        if (!liquidVolumes.ContainsKey(type))
            liquidVolumes[type] = 0f;
        float totalVolume = GetTotalVolume();
        float addAmount = Mathf.Clamp(amount, 0f, maxCapacity - totalVolume);
        liquidVolumes[type] += addAmount;
        UpdateLiquidVisual();
    }

    public virtual float GetTotalVolume()
    {
        float total = 0f;
        foreach (var amt in liquidVolumes.Values)
            total += amt;
        return total;
    }

    public virtual void RemoveProportionalLiquid(float amount)
    {
        float totalVolume = GetTotalVolume();
        if (totalVolume == 0f) return;

        foreach (var type in new List<LiquidType>(liquidVolumes.Keys))
        {
            float toRemove = amount * (liquidVolumes[type] / totalVolume);
            liquidVolumes[type] -= toRemove;
            if (liquidVolumes[type] <= 0f)
                liquidVolumes.Remove(type);
        }
        UpdateLiquidVisual();
    }

    public void PourTo(LiquidContainer target, float transferAmount)
    {
        float totalVolume = GetTotalVolume();
        if (totalVolume == 0f) return;
        foreach (var type in new List<LiquidType>(liquidVolumes.Keys))
        {
            float chunk = transferAmount * (liquidVolumes[type] / totalVolume);
            RemoveLiquid(type, chunk);
            target.AddLiquid(chunk, type);
        }
    }

    public virtual void RemoveLiquid(LiquidType type, float amount)
    {
        if (!liquidVolumes.ContainsKey(type)) return;
        liquidVolumes[type] -= amount;
        if (liquidVolumes[type] <= 0f)
            liquidVolumes.Remove(type);
        UpdateLiquidVisual();
    }

    public void UpdateLiquidVisual()
    {
        if (liquidVisual != null)
        {
            liquidVisual.SetActive(GetTotalVolume() > 0f);

            var scale = liquidVisual.transform.localScale;
            scale.y = Mathf.Lerp(0.05f, liquidFillHeight, liquidAmount);
            liquidVisual.transform.localScale = scale;

            var rend = liquidVisual.GetComponent<Renderer>();
            if (rend != null)
            {
#if UNITY_EDITOR
                rend.sharedMaterial.color = GetBlendedColor();
#else
            rend.material.color = GetBlendedColor();
#endif
            }
        }
    }

    public Color GetBlendedColor()
    {
        if (liquidVolumes.Count == 0 || GetTotalVolume() == 0f)
            return Color.clear;

        Color blended = Color.black;
        float total = GetTotalVolume();

        foreach (var pair in liquidVolumes)
        {
            blended += GetLiquidColor(pair.Key) * (pair.Value / total);
        }
        return blended;
    }

    private Color GetLiquidColor(LiquidType type)
    {
        switch (type)
        {
            case LiquidType.Espresso: return new Color(0.3f, 0.15f, 0.09f); // dark brown
            case LiquidType.Water: return new Color(0.7f, 0.85f, 1f, 0.3f); // pale blue, transparent
            case LiquidType.Milk: return new Color(1f, 1f, 1f, 0.7f);
            case LiquidType.SteamedMilk: return new Color(1f, 0.97f, 0.85f, 0.8f);
            default: return Color.clear;
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
    SteamedMilk,
    Mixed
}

