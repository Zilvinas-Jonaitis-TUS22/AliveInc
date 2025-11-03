using UnityEngine;
using Unity.Netcode;

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

        var netObj = other.GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsPlayerObject)
        {
            ulong clientId = netObj.OwnerClientId;
            TeleportPlayerServerRpc(clientId);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void TeleportPlayerServerRpc(ulong clientId)
    {
        if (NetworkManager.Singleton == null) return;
        if (!NetworkManager.Singleton.ConnectedClients.ContainsKey(clientId)) return;

        TeleportClientRpc(clientId, teleportTarget.position, teleportTarget.rotation);
    }

    [ClientRpc]
    private void TeleportClientRpc(ulong targetClientId, Vector3 pos, Quaternion rot)
    {
        if (NetworkManager.Singleton == null) return;
        if (NetworkManager.Singleton.LocalClientId != targetClientId) return;

        var localPlayer = NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject();
        if (localPlayer == null) return;

        var handler = localPlayer.GetComponent<PlayerTeleportHandler>();
        if (handler != null)
        {
            handler.TeleportLocal(pos, rot);
            return;
        }

        // fallback: if handler missing, set directly
        var controller = localPlayer.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;
        localPlayer.transform.SetPositionAndRotation(pos, rot);
        if (controller != null)
        {
            controller.Move(Vector3.zero);
            controller.enabled = true;
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
