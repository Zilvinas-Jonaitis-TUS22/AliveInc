using UnityEngine;
using Unity.Netcode;

[ExecuteAlways]
[RequireComponent(typeof(BoxCollider))]
public class TeleportZone : NetworkBehaviour
{
    [Header("Teleport Settings")]
    public Transform teleportTarget;
    public Color gizmoColor = new Color(0f, 1f, 0f, 0.25f);

    private BoxCollider box;

    private void Awake()
    {
        box = GetComponent<BoxCollider>();
        box.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        var player = other.GetComponent<NetworkObject>();
        if (player != null && player.IsPlayerObject)
        {
            Debug.Log($"[TeleportZone] Player {player.OwnerClientId} entered teleport zone.");
            TeleportPlayerServerRpc(player.OwnerClientId);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void TeleportPlayerServerRpc(ulong clientId)
    {
        var netManager = NetworkManager.Singleton;
        if (netManager == null) return;

        if (netManager.ConnectedClients.TryGetValue(clientId, out var client))
        {
            var playerObject = client.PlayerObject;
            if (playerObject != null && teleportTarget != null)
            {
                var controller = playerObject.GetComponent<CharacterController>();
                if (controller != null) controller.enabled = false; // stop physics control

                playerObject.transform.SetPositionAndRotation(
                    teleportTarget.position,
                    teleportTarget.rotation
                );

                if (controller != null) controller.enabled = true; // resume control

                Debug.Log($"[TeleportZone] Teleported player {clientId} to {teleportTarget.position}");
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (box == null) box = GetComponent<BoxCollider>();
        if (box == null) return;

        Gizmos.color = gizmoColor;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(box.center, box.size);

        if (teleportTarget != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, teleportTarget.position);
            Gizmos.DrawSphere(teleportTarget.position, 0.25f);
        }
    }
}
