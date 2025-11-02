using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

public class ChairSeatController : MonoBehaviour
{
    [Header("Seat Settings")]
    [SerializeField] private Transform seatPosition;

    [Header("Locomotion Components")]
    [SerializeField] private TeleportationProvider teleportationProvider;
    [SerializeField] private DynamicMoveProvider dynamicMoveProvider;
    [SerializeField] private SnapTurnProvider snapTurnProvider;
    [SerializeField] private ContinuousTurnProvider continuousTurnProvider;

    [Header("Stand Up Settings")]
    [SerializeField] private InputActionProperty standUpButton;
    [SerializeField] private bool showStandUpPrompt = true;
    [SerializeField] private GameObject standUpUI; // Your affordance callout

    private bool isSeated = false;
    private TeleportationAnchor teleportAnchor;
    private Transform mainCamera;

    void Start()
    {
        teleportAnchor = GetComponent<TeleportationAnchor>();
        mainCamera = Camera.main.transform;

        if (teleportAnchor != null)
        {
            teleportAnchor.selectEntered.AddListener(OnChairSelected);
        }

        if (standUpButton.action != null)
        {
            standUpButton.action.Enable();
        }

        if (standUpUI != null)
        {
            standUpUI.SetActive(false);
        }
    }

    void Update()
    {
        if (isSeated && standUpButton.action != null)
        {
            if (standUpButton.action.WasPressedThisFrame())
            {
                StandUp();
            }
        }
    }

    private void OnChairSelected(SelectEnterEventArgs args)
    {
        StartCoroutine(SitDownAfterTeleport());
    }

    private System.Collections.IEnumerator SitDownAfterTeleport()
    {
        yield return new WaitForSeconds(0.2f);
        SitDown();
    }

    private void SitDown()
    {
        if (isSeated) return;

        isSeated = true;

        // Disable all locomotion
        if (teleportationProvider != null)
            teleportationProvider.enabled = false;

        if (dynamicMoveProvider != null)
            dynamicMoveProvider.enabled = false;

        if (snapTurnProvider != null)
            snapTurnProvider.enabled = false;

        if (continuousTurnProvider != null)
            continuousTurnProvider.enabled = false;

        // Position and show the stand up prompt at player's camera location
        if (standUpUI != null)
        {
            // Position in front of the camera
            Vector3 spawnPosition = mainCamera.position + mainCamera.forward * 1.5f;
            spawnPosition.y = mainCamera.position.y - 0.3f; // Slightly below eye level

            standUpUI.transform.position = spawnPosition;

            // Make it face the camera
            standUpUI.transform.LookAt(mainCamera);
            standUpUI.transform.Rotate(0, 180, 0); // Flip to face player

            standUpUI.SetActive(true);
        }

        Debug.Log("Player seated. Press B button to stand up.");
    }

    public void StandUp()
    {
        if (!isSeated) return;

        isSeated = false;

        // Re-enable all locomotion
        if (teleportationProvider != null)
            teleportationProvider.enabled = true;

        if (dynamicMoveProvider != null)
            dynamicMoveProvider.enabled = true;

        if (snapTurnProvider != null)
            snapTurnProvider.enabled = true;

        if (continuousTurnProvider != null)
            continuousTurnProvider.enabled = true;

        // Hide stand up prompt
        if (standUpUI != null)
        {
            standUpUI.SetActive(false);
        }

        Debug.Log("Player standing up.");
    }

    void OnDestroy()
    {
        if (teleportAnchor != null)
        {
            teleportAnchor.selectEntered.RemoveListener(OnChairSelected);
        }
    }
}
