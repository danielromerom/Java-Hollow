using System.Collections.Generic;
using UnityEngine;

public class CupPourer : MonoBehaviour
{
    public LayerMask cupLayer;
    public LayerMask floorLayer;

    public LiquidContainer liquidContainer;
    public List<Transform> pourOrigins;
    public ParticleSystem pourParticles;
    public float pourAngleThreshold = 120f;
    public float pourRate = 0.3f;

    // --- Particle system control variables
    private bool isPouring = false;
    private float pourStopBuffer = 0.12f; // seconds to wait before actually stopping
    private float pourStopTimer = 0f;

    void OnDrawGizmosSelected()
    {
        if (pourOrigins != null)
        {
            Gizmos.color = Color.red;
            foreach (var origin in pourOrigins)
            {
                if (origin != null)
                    Gizmos.DrawSphere(origin.position, 0.02f);
            }
        }
    }

    Transform GetLowestPourOrigin()
    {
        Transform lowest = pourOrigins[0];
        float minY = pourOrigins[0].position.y;
        foreach (var origin in pourOrigins)
        {
            if (origin.position.y < minY)
            {
                minY = origin.position.y;
                lowest = origin;
            }
        }
        return lowest;
    }

    void Update()
    {
        if (liquidContainer == null || liquidContainer.GetTotalVolume() <= 0f)
        {
            HandlePourParticles(false);
            return;
        }

        Transform currentPourOrigin = GetLowestPourOrigin();
        Vector3 pourDir = (Vector3.down + -transform.up * 0.2f).normalized;
        Debug.DrawRay(currentPourOrigin.position, pourDir * 2f, Color.cyan);

        bool pouringThisFrame = false;

        if (Vector3.Angle(-transform.up, Vector3.down) > pourAngleThreshold)
        {
            RaycastHit hit;
            float maxDistance = 5f;
            LayerMask combinedMask = cupLayer | floorLayer;

            if (Physics.Raycast(currentPourOrigin.position, pourDir, out hit, maxDistance, combinedMask))
            {
                float streamDistance = hit.distance;

                if (!hit.collider.transform.IsChildOf(transform))
                {
                    pouringThisFrame = true;

                    if (pourParticles != null)
                    {
                        pourParticles.transform.position = currentPourOrigin.position;
                        pourParticles.transform.rotation = Quaternion.LookRotation(pourDir);
                        var main = pourParticles.main;
                        main.simulationSpace = ParticleSystemSimulationSpace.World;
                        main.gravityModifier = 1f;
                        main.startColor = liquidContainer.GetBlendedColor();
                        float speed = Mathf.Max(main.startSpeed.constant, 2f);
                        main.startLifetime = streamDistance / speed;
                    }

                    var targetCup = hit.collider.GetComponentInParent<LiquidContainer>();
                    if (targetCup && targetCup != liquidContainer)
                    {
                        float availableVolume = liquidContainer.GetTotalVolume();
                        float targetSpace = targetCup.maxCapacity - targetCup.GetTotalVolume();
                        float transfer = Mathf.Min(pourRate * Time.deltaTime, availableVolume, targetSpace);
                        liquidContainer.PourTo(targetCup, transfer);
                    }
                    else
                    {
                        float remove = Mathf.Min(pourRate * Time.deltaTime, liquidContainer.GetTotalVolume());
                        liquidContainer.RemoveProportionalLiquid(remove);
                    }
                }
            }
        }

        HandlePourParticles(pouringThisFrame);
    }

    private void HandlePourParticles(bool pouringNow)
    {
        if (pourParticles == null) return;

        if (pouringNow)
        {
            pourStopTimer = 0f;
            if (!isPouring)
            {
                // Start particles only if currently stopped
                pourParticles.Play();
                isPouring = true;
            }
        }
        else
        {
            if (isPouring)
            {
                pourStopTimer += Time.deltaTime;
                // Stop only after buffer passes (avoid flicker)
                if (pourStopTimer >= pourStopBuffer)
                {
                    pourParticles.Stop();
                    isPouring = false;
                }
            }
        }
    }
}
