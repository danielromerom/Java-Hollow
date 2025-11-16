using UnityEngine;
using UnityEngine.XR;

public class MarshmallowRoasting : MonoBehaviour
{
    [Header("References")]
    public Renderer marshmallowLeftRenderer;
    public Renderer marshmallowRightRenderer;

    [Header("Roast Settings")]
    public Color unroastedColor = Color.white;
    public Color roastedColor = new Color(0.35f, 0.23f, 0.17f);
    public float roastSpeed = 0.2f;

    // Internal roast progress trackers
    private float leftRoastAmount = 0f;
    private float rightRoastAmount = 0f;

    private bool leftIsRoasting = false;
    private bool rightIsRoasting = false;

    void Start()
    {
        if (marshmallowLeftRenderer != null)
            marshmallowLeftRenderer.material.color = unroastedColor;
        if (marshmallowRightRenderer != null)
            marshmallowRightRenderer.material.color = unroastedColor;
    }

    void Update()
    {
        if (leftIsRoasting)
        {
            leftRoastAmount += Time.deltaTime * roastSpeed;
            leftRoastAmount = Mathf.Clamp01(leftRoastAmount);
        }
        if (rightIsRoasting)
        {
            rightRoastAmount += Time.deltaTime * roastSpeed;
            rightRoastAmount = Mathf.Clamp01(rightRoastAmount);
        }

        if (marshmallowLeftRenderer != null)
            marshmallowLeftRenderer.material.color = Color.Lerp(unroastedColor, roastedColor, leftRoastAmount);
        if (marshmallowRightRenderer != null)
            marshmallowRightRenderer.material.color = Color.Lerp(unroastedColor, roastedColor, rightRoastAmount);

        InputDevice rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        bool bButtonPressed = false;
        if (rightHand.TryGetFeatureValue(CommonUsages.secondaryButton, out bButtonPressed) && bButtonPressed)
        {
            ResetMarshmallows();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Campfire"))
        {
            // Check proximity to each marshmallow
            if (IsNearCampfire(marshmallowLeftRenderer, other))
                leftIsRoasting = true;
            if (IsNearCampfire(marshmallowRightRenderer, other))
                rightIsRoasting = true;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Campfire"))
        {
            if (IsNearCampfire(marshmallowLeftRenderer, other))
                leftIsRoasting = false;
            if (IsNearCampfire(marshmallowRightRenderer, other))
                rightIsRoasting = false;
        }
    }

    void ResetMarshmallows()
    {
        leftRoastAmount = 0f;
        rightRoastAmount = 0f;
        if (marshmallowLeftRenderer != null)
            marshmallowLeftRenderer.material.color = unroastedColor;
        if (marshmallowRightRenderer != null)
            marshmallowRightRenderer.material.color = unroastedColor;
    }

    // Helper: Is the marshmallow collider the one entering/exiting the campfire?
    bool IsNearCampfire(Renderer marshmallow, Collider campfire)
    {
        Collider marshmallowCollider = marshmallow.GetComponent<Collider>();
        return campfire.bounds.Intersects(marshmallowCollider.bounds);
    }
}
