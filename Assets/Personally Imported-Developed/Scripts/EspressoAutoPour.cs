using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class EspressoAutoPour : MonoBehaviour
{
    public XRSocketInteractor cupSocket;
    public ParticleSystem espressoPourLeft;
    public ParticleSystem espressoPourRight;
    public float pourDuration = 3f;

    private void OnEnable()
    {
        if (cupSocket != null)
            cupSocket.selectEntered.AddListener(OnCupPlaced);
    }
    private void OnDisable()
    {
        if (cupSocket != null)
            cupSocket.selectEntered.RemoveListener(OnCupPlaced);
    }

    private void OnCupPlaced(SelectEnterEventArgs args)
    {
        LiquidContainer cup = args.interactableObject.transform.GetComponent<LiquidContainer>();
        if (cup != null && cup.liquidAmount < 1f)
            StartCoroutine(PourEspresso(cup));
    }

    private System.Collections.IEnumerator PourEspresso(LiquidContainer cup)
    {
        if (espressoPourLeft != null) espressoPourLeft.Play();
        if (espressoPourRight != null) espressoPourRight.Play();
        float timer = 0f;
        while (timer < pourDuration && cup.liquidAmount < 1f)
        {
            cup.AddLiquid(Time.deltaTime / pourDuration, LiquidType.Espresso);
            timer += Time.deltaTime;
            yield return null;
            if (cup.liquidAmount >= 1f)
                break;
        }
        if (espressoPourLeft != null) espressoPourLeft.Stop();
        if (espressoPourRight != null) espressoPourRight.Stop();
    }
}
