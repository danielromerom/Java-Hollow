using UnityEngine;
using System.Collections.Generic;

public class InfiniteLiquidContainer : LiquidContainer
{
    // Override methods to prevent removing liquid from the volume
    public override void RemoveProportionalLiquid(float amount)
    {
        // Do nothing: infinite source
    }

    public override void RemoveLiquid(LiquidType type, float amount)
    {
        // Do nothing: infinite source
    }

    public override float GetTotalVolume()
    {
        return maxCapacity; // Always appears "full"
    }

    public override float liquidAmount
    {
        get { return 1f; } // Always full for visual
    }

    void Awake()
    {
        if (liquidVolumes == null)
            liquidVolumes = new Dictionary<LiquidType, float>();
        // Set this to your desired always-filled source type
        if (!liquidVolumes.ContainsKey(liquidType))
            liquidVolumes[liquidType] = maxCapacity;
    }
}
