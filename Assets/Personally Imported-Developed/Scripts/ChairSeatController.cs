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
    [SerializeField] private GameObject standUpUI;

    private bool isSeated = false;
    private TeleportationAnchor teleportAnchor;

    void Start()
    {
        teleportAnchor = GetComponent<TeleportationAnchor>();

        if (teleportAnchor != null)
        {
            // Use selectEntered to detect when player clicks the chair
            teleportAnchor.selectEntered.AddListener(OnChairSelected);
        }

        // Enable the stand up button action
        if (standUpButton.action != null)
        {
            standUpButton.action.Enable();
        }

        // Hide stand up UI initially
        if (standUpUI != null)
        {
            standUpUI.SetActive(false);
        }
    }

    void Update()
    {
        if (isSeated && standUpButton.action != null)
        {
            // Check if stand up button is pressed
            if (standUpButton.action.WasPressedThisFrame())
            {
                StandUp();
            }
        }
    }

    private void OnChairSelected(SelectEnterEventArgs args)
    {
        // Don't disable locomotion yet - let the teleport happen first
        StartCoroutine(SitDownAfterTeleport());
    }

    private System.Collections.IEnumerator SitDownAfterTeleport()
    {
        // Wait for the teleportation to complete (need to wait a bit)
        yield return new WaitForSeconds(0.2f);
        SitDown();
    }

    private void SitDown()
    {
        if (isSeated) return;

        isSeated = true;

        // NOW disable all locomotion (after teleport completed)
        if (teleportationProvider != null)
            teleportationProvider.enabled = false;

        if (dynamicMoveProvider != null)
            dynamicMoveProvider.enabled = false;

        if (snapTurnProvider != null)
            snapTurnProvider.enabled = false;

        if (continuousTurnProvider != null)
            continuousTurnProvider.enabled = false;

        // Show stand up prompt
        if (standUpUI != null)
        {
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
