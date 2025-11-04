using UnityEngine;

public class CupSipper : MonoBehaviour
{
    public LiquidContainer liquidContainer;
    public Transform mouthReference;
    public float sipAngleThreshold = 60f;
    public float sipDistance = 0.2f;
    public float sipRate = 0.35f;

    void Update()
    {
        if (liquidContainer.liquidAmount > 0f &&
            Vector3.Angle(transform.forward, (mouthReference.position - transform.position)) < sipAngleThreshold &&
            Vector3.Distance(transform.position, mouthReference.position) < sipDistance)
        {
            liquidContainer.RemoveProportionalLiquid(sipRate * Time.deltaTime);
            // Trigger sound, visual effect, mouth feedback
        }
    }
}
