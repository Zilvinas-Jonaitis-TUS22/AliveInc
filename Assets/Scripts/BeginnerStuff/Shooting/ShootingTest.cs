using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(NetworkObject))]
public class ShootingTest : NetworkBehaviour
{
    [Header("Shooting Settings")]
    [Tooltip("Maximum distance for the raycast.")]
    public float shootRange = 100f;

    [Tooltip("Choose which layers the raycast should detect.")]
    public LayerMask hittableLayers;

    [Tooltip("Camera used for raycasting. If left empty, will use the player's camera.")]
    public Camera playerCamera;

    [Tooltip("How long the debug line should be visible (seconds).")]
    public float debugLineDuration = 0.2f;

    private PlayerInput playerInput;

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();

        // Bind to "Shoot" action
        playerInput.actions["Shoot"].performed += OnShootPerformed;
    }

    private void OnDestroy()
    {
        if (playerInput != null && playerInput.actions != null)
        {
            playerInput.actions["Shoot"].performed -= OnShootPerformed;
        }
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();
    }

    private void OnShootPerformed(InputAction.CallbackContext context)
    {
        if (!IsOwner || !context.performed) return;
        TryShoot();
    }

    private void TryShoot()
    {
        if (playerCamera == null)
        {
            Debug.LogWarning("ShootingTest: No camera assigned for raycast.");
            return;
        }

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, shootRange, hittableLayers))
        {
            // Draw a green line to the hit point
            Debug.DrawLine(ray.origin, hit.point, Color.green, debugLineDuration);

            NetworkObject hitNetObj = hit.collider.GetComponentInParent<NetworkObject>();
            if (hitNetObj != null)
            {
                Debug.Log($"Hit player: {hitNetObj.NetworkObjectId} (ClientId: {hitNetObj.OwnerClientId})");
                ReportHitServerRpc(hitNetObj.OwnerClientId);
            }
            else
            {
                Debug.Log($"Hit object: {hit.collider.gameObject.name} on layer {LayerMask.LayerToName(hit.collider.gameObject.layer)}");
            }
        }
        else
        {
            // Draw a red line for a miss
            Debug.DrawRay(ray.origin, ray.direction * shootRange, Color.red, debugLineDuration);
            Debug.Log("Missed. No object hit within range.");
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void ReportHitServerRpc(ulong targetClientId)
    {
        Debug.Log($"[Server] Player {OwnerClientId} hit player {targetClientId}");
    }
}
